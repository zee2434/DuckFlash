using System.Text.Json;
using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Menyimpan pengaturan aplikasi (saat ini hanya bahasa) di file JSON di
/// %LocalAppData%\DuckFlash\settings.json.
/// </summary>
public sealed class AppSettings
{
    private readonly string _filePath;

    public AppSettings(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DuckFlash",
            "settings.json");
    }

    /// <summary>Bahasa yang dipilih pengguna.</summary>
    public AppLanguage Language { get; set; } = AppLanguage.Indonesian;

    /// <summary>
    /// Baca pengaturan dari disk. Kalau file belum ada atau rusak, pakai nilai
    /// bawaan tanpa melempar exception.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return this;
            }

            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<SettingsFile>(json);

            if (data is not null && Enum.TryParse<AppLanguage>(data.Language, ignoreCase: true, out var language))
            {
                Language = language;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Pengaturan yang tidak terbaca tidak boleh menggagalkan aplikasi.
        }

        return this;
    }

    /// <summary>
    /// Simpan pengaturan ke disk. Kalau gagal, diamkan saja supaya aplikasi
    /// tetap jalan.
    /// </summary>
    public void Save()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var data = new SettingsFile { Language = Language.ToString() };
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });

            File.WriteAllText(_filePath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Tidak bisa menyimpan pengaturan bukan alasan untuk menggagalkan.
        }
    }

    /// <summary>Isi file settings.json.</summary>
    private sealed class SettingsFile
    {
        public string Language { get; set; } = nameof(AppLanguage.Indonesian);
    }
}
