using System.Text.Json;
using System.Windows.Threading;
using DuckFlash.Models;
using DuckFlash.Services;
using DuckFlash.ViewModels;
using DuckFlash.Views;
using Xunit;

namespace DuckFlash.Tests;

/// <summary>
/// Uji alat perangkat: screenshot, ekspor logcat, dan backup/pemulihan.
/// Semua perintah adb dijalankan lewat runner palsu, jadi tidak ada file yang
/// benar-benar diambil dari perangkat dan tidak ada aplikasi yang dipasang.
/// </summary>
public class BackupServiceTests
{
    [Fact]
    public void ParsePackageList_ReadsNameAndApkPath()
    {
        var output = "package:/data/app/~~aBc==/com.example-1==/base.apk=com.example\n";

        var packages = BackupService.ParsePackageList(output);

        var package = Assert.Single(packages);
        Assert.Equal("com.example", package.PackageName);
        Assert.Equal("/data/app/~~aBc==/com.example-1==/base.apk", Assert.Single(package.RemoteApkPaths));
    }

    [Fact]
    public void ParsePackageList_IgnoresLinesWithoutApkPath()
    {
        var output = "package:com.example\nError: broken\n";

        Assert.Empty(BackupService.ParsePackageList(output));
    }

    [Fact]
    public void ParsePackageList_EmptyOutput_ReturnsEmptyList()
    {
        Assert.Empty(BackupService.ParsePackageList(string.Empty));
    }

    [Fact]
    public async Task BackupAsync_PullsApkAndWritesManifest()
    {
        using var temp = new TempFolder();
        var runner = CreateBackupRunner();
        var service = new BackupService(new AdbService(runner, new FakeToolPaths()));

        var result = await service.BackupAsync(temp.Path, null);

        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.Succeeded);
        Assert.Equal(0, result.Failed);
        Assert.True(File.Exists(result.ManifestPath));

        var manifest = BackupService.LoadManifest(temp.Path);

