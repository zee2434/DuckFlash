using System.IO.Compression;
using System.Net.Http;
using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Mengunduh platform-tools dari server Google dan mengekstraknya ke
/// %LocalAppData%\DuckFlash\platform-tools.
/// </summary>
public sealed class ToolsDownloader : IToolsDownloader, IToolPaths, IDisposable
{
    /// <summary>URL unduhan resmi platform-tools untuk Windows.</summary>
    public const string DownloadUrl =
        "https://dl.google.com/android/repository/platform-tools-latest-windows.zip";

    private const string ZipFileName = "platform-tools.zip";
    private const string ToolsFolderName = "platform-tools";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly string _installRoot;
    private readonly ITextProvider _text;
    private bool _disposed;

    /// <summary>
    /// Membuat downloader dengan lokasi instalasi default di
    /// %LocalAppData%\DuckFlash\platform-tools.
    /// </summary>
    public ToolsDownloader()
        : this(null, null)
    {
    }

    /// <summary>
    /// Membuat downloader dengan lokasi instalasi dan HttpClient tertentu.
    /// Parameter ini dipakai unit test agar tidak menyentuh jaringan dan folder asli.
    /// </summary>
    public ToolsDownloader(string? installRoot, HttpClient? httpClient)
        : this(installRoot, httpClient, LocalizationService.Default)
    {
    }

    /// <summary>
    /// Membuat downloader dengan sumber teks tertentu, supaya pesan progress
    /// bisa mengikuti bahasa yang dipilih pengguna.
    /// </summary>
    public ToolsDownloader(string? installRoot, HttpClient? httpClient, ITextProvider text)
    {
        _installRoot = installRoot ?? DefaultInstallRoot();
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _text = text;
    }

    /// <inheritdoc />
    public event EventHandler<DownloadProgressEventArgs>? ProgressChanged;

    /// <summary>Folder tempat platform-tools disimpan.</summary>
    public string InstallRoot => _installRoot;

    /// <inheritdoc />
    public string LocalPath => _installRoot;

    /// <summary>Path penuh ke adb.exe, null kalau belum terpasang.</summary>
    public string? AdbPath => FindToolPath("adb.exe");

    /// <summary>Path penuh ke fastboot.exe, null kalau belum terpasang.</summary>
    public string? FastbootPath => FindToolPath("fastboot.exe");

    /// <inheritdoc />
    public Task<ToolsStatus> CheckStatusAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var adbPath = AdbPath;
        var isInstalled = adbPath is not null;

        return Task.FromResult(new ToolsStatus
        {
            IsInstalled = isInstalled,
            LocalPath = _installRoot,
            IsBusy = false
        });
    }

    /// <inheritdoc />
    public async Task<ToolsStatus> EnsureInstalledAsync(CancellationToken cancellationToken = default)
    {
        var status = await CheckStatusAsync(cancellationToken).ConfigureAwait(false);

        if (status.IsInstalled)
        {
            ReportProgress(100, _text.Get("tools.alreadyInstalled"));
            return status;
        }

        // Berkas zip ditahan di folder terpisah, karena install root dihapus
        // saat hasil ekstraksi dipindahkan ke sana.
        var downloadDirectory = _installRoot + ".download";
        var zipPath = Path.Combine(downloadDirectory, ZipFileName);

        try
        {
            Directory.CreateDirectory(downloadDirectory);

            await DownloadAsync(zipPath, cancellationToken).ConfigureAwait(false);
            ExtractAsync(zipPath, cancellationToken);

            // Berkas zip sudah tidak diperlukan setelah ekstraksi.
            TryDelete(zipPath);
            TryDeleteDirectory(downloadDirectory);

            var finalStatus = await CheckStatusAsync(cancellationToken).ConfigureAwait(false);

            if (!finalStatus.IsInstalled)
            {
                return new ToolsStatus
                {
                    IsInstalled = false,
                    LocalPath = _installRoot,
                    ErrorMessage = _text.Get("tools.extractFailed")
                };
            }

            ReportProgress(100, _text.Get("tools.ready"));
            return finalStatus;
        }
        catch (OperationCanceledException)
        {
            TryDelete(zipPath);
            TryDeleteDirectory(downloadDirectory);
            throw;
        }
        catch (Exception ex)
        {
            TryDelete(zipPath);
            TryDeleteDirectory(downloadDirectory);

            return new ToolsStatus
            {
                IsInstalled = false,
                LocalPath = _installRoot,
                ErrorMessage = _text.Get("tools.installFailed", ex.Message)
            };
        }
    }

    private async Task DownloadAsync(string zipPath, CancellationToken cancellationToken)
    {
        ReportProgress(0, _text.Get("tools.downloading"));

        using var response = await _httpClient
            .GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0L;
        var downloadedBytes = 0L;
        var lastPercentage = -1;

        await using var source = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var target = new FileStream(
            zipPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        var buffer = new byte[81920];
        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);

            downloadedBytes += read;

            // Laporan hanya kalau persentase berubah supaya event tidak membanjiri UI.
            if (totalBytes > 0)
            {
                var percentage = (int)(downloadedBytes * 100 / totalBytes);

                if (percentage != lastPercentage)
                {
                    lastPercentage = percentage;
                    ReportProgress(percentage, _text.Get("tools.downloadingProgress", percentage));
                }
            }
        }

        await target.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private void ExtractAsync(string zipPath, CancellationToken cancellationToken)
    {
        ReportProgress(95, _text.Get("tools.extracting"));

        // Diekstrak ke folder sementara supaya folder platform-tools tidak
        // setengah jadi kalau ekstraksi terhenti di tengah jalan.
        // Folder staging harus di luar install root, karena install root dihapus
        // dulu sebelum hasil ekstraksi dipindahkan ke sana.
        var stagingDirectory = _installRoot + ".staging";

        TryDeleteDirectory(stagingDirectory);
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            ZipFile.ExtractToDirectory(zipPath, stagingDirectory, overwriteFiles: true);

            var extractedTools = Path.Combine(stagingDirectory, ToolsFolderName);

            if (!Directory.Exists(extractedTools))
            {
                throw new InvalidOperationException(
                    _text.Get("tools.zipStructureUnexpected"));
            }

            // Pindahkan isi ke lokasi final.
            TryDeleteDirectory(_installRoot);
            Directory.Move(extractedTools, _installRoot);
        }
        finally
        {
            TryDeleteDirectory(stagingDirectory);
        }
    }

    private string? FindToolPath(string toolName)
    {
        var path = Path.Combine(_installRoot, toolName);

        return File.Exists(path) ? path : null;
    }

    private void ReportProgress(int percentage, string? message)
    {
        ProgressChanged?.Invoke(this, new DownloadProgressEventArgs(percentage, message));
    }

    private static string DefaultInstallRoot()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "DuckFlash", ToolsFolderName);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Berkas sisa tidak masalah, hanya membuang ruang disk.
        }
        catch (UnauthorizedAccessException)
        {
            // Berkas sisa tidak masalah, hanya membuang ruang disk.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Tidak fatal.
        }
        catch (UnauthorizedAccessException)
        {
            // Tidak fatal.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }

        _disposed = true;
    }
}
