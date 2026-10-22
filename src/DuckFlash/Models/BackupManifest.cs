using System.Text.Json.Serialization;

namespace DuckFlash.Models;

/// <summary>
/// Daftar isi folder cadangan. File ini yang dipakai saat pemulihan, jadi
/// nama paket dan nama file APK-nya harus tersimpan.
/// </summary>
public sealed class BackupManifest
{
    /// <summary>Versi format manifest yang dipakai aplikasi ini.</summary>
    public const int CurrentVersion = 1;

    /// <summary>Versi format manifest.</summary>
    [JsonPropertyName("version")]
    public int Version { get; set; } = CurrentVersion;

    /// <summary>Waktu cadangan dibuat, format ISO-8601.</summary>
    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>Daftar aplikasi yang tersimpan di folder cadangan.</summary>
    [JsonPropertyName("packages")]
    public List<BackupManifestEntry> Packages { get; set; } = new();
}

/// <summary>
/// Satu aplikasi di dalam manifest cadangan.
/// </summary>
public sealed class BackupManifestEntry
{
    public BackupManifestEntry()
    {
    }

    public BackupManifestEntry(string packageName, IEnumerable<string> apkFiles)
    {
        PackageName = packageName;
        ApkFiles = apkFiles.ToList();
    }

    /// <summary>Nama package aplikasi.</summary>
    [JsonPropertyName("packageName")]
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// Nama file APK di dalam folder cadangan (bukan path penuh), supaya
    /// folder cadangan bisa dipindah ke komputer lain.
    /// </summary>
    [JsonPropertyName("apkFiles")]
    public List<string> ApkFiles { get; set; } = new();
}
