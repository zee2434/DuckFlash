namespace DuckFlash.Models;

/// <summary>
/// Aksi pada bootloader yang bisa dilakukan lewat fastboot.
/// </summary>
public enum BootloaderAction
{
    /// <summary>Membuka kunci bootloader (fastboot flashing unlock).</summary>
    Unlock,

    /// <summary>Mengunci kembali bootloader (fastboot flashing lock).</summary>
    Lock
}

/// <summary>
/// Pemetaan nilai enum ke argumen command dan label bahasa Indonesia.
/// </summary>
public static class BootloaderActionInfo
{
    /// <summary>
    /// Argumen yang dikirim ke fastboot. Fastboot memakai dua argumen terpisah:
    /// "flashing unlock" atau "flashing lock".
    /// </summary>
    public static string[] ToArguments(this BootloaderAction action)
    {
        return action switch
        {
            BootloaderAction.Unlock => new[] { "flashing", "unlock" },
            BootloaderAction.Lock => new[] { "flashing", "lock" },
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Aksi bootloader tidak dikenal.")
        };
    }

    /// <summary>Label yang ditampilkan ke pengguna.</summary>
    public static string ToLabel(this BootloaderAction action)
    {
        return action switch
        {
            BootloaderAction.Unlock => "Unlock Bootloader",
            BootloaderAction.Lock => "Lock Bootloader",
            _ => action.ToString()
        };
    }

    /// <summary>
    /// Kunci teks untuk label aksi, supaya labelnya bisa mengikuti bahasa
    /// yang dipilih pengguna.
    /// </summary>
    public static string ToLabelKey(this BootloaderAction action)
    {
        return action switch
        {
            BootloaderAction.Unlock => "button.unlockBootloader",
            BootloaderAction.Lock => "button.lockBootloader",
            _ => "button.unlockBootloader"
        };
    }

    /// <summary>
    /// True kalau aksi ini menghapus semua data di perangkat.
    /// </summary>
    public static bool WipesData(this BootloaderAction action)
    {
        return action == BootloaderAction.Unlock;
    }
}
