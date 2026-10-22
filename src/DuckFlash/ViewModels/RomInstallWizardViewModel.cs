using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DuckFlash.Models;
using DuckFlash.Services;

namespace DuckFlash.ViewModels;

/// <summary>
/// Wizard instalasi custom ROM. Mengguide pemula lewat langkah demi langkah:
/// cek perangkat, unlock bootloader, flash recovery, wipe data, reboot ke
/// recovery, sideload ROM, lalu reboot ke Android.
///
/// Semua command tetap melewati service yang sudah ada, jadi aturan anti-brick
/// (validasi file, konfirmasi partisi, peringatan data terhapus) tetap berkaaya berlaku.
/// </summary>
public partial class RomInstallWizardViewModel : ObservableObject
{
    private readonly AdbService _adbService;
    private readonly FastbootService _fastbootService;
    private readonly DeviceCheckService _deviceCheckService;
    private readonly FlashService _flashService;
    private readonly SideloadService _sideloadService;
    private readonly BootloaderService _bootloaderService;
    private readonly WipeService _wipeService;
    private readonly RebootService _rebootService;
    private readonly ErrorTranslator _errorTranslator;
    private readonly ITextProvider _text;
    private readonly IDialogService _dialogService;
    private readonly Dispatcher _dispatcher;

    /// <summary>Baris log yang belum sembat ditampilkan, dikumpulkan dari thread proses.</summary>
    private readonly List<LogEntry> _pendingEntries = new();

    /// <summary>Pengaman thread untuk daftar antranting log.</summary>
    private readonly object _pendingLock = new();

    /// <summary>Partisi tempat file recovery di-flash.</summary>
    private const string RecoveryPartition = "recovery";

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _deviceStatus = string.Empty;

    [ObservableProperty]
    private string _deviceDetails = string.Empty;

    [ObservableProperty]
    private string _bootloaderState = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private int _currentStepIndex;

    [ObservableProperty]
    private string _romFileName = string.Empty;

    [ObservableProperty]
    private string _romSha256 = string.Empty;

    [ObservableProperty]
    private string _romSize = string.Empty;

    /// <summary>Path penuh file .zip ROM yang akan di-sideload.</summary>
    [ObservableProperty]
    private string? _selectedRomPath;

    /// <summary>
    /// Serial perangkat yang dipakai untuk semua command. Wajib diisi kalau
    /// ada lebih dari satu perangkat.
    /// </summary>
    [ObservableProperty]
    private string? _deviceSerial;

    /// <summary>Path penuh file .img recovery yang akan di-flash.</summary>
    [ObservableProperty]
    private string? _selectedRecoveryPath;

    /// <summary>Path penuh file .img recovery, dengan ukuran file-nya.</summary>
    [ObservableProperty]
    private string _recoveryFileName = string.Empty;

    /// <summary>Ukuran file .img recovery dalam byte.</summary>
    [ObservableProperty]
    private long _recoveryFileSize;

    /// <summary>Hash SHA256 file .img recovery.</summary>
    [ObservableProperty]
    private string _recoverySha256 = string.Empty;

    /// <summary>Semua baris log, dari yang paling lama ke yang terbaru.</summary>
    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    /// <summary>Teks UI sesuai bahasa aktif, dipakai untuk binding di XAML.</summary>
    public UiText Text { get; }

    /// <summary>Daftar langkah wizard, ditampilkan sebagai daftar di sisi kiri.</summary>
    public ObservableCollection<WizardStepItem> Steps { get; } = new();

    /// <summary>Langkah yang sedang dikerjakan.</summary>
    public WizardStepItem? CurrentStep =>
        CurrentStepIndex >= 0 && CurrentStepIndex < Steps.Count
            ? Steps[CurrentStepIndex]
            : null;

    /// <summary>True kalau pengguna bisa menekan tombol "Berikutnya".</summary>
    public bool CanGoNext => CurrentStep?.CanProceed == true && !IsBusy;

    /// <summary>True kalau pengguna bisa menekan tombol "Kemb kembali".</summary>
    public bool CanGoBack => CurrentStepIndex > 0 && !IsBusy;

    /// <summary>True kalau langkah yang sedang aktif bisa dijalankan.</summary>
    public bool CanRunStep => !IsBusy;

