using System.Text.Json.Serialization;

namespace DuckFlash.Models;

/// <summary>
/// Satu aplikasi yang terpasang di perangkat, lengkap dengan lokasi file APK
///-nya di perangkat. Dipakai saat pencadangan.
/// </summary>
public sealed class BackupPackage
{
    public BackupPackage()
    {
    }

    public BackupPackage(string packageName, IEnumerable<string> remoteApkPaths)
    {
        PackageName = packageName;
        RemoteApkPaths = remoteApkPaths.ToList();
    }

    /// <summary>Nama package, misalnya com.android.chrome.</summary>
    [JsonPropertyName("packageName")]
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// Path APK di perangkat. Satu aplikasi bisa punya lebih dari satu file
    /// (base.apk plus split apk), jadi dipakai daftar.
    /// </summary>
    [JsonPropertyName("remoteApkPaths")]
    public List<string> RemoteApkPaths { get; set; } = new();
}
