using System.Diagnostics;
using System.IO.Compression;
using System.Net.Http;
using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Status pemasangan driver USB Google.
/// </summary>
public enum DriverInstallStatus
{
    NotInstalled,
    Checking,
    Installing,
    Installed,
    Failed,
    ElevationDeclined
}

/// <summary>
/// Mengunduh Google USB Driver dari repository resmi Google lalu memasangnya
/// dengan pnputil. Driver tidak pernah dibuat atau dibundel oleh aplikasi ini.
/// </summary>
public sealed class DriverService : IDisposable
{
    /// <summary>URL driver USB resmi Google.</summary>
    public const string DriverUrl =
        "https://dl.google.com/android/repository/usb_driver_r13-windows.zip";

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly string _driverDirectory;
    private readonly IProcessRunner _processRunner;
    private readonly ITextProvider _text;
    private bool _disposed;

    public DriverService(IProcessRunner processRunner)
        : this(processRunner, null, null)
    {
    }

    public DriverService(IProcessRunner processRunner, string? driverDirectory, HttpClient? httpClient)
        : this(processRunner, driverDirectory, httpClient, LocalizationService.Default)
    {
    }

    public DriverService(
        IProcessRunner processRunner,
        string? driverDirectory,
        HttpClient? httpClient,
        ITextProvider text)
    {
        _processRunner = processRunner;
        _driverDirectory = driverDirectory ?? DefaultDriverDirectory();
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        _text = text;
    }

    /// <summary>Naik setiap ada perubahan status atau progress.</summary>
    public event EventHandler<DownloadProgressEventArgs>? ProgressChanged;

    /// <summary>Folder tempat driver diunduh dan diekstrak.</summary>
    public string DriverDirectory => _driverDirectory;

    /// <summary>
    /// Periksa apakah driver USB sudah terpasang, dengan membaca daftar driver
    /// dari pnputil. Tidak butuh hak Administrator untuk pemeriksaan ini.
    /// </summary>
    public async Task<DriverInstallStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _processRunner.RunAsync(
                "pnputil.exe",
                new[] { "/enum-drivers" },
                cancellationToken).ConfigureAwait(false);

            var output = result.CombinedOutput;

            // Driver Google USB punya nama contain "android" atau "google".
            if (output.Contains("android", StringComparison.OrdinalIgnoreCase)
                || output.Contains("google", StringComparison.OrdinalIgnoreCase))
            {
                return DriverInstallStatus.Installed;
            }

            return DriverInstallStatus.NotInstalled;
        }
        catch (Exception ex) when (ex is ProcessStartException or IOException)
        {
            return DriverInstallStatus.NotInstalled;
        }
    }

    /// <summary>
    /// Unduh driver USB Google lalu pasang dengan pnputil. Meminta elevasi
    /// Administrator hanya untuk langkah pemasangan.
    /// </summary>
    public async Task<DriverInstallStatus> InstallAsync(CancellationToken cancellationToken = default)
    {
        Report(0, _text.Get("driver.downloading"));

        try
        {
            var zipPath = Path.Combine(_driverDirectory, "usb_driver.zip");

            Directory.CreateDirectory(_driverDirectory);

            await DownloadDriverAsync(zipPath, cancellationToken).ConfigureAwait(false);
            ExtractDriverAsync(zipPath);

            var infFiles = Directory
                .GetFiles(_driverDirectory, "*.inf", SearchOption.AllDirectories)
                .ToList();

            if (infFiles.Count == 0)
            {
                Report(100, _text.Get("driver.infNotFound"));
                return DriverInstallStatus.Failed;
            }

            var infPath = infFiles[0];
            Report(90, _text.Get("driver.installing"));

            var installStatus = await InstallDriverAsync(infPath, cancellationToken).ConfigureAwait(false);

            if (installStatus == DriverInstallStatus.Installed)
            {
                Report(100, _text.Get("driver.installed"));
            }

            return installStatus;
        }
        catch (OperationCanceledException)
        {
            Report(0, _text.Get("driver.cancelled"));
            return DriverInstallStatus.Failed;
        }
        catch (Exception ex)
        {
            Report(0, _text.Get("driver.failed", ex.Message));
            return DriverInstallStatus.Failed;
        }
    }

    private async Task DownloadDriverAsync(string zipPath, CancellationToken cancellationToken)
    {
        using var response = await _httpClient
            .GetAsync(DriverUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0L;
        var downloadedBytes = 0L;
        var lastPercentage = -1;

        await using var source = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var target = new FileStream(
            zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

        var buffer = new byte[81920];
        int read;

        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            downloadedBytes += read;

            if (totalBytes > 0)
            {
                var percentage = (int)(downloadedBytes * 100 / totalBytes);

                if (percentage != lastPercentage)
                {
                    lastPercentage = percentage;
                    Report(percentage, _text.Get("driver.downloadingProgress", percentage));
                }
            }
        }
    }

    private void ExtractDriverAsync(string zipPath)
    {
        var staging = _driverDirectory + ".staging";

        try
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }

            Directory.CreateDirectory(staging);
            ZipFile.ExtractToDirectory(zipPath, staging, overwriteFiles: true);

            // Isi folder staging dipindahkan ke folder driver utama.
            foreach (var directory in Directory.GetDirectories(staging))
            {
                CopyDirectory(directory, _driverDirectory);
            }

            foreach (var file in Directory.GetFiles(staging))
            {
                File.Copy(file, Path.Combine(_driverDirectory, Path.GetFileName(file)), overwrite: true);
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                {
                    Directory.Delete(staging, recursive: true);
                }
            }
            catch (IOException)
            {
                // Tidak fatal.
            }
        }
    }

    private async Task<DriverInstallStatus> InstallDriverAsync(string infPath, CancellationToken cancellationToken)
    {
        // pnputil /add-driver /install butuh hak Administrator.
        // Kalau tidak elevated, jalankan ulang lewat runas.
        var startInfo = new ProcessStartInfo
        {
            FileName = "pnputil.exe",
            Arguments = $"/add-driver \"{infPath}\" /install",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Verb = "runas"
        };

        try
        {
            using var process = Process.Start(startInfo);

            if (process is null)
            {
                return DriverInstallStatus.Failed;
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            return process.ExitCode == 0
                ? DriverInstallStatus.Installed
                : DriverInstallStatus.Failed;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Pengguna menolak dialog elevasi, atau proses tidak bisa dijalankan.
            return DriverInstallStatus.ElevationDeclined;
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.GetDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private void Report(int percentage, string message)
    {
        ProgressChanged?.Invoke(this, new DownloadProgressEventArgs(percentage, message));
    }

    private static string DefaultDriverDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "DuckFlash", "usb-driver");
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
