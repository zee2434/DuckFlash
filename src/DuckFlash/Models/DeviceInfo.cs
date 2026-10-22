namespace DuckFlash.Models;

/// <summary>
/// Informasi dasar perangkat yang terdeteksi melalui adb atau fastboot.
/// </summary>
public sealed class DeviceInfo
{
    /// <summary>Serial number perangkat, dipakai untuk flag -s pada multi-device.</summary>
    public string Serial { get; init; } = string.Empty;

    /// <summary>Status koneksi: device, unauthorized, offline, fastboot, atau kosong.</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Model perangkat, contoh: Pixel 7.</summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>Codename perangkat, contoh: panther.</summary>
    public string Codename { get; init; } = string.Empty;

    /// <summary>Versi Android, contoh: 14.</summary>
    public string AndroidVersion { get; init; } = string.Empty;

    /// <summary>Status bootloader: locked, unlocked, atau tidak diketahui.</summary>
    public string BootloaderState { get; init; } = string.Empty;

    /// <summary>Slot aktif untuk perangkat A/B, contoh: a atau b.</summary>
    public string ActiveSlot { get; init; } = string.Empty;

    /// <summary>True jika perangkat terdeteksi lewat adb.</summary>
    public bool IsAdb { get; init; }

    /// <summary>True jika perangkat terdeteksi lewat fastboot.</summary>
    public bool IsFastboot { get; init; }
}
