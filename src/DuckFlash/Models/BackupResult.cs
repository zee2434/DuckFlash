namespace DuckFlash.Models;

/// <summary>
/// Hasil proses pencadangan APK aplikasi.
/// </summary>
public sealed class BackupResult
{
    /// <summary>Berapa aplikasi yang dicoba dicadangkan.</summary>
    public int Total { get; init; }

    /// <summary>Berapa aplikasi yang berhasil dicadangkan.</summary>
    public int Succeeded { get; init; }

    /// <summary>Berapa aplikasi yang gagal dicadangkan.</summary>
    public int Failed { get; init; }

    /// <summary>Path file manifest yang ditulis di folder cadangan.</summary>
    public string ManifestPath { get; init; } = string.Empty;

    /// <summary>True kalau tidak ada aplikasi yang bisa dicadangkan.</summary>
    public bool IsEmpty => Total == 0;
}

/// <summary>
/// Hasil proses pemulihan aplikasi dari folder cadangan.
/// </summary>
public sealed class RestoreResult
{
    /// <summary>Berapa aplikasi yang dicoba dipulihkan.</summary>
    public int Total { get; init; }

    /// <summary>Berapa aplikasi yang berhasil dipasang.</summary>
    public int Succeeded { get; init; }

    /// <summary>Berapa aplikasi yang gagal dipasang.</summary>
    public int Failed { get; init; }
}
