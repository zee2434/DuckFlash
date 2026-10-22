namespace DuckFlash.Models;

/// <summary>
/// Status akhir pemasangan platform-tools beserta info pentingnya.
/// </summary>
public sealed class ToolsStatus
{
    /// <summary>True jika platform-tools sudah terpasang di local path (ada adb.exe).</summary>
    public bool IsInstalled { get; init; }

    /// <summary>Path tempat platform-tools diekstraksi.</summary>
    public string LocalPath { get; init; } = string.Empty;

    /// <summary>True jika masih dalam proses download atau ekstraksi.</summary>
    public bool IsBusy { get; init; }

    /// <summary>Indeks 0 sampai 100, kosong kalau tidak ada aktivitas.</summary>
    public int ProgressPercent { get; init; }

    /// <summary>Ukuran file hasil download dalam byte, kosong kalau belum di-download.</summary>
    public long DownloadedBytes { get; init; }

    /// <summary>Total ukuran file dalam byte, kosong kalau belum di-download.</summary>
    public long TotalBytes { get; init; }

    /// <summary>Keterangan error kalau ada.</summary>
    public string? ErrorMessage { get; init; }
}