    /// <summary>True kalau langkah yang sedang aktif boleh dilewati.</summary>
    public bool CanSkipStep => !IsBusy;

    public RomInstallWizardViewModel(
        AdbService adbService,
        FastbootService fastbootService,
        DeviceCheckService deviceCheckService,
        FlashService flashService,
        SideloadService sideloadService,
        BootloaderService bootloaderService,
        WipeService wipeService,
        RebootService rebootService,
        ErrorTranslator errorTranslator,
        ITextProvider text,
        IDialogService dialogService,
        Dispatcher dispatcher)
    {
        _adbService = adbService;
        _fastbootService = fastbootService;
        _deviceCheckService = deviceCheckService;
        _flashService = flashService;
        _sideloadService = sideloadService;
        _bootloaderService = bootloaderService;
        _wipeService = wipeService;
        _rebootService = rebootService;
        _errorTranslator = errorTranslator;
        _text = text;
        _dialogService = dialogService;
        _dispatcher = dispatcher;

        // Teks untuk binding XAML. Kalau sumber teksnya memang LocalizationService,
        // dipakai ulang supaya pilihan bahasa di jendela utama ikut terbaca.
        Text = new UiText(text as LocalizationService ?? new LocalizationService());

        BuildSteps();

        // AdbService dan FastbootService memakai ProcessRunner yang sama,
        // jadi cukup satu langganan. Kalau dua-duanya dilanggani, setiap
        // baris output akan muncul dua kali di log wizard.
        _adbService.OutputReceived += OnProcessOutputReceived;
        _adbService.CommandStarting += OnCommandStarting;

        ResetLocalizedState();
        AppendLog(_text.Get("app.ready"));
    }

    /// <summary>
    /// Bangun daftar langkah dengan judul dan deskripsi sesuai bahasa aktif.
    /// Dipanggil ulang setiap kali bahasa berubah.
    /// </summary>
    private void BuildSteps()
    {
        Steps.Clear();

        foreach (var step in Enum.GetValues<WizardStep>())
        {
            Steps.Add(new WizardStepItem
            {
                Step = step,
                Title = _text.Get($"wizard.step.{step}.title"),
                Description = _text.Get($"wizard.step.{step}.description")
            });
        }
    }

    /// <summary>Isi ulang nilai tampilan yang bergantung pada bahasa aktif.</summary>
    private void ResetLocalizedState()
    {
        StatusText = _text.Get("app.ready");
        DeviceStatus = _text.Get("app.status.ready");
        RomFileName = _text.Get("value.noRom");
        RecoveryFileName = _text.Get("value.noImage");
    }

    /// <summary>
    /// Muat ulang seluruh teks wizard setelah bahasa diganti.
    /// </summary>
    public void RefreshLocalization()
    {
        BuildSteps();
        ResetLocalizedState();
        Text.Refresh();
        OnPropertyChanged(nameof(CurrentStep));
        NotifyStepStateChanged();
    }

    #region Steps

    /// <summary>Pindah ke langkah berikutnya.</summary>
    [RelayCommand]
    private void NextStep()
    {
        if (CurrentStepIndex < Steps.Count - 1)
        {
            CurrentStepIndex++;
        }
    }

    /// <summary>Kemb kembali ke langkah sebelumnya.</summary>
    [RelayCommand]
    private void BackStep()
    {
        if (CurrentStepIndex > 0)
        {
            CurrentStepIndex--;
        }
    }

    /// <summary>Lewati langkah yang sedang aktif.</summary>
    [RelayCommand]
    private void SkipStep()
    {
        var step = CurrentStep;

        if (step is null)
        {
            return;
        }

        step.State = WizardStepState.Skipped;

        NotifyStepStateChanged();

        AppendLog(_text.Get("log.romWizardSkipped"), LogSeverity.Warning);

        NextStep();
    }

    /// <summary>
    /// Jalankan aksi dari langkah yang sedang aktif.
    /// </summary>
    [RelayCommand]
    private async Task RunCurrentStepAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var step = CurrentStep;

        if (step is null)
        {
            return;
        }

        // Kalau langkah dilewati, tidak perlu jalan.
        if (step.State == WizardStepState.Skipped)
        {
            return;
        }

