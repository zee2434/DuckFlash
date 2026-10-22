namespace DuckFlash.Models;

/// <summary>
/// Hasil menjalankan satu command adb/fastboot.
/// </summary>
public sealed class CommandResult
{
    /// <summary>Exit code proses. 0 berarti sukses.</summary>
    public int ExitCode { get; init; }

    /// <summary>Seluruh isi stdout.</summary>
    public string StandardOutput { get; init; } = string.Empty;

    /// <summary>Seluruh isi stderr. fastboot menulis hampir semua outputnya ke sini.</summary>
    public string StandardError { get; init; } = string.Empty;

    /// <summary>Command lengkap yang dijalankan, untuk preview di log.</summary>
    public string CommandLine { get; init; } = string.Empty;

    /// <summary>True jika proses selesai dengan exit code 0.</summary>
    public bool IsSuccess => ExitCode == 0;

    /// <summary>Gabungan stdout dan stderr, dipakai untuk pencocokan error.</summary>
    public string CombinedOutput => string.IsNullOrWhiteSpace(StandardError)
        ? StandardOutput
        : string.IsNullOrWhiteSpace(StandardOutput)
            ? StandardError
            : StandardOutput + Environment.NewLine + StandardError;
}
