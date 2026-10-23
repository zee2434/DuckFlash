using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DuckFlash.Models;
using DuckFlash.Services;

namespace DuckFlash.ViewModels;

/// <summary>
/// State utama jendela. Logika aplikasi tidak boleh diletakkan di code-behind XAML.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IToolsDownloader _toolsDownloader;
    private readonly IProcessRunner _processRunner;
    private readonly AdbService _adbService;
    private readonly FastbootService _fastbootService;
    private readonly DeviceCheckService _deviceCheckService;
    private readonly FlashService _flashService;
    private readonly SideloadService _sideloadService;
    private readonly InstallService _installService;
    private readonly RebootService _rebootService;
    private readonly DriverService _driverService;
    private readonly BootloaderService _bootloaderService;
    private readonly IUpdateService _updateService;
    private readonly LocalizationService _localization;
    private readonly AppSettings _settings;
    private readonly ErrorTranslator _errorTranslator;
    private readonly IDialogService _dialogService;
    private readonly DebloatService _debloatService;
    private readonly ScreenshotService _screenshotService;
    private readonly LogcatService _logcatService;
    private readonly BackupService _backupService;    private readonly DispatcherTimer _logTimer;
    private readonly Dispatcher _dispatcher;
    private volatile bool _suppressProcessOutput;

    /// <summary>Baris log yang belum sempat ditampilkan, dikumpulkan dari thread proses.</summary>
    private readonly List<LogEntry> _pendingEntries = new();

    /// <summary>Pengaman thread untuk daftar antrean log.</summary>
    private readonly object _pendingLock = new();

    /// <summary>Persentase terakhir yang dilaporkan service, menunggu flushed ke UI.</summary>
    private int _pendingProgress;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _platformToolsStatus = string.Empty;

    /// <summary>
    /// Status platform-tools dalam format siap tampil, mengikuti bahasa aktif.
    /// </summary>
    public string PlatformToolsStatusText =>
        IsPlatformToolsInstalled
            ? Text.Get("status.platformTools.installed", PlatformToolsPath)
            : Text.Get("status.platformTools.missing");

    /// <summary>Path folder platform-tools yang terpasang.</summary>
    public string PlatformToolsPath { get; private set; } = string.Empty;

    [ObservableProperty]
    private string _deviceStatus = string.Empty;

    [ObservableProperty]
    private string _deviceDetails = string.Empty;

    [ObservableProperty]
    private int _downloadProgress;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunCommand))]
    [NotifyPropertyChangedFor(nameof(PlatformToolsStatusText))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlatformToolsStatusText))]
    private bool _isPlatformToolsInstalled;

    [ObservableProperty]
    private DeviceCheckStatus _adbStatus = DeviceCheckStatus.NotChecked;

    [ObservableProperty]
    private DeviceCheckStatus _fastbootStatus = DeviceCheckStatus.NotChecked;

    [ObservableProperty]
    private string? _selectedImagePath;

    [ObservableProperty]
    private string? _selectedPackagePath;

    [ObservableProperty]
    private string _imageFileName = string.Empty;

    [ObservableProperty]
    private string _imageSha256 = string.Empty;

    [ObservableProperty]
    private string _imageSize = string.Empty;

    [ObservableProperty]
    private long _imageFileSize;

    [ObservableProperty]
    private string _expectedSha256 = string.Empty;

    [ObservableProperty]
    private string _hashComparisonResult = string.Empty;

    [ObservableProperty]
    private string _packageFileName = string.Empty;

    [ObservableProperty]
    private string _packageSha256 = string.Empty;

    [ObservableProperty]
    private string? _selectedApkPath;

    [ObservableProperty]
    private string _apkFileName = string.Empty;

    [ObservableProperty]
    private string _apkSize = string.Empty;

    [ObservableProperty]
    private string _partition = "recovery";

    [ObservableProperty]
    private string _bootloaderState = string.Empty;

    [ObservableProperty]
    private string _activeSlot = string.Empty;

    [ObservableProperty]
    private bool _isDragOver;

    [ObservableProperty]
    private string _updateStatus = string.Empty;

    /// <summary>Teks UI sesuai bahasa yang dipilih pengguna.</summary>
    public UiText Text { get; }

    /// <summary>Bahasa yang bisa dipilih pengguna.</summary>
    public IReadOnlyList<LanguageOption> AvailableLanguages { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedLanguageOption))]
    private AppLanguage _selectedLanguage = AppLanguage.Indonesian;

    /// <summary>Opsi bahasa yang sedang dipilih, untuk binding ComboBox.</summary>
    public LanguageOption? SelectedLanguageOption
    {
        get => AvailableLanguages.FirstOrDefault(option => option.Language == SelectedLanguage);
        set
        {
            if (value is not null)
            {
                SelectedLanguage = value.Language;
            }
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeviceUnambiguous))]
    private string? _selectedSerial;

    /// <summary>Semua baris log, dari yang paling lama ke yang terbaru.</summary>
    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    /// <summary>Partisi yang boleh dipilih untuk flash.</summary>
    public IReadOnlyList<string> AvailablePartitions => FlashPartition.Allowed;

    /// <summary>True kalau tidak sedang ada proses yang berjalan.</summary>
    public bool CanRunCommand => !IsBusy;

    /// <summary>Baris log siap disalin ke clipboard.</summary>
    public string LogText => string.Join(Environment.NewLine, LogEntries.Select(entry => entry.Formatted));

    /// <summary>
    /// Nomor versi aplikasi yang sedang berjalan. Ditampilkan di menu
    /// Pengaturan supaya pengguna bisa menyebutkan versinya saat melapor bug.
    /// </summary>
    public string AppVersion => UpdateService.ReadAssemblyVersion();

    /// <summary>Serial perangkat yang dipilih, untuk mode multi-device.</summary>
    public ObservableCollection<string> AvailableSerials { get; } = new();

    public MainWindowViewModel()
        : this(null, new ProcessRunner(), Dispatcher.CurrentDispatcher)
    {
    }

    /// <summary>
    /// Constructor untuk unit test. Semua service bisa diganti dengan mock.
    /// </summary>
    public MainWindowViewModel(
        IToolsDownloader? toolsDownloader,
        IProcessRunner processRunner,
        Dispatcher dispatcher,
        ErrorTranslator? errorTranslator = null,
        IDialogService? dialogService = null,
        IUpdateService? updateService = null,
        LocalizationService? localization = null,
        AppSettings? settings = null)
    {
        _processRunner = processRunner;
        _dispatcher = dispatcher;
        _errorTranslator = errorTranslator ?? new ErrorTranslator();

        // Bahasa yang dipilih disimpan di disk, jadi bahasa terakhir pilihan
        // user masih dipakai waktu aplikasi dibuka lagi.
        _localization = localization ?? new LocalizationService();
        _settings = settings ?? new AppSettings();

        _dialogService = dialogService ?? new MessageBoxDialogService(_localization);

        var savedLanguage = _settings.Load().Language;
        _localization.CurrentLanguage = savedLanguage;
        _selectedLanguage = savedLanguage;

        Text = new UiText(_localization);

        // Downloader dibuat di sini supaya pesan progress-nya ikut bahasa aktif.
        _toolsDownloader = toolsDownloader ?? new ToolsDownloader(null, null, _localization);

        _adbService = new AdbService(processRunner, _toolsDownloader);
        _fastbootService = new FastbootService(processRunner, _toolsDownloader);
        _deviceCheckService = new DeviceCheckService(_adbService, _fastbootService, _toolsDownloader, _localization);
        _flashService = new FlashService(_fastbootService, _localization);
        _sideloadService = new SideloadService(_adbService, _localization);
        _installService = new InstallService(_adbService);
        _rebootService = new RebootService(_adbService, _fastbootService);
        _driverService = new DriverService(processRunner, null, null, _localization);
        _bootloaderService = new BootloaderService(_fastbootService, _localization);
        _debloatService = new DebloatService(_adbService, _localization);
        _screenshotService = new ScreenshotService(_adbService, _localization);
        _logcatService = new LogcatService(_adbService, _localization);
        _backupService = new BackupService(_adbService, _localization);
        _updateService = updateService ?? new UpdateService(null, null, _localization);

        AvailableLanguages = new List<LanguageOption>
        {
            new(AppLanguage.Indonesian, "Bahasa Indonesia"),
            new(AppLanguage.English, "English")
        };

        _toolsDownloader.ProgressChanged += OnToolsProgressChanged;
        _processRunner.OutputReceived += OnProcessOutputReceived;
        _processRunner.CommandStarting += OnCommandStarting;
        _driverService.ProgressChanged += OnToolsProgressChanged;

        // Output proses datang dari thread lain, jadi dikumpulkan dulu lalu
        // ditulis ke ObservableCollection di thread UI.
        _logTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        _logTimer.Tick += OnLogTimerTick;
        _logTimer.Start();

        ResetLocalizedState();
        AppendLog(Text.Get("app.ready"));
    }

    /// <summary>
    /// Isi ulang nilai tampilan yang bergantung pada bahasa aktif.
    /// </summary>
    private void ResetLocalizedState()
    {
        StatusText = Text.Get("app.ready");
        PlatformToolsStatus = Text.Get("status.platformTools.notInstalled");
        DeviceStatus = Text.Get("app.status.ready");
        UpdateStatus = Text.Get("status.update.notChecked");
        ImageFileName = Text.Get("value.noImage");
        PackageFileName = Text.Get("value.noPackage");
        ApkFileName = Text.Get("value.noApk");
        BootloaderState = Text.Get("value.unknown");
        ActiveSlot = Text.Get("value.unknown");
    }

    #region Platform-tools

    /// <summary>
    /// Dipanggil sekali ketika jendela selesai dimuat, untuk mengecek
    /// apakah platform-tools sudah terpasang.
    /// </summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        await RefreshPlatformToolsStatusAsync();
    }

    /// <summary>
    /// Periksa apakah platform-tools sudah terpasang, tanpa mengunduh apa pun.
    /// </summary>
    [RelayCommand]
    private async Task RefreshPlatformToolsStatusAsync()
    {
        var status = await _toolsDownloader.CheckStatusAsync();

        IsPlatformToolsInstalled = status.IsInstalled;
        PlatformToolsPath = status.LocalPath ?? string.Empty;

        PlatformToolsStatus = PlatformToolsStatusText;

        AppendLog(status.IsInstalled
            ? Text.Get("status.platformTools.installedShort", PlatformToolsPath)
            : Text.Get("status.platformTools.notInstalled"),
            status.IsInstalled ? LogSeverity.Success : LogSeverity.Warning);
    }

    /// <summary>
    /// Unduh dan pasang platform-tools kalau belum ada.
    /// </summary>
    [RelayCommand]
    private async Task InstallPlatformToolsAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        DownloadProgress = 0;
        StatusText = Text.Get("status.platformTools.installing");

        try
        {
            var status = await _toolsDownloader.EnsureInstalledAsync();

            IsPlatformToolsInstalled = status.IsInstalled;
            DownloadProgress = status.IsInstalled ? 100 : 0;

            if (status.IsInstalled)
            {
                PlatformToolsPath = status.LocalPath ?? string.Empty;
                PlatformToolsStatus = PlatformToolsStatusText;
                StatusText = Text.Get("status.platformTools.ready");
                AppendLog(Text.Get("status.platformTools.installedShort", PlatformToolsPath), LogSeverity.Success);
            }
            else
            {
                PlatformToolsStatus = Text.Get("status.platformTools.failed");
                StatusText = Text.Get("status.platformTools.failed");
                AppendLog(status.ErrorMessage ?? Text.Get("status.platformTools.failed"), LogSeverity.Error);
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog(Text.Get("log.platformToolsInstallCancelled"), LogSeverity.Warning);
            StatusText = Text.Get("status.platformTools.failed");
        }
        catch (Exception ex)
        {
            AppendLog(Text.Get("log.platformToolsFailedDetail", ex.Message), LogSeverity.Error);
            StatusText = Text.Get("status.platformTools.failed");
        }
        finally
        {
            IsBusy = false;
        }
    }

    #endregion

    #region Device check

    /// <summary>
    /// Jalankan adb devices dan tampilkan statusnya.
    /// </summary>
    [RelayCommand]
    private async Task CheckAdbAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = Text.Get("status.checkingAdb");
        _suppressProcessOutput = true;

        try
        {
            var result = await _deviceCheckService.CheckAdbAsync();

            AdbStatus = result.Status;
            DeviceStatus = Text.Get("device.adbPrefix", result.Message);

            AppendLog($"[ADB] {result.Message}",
                result.Status == DeviceCheckStatus.Detected ? LogSeverity.Success : LogSeverity.Warning);
            AppendAdbDeviceList(result.Devices);

            UpdateSerials(result.Devices);

            if (result.Status == DeviceCheckStatus.Detected)
            {
                await LoadAdbDeviceDetailsAsync();
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog(Text.Get("log.adbCancelled"), LogSeverity.Warning);
        }
        finally
        {
            _suppressProcessOutput = false;
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    /// <summary>
    /// Jalankan fastboot devices dan tampilkan statusnya.
    /// </summary>
    [RelayCommand]
    private async Task CheckFastbootAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = Text.Get("status.checkingFastboot");
        _suppressProcessOutput = true;

        try
        {
            var result = await _deviceCheckService.CheckFastbootAsync();

            FastbootStatus = result.Status;
            DeviceStatus = Text.Get("device.fastbootPrefix", result.Message);

            AppendLog($"[Fastboot] {result.Message}",
                result.Status == DeviceCheckStatus.Detected ? LogSeverity.Success : LogSeverity.Warning);
            AppendFastbootDeviceList(result.Devices);

            UpdateSerials(result.Devices);

            if (result.Status == DeviceCheckStatus.Detected)
            {
                await LoadFastbootDeviceDetailsAsync();
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog(Text.Get("log.fastbootCancelled"), LogSeverity.Warning);
        }
        finally
        {
            _suppressProcessOutput = false;
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    private void AppendAdbDeviceList(IReadOnlyList<DeviceInfo> devices)
    {
        AppendLog("[ADB] List of devices attached");

        foreach (var device in devices)
        {
            var severity = device.Status.Equals("device", StringComparison.OrdinalIgnoreCase)
                ? LogSeverity.Success
                : LogSeverity.Warning;
            AppendLog($"[ADB] {device.Serial}\t{device.Status}", severity);
        }
    }

    private void AppendFastbootDeviceList(IReadOnlyList<DeviceInfo> devices)
    {
        foreach (var device in devices)
        {
            AppendLog($"[Fastboot] {device.Serial}\t{device.Status}", LogSeverity.Success);
        }
    }

    private async Task LoadAdbDeviceDetailsAsync()
    {
        var serial = SelectedSerial
            ?? AvailableSerials.FirstOrDefault();

        if (serial is null)
        {
            return;
        }

        try
        {
            var details = await _deviceCheckService.GetAdbDeviceDetailsAsync(serial);
            DeviceDetails = Text.Get(
                "device.detailsAdb",
                details.Model,
                details.Codename,
                details.AndroidVersion);
            AppendLog($"[ADB] {DeviceDetails}");
        }
        catch (Exception ex)
        {
            AppendLog(Text.Get("log.deviceInfoFailed", ex.Message), LogSeverity.Warning);
        }
    }

    private async Task LoadFastbootDeviceDetailsAsync()
    {
        var serial = SelectedSerial
            ?? AvailableSerials.FirstOrDefault();

        if (serial is null)
        {
            return;
        }

        try
        {
            var details = await _deviceCheckService.GetFastbootDeviceDetailsAsync(serial);
            BootloaderState = details.BootloaderState;
            ActiveSlot = details.ActiveSlot;
            DeviceDetails = Text.Get(
                "device.detailsFastboot",
                details.Codename,
                details.BootloaderState,
                details.ActiveSlot);
            AppendLog($"[Fastboot] {DeviceDetails}");
        }
        catch (Exception ex)
        {
            AppendLog(Text.Get("log.deviceInfoFailed", ex.Message), LogSeverity.Warning);
        }
    }

    private void UpdateSerials(IReadOnlyList<DeviceInfo> devices)
    {
        var serials = devices
            .Select(device => device.Serial)
            .Where(serial => !string.IsNullOrWhiteSpace(serial))
            .Distinct()
            .ToList();

        var previous = SelectedSerial;

        AvailableSerials.Clear();

        foreach (var serial in serials)
        {
            AvailableSerials.Add(serial);
        }

        // Pilih otomatis kalau hanya ada satu perangkat. Kalau lebih dari satu,
        // pilihan dikosongkan supaya pengguna wajib memilih sendiri.
        if (AvailableSerials.Count == 1)
        {
            SelectedSerial = AvailableSerials[0];
        }
        else if (!AvailableSerials.Contains(previous ?? string.Empty))
        {
            SelectedSerial = null;
        }

        OnPropertyChanged(nameof(IsDeviceUnambiguous));
    }

    #endregion

    #region File selection

    /// <summary>
    /// Dipanggil dari code-behind saat file .img dijatuhkan lewat drag and drop.
    /// </summary>
    public void HandleDroppedFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);

        if (extension.Equals(".img", StringComparison.OrdinalIgnoreCase))
        {
            SelectImageFile(filePath);
        }
        else if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            SelectPackageFile(filePath);
        }
        else if (extension.Equals(".apk", StringComparison.OrdinalIgnoreCase))
        {
            SelectApkFile(filePath);
        }
        else
        {
            AppendLog(Text.Get("log.unsupportedFile"), LogSeverity.Warning);
        }
    }

    /// <summary>
    /// Validasi dan set file .img yang akan di-flash.
    /// </summary>
    public void SelectImageFile(string? filePath)
    {
        var validation = FileValidator.Validate(
            filePath,
            FileValidator.FlashExtension,
            requireZipContent: false,
            Text);

        if (!validation.IsValid)
        {
            AppendLog(validation.ErrorMessage ?? Text.Get("log.fileInvalid"), LogSeverity.Error);
            StatusText = validation.ErrorMessage ?? Text.Get("log.fileInvalid");
            return;
        }

        SelectedImagePath = validation.FilePath;
        ImageFileName = validation.FileName;
        ImageSha256 = validation.Sha256;
        ImageFileSize = validation.FileSize;
        ImageSize = FlashService.FormatSize(validation.FileSize);

        AppendLog(Text.Get("log.imageReady", validation.FileName, ImageSize), LogSeverity.Success);
        StatusText = Text.Get("status.imageReady");

        CompareHash();
    }

    /// <summary>
    /// Validasi dan set file .zip yang akan di-sideload.
    /// </summary>
    public void SelectPackageFile(string? filePath)
    {
        var validation = FileValidator.Validate(
            filePath,
            FileValidator.SideloadExtension,
            requireZipContent: true,
            Text);

        if (!validation.IsValid)
        {
            AppendLog(validation.ErrorMessage ?? Text.Get("log.fileInvalid"), LogSeverity.Error);
            StatusText = validation.ErrorMessage ?? Text.Get("log.fileInvalid");
            return;
        }

        SelectedPackagePath = validation.FilePath;
        PackageFileName = validation.FileName;
        PackageSha256 = validation.Sha256;

        AppendLog(Text.Get("log.packageReady", validation.FileName), LogSeverity.Success);
        StatusText = Text.Get("status.packageReady");
    }

    /// <summary>
    /// Validasi dan set file .apk yang akan di-install.
    /// </summary>
    public void SelectApkFile(string? filePath)
    {
        var validation = FileValidator.Validate(
            filePath,
            FileValidator.ApkExtension,
            requireZipContent: true,
            Text);

        if (!validation.IsValid)
        {
            AppendLog(validation.ErrorMessage ?? Text.Get("log.fileInvalid"), LogSeverity.Error);
            StatusText = validation.ErrorMessage ?? Text.Get("log.fileInvalid");
            return;
        }

        SelectedApkPath = validation.FilePath;
        ApkFileName = validation.FileName;
        ApkSize = FlashService.FormatSize(validation.FileSize);

        AppendLog(Text.Get("log.apkReady", validation.FileName, ApkSize), LogSeverity.Success);
        StatusText = Text.Get("status.apkReady");
    }

    /// <summary>
    /// Bandingkan SHA256 file dengan hash yang diketik pengguna.
    /// </summary>
    [RelayCommand]
    private void CompareHash()
    {
        if (string.IsNullOrWhiteSpace(ImageSha256) || string.IsNullOrWhiteSpace(ExpectedSha256))
        {
            HashComparisonResult = string.Empty;
            return;
        }

        var expected = ExpectedSha256.Trim().ToLowerInvariant();
        var actual = ImageSha256.ToLowerInvariant();

        if (expected == actual)
        {
            HashComparisonResult = Text.Get("log.hashMatch");
            AppendLog(HashComparisonResult, LogSeverity.Success);
        }
        else
        {
            HashComparisonResult = Text.Get("log.hashMismatch");
            AppendLog(HashComparisonResult, LogSeverity.Error);
        }
    }

    #endregion

    #region Flash & sideload

    /// <summary>
    /// Flash file .img ke partisi yang dipilih, dengan validasi anti-brick
    /// dan dialog konfirmasi.
    /// </summary>
    [RelayCommand]
    private async Task FlashAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (SelectedImagePath is null)
        {
            AppendLog(Text.Get("log.needImage"), LogSeverity.Warning);
            return;
        }

        // Multi-device: kalau ada beberapa perangkat, wajib pilih satu dulu.
        if (!IsDeviceUnambiguous)
        {
            var message = Text.Get("dialog.deviceNotSelected");
            AppendLog(message, LogSeverity.Error);
            ShowError(Text.Get("dialog.title.deviceNotSelected"), message);
            return;
        }

        var request = new FlashRequest
        {
            Partition = Partition,
            ImagePath = SelectedImagePath,
            FileName = ImageFileName,
            BootloaderState = BootloaderState,
            DeviceSerial = SelectedSerial,
            Sha256 = ImageSha256,
            FileSize = ImageFileSize
        };

        // Anti-brick: validasi dulu, jangan kirim command kalau tidak aman.
        var blockingReason = _flashService.ValidateRequest(request);

        if (blockingReason is not null)
        {
            AppendLog(Text.Get("log.flashBlocked", blockingReason), LogSeverity.Error);
            StatusText = blockingReason;
            ShowError(Text.Get("dialog.title.flashBlocked"), blockingReason);
            return;
        }

        var confirmation = FlashService.BuildConfirmationText(request, Text);

        if (!Confirm(Text.Get("dialog.title.flash"), confirmation))
        {
            AppendLog(Text.Get("log.flashCancelled"), LogSeverity.Warning);
            return;
        }

        IsBusy = true;
        StatusText = Text.Get("status.flashRunning", request.Partition);

        try
        {
            var result = await _flashService.FlashAsync(request);

            if (result.IsSuccess)
            {
                AppendLog(Text.Get("log.flashSuccess", request.Partition), LogSeverity.Success);
                StatusText = Text.Get("status.flashSuccess", request.Partition);
            }
            else
            {
                HandleCommandFailure("Flash", result);
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog(Text.Get("log.flashCancelled"), LogSeverity.Warning);
        }
        catch (Exception ex)
        {
            AppendLog(Text.Get("log.flashFailed", ex.Message), LogSeverity.Error);
            StatusText = Text.Get("status.flashFailed");
        }
        finally
        {
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    /// <summary>
    /// Sideload package .zip, dengan validasi dan konfirmasi.
    /// </summary>
    [RelayCommand]
    private async Task SideloadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (SelectedPackagePath is null)
        {
            AppendLog(Text.Get("log.needPackage"), LogSeverity.Warning);
            return;
        }

        if (!IsDeviceUnambiguous)
        {
            var message = Text.Get("dialog.deviceNotSelected");
            AppendLog(message, LogSeverity.Error);
            ShowError(Text.Get("dialog.title.deviceNotSelected"), message);
            return;
        }

        var validation = FileValidator.Validate(
            SelectedPackagePath,
            FileValidator.SideloadExtension,
            requireZipContent: true,
            Text);

        if (!validation.IsValid)
        {
            AppendLog(validation.ErrorMessage ?? Text.Get("log.fileInvalid"), LogSeverity.Error);
            return;
        }

        if (!Confirm(Text.Get("dialog.title.sideload"),
                    SideloadService.BuildConfirmationText(validation, Text)))
        {
            AppendLog(Text.Get("log.sideloadCancelled"), LogSeverity.Warning);
            return;
        }

        IsBusy = true;
        StatusText = Text.Get("status.sideloadRunning");
        _suppressProcessOutput = true;

        try
        {
            var result = await _sideloadService.SideloadAsync(SelectedPackagePath);

            if (result.IsSuccess)
            {
                AppendLog(Text.Get("log.sideloadSuccess"), LogSeverity.Success);
                StatusText = Text.Get("status.sideloadSuccess");
            }
            else
            {
                HandleCommandFailure("Sideload", result);
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog(Text.Get("log.sideloadCancelled"), LogSeverity.Warning);
        }
        catch (Exception ex)
        {
            AppendLog(Text.Get("log.sideloadFailed", ex.Message), LogSeverity.Error);
            StatusText = Text.Get("status.sideloadFailed");
        }
        finally
        {
            _suppressProcessOutput = false;
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    #endregion

    #region Install APK

    /// <summary>
    /// Pasang file .apk yang dijatuhkan lewat drag and drop.
    /// </summary>
    [RelayCommand]
    private async Task InstallApkAsync()
    {
        if (IsBusy)
        {
            return;
        }

        if (SelectedApkPath is null)
        {
            AppendLog(Text.Get("log.needApk"), LogSeverity.Warning);
            return;
        }

        if (!IsDeviceUnambiguous)
        {
            var message = Text.Get("dialog.deviceNotSelected");
            AppendLog(message, LogSeverity.Error);
            ShowError(Text.Get("dialog.title.deviceNotSelected"), message);
            return;
        }

        var validation = FileValidator.Validate(
            SelectedApkPath,
            FileValidator.ApkExtension,
            requireZipContent: true,
            Text);

        if (!validation.IsValid)
        {
            AppendLog(validation.ErrorMessage ?? Text.Get("log.fileInvalid"), LogSeverity.Error);
            return;
        }

        if (!Confirm(Text.Get("dialog.title.install"),
                    InstallService.BuildConfirmationText(validation.FileName, Text)))
        {
            AppendLog(Text.Get("log.installCancelled"), LogSeverity.Warning);
            return;
        }

        IsBusy = true;
        StatusText = Text.Get("status.installRunning");

        try
        {
            var result = await _installService.InstallAsync(validation.FilePath);

            if (result.IsSuccess)
            {
                AppendLog(Text.Get("log.installSuccess", validation.FileName), LogSeverity.Success);
                StatusText = Text.Get("status.installSuccess");
            }
            else
            {
                HandleCommandFailure("Install", result);
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog(Text.Get("log.installCancelled"), LogSeverity.Warning);
        }
        catch (Exception ex)
        {
            AppendLog(Text.Get("log.installFailed", ex.Message), LogSeverity.Error);
            StatusText = Text.Get("status.installFailed");
        }
        finally
        {
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    #endregion

    #region Reboot

    /// <summary>
    /// Reboot perangkat ke mode yang dipilih. Parameter datang dari XAML sebagai
    /// string (System, Recovery, Bootloader, Fastbootd), lalu diubah ke enum.
    /// </summary>
    [RelayCommand]
    private async Task RebootAsync(string target)
    {
        if (IsBusy)
        {
            return;
        }

        if (!Enum.TryParse<RebootTarget>(target, ignoreCase: true, out var rebootTarget))
        {
            AppendLog(Text.Get("log.rebootUnknownTarget", target), LogSeverity.Error);
            return;
        }

        // Multi-device: kalau perangkat belum dipilih, reboot tidak boleh dikirim
        // karena bisa mengarah ke perangkat yang salah.
        if (!IsDeviceUnambiguous)
        {
            var message = Text.Get("dialog.deviceNotSelected");
            AppendLog(message, LogSeverity.Error);
            ShowError(Text.Get("dialog.title.deviceNotSelected"), message);
            return;
        }

        var label = Text.Get(rebootTarget.ToLabelKey());

        IsBusy = true;
        StatusText = Text.Get("status.rebootRunning", label);

        try
        {
            var result = await _rebootService.RebootAsync(rebootTarget);

            if (result.IsSuccess)
            {
                AppendLog(Text.Get("status.rebootSuccess", label), LogSeverity.Success);
                StatusText = Text.Get("status.rebootSuccess", label);
            }
            else
            {
                HandleCommandFailure(label, result);
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog(Text.Get("log.rebootCancelled"), LogSeverity.Warning);
        }
        catch (Exception ex)
        {
            AppendLog(Text.Get("log.rebootFailed", ex.Message), LogSeverity.Error);
            StatusText = Text.Get("status.rebootFailed");
        }
        finally
        {
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    #endregion

    #region Bootloader

    /// <summary>
    /// Unlock atau lock bootloader. Ini aksi destruktif: semua data di
    /// perangkat akan terhapus, jadi dialog konfirmasi khusus dengan
    /// peringatan besar dan centang persetujuan wajib dipakai.
    /// Parameter datang dari XAML sebagai string (Unlock atau Lock).
    /// </summary>
    [RelayCommand]
    private async Task BootloaderActionAsync(string target)
    {
        if (IsBusy)
        {
            return;
        }

        if (!Enum.TryParse<BootloaderAction>(target, ignoreCase: true, out var action))
        {
            AppendLog(Text.Get("log.bootloaderUnknownAction", target), LogSeverity.Error);
            return;
        }

        if (!IsDeviceUnambiguous)
        {
            var message = Text.Get("dialog.deviceNotSelected");
            AppendLog(message, LogSeverity.Error);
            ShowError(Text.Get("dialog.title.deviceNotSelected"), message);
            return;
        }

        // Kalau fastboot sudah dicek dan tidak ada perangkat, blokir lebih awal.
        // Kalau belum dicek, biarkan command yang melaporkan kendalanya.
        var fastbootCheckedWithoutDevice =
            FastbootStatus != DeviceCheckStatus.NotChecked
            && FastbootStatus != DeviceCheckStatus.Detected;

        var blockingReason = _bootloaderService.ValidateAction(
            action,
            BootloaderState,
            !fastbootCheckedWithoutDevice);

        var label = Text.Get(action.ToLabelKey());

        if (blockingReason is not null)
        {
            AppendLog(Text.Get("log.bootloaderBlocked", label, blockingReason), LogSeverity.Error);
            StatusText = blockingReason;
            ShowError(Text.Get("dialog.title.bootloaderBlocked", label), blockingReason);
            return;
        }

        // Aksi destruktif: konfirmasi khusus dengan peringatan penghapusan data.
        var confirmation = BootloaderService.BuildConfirmationText(action, SelectedSerial, Text);

        if (!ConfirmDestructive(
                Text.Get("dialog.title.bootloader", label),
                confirmation,
                BootloaderService.BuildAcknowledgementLabel(action, Text)))
        {
            AppendLog(Text.Get("log.bootloaderCancelledByUser", label), LogSeverity.Warning);
            return;
        }

        IsBusy = true;
        StatusText = Text.Get("status.bootloaderRunning", label);

        try
        {
            var result = await _bootloaderService.RunAsync(action);

            if (result.IsSuccess)
            {
                AppendLog(Text.Get("log.bootloaderSuccess", label), LogSeverity.Success);
                StatusText = Text.Get("status.bootloaderSuccess", label);
            }
            else
            {
                HandleCommandFailure(label, result);
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog(Text.Get("log.bootloaderCancelledByUser", label), LogSeverity.Warning);
        }
        catch (Exception ex)
        {
            AppendLog(Text.Get("log.bootloaderFailed", label, ex.Message), LogSeverity.Error);
            StatusText = Text.Get("status.bootloaderFailed", label);
        }
        finally
        {
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    #endregion

    #region Driver USB
    /// <summary>
    /// Unduh dan pasang driver USB Google dengan elevasi Administrator.
    /// </summary>
    [RelayCommand]
    private async Task InstallDriverAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        DownloadProgress = 0;
        StatusText = Text.Get("status.driverRunning");

        try
        {
            var status = await _driverService.InstallAsync();

            switch (status)
            {
                case DriverInstallStatus.Installed:
                    AppendLog(Text.Get("log.driverInstalled"), LogSeverity.Success);
                    StatusText = Text.Get("status.driverInstalled");
                    break;

                case DriverInstallStatus.ElevationDeclined:
                    AppendLog(Text.Get("log.driverDeclined"), LogSeverity.Warning);
                    StatusText = Text.Get("status.driverDeclined");
                    break;

                default:
                    AppendLog(Text.Get("log.driverFailed"), LogSeverity.Error);
                    StatusText = Text.Get("status.driverFailed");
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog(Text.Get("log.driverCancelled"), LogSeverity.Warning);
        }
        finally
        {
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    #endregion

    #region Update

    /// <summary>
    /// Cek apakah ada versi DuckFlash yang lebih baru di GitHub Releases.
    /// </summary>
    [RelayCommand]
    private async Task CheckForUpdateAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusText = Text.Get("status.updateChecking");

        try
        {
            var info = await _updateService.CheckAsync();

            if (!info.IsCheckSuccessful)
            {
                UpdateStatus = Text.Get("status.update.failed");
                AppendLog(info.ErrorMessage ?? Text.Get("status.update.failed"), LogSeverity.Warning);
                return;
            }

            if (info.IsUpdateAvailable)
            {
                UpdateStatus = Text.Get("status.update.available", info.LatestVersion, info.CurrentVersion);

                AppendLog(
                    Text.Get("log.updateNewVersion", info.LatestVersion, info.ReleaseUrl),
                    LogSeverity.Success);

                ShowError(
                    Text.Get("dialog.title.update"),
                    Text.Get("dialog.updateAvailable", info.LatestVersion, info.CurrentVersion, info.ReleaseUrl));
            }
            else
            {
                UpdateStatus = Text.Get("status.update.latest", info.CurrentVersion);
                AppendLog(Text.Get("log.updateLatest", info.CurrentVersion), LogSeverity.Success);
            }
        }
        catch (OperationCanceledException)
        {
            AppendLog(Text.Get("log.updateCancelled"), LogSeverity.Warning);
        }
        catch (Exception ex)
        {
            UpdateStatus = Text.Get("status.update.failed");
            AppendLog(Text.Get("log.updateFailed", ex.Message), LogSeverity.Error);
        }
        finally
        {
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    #endregion

    #region Log

    /// <summary>Salin seluruh isi log ke clipboard.</summary>
    [RelayCommand]
    private void CopyLog()
    {
        if (LogEntries.Count == 0)
        {
            AppendLog(Text.Get("log.logEmpty"), LogSeverity.Warning);
            return;
        }

        try
        {
            Clipboard.SetText(LogText);
            AppendLog(Text.Get("log.logCopied"), LogSeverity.Success);
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            // Clipboard mungkin sedang dipakai aplikasi lain.
            AppendLog(Text.Get("log.logCopyFailed"), LogSeverity.Error);
        }
    }

    /// <summary>Kosongkan panel log.</summary>
    [RelayCommand]
    private void ClearLog()
    {
        LogEntries.Clear();
        AppendLog(Text.Get("log.logCleared"));
    }

    #endregion

    #region Wizard ROM

    /// <summary>
    /// Buat ViewModel untuk wizard instalasi custom ROM.
    /// Service yang dipakai sama dengan jendela utama, jadi status perangkat,
    /// bahasa aktif, dan log tetap konsisten.
    /// </summary>
    public RomInstallWizardViewModel CreateRomInstallWizard()
    {
        return new RomInstallWizardViewModel(
            _adbService,
            _fastbootService,
            _deviceCheckService,
            _flashService,
            _sideloadService,
            _bootloaderService,
            new WipeService(_fastbootService, _localization),
            _rebootService,
            _errorTranslator,
            _localization,
            _dialogService,
            _dispatcher);
    }

    #endregion

    #region Debloater

    /// <summary>
    /// Buat ViewModel untuk jendela debloater.
    /// </summary>
    public DebloatViewModel CreateDebloatViewModel()
    {
        return new DebloatViewModel(
            _adbService,
            _debloatService,
            _errorTranslator,
            _localization,
            _dialogService,
            _dispatcher);
    }

    #endregion

    #region Alat perangkat

    /// <summary>
    /// Buat ViewModel untuk jendela alat perangkat (screenshot, logcat, backup).
    /// </summary>
    public DeviceToolsViewModel CreateDeviceToolsViewModel()
    {
        return new DeviceToolsViewModel(
            _adbService,
            _screenshotService,
            _logcatService,
            _backupService,
            _errorTranslator,
            _localization,
            _dialogService,
            _dispatcher);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Setiap kali bahasa diubah, muat ulang teks UI dan simpan pilihannya.
    /// </summary>
    partial void OnSelectedLanguageChanged(AppLanguage value)
    {
        _localization.CurrentLanguage = value;
        Text.Refresh();
        ResetLocalizedState();

        _settings.Language = value;
        _settings.Save();

        OnPropertyChanged(nameof(SelectedLanguageOption));
        OnPropertyChanged(nameof(AvailableLanguages));
    }

    /// <summary>
    /// Setiap kali serial perangkat berubah, teruskan ke service adb dan fastboot
    /// supaya semua command memakai flag -s yang benar.
    /// </summary>
    partial void OnSelectedSerialChanged(string? value)
    {
        _adbService.DeviceSerial = value;
        _fastbootService.DeviceSerial = value;

        // Kalau pengguna memilih perangkat lain, detail yang tampil harus
        // ikut berubah supaya tidak menampilkan info perangkat yang salah.
        if (AvailableSerials.Count > 1 && !string.IsNullOrWhiteSpace(value))
        {
            _ = RefreshSelectedDeviceDetailsAsync();
        }
    }

    /// <summary>
    /// Muat ulang detail perangkat yang sedang dipilih.
    /// </summary>
    private async Task RefreshSelectedDeviceDetailsAsync()
    {
        var serial = SelectedSerial;

        if (string.IsNullOrWhiteSpace(serial))
        {
            return;
        }

        try
        {
            if (AdbStatus == DeviceCheckStatus.Detected)
            {
                var details = await _deviceCheckService.GetAdbDeviceDetailsAsync(serial);
                DeviceDetails = Text.Get(
                    "device.detailsAdb",
                    details.Model,
                    details.Codename,
                    details.AndroidVersion);
            }
            else if (FastbootStatus == DeviceCheckStatus.Detected)
            {
                var details = await _deviceCheckService.GetFastbootDeviceDetailsAsync(serial);
                BootloaderState = details.BootloaderState;
                ActiveSlot = details.ActiveSlot;
                DeviceDetails = Text.Get(
                    "device.detailsFastboot",
                    details.Codename,
                    details.BootloaderState,
                    details.ActiveSlot);
            }
        }
        catch (Exception ex)
        {
            AppendLog(Text.Get("log.deviceInfoFailed", ex.Message), LogSeverity.Warning);
        }
        finally
        {
            FlushPendingEntries();
        }
    }

    /// <summary>
    /// True kalau tidak ada ambiguitas perangkat. Kalau lebih dari satu perangkat
    /// terdeteksi, pengguna wajib memilih salah satu.
    /// </summary>
    public bool IsDeviceUnambiguous => AvailableSerials.Count <= 1 || !string.IsNullOrWhiteSpace(SelectedSerial);

    /// <summary>
    /// Terjemahkan kegagalan command dan tampilkan ke pengguna.
    /// </summary>
    private void HandleCommandFailure(string operation, CommandResult result)
    {
        var useEnglish = SelectedLanguage == AppLanguage.English;
        var translated = _errorTranslator.Translate(result, useEnglish);

        AppendLog(Text.Get("status.commandFailed", operation, translated.Title), LogSeverity.Error);
        AppendLog(translated.Explanation, LogSeverity.Error);
        AppendLog(Text.Get("log.solution", translated.SolutionLabel, translated.Solution), LogSeverity.Warning);

        if (!string.IsNullOrWhiteSpace(translated.RawMessage))
        {
            AppendLog(Text.Get("log.detail", translated.DetailLabel, translated.RawMessage), LogSeverity.Error);
        }

        StatusText = Text.Get("status.commandFailed", operation, translated.Title);
        ShowError(Text.Get("dialog.title.commandFailed", operation), translated.Formatted);
    }

    /// <summary>
    /// Tampilkan dialog error ke pengguna.
    /// </summary>
    protected virtual void ShowError(string title, string message)
    {
        _dialogService.ShowError(title, message);
    }

    /// <summary>
    /// Minta konfirmasi pengguna.
    /// </summary>
    protected virtual bool Confirm(string title, string message)
    {
        return _dialogService.Confirm(title, message);
    }

    /// <summary>
    /// Minta konfirmasi eksplisit untuk aksi destruktif (data akan terhapus).
    /// </summary>
    protected virtual bool ConfirmDestructive(string title, string message, string acknowledgementLabel)
    {
        return _dialogService.ConfirmDestructive(title, message, acknowledgementLabel);
    }

    private void OnToolsProgressChanged(object? sender, DownloadProgressEventArgs e)
    {
        // Event datang dari thread proses; kalau sudah di thread UI langsung ditampilkan,
        // kalau tidak antrikan dulu lalu di-flush timer.
        EnqueueLog(new LogEntry(e.Message ?? "Memproses..."));

        lock (_pendingLock)
        {
            _pendingProgress = e.Percentage;
        }
    }

    private void OnProcessOutputReceived(object? sender, ProcessOutputLineEventArgs e)
    {
        if (_suppressProcessOutput)
        {
            return;
        }

        EnqueueLog(new LogEntry(e.Line));
    }

    private void OnCommandStarting(object? sender, ProcessCommandEventArgs e)
    {
        if (_suppressProcessOutput)
        {
            return;
        }

        // Preview command: command lengkap ditulis ke log sebelum dijalankan.
        EnqueueLog(new LogEntry($"> {e.CommandLine}", LogSeverity.Success));
    }

    private void OnLogTimerTick(object? sender, EventArgs e)
    {
        FlushPendingEntries();
    }

    /// <summary>
    /// Pindahkan antrean log ke koleksi yang tampil di UI. Dipanggil timer
    /// dan juga setelah setiap command selesai, supaya log langsung lengkap
    /// tanpa harus menunggu tick berikutnya.
    /// </summary>
    private void FlushPendingEntries()
    {
        LogEntry[] entries;
        int progress;

        lock (_pendingLock)
        {
            if (_pendingEntries.Count == 0)
            {
                return;
            }

            entries = _pendingEntries.ToArray();
            _pendingEntries.Clear();
            progress = _pendingProgress;
            _pendingProgress = 0;
        }

        foreach (var entry in entries)
        {
            LogEntries.Add(entry);
        }

        DownloadProgress = progress;
    }

    /// <summary>
    /// Masukkan baris log ke UI. Kalau pemanggil sudah di thread UI, baris langsung
    /// tampil. Kalau tidak, diantrikan dulu lalu di-flush oleh timer.
    /// </summary>
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

    #endregion
}
