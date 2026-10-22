namespace DuckFlash.Models;

/// <summary>
/// Hasil pengecekan versi terbaru di GitHub Releases.
/// </summary>
public sealed class UpdateInfo
{
    /// <summary>True kalau pengecekan selesai tanpa error.</summary>
    public bool IsCheckSuccessful { get; init; }

    /// <summary>True kalau ada versi yang lebih baru dari versi sekarang.</summary>
    public bool IsUpdateAvailable { get; init; }

    /// <summary>Versi terbaru yang ditemukan, kalau ada.</summary>
    public string? LatestVersion { get; init; }

    /// <summary>URL halaman rilis di GitHub.</summary>
    public string? ReleaseUrl { get; init; }

    /// <summary>URL file .exe yang bisa diunduh.</summary>
    public string? DownloadUrl { get; init; }

    /// <summary>Pesan error kalau pengecekan gagal.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Versi aplikasi yang sedang berjalan.</summary>
    public string CurrentVersion { get; init; } = "1.0.0";
}