        Assert.NotNull(manifest);
        var entry = Assert.Single(manifest!.Packages);
        Assert.Equal("com.example", entry.PackageName);
        Assert.Equal("com.example-1.apk", Assert.Single(entry.ApkFiles));
    }

    [Fact]
    public async Task BackupAsync_WithoutUserApps_ReturnsEmptyResult()
    {
        using var temp = new TempFolder();
        var runner = new ScriptedProcessRunner();
        var service = new BackupService(new AdbService(runner, new FakeToolPaths()));

        var result = await service.BackupAsync(temp.Path, null);

        Assert.True(result.IsEmpty);
        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task RestoreAsync_InstallsApkListedInManifest()
    {
        using var temp = new TempFolder();
        temp.WriteApk("com.example-1.apk");
        temp.WriteManifest(new BackupManifestEntry("com.example", new[] { "com.example-1.apk" }));

        var runner = new ScriptedProcessRunner();
        var service = new BackupService(new AdbService(runner, new FakeToolPaths()));

        var result = await service.RestoreAsync(temp.Path, null);

        Assert.Equal(1, result.Total);
        Assert.Equal(1, result.Succeeded);

        var installCall = Assert.Single(runner.Calls, args => args.Contains("install"));

        Assert.Equal("install", installCall[0]);
        Assert.Equal("-r", installCall[1]);
        Assert.EndsWith("com.example-1.apk", installCall[2]);
    }

    [Fact]
    public async Task RestoreAsync_WithSplitApk_UsesInstallMultiple()
    {
        using var temp = new TempFolder();
        temp.WriteApk("com.example-1.apk");
        temp.WriteApk("com.example-2.apk");
        temp.WriteManifest(new BackupManifestEntry(
            "com.example",
            new[] { "com.example-1.apk", "com.example-2.apk" }));

        var runner = new ScriptedProcessRunner();
        var service = new BackupService(new AdbService(runner, new FakeToolPaths()));

        var result = await service.RestoreAsync(temp.Path, null);

        Assert.Equal(1, result.Succeeded);

        var installCall = Assert.Single(runner.Calls, args => args.Contains("install-multiple"));

        Assert.Equal("install-multiple", installCall[0]);
        Assert.Equal("-r", installCall[1]);
        Assert.Equal(4, installCall.Length);
    }

    [Fact]
    public async Task RestoreAsync_WithFileOutsideFolder_DoesNotInstallAnything()
    {
        using var temp = new TempFolder();
        temp.WriteManifest(new BackupManifestEntry("com.evil", new[] { "../../evil.apk" }));

        var runner = new ScriptedProcessRunner();
        var service = new BackupService(new AdbService(runner, new FakeToolPaths()));

        var result = await service.RestoreAsync(temp.Path, null);

        Assert.Equal(1, result.Failed);
        Assert.DoesNotContain(runner.Calls, args => args.Contains("install"));
    }

    [Fact]
    public async Task RestoreAsync_WithoutManifest_DoesNothing()
    {
        using var temp = new TempFolder();
        var runner = new ScriptedProcessRunner();
        var service = new BackupService(new AdbService(runner, new FakeToolPaths()));

        var result = await service.RestoreAsync(temp.Path, null);

        Assert.Equal(0, result.Total);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void LoadManifest_WhenFileMissing_ReturnsNull()
    {
        using var temp = new TempFolder();

        Assert.Null(BackupService.LoadManifest(temp.Path));
    }

    [Fact]
    public void LoadManifest_WithBrokenJson_ReturnsNull()
    {
        using var temp = new TempFolder();

        File.WriteAllText(Path.Combine(temp.Path, BackupService.ManifestFileName), "{ bukan json");

        Assert.Null(BackupService.LoadManifest(temp.Path));
    }

    private static ScriptedProcessRunner CreateBackupRunner()
    {
        return new ScriptedProcessRunner
        {
            Responder = args =>
            {
                if (args.Contains("list"))
                {
                    return new CommandResult
                    {
                        ExitCode = 0,
                        StandardOutput = "package:/data/app/~~aBc==/com.example-1==/base.apk=com.example\n"
                    };
                }

                if (args.Length > 0 && args[0] == "pull")
                {
                    File.WriteAllText(args[2], "isi apk palsu");
                }

                return new CommandResult { ExitCode = 0 };
            }
        };
    }
}

/// <summary>
/// Uji pengambilan screenshot.
/// </summary>
public class ScreenshotServiceTests
{
    [Fact]
    public async Task CaptureAsync_TakesScreenshotPullsFileAndCleansDevice()
    {
        using var temp = new TempFolder();
        var destination = Path.Combine(temp.Path, "shot.png");

        var runner = new ScriptedProcessRunner
        {
            Responder = args =>
            {
                if (args.Length > 0 && args[0] == "pull")
                {
                    File.WriteAllText(args[2], "png palsu");
                }

                return new CommandResult { ExitCode = 0 };
            }
        };

        var service = new ScreenshotService(new AdbService(runner, new FakeToolPaths()));

        var result = await service.CaptureAsync(destination);

        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(destination));

        Assert.Equal(
            new[] { "shell", "screencap", "-p", ScreenshotService.RemoteFilePath },
            runner.Calls[0]);
        Assert.Equal("pull", runner.Calls[1][0]);
        Assert.Equal("shell", runner.Calls[2][0]);
        Assert.Contains("rm", runner.Calls[2]);
    }

    [Fact]
    public async Task CaptureAsync_WhenScreencapFails_DoesNotPullFile()
    {
        using var temp = new TempFolder();

        var runner = new ScriptedProcessRunner
        {
            Responder = args => args.Contains("screencap")
                ? new CommandResult { ExitCode = 1, StandardError = "error: closed" }
                : new CommandResult { ExitCode = 0 }
        };

        var service = new ScreenshotService(new AdbService(runner, new FakeToolPaths()));

        var result = await service.CaptureAsync(Path.Combine(temp.Path, "shot.png"));

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain(runner.Calls, args => args.Contains("pull"));
    }

    [Fact]
    public async Task CaptureAsync_WithEmptyPath_Throws()
    {
        var runner = new ScriptedProcessRunner();
        var service = new ScreenshotService(new AdbService(runner, new FakeToolPaths()));

        await Assert.ThrowsAsync<ArgumentException>(() => service.CaptureAsync(string.Empty));
    }
}

