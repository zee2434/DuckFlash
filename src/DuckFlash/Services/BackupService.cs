using System.Text;
using System.Text.Json;
using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Pencadangan dan pemulihan aplikasi pengguna.
///
/// Cara kerjanya sederhana:
/// - Pencadangan: baca daftar aplikasi pengguna dari perangkat
///   (<c>pm list packages -3 -f</c>), lalu tarik masing-masing APK ke folder
///   yang ditentukan pengguna, beserta file manifest backup-manifest.json.
/// - Pemulihan: baca file manifest, lalu pasang ulang APK-nya di perangkat
///   (<c>adb install -r</c>) atau (<c>adb install-multiple -r</c>) kalau ada
///   lebih dari satu file APK untuk satu aplikasi.
///
/// PENTING: pencadangan ini TIDAK menyimpan data aplikasi (foto, pesan,
/// setelan), hanya file APK-nya. Penjelasan ini wajib ditampilkan ke pengguna.
/// </summary>
public sealed class BackupService
{
    /// <summary>Nama file manifest di dalam folder cadangan.</summary>
    public const string ManifestFileName = "backup-manifest.json";

    private readonly AdbService _adbService;
    private readonly ITextProvider _text;

    public BackupService(AdbService adbService)
        : this(adbService, LocalizationService.Default)
    {
    }

    public BackupService(AdbService adbService, ITextProvider text)
    {
        _adbService = adbService;
        _text = text;
    }

    /// <summary>
    /// Baca daftar aplikasi pengguna beserta path file APK-nya di perangkat.
    /// </summary>
    public async Task<IReadOnlyList<BackupPackage>> ListUserPackagesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await _adbService
            .ExecuteAsync(new[] { "shell", "pm", "list", "packages", "-3", "-f" }, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Array.Empty<BackupPackage>();
        }

