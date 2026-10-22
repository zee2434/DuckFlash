namespace DuckFlash.Models;

/// <summary>
/// Partisi yang boleh dipilih untuk fastboot flash. Daftar ini dibatasi demi keamanan.
/// </summary>
public static class FlashPartition
{
    /// <summary>Partisi yang diizinkan untuk flashing.</summary>
    public static readonly IReadOnlyList<string> Allowed = new[]
    {
        "recovery",
        "boot",
        "vendor_boot",
        "init_boot",
        "vbmeta",
        "dtbo"
    };

    /// <summary>True jika nama partisi termasuk daftar yang diizinkan.</summary>
    public static bool IsAllowed(string? partition)
    {
        if (string.IsNullOrWhiteSpace(partition))
        {
            return false;
        }

        return Allowed.Contains(partition.Trim(), StringComparer.OrdinalIgnoreCase);
    }
}
