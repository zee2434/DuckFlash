namespace DuckFlash.Models;

/// <summary>
/// Hasil penerjemahan error mentah dari adb/fastboot.
/// </summary>
public sealed class TranslatedError
{
    /// <summary>Judul singkat, contoh: "Perangkat belum diizinkan".</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Penjelasan apa yang terjadi.</summary>
    public string Explanation { get; init; } = string.Empty;

    /// <summary>Langkah solusi.</summary>
    public string Solution { get; init; } = string.Empty;

    /// <summary>Error mentah aslinya, ditampilkan sebagai bukti.</summary>
    public string RawMessage { get; init; } = string.Empty;

    /// <summary>Pengenal entri pemetaan yang cocok, atau null kalau tidak ada yang cocok.</summary>
    public string? MappingId { get; init; }

    /// <summary>True kalau error mentah berhasil dikenali.</summary>
    public bool IsKnown => MappingId is not null;

    /// <summary>
    /// Label untuk bagian solusi, mengikuti bahasa yang dipakai.
    /// </summary>
    public string SolutionLabel { get; init; } = "Solusi";

    /// <summary>
    /// Label untuk bagian detail, mengikuti bahasa yang dipakai.
    /// </summary>
    public string DetailLabel { get; init; } = "Detail";

    /// <summary>Versi lengkap siap ditampilkan di UI.</summary>
    public string Formatted =>
        $"[{Title}]\n{Explanation}\n\n{SolutionLabel}: {Solution}"
        + (string.IsNullOrWhiteSpace(RawMessage) ? string.Empty : $"\n\n{DetailLabel}: {RawMessage}");
}
