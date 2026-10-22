namespace DuckFlash.Services;

/// <summary>
/// Sumber teks untuk service. Implementasinya adalah LocalizationService,
/// supaya semua teks yang tampil ke pengguna bisa mengikuti bahasa pilihan.
/// </summary>
public interface ITextProvider
{
    /// <summary>
    /// Ambil teks untuk kunci tertentu. Kalau kuncinya tidak ada, teks aslinya
    /// dikembalikan supaya tidak ada string kosong di UI.
    /// </summary>
    string Get(string key, params object?[] args);
}
