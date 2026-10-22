namespace DuckFlash.Models;

/// <summary>
/// Opsi bahasa untuk ditampilkan di ComboBox pengaturan.
/// </summary>
public sealed class LanguageOption
{
    public LanguageOption(AppLanguage language, string displayName)
    {
        Language = language;
        DisplayName = displayName;
    }

    /// <summary>Bahasa yang dipilih.</summary>
    public AppLanguage Language { get; }

    /// <summary>Nama bahasa yang ditampilkan ke pengguna.</summary>
    public string DisplayName { get; }

    /// <summary>Nama bahasa ditampilkan otomatis di daftar.</summary>
    public override string ToString()
    {
        return DisplayName;
    }
}
