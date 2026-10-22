using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Menjalankan aksi unlock/lock bootloader lewat fastboot.
/// </summary>
public sealed class BootloaderService
{
    private readonly FastbootService _fastbootService;
    private readonly ITextProvider _text;

    public BootloaderService(FastbootService fastbootService)
        : this(fastbootService, LocalizationService.Default)
    {
    }

    public BootloaderService(FastbootService fastbootService, ITextProvider text)
    {
        _fastbootService = fastbootService;
        _text = text;
    }

    /// <summary>
    /// Periksa apakah aksi bootloader boleh dijalankan. Mengembalikan null kalau
    /// aman, atau pesan alasan dalam bahasa aktif kalau tidak aman.
    /// </summary>
    /// <param name="action">Aksi yang mau dijalankan.</param>
    /// <param name="bootloaderState">Status bootloader saat ini.</param>
    /// <param name="fastbootDetected">True kalau perangkat terdeteksi dalam mode fastboot.</param>
    public string? ValidateAction(BootloaderAction action, string bootloaderState, bool fastbootDetected = true)
    {
        // Aksi bootloader hanya bisa jalan kalau perangkat dalam mode fastboot.
        if (!fastbootDetected)
        {
            return _text.Get("bootloader.notInFastboot");
        }

        // Jangan buka kunci bootloader yang sudah terbuka.
        if (action == BootloaderAction.Unlock
            && bootloaderState.Equals("unlocked", StringComparison.OrdinalIgnoreCase))
        {
            return _text.Get("bootloader.alreadyUnlocked");
        }

        // Jangan kunci bootloader yang masih terkunci.
        if (action == BootloaderAction.Lock
            && bootloaderState.Equals("locked", StringComparison.OrdinalIgnoreCase))
        {
            return _text.Get("bootloader.alreadyLocked");
        }

        return null;
    }

    /// <summary>
    /// Jalankan aksi bootloader. Pemanggil WAJIB memvalidasi lebih dulu dan
    /// menampilkan konfirmasi dengan peringatan data terhapus.
    /// </summary>
    public async Task<CommandResult> RunAsync(
        BootloaderAction action,
        CancellationToken cancellationToken = default)
    {
        var arguments = action.ToArguments();

        return await _fastbootService.ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Susun teks konfirmasi untuk aksi bootloader. Peringatan penghapusan data
    /// ditulis besar dan jelas, sesuai aturan anti-brick.
    /// </summary>
    public static string BuildConfirmationText(BootloaderAction action, string? deviceSerial)
    {
        return BuildConfirmationText(action, deviceSerial, LocalizationService.Default);
    }

    /// <summary>
    /// Susun teks konfirmasi sesuai bahasa aktif. Peringatan penghapusan data
    /// ditulis besar dan jelas, sesuai aturan anti-brick.
    /// </summary>
    public static string BuildConfirmationText(
        BootloaderAction action,
        string? deviceSerial,
        ITextProvider text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var target = string.IsNullOrWhiteSpace(deviceSerial)
            ? text.Get("value.yourDevice")
            : deviceSerial;

        var key = action == BootloaderAction.Unlock
            ? "confirm.bootloaderUnlock"
            : "confirm.bootloaderLock";

        return text.Get(key, target);
    }

    /// <summary>
    /// Label persetujuan yang harus dicentang pengguna di dialog konfirmasi.
    /// </summary>
    public static string BuildAcknowledgementLabel(BootloaderAction action)
    {
        return BuildAcknowledgementLabel(action, LocalizationService.Default);
    }

    /// <summary>
    /// Label persetujuan sesuai bahasa aktif.
    /// </summary>
    public static string BuildAcknowledgementLabel(BootloaderAction action, ITextProvider text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Get("ack.bootloader");
    }
}
