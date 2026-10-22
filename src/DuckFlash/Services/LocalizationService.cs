using System.Text.Json;
using System.Text.Json.Serialization;
using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Menyediakan teks UI sesuai bahasa yang dipilih pengguna.
/// Teks diambil dari file Resources/strings.&lt;kode&gt;.json supaya mudah
/// diterjemahkan tanpa menyentuh kode.
/// </summary>
public sealed class LocalizationService : ITextProvider
{
    private const string EmbeddedResourcePrefix = "DuckFlash.Resources.strings.";

    private readonly Dictionary<AppLanguage, IReadOnlyDictionary<string, string>> _catalogs = new();

    private AppLanguage _currentLanguage = AppLanguage.Indonesian;

    /// <summary>
    /// Sumber teks bawaan berbahasa Indonesia. Dipakai service yang
    /// dikonstruksi tanpa ViewModel, misalnya saat aplikasi dibuka pertama kali.
    /// </summary>
    private static readonly Lazy<ITextProvider> IndonesianFallback =
        new(() => new LocalizationService(AppLanguage.Indonesian));

    /// <summary>Sumber teks bawaan, selalu bahasa Indonesia.</summary>
    public static ITextProvider Default => IndonesianFallback.Value;

    public LocalizationService()
    {
        LoadCatalog(AppLanguage.Indonesian);
        LoadCatalog(AppLanguage.English);
    }

    /// <summary>
    /// Buat service dengan katalog tertentu. Dipakai unit test supaya tidak
    /// bergantung pada file JSON.
    /// </summary>
    public LocalizationService(
        IReadOnlyDictionary<AppLanguage, IReadOnlyDictionary<string, string>> catalogs)
    {
        foreach (var pair in catalogs)
        {
            _catalogs[pair.Key] = pair.Value;
        }
    }

    private LocalizationService(AppLanguage language)
    {
        LoadCatalog(language);
    }

    /// <summary>Bahasa yang sedang dipakai.</summary>
    public AppLanguage CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (!_catalogs.ContainsKey(value))
            {
                return;
            }

            _currentLanguage = value;
        }
    }

    /// <summary>Bahasa yang bisa dipilih pengguna.</summary>
    public IReadOnlyList<AppLanguage> AvailableLanguages { get; } = new[]
    {
        AppLanguage.Indonesian,
        AppLanguage.English
    };

    /// <summary>
    /// Ambil teks untuk kunci tertentu. Kalau kuncinya tidak ada, teks aslinya
    /// dikembalikan supaya tidak ada string kosong di UI.
    /// </summary>
    public string Get(string key, params object?[] args)
    {
        if (_catalogs.TryGetValue(_currentLanguage, out var catalog)
            && catalog.TryGetValue(key, out var text))
        {
            return args.Length == 0
                ? text
                : string.Format(text, args);
        }

        // Coba bahasa Indonesia sebagai cadangan.
        if (_currentLanguage != AppLanguage.Indonesian
            && _catalogs.TryGetValue(AppLanguage.Indonesian, out var fallback)
            && fallback.TryGetValue(key, out var fallbackText))
        {
            return args.Length == 0
                ? fallbackText
                : string.Format(fallbackText, args);
        }

        return key;
    }

    /// <summary>
    /// Ambil teks untuk kunci tertentu, atau nilai bawaan kalau kuncinya tidak ada.
    /// </summary>
    public string GetOrDefault(string key, string fallback, params object?[] args)
    {
        var text = Get(key, args);

        return text == key ? fallback : text;
    }

    /// <summary>
    /// Nama bahasa yang bisa ditampilkan ke pengguna.
    /// </summary>
    public string GetLanguageName(AppLanguage language)
    {
        return language switch
        {
            AppLanguage.Indonesian => "Bahasa Indonesia",
            AppLanguage.English => "English",
            _ => language.ToString()
        };
    }

    private void LoadCatalog(AppLanguage language)
    {
        var code = language == AppLanguage.Indonesian ? "id" : "en";

        var strings = LoadEmbedded(code) ?? LoadFromDisk(code);

        if (strings is not null)
        {
            _catalogs[language] = strings;
        }
    }

    /// <summary>
    /// Baca katalog dari resource yang ditanam di dalam assembly.
    /// </summary>
    private static IReadOnlyDictionary<string, string>? LoadEmbedded(string code)
    {
        var assembly = typeof(LocalizationService).Assembly;

        using var stream = assembly.GetManifestResourceStream($"{EmbeddedResourcePrefix}{code}.json");

        if (stream is null)
        {
            return null;
        }

        try
        {
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            return Parse(json);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Baca katalog dari file di disk, dipakai saat development.
    /// </summary>
    private static IReadOnlyDictionary<string, string>? LoadFromDisk(string code)
    {
        try
        {
            var baseDirectory = AppContext.BaseDirectory;

            var candidates = new[]
            {
                Path.Combine(baseDirectory, "Resources", $"strings.{code}.json"),
                Path.Combine(baseDirectory, $"strings.{code}.json")
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return Parse(File.ReadAllText(candidate));
                }
            }

            var directory = new DirectoryInfo(baseDirectory);

            for (var level = 0; level < 8 && directory is not null; level++)
            {
                var candidate = Path.Combine(
                    directory.FullName, "src", "DuckFlash", "Resources", $"strings.{code}.json");

                if (File.Exists(candidate))
                {
                    return Parse(File.ReadAllText(candidate));
                }

                directory = directory.Parent;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            return null;
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string>? Parse(string json)
    {
        var file = JsonSerializer.Deserialize<LocalizationFile>(json);

        if (file?.Strings is null)
        {
            return null;
        }

        return file.Strings.ToDictionary(pair => pair.Key, pair => pair.Value);
    }
}

/// <summary>
/// Isi file strings.&lt;kode&gt;.json.
/// </summary>
public sealed class LocalizationFile
{
    /// <summary>Kode bahasa, contoh "id" atau "en".</summary>
    [JsonPropertyName("language")]
    public string Language { get; set; } = string.Empty;

    /// <summary>Nama bahasa yang ditampilkan ke pengguna.</summary>
    [JsonPropertyName("languageName")]
    public string LanguageName { get; set; } = string.Empty;

    /// <summary>Daftar teks dengan kuncinya.</summary>
    [JsonPropertyName("strings")]
    public Dictionary<string, string> Strings { get; set; } = new();
}
