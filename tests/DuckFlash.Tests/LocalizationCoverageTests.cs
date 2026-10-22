using System.Text.Json;
using DuckFlash.Models;
using Xunit;

namespace DuckFlash.Services;

/// <summary>
/// Uji bahwa kedua katalog bahasa punya kunci yang sama dan nilai yang
/// tidak kosong. Kunci yang hilang di salah satu bahasa akan membuat UI
/// campur bahasa, jadi harus ketahuan lewat test.
/// </summary>
public class LocalizationCoverageTests
{
    private static Dictionary<string, string> LoadCatalog(string languageCode)
    {
        var assembly = typeof(LocalizationService).Assembly;

        using var stream = assembly.GetManifestResourceStream(
            $"DuckFlash.Resources.strings.{languageCode}.json");

        Assert.NotNull(stream);

        using var reader = new StreamReader(stream!);
        var json = reader.ReadToEnd();

        var file = JsonSerializer.Deserialize<LocalizationFile>(json);

        Assert.NotNull(file);
        return file!.Strings;
    }

    [Fact]
    public void BothCatalogsContainTheSameKeys()
    {
        var indonesian = LoadCatalog("id");
        var english = LoadCatalog("en");

        var missingInEnglish = indonesian.Keys
            .Where(key => !english.ContainsKey(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        var missingInIndonesian = english.Keys
            .Where(key => !indonesian.ContainsKey(key))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missingInEnglish.Count == 0,
            "Kunci hilang di katalog English: " + string.Join(", ", missingInEnglish));

        Assert.True(
            missingInIndonesian.Count == 0,
            "Kunci hilang di katalog Indonesia: " + string.Join(", ", missingInIndonesian));
    }

    [Fact]
    public void NoCatalogEntryIsEmpty()
    {
        foreach (var languageCode in new[] { "id", "en" })
        {
            var catalog = LoadCatalog(languageCode);

            var empty = catalog
                .Where(pair => string.IsNullOrWhiteSpace(pair.Value))
                .Select(pair => pair.Key)
                .ToList();

            Assert.True(
                empty.Count == 0,
                $"Nilai kosong di katalog {languageCode}: " + string.Join(", ", empty));
        }
    }

    [Fact]
    public void FormatPlaceholdersMatchBetweenCatalogs()
    {
        var indonesian = LoadCatalog("id");
        var english = LoadCatalog("en");

        foreach (var (key, indonesianText) in indonesian)
        {
            if (!english.TryGetValue(key, out var englishText))
            {
                continue;
            }

            var indonesianPlaceholders = ExtractPlaceholders(indonesianText);
            var englishPlaceholders = ExtractPlaceholders(englishText);

            Assert.True(
                indonesianPlaceholders.SequenceEqual(englishPlaceholders),
                $"Tempat parameter berbeda untuk kunci \"{key}\": "
                + $"[{string.Join(",", indonesianPlaceholders)}] vs [{string.Join(",", englishPlaceholders)}]");
        }
    }

    /// <summary>
    /// Ambil semua nomor placeholder {0}, {1}, dst dari sebuah teks.
    /// </summary>
    private static List<int> ExtractPlaceholders(string text)
    {
        var result = new List<int>();
        var index = 0;

        while (index < text.Length)
        {
            var open = text.IndexOf('{', index);

            if (open < 0)
            {
                break;
            }

            var close = text.IndexOf('}', open);

            if (close < 0)
            {
                break;
            }

            var inner = text.Substring(open + 1, close - open - 1);

            if (int.TryParse(inner, out var number))
            {
                result.Add(number);
            }

            index = close + 1;
        }

        return result;
    }
}
