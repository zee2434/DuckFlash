using System.Text.Json;
using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Menerjemahkan error mentah dari adb/fastboot menjadi penjelasan bahasa Indonesia
/// beserta solusinya. Daftar pemetaan dibaca dari Resources/error-mappings.json,
/// bukan hardcode di dalam kode.
/// </summary>
public sealed class ErrorTranslator
{
    /// <summary>Nama resource yang CLR gunakan untuk daftar pemetaan error.</summary>
    private const string EmbeddedResourceName = "DuckFlash.Resources.error-mappings.json";

    /// <summary>Nama resource untuk daftar pemetaan error berbahasa Inggris.</summary>
    private const string EmbeddedEnglishResourceName = "DuckFlash.Resources.error-mappings.en.json";

    private readonly IReadOnlyList<ErrorMapping> _mappings;

    /// <summary>Pemetaan bahasa Inggris, dipakai kalau pengguna memilih English.</summary>
    private readonly IReadOnlyList<ErrorMapping> _englishMappings;

    /// <summary>
    /// Muat daftar pemetaan dari file Resources/error-mappings.json.
    /// </summary>
    public ErrorTranslator()
        : this(LoadDefaultMappings(), LoadDefaultEnglishMappings())
    {
    }

    /// <summary>
    /// Buat translator dengan daftar pemetaan tertentu. Dipakai unit test.
    /// </summary>
    public ErrorTranslator(IReadOnlyList<ErrorMapping> mappings)
        : this(mappings, mappings)
    {
    }

    /// <summary>
    /// Buat translator dengan daftar pemetaan Indonesia dan Inggris.
    /// </summary>
    public ErrorTranslator(
        IReadOnlyList<ErrorMapping> mappings,
        IReadOnlyList<ErrorMapping> englishMappings)
    {
        _mappings = mappings;
        _englishMappings = englishMappings;
    }

    /// <summary>Jumlah entri pemetaan yang dimuat.</summary>
    public int MappingCount => _mappings.Count;

    /// <summary>
    /// Terjemahkan error mentah. Kalau tidak ada yang cocok, tetap mengembalikan
    /// hasil supaya UI bisa menampilkan pesan umum beserta error aslinya.
    /// </summary>
    public TranslatedError Translate(string? rawMessage)
    {
        return Translate(rawMessage, useEnglish: false);
    }

    /// <summary>
    /// Terjemahkan error mentah memakai daftar pemetaan bahasa Inggris.
    /// </summary>
    public TranslatedError TranslateEnglish(string? rawMessage)
    {
        return Translate(rawMessage, useEnglish: true);
    }

    /// <summary>
    /// Terjemahkan error mentah dengan memilih daftar pemetaan sesuai bahasa.
    /// </summary>
    public TranslatedError Translate(string? rawMessage, bool useEnglish)
    {
        var raw = rawMessage?.Trim() ?? string.Empty;
        var mappings = useEnglish ? _englishMappings : _mappings;

        if (raw.Length == 0)
        {
            return useEnglish ? BuildEmptyEnglish() : BuildEmptyIndonesian();
        }

        foreach (var mapping in mappings)
        {
            foreach (var pattern in mapping.Patterns)
            {
                if (pattern.Length == 0)
                {
                    continue;
                }

                if (raw.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return new TranslatedError
                    {
                        Title = mapping.Title,
                        Explanation = mapping.Explanation,
                        Solution = mapping.Solution,
                        RawMessage = raw,
                        MappingId = mapping.Id,
                        SolutionLabel = useEnglish ? "Solution" : "Solusi",
                        DetailLabel = useEnglish ? "Detail" : "Detail"
                    };
                }
            }
        }

        return useEnglish ? BuildUnknownEnglish(raw) : BuildUnknownIndonesian(raw);
    }

    /// <summary>
    /// Terjemahkan hasil CommandResult, memakai stdout dan stderr sekaligus.
    /// </summary>
    public TranslatedError Translate(CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return Translate(result.CombinedOutput);
    }

    /// <summary>
    /// Terjemahkan hasil CommandResult dengan memilih daftar pemetaan sesuai bahasa.
    /// </summary>
    public TranslatedError Translate(CommandResult result, bool useEnglish)
    {
        ArgumentNullException.ThrowIfNull(result);

        return Translate(result.CombinedOutput, useEnglish);
    }

