namespace DuckFlash.Models;

/// <summary>
/// Status hasil pemeriksaan perangkat, dipakai untuk menampilkan kondisi yang jelas
/// ke pengguna: terdeteksi, tidak ada, unauthorized, dan seterusnya.
/// </summary>
public enum DeviceCheckStatus
{
    /// <summary>Belum pernah diperiksa.</summary>
    NotChecked,

    /// <summary>Platform-tools belum terpasang, jadi tidak bisa diperiksa.</summary>
    ToolsMissing,

    /// <summary>Tidak ada perangkat yang terdeteksi.</summary>
    NoDevice,

    /// <summary>Perangkat terdeteksi tapi belum diizinkan (unauthorized).</summary>
    Unauthorized,

    /// <summary>Perangkat terdeteksi tapi statusnya offline.</summary>
    Offline,

    /// <summary>Perangkat terdeteksi dan siap dipakai.</summary>
    Detected,

    /// <summary>Pemeriksaan gagal karena ada error lain.</summary>
    Error
}

/// <summary>
/// Hasil pemeriksaan perangkat lewat adb atau fastboot.
/// </summary>
public sealed class DeviceCheckResult
{
    /// <summary>Status hasil pemeriksaan.</summary>
    public DeviceCheckStatus Status { get; init; } = DeviceCheckStatus.NotChecked;

    /// <summary>Penjelasan singkat dalam bahasa Indonesia untuk ditampilkan di UI.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>Daftar perangkat yang terdeteksi.</summary>
    public IReadOnlyList<DeviceInfo> Devices { get; init; } = Array.Empty<DeviceInfo>();

    /// <summary>True kalau ada minimal satu perangkat yang siap dipakai.</summary>
    public bool HasUsableDevice => Status == DeviceCheckStatus.Detected && Devices.Count > 0;
}
