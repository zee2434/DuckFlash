using System.IO.Compression;
using System.Windows.Threading;
using DuckFlash.Models;
using DuckFlash.Services;
using DuckFlash.ViewModels;
using DuckFlash.Views;
using Xunit;

namespace DuckFlash.Tests;

/// <summary>
/// Uji wizard instalasi custom ROM. Semua perintah dijalankan lewat
/// FakeProcessRunner, jadi tidak ada command yang benar-benar dikirim ke
/// perangkat. Aturan anti-brick (validasi file, partisi, konfirmasi, dan
/// pemblokiran saat bootloader terkunci) ikut diuji di sini.
/// </summary>
public class RomInstallWizardViewModelTests
{
    [Fact]
    public void Steps_AreSevenStepsInCorrectOrder()
    {
        var (viewModel, _, _) = CreateViewModel();

        Assert.Equal(7, viewModel.Steps.Count);
        Assert.Equal(WizardStep.CheckDevice, viewModel.Steps[0].Step);
        Assert.Equal(WizardStep.UnlockBootloader, viewModel.Steps[1].Step);
        Assert.Equal(WizardStep.FlashRecovery, viewModel.Steps[2].Step);
        Assert.Equal(WizardStep.WipeData, viewModel.Steps[3].Step);
        Assert.Equal(WizardStep.RebootToRecovery, viewModel.Steps[4].Step);
        Assert.Equal(WizardStep.SideloadRom, viewModel.Steps[5].Step);
        Assert.Equal(WizardStep.RebootToSystem, viewModel.Steps[6].Step);
    }

    [Fact]
    public void Steps_HaveTitleAndDescription()
    {
        var (viewModel, _, _) = CreateViewModel();

        Assert.All(viewModel.Steps, step =>
        {
            Assert.False(string.IsNullOrWhiteSpace(step.Title));
            Assert.False(string.IsNullOrWhiteSpace(step.Description));
        });
    }

    [Fact]
    public void NextStep_MovesForward_AndStopsAtLastStep()
    {
        var (viewModel, _, _) = CreateViewModel();

        viewModel.NextStepCommand.Execute(null);
        Assert.Equal(1, viewModel.CurrentStepIndex);

        for (var i = 0; i < 10; i++)
        {
            viewModel.NextStepCommand.Execute(null);
        }

        Assert.Equal(viewModel.Steps.Count - 1, viewModel.CurrentStepIndex);
    }

    [Fact]
    public void BackStep_MovesBack_AndCannotGoBeforeFirstStep()
    {
        var (viewModel, _, _) = CreateViewModel();

        viewModel.NextStepCommand.Execute(null);
        viewModel.BackStepCommand.Execute(null);

        Assert.Equal(0, viewModel.CurrentStepIndex);

        viewModel.BackStepCommand.Execute(null);

        Assert.Equal(0, viewModel.CurrentStepIndex);
    }

    [Fact]
    public void CanGoNext_IsFalse_UntilCurrentStepSucceeds()
    {
        var (viewModel, _, _) = CreateViewModel();

        Assert.False(viewModel.CanGoNext);

        viewModel.Steps[0].State = WizardStepState.Done;

        Assert.True(viewModel.CanGoNext);
    }

    [Fact]
    public void SkipStep_MarksStepAsSkipped_AndMovesNext()
    {
        var (viewModel, _, _) = CreateViewModel();

        viewModel.SkipStepCommand.Execute(null);

        Assert.Equal(WizardStepState.Skipped, viewModel.Steps[0].State);
        Assert.Equal(1, viewModel.CurrentStepIndex);
    }