    private static TranslatedError BuildEmptyIndonesian()
    {
        return new TranslatedError
        {
            Title = "Perintah gagal",
            Explanation = "Perintah yang dijalankan tidak berhasil.",
            Solution = "Coba ulangi perintahnya. Kalau masih gagal, lihat detail pada panel log.",
            RawMessage = string.Empty
        };
    }

    private static TranslatedError BuildEmptyEnglish()
    {
        return new TranslatedError
        {
            Title = "Command failed",
            Explanation = "The command that was run did not succeed.",
            Solution = "Try running the command again. If it still fails, check the details in the log panel.",
            RawMessage = string.Empty,
            SolutionLabel = "Solution",
            DetailLabel = "Detail"
        };
    }

    private static TranslatedError BuildUnknownIndonesian(string raw)
    {
        return new TranslatedError
        {
            Title = "Perintah gagal",
            Explanation = "Perintah yang dijalankan tidak berhasil dan pesan errornya belum dikenali.",
            Solution = "Baca detail pesan di bawah, lalu coba ulangi perintahnya. Pastikan perangkat dalam mode yang benar.",
            RawMessage = raw
        };
    }

    private static TranslatedError BuildUnknownEnglish(string raw)
    {
        return new TranslatedError
        {
            Title = "Command failed",
            Explanation = "The command that was run did not succeed and its error message is not recognized.",
            Solution = "Read the details below, then try the command again. Make sure the device is in the right mode.",
            RawMessage = raw,
            SolutionLabel = "Solution",
            DetailLabel = "Detail"
        };
    }

    private static IReadOnlyList<ErrorMapping> LoadDefaultMappings()
    {
        return LoadMappings(EmbeddedResourceName, "error-mappings.json");
    }

    private static IReadOnlyList<ErrorMapping> LoadDefaultEnglishMappings()
    {
        return LoadMappings(EmbeddedEnglishResourceName, "error-mappings.en.json");
    }

    /// <summary>
    /// Muat daftar pemetaan. Sumber pertama: resource yang ditanam di dalam
    /// assembly (dipakai aplikasi yang dipublish sebagai .exe single-file).
    /// Sumber kedua: file di disk, dipakai saat development.
    /// </summary>
    private static IReadOnlyList<ErrorMapping> LoadMappings(string embeddedName, string fileName)
    {
        var embedded = LoadEmbeddedMappings(embeddedName);

        if (embedded.Count > 0)
        {
            return embedded;
        }

        try
        {
            var path = ResolveMappingsPath(fileName);

            if (path is null || !File.Exists(path))
            {
                return Array.Empty<ErrorMapping>();
            }

            var json = File.ReadAllText(path);
            var file = JsonSerializer.Deserialize<ErrorMappingFile>(json);

            return file?.Mappings ?? (IReadOnlyList<ErrorMapping>)Array.Empty<ErrorMapping>();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Kalau file mapping tidak terbaca, aplikasi tetap jalan dengan
            // penjelasan umum. Error aslinya tetap ditampilkan ke pengguna.
            return Array.Empty<ErrorMapping>();
        }
    }

    /// <summary>
    /// Baca daftar pemetaan dari resource yang ditanam di dalam assembly.
    /// </summary>
    private static IReadOnlyList<ErrorMapping> LoadEmbeddedMappings(string resourceName)
    {
        var assembly = typeof(ErrorTranslator).Assembly;

        using var stream = assembly.GetManifestResourceStream(resourceName);

        if (stream is null)
        {
            return Array.Empty<ErrorMapping>();
        }

        try
        {
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();
            var file = JsonSerializer.Deserialize<ErrorMappingFile>(json);

            return file?.Mappings ?? (IReadOnlyList<ErrorMapping>)Array.Empty<ErrorMapping>();
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            return Array.Empty<ErrorMapping>();
        }
    }

    private static string? ResolveMappingsPath(string fileName)
    {
        // Saat development: folder output debugging/release.
        var baseDirectory = AppContext.BaseDirectory;

        var candidates = new[]
        {
            Path.Combine(baseDirectory, "Resources", fileName),
            Path.Combine(baseDirectory, fileName)
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // Naik sampai beberapa tingkat untuk menemukan folder src saat development
        // atau folder output proyek tes.
        var directory = new DirectoryInfo(baseDirectory);

        for (var level = 0; level < 8 && directory is not null; level++)
        {
            var candidate = Path.Combine(
                directory.FullName, "src", "DuckFlash", "Resources", fileName);

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        return null;
    }
}
