using System.Windows.Threading;
using DuckFlash.Models;
using DuckFlash.Services;
using DuckFlash.ViewModels;
using Xunit;

namespace DuckFlash.Tests;

/// <summary>
/// Uji debloater. Semua perintah adb dijalankan lewat runner palsu, jadi tidak
/// ada aplikasi yang benar-benar dihapus dari perangkat.
/// </summary>
public class DebloatServiceTests
{
    [Fact]
    public void ParsePackages_ReadsPackageNames()
    {
        var output = "package:com.android.chrome\npackage:com.example.app\n";

        var packages = DebloatService.ParsePackages(output);

        Assert.Equal(new[] { "com.android.chrome", "com.example.app" }, packages);
    }

    [Fact]
    public void ParsePackages_IgnoresOtherLines()
    {
        var output = "List of packages\npackage:com.example.app\nError: something\n";

        var packages = DebloatService.ParsePackages(output);

        Assert.Equal(new[] { "com.example.app" }, packages);
    }

    [Fact]
    public void ParsePackages_EmptyOutput_ReturnsEmptyList()
    {
        var packages = DebloatService.ParsePackages(string.Empty);

        Assert.Empty(packages);
    }

    [Fact]
    public async Task UninstallAsync_UsesUserZeroFlag()
    {
        var runner = new ScriptedProcessRunner();
        var service = new DebloatService(new AdbService(runner, new FakeToolPaths()));

        await service.UninstallAsync("com.example.app");

        Assert.Equal(
            new[] { "shell", "pm", "uninstall", "--user", "0", "com.example.app" },
            runner.LastArguments);
    }

    [Fact]
    public async Task UninstallAsync_WithInvalidPackageName_Throws()
    {
        var runner = new ScriptedProcessRunner();
        var service = new DebloatService(new AdbService(runner, new FakeToolPaths()));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UninstallAsync("com.example.app; rm -rf /"));
    }

    [Fact]
    public void IsValidPackageName_AcceptsNormalName()
    {
        Assert.True(DebloatService.IsValidPackageName("com.android.chrome"));
        Assert.True(DebloatService.IsValidPackageName("com.example.my_app2"));
    }

    [Fact]
    public void IsValidPackageName_RejectsUnsafeName()
    {
        Assert.False(DebloatService.IsValidPackageName("com.example.app; reboot"));
        Assert.False(DebloatService.IsValidPackageName(""));
        Assert.False(DebloatService.IsValidPackageName(null));
    }

    [Fact]
    public void IsUninstallSuccessful_DetectsSuccessMessage()
    {
        var success = new CommandResult { ExitCode = 0, StandardOutput = "Success" };
        var failure = new CommandResult { ExitCode = 0, StandardOutput = "Failure [DELETE_FAILED_INTERNAL_ERROR]" };

        Assert.True(DebloatService.IsUninstallSuccessful(success));
        Assert.False(DebloatService.IsUninstallSuccessful(failure));
    }

    [Fact]
    public void BuildConfirmationText_ListsPackageNames()
    {
        var text = DebloatService.BuildConfirmationText(
            new[] { "com.a", "com.b" },
            LocalizationService.Default);

        Assert.Contains("com.a", text);
        Assert.Contains("com.b", text);
    }

    [Fact]
    public void BuildConfirmationText_EmptyList_MentionsNothingSelected()
    {
        var text = DebloatService.BuildConfirmationText(
            Array.Empty<string>(),
            LocalizationService.Default);

        Assert.Contains("Belum ada", text);
    }
}

