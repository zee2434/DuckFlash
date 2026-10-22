using System.Text.Json.Serialization;

namespace DuckFlash.Models;

/// <summary>
/// Satu entri pemetaan error dari file Resources/error-mappings.json.
/// </summary>
public sealed class ErrorMapping
{
    /// <summary>Pengenal unik untuk entri ini.</summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Pola error mentah yang dikenali, dicocokkan tanpa case sensitive.</summary>
    [JsonPropertyName("patterns")]
    public List<string> Patterns { get; set; } = new();

    /// <summary>Judul singkat dalam bahasa Indonesia.</summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>Penjelasan apa yang terjadi.</summary>
    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;

    /// <summary>Langkah solusi untuk pengguna.</summary>
    [JsonPropertyName("solution")]
    public string Solution { get; set; } = string.Empty;
}

/// <summary>
/// Isi file error-mappings.json.
/// </summary>
public sealed class ErrorMappingFile
{
    [JsonPropertyName("mappings")]
    public List<ErrorMapping> Mappings { get; set; } = new();
}
