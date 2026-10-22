using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Mengubah output mentah dari adb/fastboot menjadi data yang bisa ditampilkan.
/// Semua parser di sini stateless supaya mudah diuji.
/// </summary>
public static class DeviceOutputParser
{
    /// <summary>
    /// Parse output dari <c>adb devices</c>.
    /// Contoh output:
    /// <code>
    /// List of devices attached
    /// ABC123     device
    /// DEF456     unauthorized
    /// </code>
    /// </summary>
    public static IReadOnlyList<DeviceInfo> ParseAdbDevices(string? output)
    {
        var devices = new List<DeviceInfo>();

        if (string.IsNullOrWhiteSpace(output))
        {
            return devices;
        }

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();

            // Lewati baris judul dan baris kosong.
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("List of devices", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (line.StartsWith("*", StringComparison.Ordinal))
            {
                // Baris pesan pembuka daemon adb, bukan perangkat.
                continue;
            }

            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                continue;
            }

            devices.Add(new DeviceInfo
            {
                Serial = parts[0],
                Status = parts[1],
                IsAdb = true
            });
        }

        return devices;
    }

    /// <summary>
    /// Parse output dari <c>fastboot devices</c>.
    /// Contoh output:
    /// <code>
    /// ABC123    fastboot
    /// </code>
    /// </summary>
    public static IReadOnlyList<DeviceInfo> ParseFastbootDevices(string? output)
    {
        var devices = new List<DeviceInfo>();

        if (string.IsNullOrWhiteSpace(output))
        {
            return devices;
        }

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            // fastboot bisa menulis "<serial> fastboot" atau "<serial> <status>".
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length < 2)
            {
                continue;
            }

            devices.Add(new DeviceInfo
            {
                Serial = parts[0],
                Status = parts[1],
                IsFastboot = true
            });
        }

        return devices;
    }

    /// <summary>
    /// Parse output dari <c>adb shell getprop</c> yang berformat <c>[key]: [value]</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParseGetProp(string? output)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(output))
        {
            return result;
        }

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            // Format: [ro.product.model]: [Pixel 7]
            var separator = line.IndexOf("]: [", StringComparison.Ordinal);

            if (separator < 0)
            {
                continue;
            }

            var key = line[..separator].TrimStart('[').Trim();
            var value = line[(separator + 3)..].Trim().TrimStart('[').TrimEnd(']').Trim();

            if (key.Length == 0)
            {
                continue;
            }

            result[key] = value;
        }

        return result;
    }

    /// <summary>
    /// Parse output dari <c>fastboot getvar all</c>.
    /// Contoh output:
    /// <code>
    /// product: panther
    /// unlocked: yes
    /// current-slot: a
    /// </code>
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParseGetVar(string? output)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(output))
        {
            return result;
        }

        foreach (var rawLine in output.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length == 0)
            {
                continue;
            }

            // fastboot=getvar wrote a "<nama>: <nilai>" per baris.
            var separator = line.IndexOf(':', StringComparison.Ordinal);

            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            if (key.Length == 0)
            {
                continue;
            }

            result[key] = value;
        }

        return result;
    }

    /// <summary>
    /// Baca nilai bootloader dari hasil getvar.
    /// </summary>
    public static string ParseBootloaderState(IReadOnlyDictionary<string, string> getVar)
    {
        return ParseBootloaderState(getVar, "tidak diketahui");
    }

    /// <summary>
    /// Baca nilai bootloader dari hasil getvar, dengan teks untuk nilai yang
    /// tidak diketahui.
    /// </summary>
    public static string ParseBootloaderState(
        IReadOnlyDictionary<string, string> getVar,
        string unknownValue)
    {
        // fastboot melaporkan "unlocked: yes" atau "unlocked: no".
        if (getVar.TryGetValue("unlocked", out var unlocked))
        {
            return unlocked.Equals("yes", StringComparison.OrdinalIgnoreCase)
                ? "unlocked"
                : "locked";
        }

        if (getVar.TryGetValue("bootloader", out var bootloader))
        {
            return bootloader.Equals("unlocked", StringComparison.OrdinalIgnoreCase)
                ? "unlocked"
                : "locked";
        }

        return unknownValue;
    }

    /// <summary>
    /// Baca slot aktif dari hasil getvar.
    /// </summary>
    public static string ParseActiveSlot(IReadOnlyDictionary<string, string> getVar)
    {
        return ParseActiveSlot(getVar, "tidak diketahui");
    }

    /// <summary>
    /// Baca slot aktif dari hasil getvar, dengan teks untuk nilai yang tidak
    /// diketahui.
    /// </summary>
    public static string ParseActiveSlot(
        IReadOnlyDictionary<string, string> getVar,
        string unknownValue)
    {
        if (getVar.TryGetValue("current-slot", out var slot) && !string.IsNullOrWhiteSpace(slot))
        {
            return slot;
        }

        return unknownValue;
    }
}
