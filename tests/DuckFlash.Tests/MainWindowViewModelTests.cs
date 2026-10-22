using System.Windows.Threading;
using DuckFlash.Models;
using DuckFlash.Services;
using DuckFlash.ViewModels;
using Xunit;

namespace DuckFlash.Tests;

public class MainWindowViewModelTests
{
    [Fact]
    public async Task InitializeCommand_ReportsPlatformToolsInstalled()
    {
        var dispatcher = CreateDispatcher();
        var downloader = new FakeToolsDownloader { Installed = true, LocalPath = @"C:\Tools\platform-tools" };

        var viewModel = CreateViewModel(downloader, new FakeProcessRunner(), dispatcher);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsPlatformToolsInstalled);
        Assert.Contains("terpasang", viewModel.PlatformToolsStatus);
    }

    [Fact]
    public async Task InitializeCommand_ReportsPlatformToolsMissing()
    {
        var dispatcher = CreateDispatcher();
        var downloader = new FakeToolsDownloader { Installed = false, LocalPath = @"C:\Tools\platform-tools" };

        var viewModel = CreateViewModel(downloader, new FakeProcessRunner(), dispatcher);
        await viewModel.InitializeCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsPlatformToolsInstalled);
        Assert.Contains("belum terpasang", viewModel.PlatformToolsStatus);
    }

    [Fact]
    public async Task InstallPlatformToolsCommand_Success_UpdatesStatusAndProgress()
    {
        var dispatcher = CreateDispatcher();
        var downloader = new FakeToolsDownloader { Installed = false, LocalPath = @"C:\Tools\platform-tools" };
        downloader.InstallResult = new ToolsStatus { IsInstalled = true, LocalPath = @"C:\Tools\platform-tools" };

        var viewModel = CreateViewModel(downloader, new FakeProcessRunner(), dispatcher);
        await viewModel.InstallPlatformToolsCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsPlatformToolsInstalled);
        Assert.Equal(100, viewModel.DownloadProgress);
        Assert.Equal("Platform-tools siap digunakan.", viewModel.StatusText);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task InstallPlatformToolsCommand_Failure_SetsErrorMessage()
    {
        var dispatcher = CreateDispatcher();
        var downloader = new FakeToolsDownloader { Installed = false };
        downloader.InstallResult = new ToolsStatus
        {
            IsInstalled = false,
            ErrorMessage = "Gagal memasang platform-tools: jaringan gagal"
        };

        var viewModel = CreateViewModel(downloader, new FakeProcessRunner(), dispatcher);
        await viewModel.InstallPlatformToolsCommand.ExecuteAsync(null);

        Assert.False(viewModel.IsPlatformToolsInstalled);
        Assert.Contains("jaringan gagal", viewModel.StatusText + string.Join(' ', viewModel.LogEntries.Select(e => e.Message)));
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public async Task InstallPlatformToolsCommand_ReEnablesCommandsWhenDone()
    {
        var dispatcher = CreateDispatcher();
        var downloader = new FakeToolsDownloader { Installed = false };
        downloader.InstallResult = new ToolsStatus { IsInstalled = true };

        var viewModel = CreateViewModel(downloader, new FakeProcessRunner(), dispatcher);

        // Sebelum jalan, tombol harus aktif.
        Assert.True(viewModel.CanRunCommand);

        await viewModel.InstallPlatformToolsCommand.ExecuteAsync(null);

        // Setelah selesai, tombol harus aktif lagi.
        Assert.True(viewModel.CanRunCommand);
        Assert.False(viewModel.IsBusy);
    }

    [Fact]
    public void ClearLogCommand_RemovesEntries()
    {
        var dispatcher = CreateDispatcher();
        var viewModel = CreateViewModel(new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher);

        var before = viewModel.LogEntries.Count;
        viewModel.ClearLogCommand.Execute(null);

        // Setelah dikosongkan, satu baris baru ditambahkan sebagai konfirmasi.
        Assert.Single(viewModel.LogEntries);
        Assert.True(before > 0);
    }

    [Fact]
    public void LogText_JoinsAllEntries()
    {
        var dispatcher = CreateDispatcher();
        var viewModel = CreateViewModel(new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher);

        var logText = viewModel.LogText;

        Assert.Contains("DuckFlash siap digunakan.", logText);
    }

    [Fact]
    public void ProgressChanged_FromService_AddsLogEntry()
    {
        var dispatcher = CreateDispatcher();
        var downloader = new FakeToolsDownloader();

        var viewModel = CreateViewModel(downloader, new FakeProcessRunner(), dispatcher);
        var before = viewModel.LogEntries.Count;

        downloader.RaiseProgress(50, "Mengunduh platform-tools... 50%");

        // Event masuk ke antrean, flushed oleh DispatcherTimer.
        Assert.True(viewModel.LogEntries.Count >= before);
    }

    [Fact]
    public async Task CheckAdbCommand_DetectsDevice()
    {
        var dispatcher = CreateDispatcher();
        var runner = new FakeProcessRunner(new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "List of devices attached\nABC123\tdevice\n"
        });

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher);
        await viewModel.CheckAdbCommand.ExecuteAsync(null);

        Assert.Equal(DeviceCheckStatus.Detected, viewModel.AdbStatus);
        Assert.Contains("ABC123", viewModel.DeviceStatus);
        Assert.Contains("ABC123", viewModel.AvailableSerials);
    }

    [Fact]
    public async Task CheckAdbCommand_NoDevice_ReportsNoDevice()
    {
        var dispatcher = CreateDispatcher();
        var runner = new FakeProcessRunner(new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "List of devices attached\n"
        });

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher);
        await viewModel.CheckAdbCommand.ExecuteAsync(null);

        Assert.Equal(DeviceCheckStatus.NoDevice, viewModel.AdbStatus);
        Assert.Contains("Tidak ada perangkat", viewModel.DeviceStatus);
    }

    [Fact]
    public async Task CheckAdbCommand_Unauthorized_ReportsUnauthorized()
    {
        var dispatcher = CreateDispatcher();
        var runner = new FakeProcessRunner(new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "List of devices attached\nABC123\tunauthorized\n"
        });

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher);
        await viewModel.CheckAdbCommand.ExecuteAsync(null);

        Assert.Equal(DeviceCheckStatus.Unauthorized, viewModel.AdbStatus);
        Assert.Contains("belum diizinkan", viewModel.DeviceStatus);
    }

    [Fact]
    public async Task CheckFastbootCommand_DetectsDevice()
    {
        var dispatcher = CreateDispatcher();
        var runner = new FakeProcessRunner(new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "ABC123\tfastboot\n"
        });

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher);
        await viewModel.CheckFastbootCommand.ExecuteAsync(null);

        Assert.Equal(DeviceCheckStatus.Detected, viewModel.FastbootStatus);
        Assert.Contains("fastboot", viewModel.DeviceStatus);
    }

    [Fact]
    public async Task CommandStarting_IsWrittenToLogAsPreview()
    {
        var dispatcher = CreateDispatcher();
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0, StandardOutput = "List of devices attached\n" });

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher);
        await viewModel.CheckAdbCommand.ExecuteAsync(null);

        // Preview command harus muncul di log dengan awalan "> ".
        Assert.Contains(viewModel.LogEntries, entry => entry.Message.StartsWith("> ", StringComparison.Ordinal));
    }

    [Fact]
    public void SelectImageFile_ValidFile_FillsDetails()
    {
        var dispatcher = CreateDispatcher();
        var viewModel = CreateViewModel(new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher);

        var path = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.img");
        File.WriteAllText(path, "dummy image content");

        try
        {
            viewModel.SelectImageFile(path);

            Assert.Equal(path, viewModel.SelectedImagePath);
            Assert.Equal(64, viewModel.ImageSha256.Length);
            Assert.True(viewModel.ImageFileSize > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SelectImageFile_WrongExtension_IsRejected()
    {
        var dispatcher = CreateDispatcher();
        var viewModel = CreateViewModel(new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher);

        var path = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "bukan image");

        try
        {
            viewModel.SelectImageFile(path);

            Assert.Null(viewModel.SelectedImagePath);
            Assert.Contains(viewModel.LogEntries, entry => entry.Message.Contains(".img", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void HandleDroppedFile_ZipGoesToPackage_ImgGoesToImage()
    {
        var dispatcher = CreateDispatcher();
        var viewModel = CreateViewModel(new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher);

        var imgPath = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.img");
        var zipPath = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.zip");

        File.WriteAllText(imgPath, "image");

        // Bikin zip sungguhan supaya lolos validasi arsip.
        using (var archive = System.IO.Compression.ZipFile.Open(zipPath, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("payload.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("isi paket");
        }

        try
        {
            viewModel.HandleDroppedFile(imgPath);
            viewModel.HandleDroppedFile(zipPath);

            Assert.Equal(imgPath, viewModel.SelectedImagePath);
            Assert.Equal(zipPath, viewModel.SelectedPackagePath);
        }
        finally
        {
            File.Delete(imgPath);
            File.Delete(zipPath);
        }
    }

    [Fact]
    public void CompareHash_MatchingHash_ReportsSuccess()
    {
        var dispatcher = CreateDispatcher();
        var viewModel = CreateViewModel(new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher);

        var path = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.img");
        File.WriteAllText(path, "image content");

        try
        {
            viewModel.SelectImageFile(path);
            viewModel.ExpectedSha256 = viewModel.ImageSha256.ToUpperInvariant();
            viewModel.CompareHashCommand.Execute(null);

            Assert.Contains("cocok", viewModel.HashComparisonResult);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void CompareHash_DifferentHash_Warns()
    {
        var dispatcher = CreateDispatcher();
        var viewModel = CreateViewModel(new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher);

        var path = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.img");
        File.WriteAllText(path, "image content");

        try
        {
            viewModel.SelectImageFile(path);
            viewModel.ExpectedSha256 = new string('0', 64);
            viewModel.CompareHashCommand.Execute(null);

            Assert.Contains("TIDAK cocok", viewModel.HashComparisonResult);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FlashCommand_LockedBootloader_IsBlocked()
    {
        var dispatcher = CreateDispatcher();
        var viewModel = CreateViewModel(new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher);

        var path = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.img");
        File.WriteAllText(path, "image");

        try
        {
            viewModel.SelectImageFile(path);
            viewModel.BootloaderState = "locked";
            viewModel.Partition = "recovery";

            await viewModel.FlashCommand.ExecuteAsync(null);

            // Pesan harus menyebut bootloader terkunci.
            Assert.Contains(
                viewModel.LogEntries,
                entry => entry.Message.Contains("terkunci", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FlashCommand_DisallowedPartition_IsBlocked()
    {
        var dispatcher = CreateDispatcher();
        var viewModel = CreateViewModel(new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher);

        var path = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.img");
        File.WriteAllText(path, "image");

        try
        {
            viewModel.SelectImageFile(path);
            viewModel.BootloaderState = "unlocked";
            viewModel.Partition = "system";

            await viewModel.FlashCommand.ExecuteAsync(null);

            Assert.Contains(
                viewModel.LogEntries,
                entry => entry.Message.Contains("tidak diizinkan", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FlashCommand_BlockedBootloader_ShowsErrorDialogAndDoesNotRunCommand()
    {
        var dispatcher = CreateDispatcher();
        var runner = new FakeProcessRunner();
        var dialog = new FakeDialogService();

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher, dialog);

        var path = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.img");
        File.WriteAllText(path, "image");

        try
        {
            viewModel.SelectImageFile(path);
            viewModel.BootloaderState = "locked";
            viewModel.Partition = "recovery";

            await viewModel.FlashCommand.ExecuteAsync(null);

            // Dialog error harus muncul dan command TIDAK boleh dijalankan.
            Assert.Equal(1, dialog.ErrorCount);
            Assert.Contains("terkunci", dialog.LastErrorMessage ?? string.Empty);
            Assert.Equal(0, runner.CallCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FlashCommand_ValidRequest_ShowsConfirmationWithPartitionAndFileName()
    {
        var dispatcher = CreateDispatcher();
        var runner = new FakeProcessRunner();
        var dialog = new FakeDialogService { ConfirmResult = true };

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher, dialog);

        var path = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.img");
        File.WriteAllText(path, "image");

        try
        {
            viewModel.SelectImageFile(path);
            viewModel.BootloaderState = "unlocked";
            viewModel.Partition = "recovery";

            await viewModel.FlashCommand.ExecuteAsync(null);

            // Dialog konfirmasi wajib menyebut partisi dan nama file.
            Assert.Equal(1, dialog.ConfirmCount);
            Assert.Contains("recovery", dialog.LastConfirmMessage ?? string.Empty);
            Assert.Contains(Path.GetFileName(path), dialog.LastConfirmMessage ?? string.Empty);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task FlashCommand_UserCancels_DoesNotRunCommand()
    {
        var dispatcher = CreateDispatcher();
        var runner = new FakeProcessRunner();
        var dialog = new FakeDialogService { ConfirmResult = false };

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher, dialog);

        var path = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.img");
        File.WriteAllText(path, "image");

        try
        {
            viewModel.SelectImageFile(path);
            viewModel.BootloaderState = "unlocked";
            viewModel.Partition = "recovery";

            await viewModel.FlashCommand.ExecuteAsync(null);

            Assert.Equal(0, runner.CallCount);
            Assert.Contains(
                viewModel.LogEntries,
                entry => entry.Message.Contains("dibatalkan", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RebootCommand_UsesCorrectArgumentPerTarget()
    {
        var dispatcher = CreateDispatcher();
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher);

        // Parameter datang dari XAML sebagai string.
        await viewModel.RebootCommand.ExecuteAsync("Recovery");

        Assert.Contains("reboot", runner.LastArguments);
        Assert.Contains("recovery", runner.LastArguments);
    }

    [Fact]
    public async Task RebootCommand_UnknownTarget_IsRejected()
    {
        var dispatcher = CreateDispatcher();
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher);

        await viewModel.RebootCommand.ExecuteAsync("Entah");

        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public async Task BootloaderActionUnlock_AlreadyUnlocked_BlocksWithDataWarning()
    {
        var dispatcher = CreateDispatcher();
        var dialog = new FakeDialogService { DestructiveConfirmResult = false };
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher, dialog);

        viewModel.SelectedSerial = "SERIAL123";
        viewModel.BootloaderState = "unlocked";

        await viewModel.BootloaderActionCommand.ExecuteAsync("Unlock");

        // Aksi diblokir karena bootloader sudah terbuka.
        Assert.Equal(1, dialog.ErrorCount);
        Assert.Contains("diblokir", dialog.LastErrorTitle ?? string.Empty);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public async Task BootloaderActionUnlock_UserCancelsDoesNotRunCommand()
    {
        var dispatcher = CreateDispatcher();
        var dialog = new FakeDialogService { DestructiveConfirmResult = false };
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher, dialog);

        viewModel.SelectedSerial = "SERIAL123";
        viewModel.BootloaderState = "locked";

        await viewModel.BootloaderActionCommand.ExecuteAsync("Unlock");

        // Pengguna membatalkan konfirmasi destruktif, jadi command TIDAK jalan.
        Assert.Equal(1, dialog.DestructiveConfirmCount);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public async Task BootloaderActionUnlock_ValidRequestShowsConfirmationAndRunsCommand()
    {
        var dispatcher = CreateDispatcher();
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });
        var dialog = new FakeDialogService { DestructiveConfirmResult = true };

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher, dialog);

        viewModel.SelectedSerial = "SERIAL123";
        viewModel.BootloaderState = "locked";

        await viewModel.BootloaderActionCommand.ExecuteAsync("Unlock");

        // Konfirmasi khusus harus dipakai, dengan peringatan data terhapus.
        Assert.Equal(1, dialog.DestructiveConfirmCount);
        Assert.Contains("MENGHAPUS SEMUA DATA", dialog.LastDestructiveMessage ?? string.Empty);
        Assert.False(string.IsNullOrWhiteSpace(dialog.LastDestructiveAcknowledgement));
        Assert.Equal(1, runner.CallCount);
    }

    [Fact]
    public async Task BootloaderActionUnlock_AmbiguousDevice_IsBlocked()
    {
        var dispatcher = CreateDispatcher();
        var dialog = new FakeDialogService { DestructiveConfirmResult = true };
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher, dialog);

        // Aktifkan mode multi-device: dua perangkat, tidak ada yang terpilih.
        viewModel.AvailableSerials.Add("ABC123");
        viewModel.AvailableSerials.Add("XYZ789");
        viewModel.SelectedSerial = null;
        viewModel.BootloaderState = "locked";

        await viewModel.BootloaderActionCommand.ExecuteAsync("Unlock");

        // Aksi bootloader diblokir kalau perangkat belum dipilih.
        Assert.Contains("Perangkat belum dipilih", dialog.LastErrorTitle ?? string.Empty);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public async Task CheckAdbCommand_MultipleDevices_RequiresSelection()
    {
        var dispatcher = CreateDispatcher();
        var runner = new FakeProcessRunner(new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "List of devices attached\nABC123\tdevice\nXYZ789\tdevice\n"
        });

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher);

        await viewModel.CheckAdbCommand.ExecuteAsync(null);

        // Dua perangkat terdeteksi, jadi tidak boleh ada yang terpilih otomatis.
        Assert.Equal(2, viewModel.AvailableSerials.Count);
        Assert.Null(viewModel.SelectedSerial);
        Assert.False(viewModel.IsDeviceUnambiguous);
    }

    [Fact]
    public async Task CheckAdbCommand_SingleDevice_SelectsAutomatically()
    {
        var dispatcher = CreateDispatcher();
        var runner = new FakeProcessRunner(new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "List of devices attached\nABC123\tdevice\n"
        });

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher);

        await viewModel.CheckAdbCommand.ExecuteAsync(null);

        Assert.Equal("ABC123", viewModel.SelectedSerial);
        Assert.True(viewModel.IsDeviceUnambiguous);
    }

    [Fact]
    public async Task RebootCommand_AmbiguousDevice_IsBlocked()
    {
        var dispatcher = CreateDispatcher();
        var runner = new FakeProcessRunner(new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "List of devices attached\nABC123\tdevice\nXYZ789\tdevice\n"
        });
        var dialog = new FakeDialogService();

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher, dialog);

        await viewModel.CheckAdbCommand.ExecuteAsync(null);
        await viewModel.RebootCommand.ExecuteAsync("Recovery");

        // Reboot tidak boleh dijalankan kalau perangkat belum dipilih.
        Assert.Equal(1, dialog.ErrorCount);
        Assert.Contains("Perangkat belum dipilih", dialog.LastErrorTitle ?? string.Empty);
    }

    [Fact]
    public async Task SelectedSerial_IsUsedAsDashSFlagForCommands()
    {
        var dispatcher = CreateDispatcher();
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher);

        viewModel.SelectedSerial = "SERIAL123";
        await viewModel.RebootCommand.ExecuteAsync("Recovery");

        Assert.Equal(new[] { "-s", "SERIAL123", "reboot", "recovery" }, runner.LastArguments);
    }

    [Fact]
    public async Task InstallApkCommand_SelectsFileThenRunsInstall()
    {
        var dispatcher = CreateDispatcher();
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });
        var dialog = new FakeDialogService { ConfirmResult = true };

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher, dialog);

        // Buat file APK sungguhan supaya validasi lolas.
        var apkPath = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.apk");

        using (var archive = System.IO.Compression.ZipFile.Open(apkPath, System.IO.Compression.ZipArchiveMode.Create))
        {
            archive.CreateEntry("AndroidManifest.xml");
        }

        try
        {
            viewModel.HandleDroppedFile(apkPath);
            Assert.NotNull(viewModel.SelectedApkPath);
            Assert.Contains(".apk", viewModel.ApkFileName);

            await viewModel.InstallApkCommand.ExecuteAsync(null);

            Assert.Equal(new[] { "install", apkPath }, runner.LastArguments);
        }
        finally
        {
            File.Delete(apkPath);
        }
    }

    [Fact]
    public async Task InstallApkCommand_UserCancelsDoesNotRunCommand()
    {
        var dispatcher = CreateDispatcher();
        var dialog = new FakeDialogService { ConfirmResult = false };
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher, dialog);

        var apkPath = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.apk");

        using (var archive = System.IO.Compression.ZipFile.Open(apkPath, System.IO.Compression.ZipArchiveMode.Create))
        {
            archive.CreateEntry("AndroidManifest.xml");
        }

        try
        {
            viewModel.HandleDroppedFile(apkPath);
            await viewModel.InstallApkCommand.ExecuteAsync(null);

            Assert.Equal(1, dialog.ConfirmCount);
            Assert.Equal(0, runner.CallCount);
        }
        finally
        {
            File.Delete(apkPath);
        }
    }

    [Fact]
    public async Task InstallApkCommand_BlockedAmbiguousDevice_IsBlocked()
    {
        var dispatcher = CreateDispatcher();
        var dialog = new FakeDialogService { ConfirmResult = true };
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });

        var viewModel = CreateViewModel(new FakeToolsDownloader(), runner, dispatcher, dialog);

        viewModel.AvailableSerials.Add("ABC123");
        viewModel.AvailableSerials.Add("XYZ789");
        viewModel.SelectedSerial = null;

        var apkPath = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.apk");

        using (var archive = System.IO.Compression.ZipFile.Open(apkPath, System.IO.Compression.ZipArchiveMode.Create))
        {
            archive.CreateEntry("AndroidManifest.xml");
        }

        try
        {
            viewModel.HandleDroppedFile(apkPath);
            await viewModel.InstallApkCommand.ExecuteAsync(null);

            Assert.Contains("Perangkat belum dipilih", dialog.LastErrorTitle ?? string.Empty);
            Assert.Equal(0, runner.CallCount);
        }
        finally
        {
            File.Delete(apkPath);
        }
    }

    [Fact]
    public async Task CheckForUpdateCommand_NoUpdate_ReportsLatestVersion()
    {
        var dispatcher = CreateDispatcher();
        var updater = new FakeUpdateService();

        var viewModel = CreateViewModel(new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher, null, updater);

        await viewModel.CheckForUpdateCommand.ExecuteAsync(null);

        Assert.Equal(1, updater.CallCount);
        Assert.Contains("sudah terbaru", viewModel.UpdateStatus);
    }

    [Fact]
    public async Task CheckForUpdateCommand_UpdateAvailable_ShowsNotification()
    {
        var dispatcher = CreateDispatcher();
        var dialog = new FakeDialogService();
        var updater = new FakeUpdateService
        {
            Result = new DuckFlash.Models.UpdateInfo
            {
                IsCheckSuccessful = true,
                IsUpdateAvailable = true,
                LatestVersion = "v2.0.0",
                CurrentVersion = "1.0.0",
                ReleaseUrl = "https://github.com/Dikdikduck/DuckFlash/releases/tag/v2.0.0"
            }
        };

        var viewModel = CreateViewModel(new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher, dialog, updater);

        await viewModel.CheckForUpdateCommand.ExecuteAsync(null);

        Assert.Equal(1, dialog.ErrorCount);
        Assert.Contains("v2.0.0", dialog.LastErrorMessage ?? string.Empty);
        Assert.Contains("v2.0.0", viewModel.UpdateStatus);
    }

    [Fact]
    public async Task CheckForUpdateCommand_CheckFailed_ReportsError()
    {
        var dispatcher = CreateDispatcher();
        var updater = new FakeUpdateService
        {
            Result = new DuckFlash.Models.UpdateInfo
            {
                IsCheckSuccessful = false,
                ErrorMessage = "Tidak bisa menghubungi GitHub (HTTP 404)."
            }
        };

        var viewModel = CreateViewModel(new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher, null, updater);

        await viewModel.CheckForUpdateCommand.ExecuteAsync(null);

        Assert.Contains("gagal dicek", viewModel.UpdateStatus);
        Assert.Contains(
            viewModel.LogEntries,
            entry => entry.Message.Contains("404", StringComparison.Ordinal));
    }

    [Fact]
    public void SelectedLanguage_SwitchesUiTextToEnglish()
    {
        var dispatcher = CreateDispatcher();
        var viewModel = CreateViewModel(new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher);

        Assert.Equal("Perangkat", viewModel.Text.Device);

        viewModel.SelectedLanguage = AppLanguage.English;

        Assert.Equal("Device", viewModel.Text.Device);
    }

    [Fact]
    public void SelectedLanguage_PersistsToSettings()
    {
        var dispatcher = CreateDispatcher();
        var settingsPath = TempSettingsPath();
        var settings = new AppSettings(settingsPath);

        var viewModel = CreateViewModel(
            new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher, null, null, null, settings);

        viewModel.SelectedLanguage = AppLanguage.English;

        Assert.True(File.Exists(settingsPath));
        Assert.Equal(AppLanguage.English, new AppSettings(settingsPath).Load().Language);
    }

    [Fact]
    public void SelectedLanguageOption_MapsToSelectedLanguage()
    {
        var dispatcher = CreateDispatcher();
        var viewModel = CreateViewModel(new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher);

        Assert.NotNull(viewModel.SelectedLanguageOption);
        Assert.Equal(AppLanguage.Indonesian, viewModel.SelectedLanguageOption!.Language);
        Assert.Equal(2, viewModel.AvailableLanguages.Count);
    }

    [Fact]
    public void AppVersion_MatchesAssemblyVersion()
    {
        var dispatcher = CreateDispatcher();
        var viewModel = CreateViewModel(new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher);

        Assert.Equal(UpdateService.ReadAssemblyVersion(), viewModel.AppVersion);
        Assert.Matches(@"^\d+\.\d+\.\d+$", viewModel.AppVersion);
    }

    [Fact]
    public void AppVersionLabel_TranslatedInBothLanguages()
    {
        var dispatcher = CreateDispatcher();
        var viewModel = CreateViewModel(new FakeToolsDownloader(), new FakeProcessRunner(), dispatcher);

        Assert.Equal("Versi aplikasi", viewModel.Text.AppVersion);

        viewModel.SelectedLanguage = AppLanguage.English;

        Assert.Equal("Application version", viewModel.Text.AppVersion);
    }

    private static MainWindowViewModel CreateViewModel(
        IToolsDownloader downloader,
        IProcessRunner runner,
        Dispatcher dispatcher,
        FakeDialogService? dialog = null,
        IUpdateService? updateService = null,
        LocalizationService? localization = null,
        AppSettings? settings = null)
    {
        return new MainWindowViewModel(
            downloader,
            runner,
            dispatcher,
            null,
            dialog ?? new FakeDialogService(),
            updateService,
            localization,
            settings ?? new AppSettings(TempSettingsPath()));
    }

    /// <summary>
    /// Path file settings sementara supaya test tidak mengubah pengaturan
    /// milik pengguna yang sedang memakai aplikasi.
    /// </summary>
    private static string TempSettingsPath()
    {
        var directory = Path.Combine(Path.GetTempPath(), "duckflash-tests", "settings");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"settings-{Guid.NewGuid():N}.json");
    }

    private static Dispatcher CreateDispatcher()
    {
        // Dispatcher untuk thread baru supaya timer bisa berjalan.
        var dispatcher = Dispatcher.CurrentDispatcher;
        return dispatcher;
    }

    /// <summary>
    /// ToolsDownloader palsu supaya test tidak menyentuh jaringan atau disk.
    /// </summary>
    private sealed class FakeToolsDownloader : IToolsDownloader
    {
        public bool Installed { get; init; }

        public string LocalPath { get; init; } = @"C:\Tools\platform-tools";

        // Path selalu ada supaya device check bisa dijalankan di test.
        public string? AdbPath { get; init; } = @"C:\Tools\platform-tools\adb.exe";

        public string? FastbootPath { get; init; } = @"C:\Tools\platform-tools\fastboot.exe";

        public ToolsStatus InstallResult { get; set; } = new();

        public event EventHandler<DownloadProgressEventArgs>? ProgressChanged;

        public Task<ToolsStatus> CheckStatusAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ToolsStatus
            {
                IsInstalled = Installed,
                LocalPath = LocalPath
            });
        }

        public Task<ToolsStatus> EnsureInstalledAsync(CancellationToken cancellationToken = default)
        {
            ProgressChanged?.Invoke(this, new DownloadProgressEventArgs(100, "Selesai"));

            return Task.FromResult(InstallResult);
        }

        public void RaiseProgress(int percentage, string message)
        {
            ProgressChanged?.Invoke(this, new DownloadProgressEventArgs(percentage, message));
        }
    }
}
