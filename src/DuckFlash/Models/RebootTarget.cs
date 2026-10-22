namespace DuckFlash.Models;

/// <summary>
/// Tujuan reboot yang tersedia.
/// </summary>
public enum RebootTarget
{
    System,
    Recovery,
    Bootloader,
    Fastbootd
}

/// <summary>
/// Pemetaan nilai enum ke argumen command dan label bahasa Indonesia.
/// </summary>
public static class RebootTargetInfo
{
    /// <summary>Argumen yang dikirim ke adb atau fastboot.</summary>
    public static string ToArgument(this RebootTarget target)
    {
        return target switch
        {
            RebootTarget.System => "system",
            RebootTarget.Recovery => "recovery",
            RebootTarget.Bootloader => "bootloader",
            RebootTarget.Fastbootd => "fastbootd",
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Target reboot tidak dikenal.")
        };
    }

    /// <summary>Label yang ditampilkan ke pengguna.</summary>
    public static string ToLabel(this RebootTarget target)
    {
        return target switch
        {
            RebootTarget.System => "Reboot System",
            RebootTarget.Recovery => "Reboot Recovery",
            RebootTarget.Bootloader => "Reboot Bootloader",
            RebootTarget.Fastbootd => "Reboot Fastbootd",
            _ => target.ToString()
        };
    }

    /// <summary>
    /// Kunci teks untuk label reboot, supaya labelnya bisa mengikuti bahasa
    /// yang dipilih pengguna.
    /// </summary>
    public static string ToLabelKey(this RebootTarget target)
    {
        return target switch
        {
            RebootTarget.System => "button.rebootSystem",
            RebootTarget.Recovery => "button.rebootRecovery",
            RebootTarget.Bootloader => "button.rebootBootloader",
            RebootTarget.Fastbootd => "button.rebootFastbootd",
            _ => "button.rebootSystem"
        };
    }
}