/// <summary>
/// Uji ekspor logcat.
/// </summary>
public class LogcatServiceTests
{
    [Fact]
    public async Task ExportAsync_WritesLogcatToFile()
    {
        using var temp = new TempFolder();
        var destination = Path.Combine(temp.Path, "logcat.txt");

        var runner = new ScriptedProcessRunner
        {
            Responder = _ => new CommandResult
            {
                ExitCode = 0,
                StandardOutput = "baris pertama\nbaris kedua"
            }
        };

        var service = new LogcatService(new AdbService(runner, new FakeToolPaths()));

        await service.ExportAsync(destination);

        Assert.True(File.Exists(destination));

        var content = File.ReadAllText(destination);

        Assert.Contains("baris pertama", content);
        Assert.Contains("baris kedua", content);
        Assert.Equal(new[] { "logcat", "-d", "-v", "threadtime" }, runner.LastArguments);
    }

    [Fact]
    public async Task ExportAsync_WhenCommandFails_DoesNotWriteFile()
    {
        using var temp = new TempFolder();
        var destination = Path.Combine(temp.Path, "logcat.txt");

        var runner = new ScriptedProcessRunner
        {
            Responder = _ => new CommandResult { ExitCode = 1, StandardError = "no devices" }
        };

        var service = new LogcatService(new AdbService(runner, new FakeToolPaths()));

        var result = await service.ExportAsync(destination);

        Assert.False(result.IsSuccess);
        Assert.False(File.Exists(destination));
    }

    [Fact]
    public async Task ClearAsync_ClearsDeviceBuffer()
    {
        var runner = new ScriptedProcessRunner();
        var service = new LogcatService(new AdbService(runner, new FakeToolPaths()));

        await service.ClearAsync();

        Assert.Equal(new[] { "logcat", "-c" }, runner.LastArguments);
    }
}

/// <summary>
/// Uji ViewModel alat perangkat.
/// </summary>
public class DeviceToolsViewModelTests
{
    [Fact]
    public async Task CaptureScreenshot_SavesFileAndReportsStatus()
    {
        using var temp = new TempFolder();
        var destination = Path.Combine(temp.Path, "shot.png");

        var runner = new ScriptedProcessRunner
        {
            Responder = args =>
            {
                if (args.Length > 0 && args[0] == "pull")
                {
                    File.WriteAllText(args[2], "png palsu");
                }

                return new CommandResult { ExitCode = 0 };
            }
        };

        var viewModel = CreateViewModel(runner);

        await viewModel.CaptureScreenshotAsync(destination);

        Assert.True(File.Exists(destination));
        Assert.Contains("shot.png", viewModel.StatusText);
    }

    [Fact]
    public async Task ExportLogcat_WritesFileAndReportsStatus()
    {
        using var temp = new TempFolder();
        var destination = Path.Combine(temp.Path, "logcat.txt");

        var runner = new ScriptedProcessRunner
        {
            Responder = _ => new CommandResult { ExitCode = 0, StandardOutput = "isi logcat" }
        };

        var viewModel = CreateViewModel(runner);

        await viewModel.ExportLogcatAsync(destination);

        Assert.True(File.Exists(destination));
        Assert.Contains("logcat.txt", viewModel.StatusText);
    }

