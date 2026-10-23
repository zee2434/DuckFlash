using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DuckFlash.Models;
using DuckFlash.Services;

namespace DuckFlash.ViewModels;

/// <summary>
/// Debloater: menampilkan daftar aplikasi yang terpasang di perangkat dan
/// menghapus aplikasi yang dicentang pengguna.
///
/// Penghapusan memakai <c>pm uninstall --user 0</c>, jadi hanya data aplikasi
/// untuk pengguna utama yang dibuang. Aplikasi sistem tidak hilang dan masih
/// bisa dikembalikan.
/// </summary>
public partial class DebloatViewModel : ObservableObject
{
    private readonly AdbService _adbService;
    private readonly DebloatService _debloatService;
    private readonly ErrorTranslator _errorTranslator;
    private readonly ITextProvider _text;
    private readonly IDialogService _dialogService;
    private readonly Dispatcher _dispatcher;

    /// <summary>Baris log yang belum sempat ditampilkan, dikumpulkan dari thread proses.</summary>
    private readonly List<LogEntry> _pendingEntries = new();

    /// <summary>Pengaman thread untuk daftar antrean log.</summary>
    private readonly object _pendingLock = new();

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    /// <summary>Kata kunci pencarian nama package.</summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>True kalau aplikasi sistem ikut ditampilkan.</summary>
    [ObservableProperty]
    private bool _showSystemApps;

    /// <summary>Berapa aplikasi yang sedang dicentang.</summary>
    [ObservableProperty]
    private int _selectedCount;

    /// <summary>Semua aplikasi yang dimuat, hasil filter atau tidak.</summary>
    public ObservableCollection<DebloatPackage> Packages { get; } = new();

    /// <summary>Aplikasi yang ditampilkan di daftar, sesuai pencarian.</summary>
    public ObservableCollection<DebloatPackage> VisiblePackages { get; } = new();

    /// <summary>Semua baris log, dari yang paling lama ke yang terbaru.</summary>
    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    /// <summary>Teks UI sesuai bahasa aktif, dipakai untuk binding di XAML.</summary>
    public UiText Text { get; }

    /// <summary>True kalau ada aplikasi yang dicentang dan siap dihapus.</summary>
    public bool HasSelection => SelectedCount > 0;

    /// <summary>True kalau daftar aplikasi sudah dimuat.</summary>
    public bool HasPackages => Packages.Count > 0;

    /// <summary>True kalau pengguna boleh berinteraksi (tidak sedang ada proses).</summary>
    public bool CanInteract => !IsBusy;

    /// <summary>Teks jumlah aplikasi yang dicentang, mengikuti bahasa aktif.</summary>
    public string SelectedCountText =>
        _text.Get("status.debloat.selectedCount", SelectedCount.ToString());

    public DebloatViewModel(
        AdbService adbService,
        DebloatService debloatService,
        ErrorTranslator errorTranslator,
        ITextProvider text,
        IDialogService dialogService,
        Dispatcher dispatcher)
    {
        _adbService = adbService;
        _debloatService = debloatService;
        _errorTranslator = errorTranslator;
        _text = text;
        _dialogService = dialogService;
        _dispatcher = dispatcher;

        Text = new UiText(text as LocalizationService ?? new LocalizationService());

        _adbService.OutputReceived += OnProcessOutputReceived;
        _adbService.CommandStarting += OnCommandStarting;

        ResetLocalizedState();
    }

    /// <summary>Isi ulang nilai tampilan yang bergantung pada bahasa aktif.</summary>
    private void ResetLocalizedState()
    {
        StatusText = _text.Get("app.ready");
    }

    /// <summary>
    /// Muat ulang seluruh teks setelah bahasa diganti.
    /// </summary>
    public void RefreshLocalization()
    {
        ResetLocalizedState();
        Text.Refresh();
    }

    #region Daftar aplikasi

    /// <summary>
    /// Baca daftar aplikasi yang terpasang di perangkat.
    /// </summary>
    [RelayCommand]
    private async Task LoadPackagesAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            await RefreshPackagesAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    private async Task RefreshPackagesAsync()
    {
        StatusText = _text.Get("status.debloat.loading");

        try
        {
            var userPackages = await _debloatService.ListUserPackagesAsync().ConfigureAwait(true);

            Packages.Clear();

            foreach (var packageName in userPackages)
            {
                AddPackage(packageName, isSystem: false);
            }

            // Aplikasi sistem hanya dimuat kalau pengguna memintanya, karena
            // daftarnya jauh lebih panjang.
            if (ShowSystemApps)
            {
                var systemPackages = await _debloatService.ListSystemPackagesAsync().ConfigureAwait(true);

                foreach (var packageName in systemPackages)
                {
                    AddPackage(packageName, isSystem: true);
                }
            }

            ApplyFilter();

            var message = _text.Get("status.debloat.loaded", Packages.Count.ToString());

            StatusText = message;
            AppendLog(message, LogSeverity.Success);
        }
        catch (Exception ex)
        {
            StatusText = _text.Get("status.debloat.loadFailed", ex.Message);
            AppendLog(StatusText, LogSeverity.Error);
        }
    }