        return ParsePackageList(result.StandardOutput);
    }

    /// <summary>
    /// Parse keluaran <c>pm list packages -3 -f</c>.
    /// Format tiap baris: <c>package:/data/app/.../base.apk=com.foo</c>.
    /// Path APK bisa mengandung tanda '=' (misalnya folder berhash), jadi
    /// pemecahan dilakukan pada tanda '=' terakhir.
    /// </summary>
    public static IReadOnlyList<BackupPackage> ParsePackageList(string? output)
    {
        var packages = new List<BackupPackage>();

        if (string.IsNullOrWhiteSpace(output))
        {
            return packages;
        }

        foreach (var raw in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();

            if (!line.StartsWith("package:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var separatorIndex = line.LastIndexOf('=');

            if (separatorIndex < 0)
            {
                continue;
            }

            var remotePath = line["package:".Length..separatorIndex].Trim();
            var packageName = line[(separatorIndex + 1)..].Trim();

            if (remotePath.Length == 0 || packageName.Length == 0)
            {
                continue;
            }

            packages.Add(new BackupPackage(packageName, new[] { remotePath }));
        }

        return packages;
    }

    /// <summary>
    /// Cadangkan semua APK aplikasi pengguna ke <paramref name="folderPath"/>.
    /// </summary>
    public async Task<BackupResult> BackupAsync(
        string folderPath,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        Directory.CreateDirectory(folderPath);

        var packages = await ListUserPackagesAsync(cancellationToken).ConfigureAwait(false);

        if (packages.Count == 0)
        {
            return new BackupResult
            {
                Total = 0,
                Succeeded = 0,
                Failed = 0,
                ManifestPath = Path.Combine(folderPath, ManifestFileName)
            };
        }

        progress?.Report(_text.Get("status.backup.started", packages.Count.ToString()));

        var entries = new List<BackupManifestEntry>();
        int succeeded = 0;
        int failed = 0;

        foreach (var package in packages)
        {
            var apkFiles = new List<string>();
            var index = 0;

            foreach (var remotePath in package.RemoteApkPaths)
            {
                index++;

                var fileName = BuildApkFileName(package.PackageName, index);
                var localPath = Path.Combine(folderPath, fileName);

                var pullResult = await _adbService
                    .ExecuteAsync(new[] { "pull", remotePath, localPath }, cancellationToken)
                    .ConfigureAwait(false);

                if (pullResult.IsSuccess && File.Exists(localPath))
                {
                    apkFiles.Add(fileName);
                }
            }

            if (apkFiles.Count > 0)
            {
                entries.Add(new BackupManifestEntry(package.PackageName, apkFiles));
                succeeded++;
            }
            else
            {
                failed++;
            }

            progress?.Report(package.PackageName);
        }

        var manifestPath = await WriteManifestAsync(folderPath, entries, DateTimeOffset.UtcNow.ToString("o"))
            .ConfigureAwait(false);

        return new BackupResult
        {
            Total = packages.Count,
            Succeeded = succeeded,
            Failed = failed,
            ManifestPath = manifestPath
        };
    }

    /// <summary>
    /// Pulihkan aplikasi dari folder <paramref name="folderPath"/>.
    /// Folder ini harus punya file backup-manifest.json.
    /// </summary>
    public async Task<RestoreResult> RestoreAsync(
        string folderPath,
        IProgress<string>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(folderPath);

        var manifest = LoadManifest(folderPath);

        if (manifest is null || manifest.Packages.Count == 0)
        {
            return new RestoreResult
            {
                Total = 0,
                Succeeded = 0,
                Failed = 0
            };
        }

        int succeeded = 0;
        int failed = 0;

        progress?.Report(_text.Get("status.restore.started"));

        foreach (var entry in manifest.Packages)
        {
            progress?.Report(entry.PackageName);

            var localFiles = entry.ApkFiles
                .Select(fileName => ResolveInsideFolder(folderPath, fileName))
                .Where(path => !string.IsNullOrEmpty(path) && File.Exists(path))
                .Select(path => path!)
                .ToList();

            if (localFiles.Count == 0)
            {
                failed++;
                continue;
            }

            // Split APK harus dipasang sekaligus supaya base APK-nya cocok.
            var installArgs = localFiles.Count > 1
                ? new[] { "install-multiple", "-r" }.Concat(localFiles).ToArray()
                : new[] { "install", "-r", localFiles[0] };

            var result = await _adbService
                .ExecuteAsync(installArgs, cancellationToken)
                .ConfigureAwait(false);

            if (result.IsSuccess)
            {
                succeeded++;
            }
            else
            {
                failed++;
            }
        }

        return new RestoreResult
        {
            Total = manifest.Packages.Count,
            Succeeded = succeeded,
            Failed = failed
        };
    }

    /// <summary>
    /// Baca file manifest dari folder cadangan. Null kalau file tidak ada,
    /// rusak, atau versinya tidak cocok.
    /// </summary>
    public static BackupManifest? LoadManifest(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return null;
        }

        string path;

        try
        {
            path = Path.GetFullPath(Path.Combine(folderPath, ManifestFileName));
        }
        catch (Exception)
        {
            return null;
        }

        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var manifest = JsonSerializer.Deserialize<BackupManifest>(stream);

            if (manifest is null
                || manifest.Version != BackupManifest.CurrentVersion
                || manifest.Packages is null
                || manifest.Packages.Count == 0)
            {
                return null;
            }

            return manifest;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Susun nama file APK di dalam folder cadangan.
    /// </summary>
    public static string BuildApkFileName(string packageName, int index)
    {
        // Nama package sudah divalidasi (huruf, angka, titik, garis bawah),
        // jadi aman dipakai sebagai nama file.
        return $"{packageName}-{index}.apk";
    }

    /// <summary>
    /// Tulis file manifest ke dalam folder cadangan.
    /// </summary>
    private static async Task<string> WriteManifestAsync(
        string folderPath,
        IReadOnlyList<BackupManifestEntry> entries,
        string createdAt)
    {
        var safeFolder = Path.GetFullPath(folderPath);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        var json = JsonSerializer.Serialize(
            new BackupManifest
            {
                Version = BackupManifest.CurrentVersion,
                CreatedAt = createdAt,
                Packages = entries.ToList()
            },
            options);

        var manifestPath = Path.Combine(safeFolder, ManifestFileName);

        await File.WriteAllTextAsync(manifestPath, json, new UTF8Encoding(false)).ConfigureAwait(false);

        return manifestPath;
    }

    /// <summary>
    /// Ubah nama file relatif jadi path penuh, tapi hanya kalau hasilnya tetap
    /// di dalam folder cadangan. Ini mencegah nama file dari manifest tries
    /// keluar dari folder.
    /// </summary>
    private static string? ResolveInsideFolder(string folderPath, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        string folder;
        string fullPath;

        try
        {
            folder = Path.GetFullPath(folderPath);
            fullPath = Path.GetFullPath(Path.Combine(folder, fileName));
        }
        catch (Exception)
        {
            return null;
        }

        if (!fullPath.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return fullPath;
    }
}