    [Fact]
    public async Task BackupAsync_WithMissingFolder_ShowsWarning()
    {
        using var temp = new TempFolder();
        var runner = new ScriptedProcessRunner();
        var viewModel = CreateViewModel(runner);

        await viewModel.BackupAsync(Path.Combine(temp.Path, "tidak-ada"));

        Assert.Equal(LocalizationService.Default.Get("status.backup.empty"), viewModel.StatusText);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task RestoreAsync_WithoutManifest_ShowsWarning()
    {
        using var temp = new TempFolder();
        var runner = new ScriptedProcessRunner();
        var viewModel = CreateViewModel(runner);

        await viewModel.RestoreAsync(temp.Path);

        Assert.Equal(LocalizationService.Default.Get("status.restore.noManifest"), viewModel.StatusText);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task RestoreAsync_WithoutConfirmation_SendsNoInstallCommand()
    {
        using var temp = new TempFolder();
        temp.WriteApk("com.example-1.apk");
        temp.WriteManifest(new BackupManifestEntry("com.example", new[] { "com.example-1.apk" }));

        var runner = new ScriptedProcessRunner();
        var dialog = new FakeDialogService { ConfirmResult = false };
        var viewModel = CreateViewModel(runner, dialog);

        await viewModel.RestoreAsync(temp.Path);

        Assert.DoesNotContain(runner.Calls, args => args.Contains("install"));
    }

    [Fact]
    public async Task RestoreAsync_WithConfirmation_InstallsApps()
    {
        using var temp = new TempFolder();
        temp.WriteApk("com.example-1.apk");
        temp.WriteManifest(new BackupManifestEntry("com.example", new[] { "com.example-1.apk" }));

        var runner = new ScriptedProcessRunner();
        var dialog = new FakeDialogService { ConfirmResult = true };
        var viewModel = CreateViewModel(runner, dialog);

        await viewModel.RestoreAsync(temp.Path);

        Assert.Contains(runner.Calls, args => args.Contains("install"));
    }

    [Fact]
    public async Task Log_ContainsCommandPreview()
    {
        var runner = new ScriptedProcessRunner();
        var viewModel = CreateViewModel(runner);

        await viewModel.ClearLogcatAsync();

        Assert.Contains(
            viewModel.LogEntries,
            entry => entry.Formatted.Contains("> ", StringComparison.Ordinal));
    }

    [Fact]
    public void DeviceToolsWindow_CreatesWithoutXamlErrors()
    {
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                var runner = new ScriptedProcessRunner();
                var window = new DeviceToolsWindow(CreateViewModel(runner));

                Assert.NotNull(window.Content);

                window.Close();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        // Window WPF hanya boleh dibuat di thread STA.
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Jendela alat perangkat tidak selesai dimuat");
        Assert.Null(error);
    }

    private static DeviceToolsViewModel CreateViewModel(
        IProcessRunner runner,
        FakeDialogService? dialog = null)
    {
        var adb = new AdbService(runner, new FakeToolPaths());
        var text = LocalizationService.Default;

        return new DeviceToolsViewModel(
            adb,
            new ScreenshotService(adb, text),
            new LogcatService(adb, text),
            new BackupService(adb, text),
            new ErrorTranslator(),
            text,
            dialog ?? new FakeDialogService(),
            Dispatcher.CurrentDispatcher);
    }
}

/// <summary>
/// Folder sementara yang dihapus otomatis setelah selesai dipakai.
/// </summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "duckflash-tests",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void WriteApk(string fileName)
    {
        File.WriteAllText(System.IO.Path.Combine(Path, fileName), "isi apk palsu");
    }

    public void WriteManifest(params BackupManifestEntry[] entries)
    {
        var manifest = new BackupManifest
        {
            CreatedAt = DateTimeOffset.UtcNow.ToString("o"),
            Packages = entries.ToList()
        };

        var json = JsonSerializer.Serialize(manifest);

        File.WriteAllText(
            System.IO.Path.Combine(Path, BackupService.ManifestFileName),
            json);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
        catch (Exception)
        {
            // Folder sementara yang gagal dihapus tidak boleh menggagalkan test.
        }
    }
}
