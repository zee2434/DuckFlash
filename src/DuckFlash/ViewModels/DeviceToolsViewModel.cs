using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DuckFlash.Models;
using DuckFlash.Services;

namespace DuckFlash.ViewModels;

/// <summary>
/// Alat perangkat: screenshot, logcat, dan pencadangan/pemulihan aplikasi.
/// </summary>
public partial class DeviceToolsViewModel : ObservableObject
{
    private readonly AdbService _adbService;
    private readonly ScreenshotService _screenshotService;
    private readonly LogcatService _logcatService;
    private readonly BackupService _backupService;
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

    [ObservableProperty]
    private string _screenshotPath = string.Empty;

    [ObservableProperty]
    private string _logcatPath = string.Empty;

    [ObservableProperty]
    private string _backupFolder = string.Empty;

    /// <summary>Semua baris log, dari yang paling lama ke yang terbaru.</summary>
    public ObservableCollection<LogEntry> LogEntries { get; } = new();

    /// <summary>Teks UI sesuai bahasa aktif, dipakai untuk binding di XAML.</summary>
    public UiText Text { get; }

    /// <summary>True kalau pengguna boleh berinteraksi (tidak sedang ada proses).</summary>
    public bool CanInteract => !IsBusy;

    public DeviceToolsViewModel(
        AdbService adbService,
        ScreenshotService screenshotService,
        LogcatService logcatService,
        BackupService backupService,
        ErrorTranslator errorTranslator,
        ITextProvider text,
        IDialogService dialogService,
        Dispatcher dispatcher)
    {
        _adbService = adbService;
        _screenshotService = screenshotService;
        _logcatService = logcatService;
        _backupService = backupService;
        _errorTranslator = errorTranslator;
        _text = text;
        _dialogService = dialogService;
        _dispatcher = dispatcher;

        Text = new UiText(text as LocalizationService ?? new LocalizationService());

        // Preview command dan output proses ditulis ke log, sesuai aturan CLAUDE.md.
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

    #region Screenshot

    /// <summary>Ambil screenshot dan simpan ke path yang ditentukan pengguna.</summary>
    public async Task CaptureScreenshotAsync(string destinationPath)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var result = await _screenshotService.CaptureAsync(destinationPath).ConfigureAwait(true);

            if (result.IsSuccess && File.Exists(Path.GetFullPath(destinationPath)))
            {
                StatusText = _text.Get("status.screenshot.saved", destinationPath);
                AppendLog(StatusText, LogSeverity.Success);
            }
            else
            {
                StatusText = _text.Get("status.screenshot.failed", result.StandardError);
                AppendLog(StatusText, LogSeverity.Error);

                var translated = _errorTranslator.Translate(result, useEnglish: false);

                AppendLog(translated.Explanation, LogSeverity.Error);
                AppendLog(_text.Get("log.solution", translated.SolutionLabel, translated.Solution), LogSeverity.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusText = _text.Get("status.screenshot.failed", ex.Message);
            AppendLog(StatusText, LogSeverity.Error);
        }
        finally
        {
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    #endregion

    #region Logcat

    /// <summary>Ekspor logcat perangkat ke file teks.</summary>
    public async Task ExportLogcatAsync(string destinationPath)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var result = await _logcatService.ExportAsync(destinationPath).ConfigureAwait(true);

            if (result.IsSuccess && File.Exists(Path.GetFullPath(destinationPath)))
            {
                StatusText = _text.Get("status.logcat.exported", destinationPath);
                AppendLog(StatusText, LogSeverity.Success);
            }
            else
            {
                StatusText = _text.Get("status.logcat.exportFailed", result.StandardError);
                AppendLog(StatusText, LogSeverity.Error);

                var translated = _errorTranslator.Translate(result, useEnglish: false);

                AppendLog(translated.Explanation, LogSeverity.Error);
                AppendLog(_text.Get("log.solution", translated.SolutionLabel, translated.Solution), LogSeverity.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusText = _text.Get("status.logcat.exportFailed", ex.Message);
            AppendLog(StatusText, LogSeverity.Error);
        }
        finally
        {
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    /// <summary>Bersihkan buffer logcat di perangkat.</summary>
    public async Task ClearLogcatAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var result = await _logcatService.ClearAsync().ConfigureAwait(true);

            StatusText = _text.Get("status.logcat.cleared");
            AppendLog(StatusText, LogSeverity.Success);
        }
        catch (Exception ex)
        {
            StatusText = _text.Get("status.logcat.clearFailed", ex.Message);
            AppendLog(StatusText, LogSeverity.Error);
        }
        finally
        {
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    #endregion

    #region Backup / Restore

    /// <summary>Cadangkan semua APK aplikasi pengguna ke folder yang ditentukan.</summary>
    public async Task BackupAsync(string folderPath)
    {
        if (IsBusy)
        {
            return;
        }

        if (!Directory.Exists(folderPath))
        {
            StatusText = _text.Get("status.backup.empty");
            AppendLog(StatusText, LogSeverity.Warning);
            return;
        }

        IsBusy = true;

        try
        {
            var progress = new Progress<string>(message =>
            {
                StatusText = message;
            });

            var result = await _backupService.BackupAsync(folderPath, progress).ConfigureAwait(true);

            if (result.IsEmpty)
            {
                StatusText = _text.Get("status.backup.empty");
                AppendLog(StatusText, LogSeverity.Warning);

                return;
            }

            var message = result.Succeeded == 0
                ? _text.Get("status.backup.failed", result.Failed.ToString(), result.ManifestPath)
                : _text.Get("status.backup.done", result.Succeeded.ToString(), result.Failed.ToString(), result.ManifestPath);

            StatusText = message;
            AppendLog(message, result.Succeeded == 0 ? LogSeverity.Error : LogSeverity.Success);
            AppendLog($"Manifest: {result.ManifestPath}", LogSeverity.Info);
        }
        catch (Exception ex)
        {
            StatusText = _text.Get("status.backup.failed", ex.Message);
            AppendLog(StatusText, LogSeverity.Error);
        }
        finally
        {
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    /// <summary>
    /// Pulihkan aplikasi dari folder cadangan. Aksi ini memasang APK di perangkat,
    /// jadi wajib diberi konfirmasi kalau ada aplikasi yang sudah terpasang.
    /// </summary>
    public async Task RestoreAsync(string folderPath)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;

        try
        {
            var manifest = BackupService.LoadManifest(folderPath);

            if (manifest is null || manifest.Packages.Count == 0)
            {
                StatusText = _text.Get("status.restore.noManifest");
                AppendLog(StatusText, LogSeverity.Warning);

                return;
            }

            var listing = string.Join(Environment.NewLine, manifest.Packages
                .Select(entry => $"- {entry.PackageName}"));

            var confirmation = _text.Get("confirm.restore", manifest.Packages.Count.ToString(), listing);

            if (!_dialogService.Confirm(
                    _text.Get("dialog.title.restoreConfirm"),
                    confirmation))
            {
                AppendLog(_text.Get("log.restoredCancelled"), LogSeverity.Warning);

                return;
            }

            var progress = new Progress<string>(message =>
            {
                StatusText = message;
            });

            var result = await _backupService.RestoreAsync(folderPath, progress).ConfigureAwait(true);

            if (result.Total == 0)
            {
                StatusText = _text.Get("status.restore.noManifest");
                AppendLog(StatusText, LogSeverity.Warning);

                return;
            }

            StatusText = result.Failed == 0
                ? _text.Get("status.restore.done", result.Total.ToString(), result.Failed.ToString())
                : _text.Get("status.restore.done", result.Total.ToString(), result.Failed.ToString());

            AppendLog(StatusText, result.Failed == 0 ? LogSeverity.Success : LogSeverity.Warning);
        }
        catch (Exception ex)
        {
            StatusText = _text.Get("status.restore.failed", ex.Message);
            AppendLog(StatusText, LogSeverity.Error);
        }
        finally
        {
            IsBusy = false;
            FlushPendingEntries();
        }
    }

    #endregion

    #region Helpers

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CanInteract));
    }

    private void OnProcessOutputReceived(object? sender, ProcessOutputLineEventArgs e)
    {
        EnqueueLog(new LogEntry(e.Line));
    }

    private void OnCommandStarting(object? sender, ProcessCommandEventArgs e)
    {
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