/// <summary>
/// Uji ViewModel debloater: pembacaan daftar, penyaringan, dan penghapusan.
/// </summary>
public class DebloatViewModelTests
{
    [Fact]
    public async Task LoadPackagesAsync_ReadsUserPackages()
    {
        var runner = new ScriptedProcessRunner
        {
            Responder = args => args.Contains("-3")
                ? new CommandResult { ExitCode = 0, StandardOutput = "package:com.a\npackage:com.b\n" }
                : new CommandResult { ExitCode = 0, StandardOutput = "package:com.android.system\n" }
        };

        var viewModel = CreateViewModel(runner);

        await viewModel.LoadPackagesCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Packages.Count);
        Assert.Equal(2, viewModel.VisiblePackages.Count);
        Assert.DoesNotContain(viewModel.Packages, package => package.IsSystem);
    }

    [Fact]
    public async Task LoadPackagesAsync_WithShowSystemApps_AlsoReadsSystemPackages()
    {
        var runner = new ScriptedProcessRunner
        {
            Responder = args => args.Contains("-3")
                ? new CommandResult { ExitCode = 0, StandardOutput = "package:com.a\n" }
                : new CommandResult { ExitCode = 0, StandardOutput = "package:com.android.system\n" }
        };

        var viewModel = CreateViewModel(runner);
        viewModel.ShowSystemApps = true;

        await viewModel.LoadPackagesCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Packages.Count);
        Assert.Equal(2, viewModel.VisiblePackages.Count);
    }

    [Fact]
    public async Task SearchText_FiltersVisiblePackages()
    {
        var runner = new ScriptedProcessRunner
        {
            Responder = _ => new CommandResult
            {
                ExitCode = 0,
                StandardOutput = "package:com.android.chrome\npackage:com.example.app\n"
            }
        };

        var viewModel = CreateViewModel(runner);

        await viewModel.LoadPackagesCommand.ExecuteAsync(null);

        viewModel.SearchText = "chrome";

        Assert.Single(viewModel.VisiblePackages);
        Assert.Equal("com.android.chrome", viewModel.VisiblePackages[0].PackageName);
    }

    [Fact]
    public async Task SelectAllAndClearSelection_UpdateSelectedCount()
    {
        var runner = new ScriptedProcessRunner
        {
            Responder = _ => new CommandResult
            {
                ExitCode = 0,
                StandardOutput = "package:com.a\npackage:com.b\n"
            }
        };

        var viewModel = CreateViewModel(runner);

        await viewModel.LoadPackagesCommand.ExecuteAsync(null);

        viewModel.SelectAllCommand.Execute(null);
        Assert.Equal(2, viewModel.SelectedCount);

        viewModel.ClearSelectionCommand.Execute(null);
        Assert.Equal(0, viewModel.SelectedCount);
    }

    [Fact]
    public async Task UninstallSelected_WithoutConfirmation_SendsNoCommand()
    {
        var runner = new ScriptedProcessRunner
        {
            Responder = _ => new CommandResult { ExitCode = 0, StandardOutput = "package:com.a\n" }
        };

        var dialog = new FakeDialogService { DestructiveConfirmResult = false };
        var viewModel = CreateViewModel(runner, dialog);

        await viewModel.LoadPackagesCommand.ExecuteAsync(null);
        viewModel.SelectAllCommand.Execute(null);

        var callsBefore = runner.Calls.Count;

        await viewModel.UninstallSelectedCommand.ExecuteAsync(null);

        Assert.Equal(1, dialog.DestructiveConfirmCount);
        Assert.Equal(callsBefore, runner.Calls.Count);
    }

    [Fact]
    public async Task UninstallSelected_WithConfirmation_SendsUninstallPerPackage()
    {
        var runner = new ScriptedProcessRunner
        {
            Responder = args => args.Contains("uninstall")
                ? new CommandResult { ExitCode = 0, StandardOutput = "Success\n" }
                : new CommandResult { ExitCode = 0, StandardOutput = "package:com.a\npackage:com.b\n" }
        };

        var dialog = new FakeDialogService { DestructiveConfirmResult = true };
        var viewModel = CreateViewModel(runner, dialog);

        await viewModel.LoadPackagesCommand.ExecuteAsync(null);
        viewModel.SelectAllCommand.Execute(null);

        await viewModel.UninstallSelectedCommand.ExecuteAsync(null);

        var uninstallCalls = runner.Calls
            .Where(args => args.Contains("uninstall"))
            .ToList();

        Assert.Equal(2, uninstallCalls.Count);
        Assert.Contains("com.a", uninstallCalls[0]);
        Assert.Contains("com.b", uninstallCalls[1]);
    }

    [Fact]
    public async Task UninstallSelected_RefreshesPackagesAndReenablesLoadButton()
    {
        var userPackageListCalls = 0;
        var runner = new ScriptedProcessRunner
        {
            Responder = args =>
            {
                if (args.Contains("uninstall"))
                {
                    return new CommandResult { ExitCode = 0, StandardOutput = "Success\n" };
                }

                if (args.Contains("-3"))
                {
                    userPackageListCalls++;
                    return new CommandResult
                    {
                        ExitCode = 0,
                        StandardOutput = userPackageListCalls == 1 ? "package:com.a\n" : string.Empty
                    };
                }

                return new CommandResult { ExitCode = 0 };
            }
        };

        var dialog = new FakeDialogService { DestructiveConfirmResult = true };
        var viewModel = CreateViewModel(runner, dialog);

        await viewModel.LoadPackagesCommand.ExecuteAsync(null);
        viewModel.SelectAllCommand.Execute(null);
        await viewModel.UninstallSelectedCommand.ExecuteAsync(null);

        Assert.Equal(2, userPackageListCalls);
        Assert.Empty(viewModel.Packages);
        Assert.Empty(viewModel.VisiblePackages);
        Assert.Equal(0, viewModel.SelectedCount);
        Assert.False(viewModel.IsBusy);
        Assert.True(viewModel.CanInteract);
    }

    [Fact]
    public async Task UninstallSelected_WithoutSelection_DoesNothing()
    {
        var runner = new ScriptedProcessRunner();
        var dialog = new FakeDialogService { DestructiveConfirmResult = true };
        var viewModel = CreateViewModel(runner, dialog);

        await viewModel.UninstallSelectedCommand.ExecuteAsync(null);

        Assert.Equal(0, dialog.DestructiveConfirmCount);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Log_ContainsCommandPreview()
    {
        var runner = new ScriptedProcessRunner
        {
            Responder = args => args.Contains("uninstall")
                ? new CommandResult { ExitCode = 0, StandardOutput = "Success\n" }
                : new CommandResult { ExitCode = 0, StandardOutput = "package:com.a\n" }
        };

        var dialog = new FakeDialogService { DestructiveConfirmResult = true };
        var viewModel = CreateViewModel(runner, dialog);

        await viewModel.LoadPackagesCommand.ExecuteAsync(null);
        viewModel.SelectAllCommand.Execute(null);
        await viewModel.UninstallSelectedCommand.ExecuteAsync(null);

        Assert.Contains(
            viewModel.LogEntries,
            entry => entry.Formatted.Contains("> ", StringComparison.Ordinal));
    }

    private static DebloatViewModel CreateViewModel(
        IProcessRunner runner,
        FakeDialogService? dialog = null)
    {
        var adb = new AdbService(runner, new FakeToolPaths());
        var text = LocalizationService.Default;

        return new DebloatViewModel(
            adb,
            new DebloatService(adb, text),
            new ErrorTranslator(),
            text,
            dialog ?? new FakeDialogService(),
            Dispatcher.CurrentDispatcher);
    }
}

/// <summary>
/// Runner palsu yang bisa membalas berbeda-bedanya bergantung pada argumen,
/// dan mencatat semua pemanggilan yang masuk.
/// </summary>
internal sealed class ScriptedProcessRunner : IProcessRunner
{
    public List<string[]> Calls { get; } = new();

    public Func<string[], CommandResult> Responder { get; set; } =
        _ => new CommandResult { ExitCode = 0 };

    public string[] LastArguments => Calls.Count > 0 ? Calls[^1] : Array.Empty<string>();

    public event EventHandler<ProcessOutputLineEventArgs>? OutputReceived;

    public event EventHandler<ProcessCommandEventArgs>? CommandStarting;

    public Task<CommandResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var args = arguments.ToArray();

        Calls.Add(args);

        CommandStarting?.Invoke(this, new ProcessCommandEventArgs(
            $"{fileName} {string.Join(' ', args)}"));

        var result = Responder(args);

        foreach (var line in result.CombinedOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            OutputReceived?.Invoke(this, new ProcessOutputLineEventArgs(
                line.TrimEnd('\r'), ProcessOutputSource.StandardOutput));
        }

        return Task.FromResult(result);
    }
}