    [Fact]
    public void SelectRecoveryFile_WithWrongExtension_IsRejected()
    {
        var (viewModel, _, _) = CreateViewModel();
        var path = CreateTempFile(".txt", "bukan image");

        try
        {
            viewModel.SelectRecoveryFile(path);

            Assert.Null(viewModel.SelectedRecoveryPath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SelectRecoveryFile_WithMissingFile_IsRejected()
    {
        var (viewModel, _, _) = CreateViewModel();

        viewModel.SelectRecoveryFile(Path.Combine(Path.GetTempPath(), "tidak-ada.img"));

        Assert.Null(viewModel.SelectedRecoveryPath);
    }

    [Fact]
    public void SelectRecoveryFile_WithValidImage_FillsNameSizeAndSha256()
    {
        var (viewModel, _, _) = CreateViewModel();
        var path = CreateTempFile(".img", "isi image recovery");

        try
        {
            viewModel.SelectRecoveryFile(path);

            Assert.NotNull(viewModel.SelectedRecoveryPath);
            Assert.Equal(Path.GetFileName(path), viewModel.RecoveryFileName);
            Assert.True(viewModel.RecoveryFileSize > 0);
            Assert.Equal(64, viewModel.RecoverySha256.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SelectRomPackage_WithBrokenZip_IsRejected()
    {
        var (viewModel, _, _) = CreateViewModel();
        var path = CreateTempFile(".zip", "ini bukan arsip zip");

        try
        {
            viewModel.SelectRomPackage(path);

            Assert.Null(viewModel.SelectedRomPath);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SelectRomPackage_WithValidZip_FillsNameAndSha256()
    {
        var (viewModel, _, _) = CreateViewModel();
        var path = CreateTempZip();

        try
        {
            viewModel.SelectRomPackage(path);

            Assert.NotNull(viewModel.SelectedRomPath);
            Assert.Equal(Path.GetFileName(path), viewModel.RomFileName);
            Assert.Equal(64, viewModel.RomSha256.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunCurrentStep_FlashRecovery_WithoutFile_DoesNotSendCommand()
    {
        var (viewModel, runner, _) = CreateViewModel();
        viewModel.CurrentStepIndex = 2;

        await viewModel.RunCurrentStepCommand.ExecuteAsync(null);

        Assert.Equal(WizardStepState.Failed, viewModel.Steps[2].State);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public async Task RunCurrentStep_FlashRecovery_WithLockedBootloader_IsBlocked()
    {
        var (viewModel, runner, dialog) = CreateViewModel();
        var path = CreateTempFile(".img", "isi image recovery");

        try
        {
            viewModel.SelectRecoveryFile(path);
            viewModel.BootloaderState = "locked";
            viewModel.CurrentStepIndex = 2;

            await viewModel.RunCurrentStepCommand.ExecuteAsync(null);

            Assert.Equal(WizardStepState.Failed, viewModel.Steps[2].State);
            Assert.Equal(0, runner.CallCount);
            Assert.True(dialog.ErrorCount > 0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunCurrentStep_FlashRecovery_Confirmed_FlashesRecoveryPartition()
    {
        var (viewModel, runner, dialog) = CreateViewModel();
        dialog.ConfirmResult = true;
        var path = CreateTempFile(".img", "isi image recovery");

        try
        {
            viewModel.SelectRecoveryFile(path);
            viewModel.CurrentStepIndex = 2;

            await viewModel.RunCurrentStepCommand.ExecuteAsync(null);

            Assert.Equal(WizardStepState.Done, viewModel.Steps[2].State);
            Assert.Contains("flash", runner.LastArguments);
            Assert.Contains("recovery", runner.LastArguments);
            Assert.Contains(Path.GetFullPath(path), runner.LastArguments);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunCurrentStep_FlashRecovery_NotConfirmed_DoesNotSendCommand()
    {
        var (viewModel, runner, dialog) = CreateViewModel();
        dialog.ConfirmResult = false;
        var path = CreateTempFile(".img", "isi image recovery");

        try
        {
            viewModel.SelectRecoveryFile(path);
            viewModel.CurrentStepIndex = 2;

            await viewModel.RunCurrentStepCommand.ExecuteAsync(null);

            Assert.Equal(WizardStepState.Failed, viewModel.Steps[2].State);
            Assert.Equal(0, runner.CallCount);
            Assert.Equal(1, dialog.ConfirmCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task RunCurrentStep_SideloadRom_WithoutFile_DoesNotSendCommand()
    {
        var (viewModel, runner, _) = CreateViewModel();
        viewModel.CurrentStepIndex = 5;

        await viewModel.RunCurrentStepCommand.ExecuteAsync(null);

        Assert.Equal(WizardStepState.Failed, viewModel.Steps[5].State);
        Assert.Equal(0, runner.CallCount);
    }

    [Fact]
    public async Task RunCurrentStep_WipeData_RequiresDestructiveConfirmation()
    {
        var (viewModel, runner, dialog) = CreateViewModel();
        dialog.DestructiveConfirmResult = false;
        viewModel.CurrentStepIndex = 3;

        await viewModel.RunCurrentStepCommand.ExecuteAsync(null);

        Assert.Equal(1, dialog.DestructiveConfirmCount);
        Assert.Equal(0, runner.CallCount);
        Assert.Equal(WizardStepState.Failed, viewModel.Steps[3].State);
    }

    [Fact]
    public async Task RunCurrentStep_WipeData_Confirmed_RunsFastbootWipe()
    {
        var (viewModel, runner, dialog) = CreateViewModel();
        dialog.DestructiveConfirmResult = true;
        viewModel.CurrentStepIndex = 3;

        await viewModel.RunCurrentStepCommand.ExecuteAsync(null);

        Assert.Equal(1, dialog.DestructiveConfirmCount);
        Assert.Contains("-w", runner.LastArguments);
        Assert.Equal(WizardStepState.Done, viewModel.Steps[3].State);
    }

    [Fact]
    public async Task RunCurrentStep_WipeData_WritesCommandPreviewToLog()
    {
        var (viewModel, _, dialog) = CreateViewModel();
        dialog.DestructiveConfirmResult = true;
        viewModel.CurrentStepIndex = 3;

        await viewModel.RunCurrentStepCommand.ExecuteAsync(null);

        Assert.Contains(
            viewModel.LogEntries,
            entry => entry.Formatted.Contains("> ", StringComparison.Ordinal));
    }

    [Fact]
    public void CreateRomInstallWizard_UsesSameServicesAsMainWindow()
    {
        var runner = new FakeProcessRunner();
        var viewModel = new MainWindowViewModel(
            null,
            runner,
            Dispatcher.CurrentDispatcher,
            null,
            new FakeDialogService(),
            null,
            null,
            new AppSettings(Path.Combine(Path.GetTempPath(), "duckflash-tests", "wizard-settings.json")));

        var wizard = viewModel.CreateRomInstallWizard();

        Assert.Equal(7, wizard.Steps.Count);
        Assert.NotNull(wizard.Text);
    }

    [Fact]
    public void WizardWindow_CreatesWithoutXamlErrors()
    {
        Exception? error = null;

        var thread = new Thread(() =>
        {
            try
            {
                var (viewModel, _, _) = CreateViewModel();
                var window = new RomInstallWizardWindow(viewModel);

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

        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "Jendela wizard tidak selesai dimuat");
        Assert.Null(error);
    }

    private static (RomInstallWizardViewModel ViewModel, FakeProcessRunner Runner, FakeDialogService Dialog) CreateViewModel()
    {
        var runner = new FakeProcessRunner();
        var dialog = new FakeDialogService();
        var paths = new FakeToolPaths();
        var adb = new AdbService(runner, paths);
        var fastboot = new FastbootService(runner, paths);
        var text = LocalizationService.Default;

        var viewModel = new RomInstallWizardViewModel(
            adb,
            fastboot,
            new DeviceCheckService(adb, fastboot, paths, text),
            new FlashService(fastboot, text),
            new SideloadService(adb, text),
            new BootloaderService(fastboot, text),
            new WipeService(fastboot, text),
            new RebootService(adb, fastboot),
            new ErrorTranslator(),
            text,
            dialog,
            Dispatcher.CurrentDispatcher);

        return (viewModel, runner, dialog);
    }

    private static string CreateTempFile(string extension, string content)
    {
        var directory = Path.Combine(Path.GetTempPath(), "duckflash-tests", "wizard");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"file-{Guid.NewGuid():N}{extension}");
        File.WriteAllText(path, content);

        return path;
    }

    private static string CreateTempZip()
    {
        var directory = Path.Combine(Path.GetTempPath(), "duckflash-tests", "wizard");
        Directory.CreateDirectory(directory);

        var path = Path.Combine(directory, $"rom-{Guid.NewGuid():N}.zip");

        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("payload.img");
            using var stream = entry.Open();
            stream.Write(new byte[] { 1, 2, 3, 4 });
        }

        return path;
    }
}