    private void AddPackage(string packageName, bool isSystem)
    {
        var package = new DebloatPackage(packageName, isSystem);

        package.PropertyChanged += OnPackagePropertyChanged;

        Packages.Add(package);
    }

    /// <summary>
    /// Terapkan pencarian dan filter aplikasi sistem ke daftar yang tampil.
    /// </summary>
    private void ApplyFilter()
    {
        VisiblePackages.Clear();

        var keyword = SearchText?.Trim() ?? string.Empty;

        foreach (var package in Packages)
        {
            if (package.IsSystem && !ShowSystemApps)
            {
                continue;
            }

            if (keyword.Length > 0
                && !package.PackageName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            VisiblePackages.Add(package);
        }

        OnPropertyChanged(nameof(HasPackages));
    }

    /// <summary>
    /// Centang semua aplikasi yang terlihat di daftar.
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        foreach (var package in VisiblePackages)
        {
            package.IsSelected = true;
        }

        UpdateSelectedCount();
    }

    /// <summary>
    /// Hilangkan centang dari semua aplikasi.
    /// </summary>
    [RelayCommand]
    private void ClearSelection()
    {
        foreach (var package in Packages)
        {
            package.IsSelected = false;
        }

        UpdateSelectedCount();
    }

    #endregion

    #region Penghapusan

    /// <summary>
    /// Hapus aplikasi yang dicentang. Selalu ada konfirmasi dulu karena
    /// data aplikasi yang dihapus tidak bisa dikembalikan.
    /// </summary>
    [RelayCommand]
    private async Task UninstallSelectedAsync()
    {
        if (IsBusy)
        {
            return;
        }

        var selected = Packages
            .Where(package => package.IsSelected)
            .Select(package => package.PackageName)
            .ToList();

        if (selected.Count == 0)
        {
            StatusText = _text.Get("status.debloat.nothingSelected");
            AppendLog(StatusText, LogSeverity.Warning);

            return;
        }

        var confirmation = DebloatService.BuildConfirmationText(selected, _text);

        if (!_dialogService.ConfirmDestructive(
                _text.Get("dialog.title.debloatConfirm"),
                confirmation,
                DebloatService.BuildAcknowledgementLabel(_text)))
        {
            AppendLog(_text.Get("log.debloatUserCancelled"), LogSeverity.Warning);

            return;
        }

        IsBusy = true;

        try
        {
            var failed = 0;

            foreach (var packageName in selected)
            {
                var result = await _debloatService.UninstallAsync(packageName).ConfigureAwait(true);

                if (DebloatService.IsUninstallSuccessful(result))
                {
                    AppendLog(_text.Get("log.debloatRemoved", packageName), LogSeverity.Success);
                }
                else
                {
                    failed++;

                    var translated = _errorTranslator.Translate(result, useEnglish: false);

                    AppendLog(_text.Get("log.debloatFailed", packageName), LogSeverity.Error);
                    AppendLog(translated.Explanation, LogSeverity.Error);
                }
            }

            var message = failed == 0
                ? _text.Get("status.debloat.done", (selected.Count - failed).ToString())
                : _text.Get("status.debloat.doneWithErrors", (selected.Count - failed).ToString(), failed.ToString());

            StatusText = message;
            AppendLog(message, failed == 0 ? LogSeverity.Success : LogSeverity.Warning);

            // Muat ulang daftar supaya aplikasi yang berhasil dihapus hilang dari daftar.
            Packages.Clear();
            VisiblePackages.Clear();
            UpdateSelectedCount();

            await RefreshPackagesAsync().ConfigureAwait(true);
        }
        finally
        {
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    #endregion

    #region Helpers

    private void OnPackagePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DebloatPackage.IsSelected))
        {
            UpdateSelectedCount();
        }
    }

    private void UpdateSelectedCount()
    {
        SelectedCount = Packages.Count(package => package.IsSelected);

        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(SelectedCountText));
    }

    /// <summary>
    /// Saat proses berjalan, tombol aksi ikut dinonaktifkan.
    /// </summary>
    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanInteract));
    }

    partial void OnSearchTextChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnShowSystemAppsChanged(bool value)
    {
        ApplyFilter();
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

    #endregion
}