        step.State = WizardStepState.Running;

        try
        {
            var succeeded = step.Step switch
            {
                WizardStep.CheckDevice => await RunCheckDeviceAsync(),
                WizardStep.UnlockBootloader => await RunUnlockBootloaderAsync(),
                WizardStep.FlashRecovery => await RunFlashRecoveryAsync(),
                WizardStep.WipeData => await RunWipeDataAsync(),
                WizardStep.RebootToRecovery => await RunRebootToRecoveryAsync(),
                WizardStep.SideloadRom => await RunSideloadRomAsync(),
                WizardStep.RebootToSystem => await RunRebootToSystemAsync(),
                _ => false
            };

            if (succeeded)
            {
                step.State = WizardStepState.Done;
            }
            else
            {
                step.State = WizardStepState.Failed;
            }

            NotifyStepStateChanged();
        }
        catch (OperationCanceledException)
        {
            step.State = WizardStepState.Failed;
            AppendLog(_text.Get("log.romWizardUserCancelled"), LogSeverity.Warning);
        }
        catch (Exception ex)
        {
            step.State = WizardStepState.Failed;
            AppendLog(_text.Get("log.romWizardError", step.Title, ex.Message), LogSeverity.Error);
            StatusText = _text.Get("log.romWizardError", step.Title, ex.Message);
        }
        finally
        {
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    /// <summary>
    /// Langkah 1: cek perangkat yang terhubung, baik lewat adb maupun fastboot.
    /// </summary>
    private async Task<bool> RunCheckDeviceAsync()
    {
        IsBusy = true;
        StatusText = _text.Get("status.romWizard.checkingDevice");

        var adbResult = await _deviceCheckService.CheckAdbAsync();
        var fastbootResult = await _deviceCheckService.CheckFastbootAsync();

        var adbDetected = adbResult.Status == DeviceCheckStatus.Detected;
        var fastbootDetected = fastbootResult.Status == DeviceCheckStatus.Detected;

        if (!adbDetected && !fastbootDetected)
        {
            var message = _text.Get("status.romWizard.deviceUnavailable");

            StatusText = message;
            AppendLog(message, LogSeverity.Error);
            _dialogService.ShowError(_text.Get("dialog.title.romDeviceUnavailable"), message);

            return false;
        }

        var serials = adbDetected
            ? adbResult.Devices.Select(device => device.Serial)
            : fastbootResult.Devices.Select(device => device.Serial);

        var distinctSerials = serials
            .Where(serial => !string.IsNullOrWhiteSpace(serial))
            .Distinct()
            .ToList();

        if (distinctSerials.Count > 1)
        {
            var message = _text.Get("dialog.deviceNotSelected");

            StatusText = message;
            AppendLog(message, LogSeverity.Error);
            _dialogService.ShowError(_text.Get("dialog.title.romDeviceUnavailable"), message);

            return false;
        }

        DeviceSerial = distinctSerials.FirstOrDefault();

        _adbService.DeviceSerial = DeviceSerial;
        _fastbootService.DeviceSerial = DeviceSerial;

        if (fastbootDetected)
        {
            var details = await _deviceCheckService.GetFastbootDeviceDetailsAsync(DeviceSerial!);

            BootloaderState = details.BootloaderState;
            DeviceDetails = _text.Get(
                "device.detailsFastboot",
                details.Codename,
                details.BootloaderState,
                details.ActiveSlot);
        }
        else
        {
            var details = await _deviceCheckService.GetAdbDeviceDetailsAsync(DeviceSerial!);

            DeviceDetails = _text.Get(
                "device.detailsAdb",
                details.Model,
                details.Codename,
                details.AndroidVersion);
        }

        DeviceStatus = _text.Get("device.adbDetected", DeviceSerial);
        StatusText = DeviceDetails;
        AppendLog(DeviceDetails, LogSeverity.Success);

        return true;
    }

    /// <summary>
    /// Langkah 2: buka kunci bootloader. Kalau sudah terbuka, dilewati.
    /// </summary>
    private async Task<bool> RunUnlockBootloaderAsync()
    {
        if (BootloaderState.Equals("unlocked", StringComparison.OrdinalIgnoreCase))
        {
            AppendLog(_text.Get("status.romWizard.bootloaderAlreadyOpen"), LogSeverity.Success);
            StatusText = _text.Get("status.romWizard.bootloaderAlreadyOpen");

            return true;
        }

        // Perangkat belum dalam mode fastboot, jadi pengguna harus mereboot
        // perangkatnya sendiri dulu.
        var blockingReason = _bootloaderService.ValidateAction(
            BootloaderAction.Unlock,
            BootloaderState,
            fastbootDetected: false);

        if (blockingReason is not null)
        {
            var message = _text.Get("bootloader.notInFastboot");

            StatusText = message;
            AppendLog(_text.Get("log.romWizardBlocked", message), LogSeverity.Warning);
            _dialogService.ShowError(_text.Get("dialog.title.romStateWrong"), message);

            return false;
        }

        // Aksi destruktif: konfirmasi khusus dengan peringatan data terhapus.
        var confirmation = BootloaderService.BuildConfirmationText(
            BootloaderAction.Unlock,
            DeviceSerial,
            _text);

        if (!_dialogService.ConfirmDestructive(
                _text.Get("dialog.title.bootloader", _text.Get("button.unlockBootloader")),
                confirmation,
                BootloaderService.BuildAcknowledgementLabel(BootloaderAction.Unlock, _text)))
        {
            AppendLog(_text.Get("log.romWizardUserCancelled"), LogSeverity.Warning);

            return false;
        }

        IsBusy = true;

        var result = await _bootloaderService.RunAsync(BootloaderAction.Unlock);

        if (result.IsSuccess)
        {
            BootloaderState = "unlocked";
            StatusText = _text.Get("status.bootloaderSuccess", _text.Get("button.unlockBootloader"));
            AppendLog(StatusText, LogSeverity.Success);

            return true;
        }

        ReportFailure(_text.Get("dialog.title.commandFailed", _text.Get("button.unlockBootloader")), result);

        return false;
    }

    /// <summary>
    /// Langkah 3: flash file recovery ke partisi recovery.
    /// </summary>
    private async Task<bool> RunFlashRecoveryAsync()
    {
        if (SelectedRecoveryPath is null)
        {
            var message = _text.Get("log.needImage");

            StatusText = message;
            AppendLog(message, LogSeverity.Error);

            return false;
        }

        var request = new FlashRequest
        {
            Partition = RecoveryPartition,
            ImagePath = SelectedRecoveryPath,
            FileName = RecoveryFileName,
            BootloaderState = BootloaderState,
            DeviceSerial = DeviceSerial,
            Sha256 = RecoverySha256,
            FileSize = RecoveryFileSize
        };

        // Anti-brick: validasi dulu, jangan kirim command kalau tidak aman.
        var blockingReason = _flashService.ValidateRequest(request);

        if (blockingReason is not null)
        {
            StatusText = blockingReason;
            AppendLog(_text.Get("log.romWizardBlocked", blockingReason), LogSeverity.Error);
            _dialogService.ShowError(_text.Get("dialog.title.romBlocked", blockingReason), blockingReason);

            return false;
        }

        var confirmation = _text.Get(
            "confirm.flashRecovery",
            request.Partition,
            request.FileName,
            FlashService.FormatSize(request.FileSize),
            request.Sha256);

        if (!_dialogService.Confirm(_text.Get("dialog.title.flash"), confirmation))
        {
            AppendLog(_text.Get("log.romWizardUserCancelled"), LogSeverity.Warning);

            return false;
        }

        IsBusy = true;

        var result = await _flashService.FlashAsync(request);

        if (result.IsSuccess)
        {
            StatusText = _text.Get("status.romWizard.flashSuccess");
            AppendLog(_text.Get("log.romWizardFlashComplete"), LogSeverity.Success);

            return true;
        }

        ReportFailure(_text.Get("dialog.title.romFlashFailed"), result);

        return false;
    }

    /// <summary>
    /// Langkah 4: hapus data pengguna. Konfirmasi khusus dengan peringatan penghapusan data.
    /// </summary>
    private async Task<bool> RunWipeDataAsync()
    {
        var confirmation = WipeService.BuildConfirmationText(DeviceSerial, _text);

        if (!_dialogService.ConfirmDestructive(
                _text.Get("dialog.title.romWipeConfirm"),
                confirmation,
                WipeService.BuildAcknowledgementLabel(_text)))
        {
            AppendLog(_text.Get("log.romWizardUserCancelled"), LogSeverity.Warning);

            return false;
        }

        IsBusy = true;

        var result = await _wipeService.WipeAsync(DeviceSerial);

        if (result.IsSuccess)
        {
            StatusText = _text.Get("status.romWizard.wipeSuccess");
            AppendLog(StatusText, LogSeverity.Success);

            return true;
        }

        ReportFailure(_text.Get("dialog.title.commandFailed", _text.Get("wizard.step.WipeData.title")), result);

        return false;
    }

    /// <summary>
    /// Langkah 5: reboot perangkat ke mode recovery supaya ROM bisa di-sideload.
    /// </summary>
    private async Task<bool> RunRebootToRecoveryAsync()
    {
        IsBusy = true;

        var result = await _rebootService.RebootAsync(RebootTarget.Recovery);

        if (result.IsSuccess)
        {
            StatusText = _text.Get("status.romWizard.rebootToRecoverySuccess");
            AppendLog(StatusText, LogSeverity.Success);
            AppendLog(_text.Get("status.romWizard.waitRecoveryHint"), LogSeverity.Warning);

            return true;
        }

        ReportFailure(_text.Get("dialog.title.romRebootFailed"), result);

        return false;
    }

    /// <summary>
    /// Langkah 6: sideload package ROM ke perangkat.
    /// </summary>
    private async Task<bool> RunSideloadRomAsync()
    {
        if (SelectedRomPath is null)
        {
            var message = _text.Get("log.needPackage");

            StatusText = message;
            AppendLog(message, LogSeverity.Error);

            return false;
        }

        var validation = FileValidator.Validate(
            SelectedRomPath,
            FileValidator.SideloadExtension,
            requireZipContent: true,
            _text);

        if (!validation.IsValid)
        {
            StatusText = validation.ErrorMessage ?? _text.Get("log.fileInvalid");
            AppendLog(StatusText, LogSeverity.Error);

            return false;
        }

        var confirmation = _text.Get(
            "confirm.sideload",
            validation.FileName,
            FlashService.FormatSize(validation.FileSize),
            validation.Sha256);

        if (!_dialogService.Confirm(_text.Get("dialog.title.sideload"), confirmation))
        {
            AppendLog(_text.Get("log.romWizardUserCancelled"), LogSeverity.Warning);

            return false;
        }

        IsBusy = true;

        var result = await _sideloadService.SideloadAsync(SelectedRomPath);

        if (result.IsSuccess)
        {
            StatusText = _text.Get("status.romWizard.sideloadSuccess");
            AppendLog(_text.Get("log.romWizardSideloadComplete"), LogSeverity.Success);

            return true;
        }

        ReportFailure(_text.Get("dialog.title.romSideloadFailed"), result);

        return false;
    }

    /// <summary>
    /// Langkah 7: reboot perangkat ke Android.
    /// </summary>
    private async Task<bool> RunRebootToSystemAsync()
    {
        if (!_dialogService.Confirm(
                _text.Get("dialog.title.romRebootFailed"),
                _text.Get("confirm.rebootSystem")))
        {
            AppendLog(_text.Get("log.romWizardUserCancelled"), LogSeverity.Warning);

            return false;
        }

        IsBusy = true;

        var result = await _rebootService.RebootAsync(RebootTarget.System);

        if (result.IsSuccess)
        {
            StatusText = _text.Get("status.romWizard.systemReached");
            AppendLog(StatusText, LogSeverity.Success);

            return true;
        }

        ReportFailure(_text.Get("dialog.title.romRebootFailed"), result);

        return false;
    }

    #endregion

    #region File selection

    /// <summary>
    /// Validasi dan set file .img recovery yang akan di-flash.
    /// </summary>
    public void SelectRecoveryFile(string? filePath)
    {
        var validation = FileValidator.Validate(
            filePath,
            FileValidator.FlashExtension,
            requireZipContent: false,
            _text);

        if (!validation.IsValid)
        {
            AppendLog(validation.ErrorMessage ?? _text.Get("log.fileInvalid"), LogSeverity.Error);
            StatusText = validation.ErrorMessage ?? _text.Get("log.fileInvalid");
            return;
        }

        SelectedRecoveryPath = validation.FilePath;
        RecoveryFileName = validation.FileName;
        RecoverySha256 = validation.Sha256;
        RecoveryFileSize = validation.FileSize;

        AppendLog(
            _text.Get("log.imageReady", validation.FileName, FlashService.FormatSize(validation.FileSize)),
            LogSeverity.Success);
        StatusText = _text.Get("status.imageReady");
    }

    /// <summary>
    /// Validasi dan set file .zip ROM yang akan di-sideload.
    /// </summary>
    public void SelectRomPackage(string? filePath)
    {
        var validation = FileValidator.Validate(
            filePath,
            FileValidator.SideloadExtension,
            requireZipContent: true,
            _text);

        if (!validation.IsValid)
        {
            AppendLog(validation.ErrorMessage ?? _text.Get("log.fileInvalid"), LogSeverity.Error);
            StatusText = validation.ErrorMessage ?? _text.Get("log.fileInvalid");
            return;
        }

        SelectedRomPath = validation.FilePath;
        RomFileName = validation.FileName;
        RomSha256 = validation.Sha256;
        RomSize = FlashService.FormatSize(validation.FileSize);

        AppendLog(_text.Get("log.packageReady", validation.FileName), LogSeverity.Success);
        StatusText = _text.Get("status.packageReady");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Terjemahkan kegagalan command dan tampilkan ke pengguna.
    /// </summary>
    private void ReportFailure(string title, CommandResult result)
    {
        var translated = _errorTranslator.Translate(result, useEnglish: false);

        AppendLog(_text.Get("status.commandFailed", title, translated.Title), LogSeverity.Error);
        AppendLog(translated.Explanation, LogSeverity.Error);
        AppendLog(_text.Get("log.solution", translated.SolutionLabel, translated.Solution), LogSeverity.Warning);

        if (!string.IsNullOrWhiteSpace(translated.RawMessage))
        {
            AppendLog(_text.Get("log.detail", translated.DetailLabel, translated.RawMessage), LogSeverity.Error);
        }

        StatusText = _text.Get("status.commandFailed", title, translated.Title);
        _dialogService.ShowError(title, translated.Formatted);
    }

    private void OnProcessOutputReceived(object? sender, ProcessOutputLineEventArgs e)
    {
        EnqueueLog(new LogEntry(e.Line));
    }

    private void OnCommandStarting(object? sender, ProcessCommandEventArgs e)
    {
        // Preview command: command lengkap ditulis ke log sebelum dijalankan.
        EnqueueLog(new LogEntry($"> {e.CommandLine}", LogSeverity.Success));
    }

    private void FlushPendingEntries()
    {
        LogEntry[] entries;

        lock (_pendingLock)
        {
            if (_pendingEntries.Count == 0)
            {
                return;
            }

            entries = _pendingEntries.ToArray();
            _pendingEntries.Clear();
        }

        foreach (var entry in entries)
        {
            LogEntries.Add(entry);
        }
    }

    private void EnqueueLog(LogEntry entry)
    {
        if (_dispatcher.CheckAccess())
        {
            LogEntries.Add(entry);
            return;
        }

        lock (_pendingLock)
        {
            _pendingEntries.Add(entry);
        }
    }

    private void AppendLog(string message, LogSeverity severity = LogSeverity.Info)
    {
        EnqueueLog(new LogEntry(message, severity));
    }

    /// <summary>
    /// Setiap kali lang aktif berubah, perbarui properti turutan yang
    /// dependant padanya.
    /// </summary>
    partial void OnCurrentStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
    }

    /// <summary>
    /// Saat proses berjalan, tombol navigasi ikut dinonaktifkan.
    /// </summary>
    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanRunStep));
        OnPropertyChanged(nameof(CanSkipStep));
    }

    /// <summary>
    /// Status langkah berubah, jadi status tombol "Berikutnya" ikut diperbarui.
    /// </summary>
    private void NotifyStepStateChanged()
    {
        OnPropertyChanged(nameof(CanGoNext));
    }

    #endregion
}
