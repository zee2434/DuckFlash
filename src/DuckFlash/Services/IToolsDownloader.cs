using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Mengecek dan mengunduh platform-tools.
/// Platform-tools tidak dibundel di repo. File diunduh dari repository resmi Google
/// (platform-tools-latest-windows.zip) lalu diekstraksi ke folder
/// %LocalAppData%\DuckFlash\platform-tools.
/// Aplikasi memanggil adb.exe dan fastboot.exe dari folder itu dengan path penuh,
/// sehingga tidak bergantung pada PATH.
/// </summary>
public interface IToolsDownloader : IToolPaths
{
    /// <summary>Naik setiap kali ada progress dari download atau ekstraksi.</summary>
    event EventHandler<DownloadProgressEventArgs>? ProgressChanged;

    /// <summary>Periksa apakah platform-tools sudah terpasang secara lokal.</summary>
    Task<ToolsStatus> CheckStatusAsync(CancellationToken cancellationToken = default);

    /// <summary>Pastikan platform-tools tersedia (download kalau belum), update progress.</summary>
    Task<ToolsStatus> EnsureInstalledAsync(CancellationToken cancellationToken = default);
}
