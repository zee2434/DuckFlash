namespace DuckFlash.Models;

/// <summary>
/// Hasil validasi file sebelum flash atau sideload.
/// </summary>
public sealed class FileValidationResult
{
    /// <summary>True kalau file aman untuk dipakai.</summary>
    public bool IsValid { get; init; }

    /// <summary>Path penuh file yang diperiksa.</summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>Nama file saja, tanpa folder.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Ukuran file dalam byte.</summary>
    public long FileSize { get; init; }

    /// <summary>Hash SHA256 file dalam format heksadesimal huruf kecil.</summary>
    public string Sha256 { get; init; } = string.Empty;

    /// <summary>Alasan kalau file tidak valid, dalam bahasa Indonesia.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>True kalau file ini cocok untuk fastboot flash (ekstensi .img).</summary>
    public bool IsFlashImage { get; init; }

    /// <summary>True kalau file ini cocok untuk adb sideload (ekstensi .zip).</summary>
    public bool IsSideloadPackage { get; init; }
}
