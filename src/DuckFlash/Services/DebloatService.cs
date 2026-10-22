using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Membaca daftar aplikasi yang terpasang di perangkat dan menghapus aplikasi
/// yang dipilih dengan <c>adb shell pm uninstall --user 0</c>.
///
/// Flag <c>--user 0</c> hanya menghapus data aplikasi untuk pengguna utama
/// (user 0). Aplikasi sistem tidak hilang, jadi masih bisa dikembalikan dengan
/// <c>cmd package install-existing</c> atau dipasang ulang dari Play Store.
/// </summary>
public sealed class DebloatService
{
    private readonly AdbService _adbService;
    private readonly ITextProvider _text;

    public DebloatService(AdbService adbService)
        : this(adbService, LocalizationService.Default)
    {
    }

    public DebloatService(AdbService adbService, ITextProvider text)
    {
        _adbService = adbService;
        _text = text;
    }

    /// <summary>
    /// Daftar aplikasi yang dipasang pengguna (bukan aplikasi sistem).
    /// </summary>
    public async Task<IReadOnlyList<string>> ListUserPackagesAsync(
        CancellationToken cancellationToken = default)
    {
        return await ListPackagesAsync(system: false, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Daftar aplikasi bawaan sistem.
    /// </summary>
    public async Task<IReadOnlyList<string>> ListSystemPackagesAsync(
        CancellationToken cancellationToken = default)
    {
        return await ListPackagesAsync(system: true, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<string>> ListPackagesAsync(
        bool system,
        CancellationToken cancellationToken)
    {
        // pm list packages -3 = aplikasi pengguna, -s = aplikasi sistem.
        var arguments = system
            ? new[] { "shell", "pm", "list", "packages", "-s" }
            : new[] { "shell", "pm", "list", "packages", "-3" };

        var result = await _adbService.ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return Array.Empty<string>();
        }

        return ParsePackages(result.StandardOutput);
    }

    /// <summary>
    /// Parse keluaran <c>pm list packages</c> yangberbentuk
    /// "package:com.android.chrome".
    /// </summary>
    public static IReadOnlyList<string> ParsePackages(string? output)
    {
        var packages = new List<string>();

        foreach (var raw in (output ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var line = raw.Trim();

            if (!line.StartsWith("package:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = line["package:".Length..].Trim();

            if (name.Length > 0)
            {
                packages.Add(name);
            }
        }

        return packages;
    }

    /// <summary>
    /// Hapus aplikasi untuk user 0. Pemanggil WAJIB menampilkan konfirmasi
    /// dulu karena aksi ini membuang data aplikasi.
    /// </summary>
    public async Task<CommandResult> UninstallAsync(
        string packageName,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidPackageName(packageName))
        {
            throw new ArgumentException(
                _text.Get("validate.packageNameInvalid", packageName),
                nameof(packageName));
        }

        return await _adbService
            .ExecuteAsync(
                new[] { "shell", "pm", "uninstall", "--user", "0", packageName },
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// True kalau adb melaporkan penghapusan berhasil.
    /// </summary>
    public static bool IsUninstallSuccessful(CommandResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.IsSuccess
            && result.CombinedOutput.Contains("Success", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Cek nama package supaya tidak ada karakter aneh yang ikut dikirim
    /// ke shell perangkat.
    /// </summary>
    public static bool IsValidPackageName(string? packageName)
    {
        if (string.IsNullOrWhiteSpace(packageName))
        {
            return false;
        }

        foreach (var character in packageName)
        {
            if (!char.IsLetterOrDigit(character) && character != '.' && character != '_')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Susun teks konfirmasi berisi daftar aplikasi yang akan dihapus.
    /// </summary>
    public static string BuildConfirmationText(
        IReadOnlyList<string> packageNames,
        ITextProvider text)
    {
        ArgumentNullException.ThrowIfNull(packageNames);
        ArgumentNullException.ThrowIfNull(text);

        if (packageNames.Count == 0)
        {
            return text.Get("confirm.debloatEmpty");
        }

        var list = string.Join(Environment.NewLine, packageNames.Select(name => $"- {name}"));

        return text.Get("confirm.debloat", packageNames.Count.ToString(), list);
    }

    /// <summary>
    /// Label persetujuan di dialog konfirmasi destruktif.
    /// </summary>
    public static string BuildAcknowledgementLabel(ITextProvider text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Get("ack.debloat");
    }
}
