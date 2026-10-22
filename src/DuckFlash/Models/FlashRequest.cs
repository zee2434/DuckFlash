namespace DuckFlash.Models;

/// <summary>
/// Permintaan flash yang sudah lolos validasi, siap dikirim ke fastboot.
/// </summary>
public sealed class FlashRequest
{
    /// <summary>Partisi tujuan, harus termasuk daftar yang diizinkan.</summary>
    public string Partition { get; init; } = string.Empty;

    /// <summary>Path penuh file .img.</summary>
    public string ImagePath { get; init; } = string.Empty;

    /// <summary>Nama file untuk ditampilkan di dialog konfirmasi.</summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>Status bootloader perangkat: locked, unlocked, atau tidak diketahui.</summary>
    public string BootloaderState { get; init; } = string.Empty;

    /// <summary>Serial perangkat, untuk mode multi-device.</summary>
    public string? DeviceSerial { get; init; }

    /// <summary>Hash SHA256 file, ditampilkan di dialog konfirmasi.</summary>
    public string Sha256 { get; init; } = string.Empty;

    /// <summary>Ukuran file dalam byte.</summary>
    public long FileSize { get; init; }
}
