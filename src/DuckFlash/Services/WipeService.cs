using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Menghapus data pengguna di perangkat lewat <c>fastboot -w</c>.
///
/// Ini aksi destruktif: semua data pengguna (aplikasi, foto, kontak) akan
/// hilang, jadi pemanggil WAJIB menampilkan konfirmasi dengan peringatan
/// besar sebelum memanggil <see cref="WipeAsync"/>.
/// </summary>
public sealed class WipeService
{
    private readonly FastbootService _fastbootService;
    private readonly ITextProvider _text;

    public WipeService(FastbootService fastbootService)
        : this(fastbootService, LocalizationService.Default)
    {
    }

    public WipeService(FastbootService fastbootService, ITextProvider text)
    {
        _fastbootService = fastbootService;
        _text = text;
    }

    /// <summary>
    /// Periksa apakah wipe boleh dijalankan. Mengembalikan null kalau aman,
    /// atau pesan alasan dalam bahasa aktif kalau tidak aman.
    /// </summary>
    /// <param name="fastbootDetected">True kalau perangkat terdeteksi dalam mode fastboot.</param>
    public string? Validate(bool fastbootDetected)
    {
        // Wipe hanya bisa jalan di mode fastboot, jadi mode lain diblokir.
        if (!fastbootDetected)
        {
            return _text.Get("bootloader.notInFastboot");
        }

        return null;
    }

    /// <summary>
    /// Jalankan wipe data. Pemanggil WAJIB memvalidasi lebih dulu dan
    /// menampilkan konfirmasi dengan peringatan data terhapus.
    /// </summary>
    public async Task<CommandResult> WipeAsync(
        string? deviceSerial = null,
        CancellationToken cancellationToken = default)
    {
        // Serial dari pemanggil adalah acuan, supaya wipe selalu mengarah ke
        // perangkat yang memang dipilih pengguna.
        if (!string.IsNullOrWhiteSpace(deviceSerial))
        {
            _fastbootService.DeviceSerial = deviceSerial;
        }

        return await _fastbootService
            .ExecuteAsync(new[] { "-w" }, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Susun teks konfirmasi untuk wipe. Peringatan penghapusan data ditulis
    /// besar dan jelas, sesuai aturan anti-brick.
    /// </summary>
    public static string BuildConfirmationText(string? deviceSerial)
    {
        return BuildConfirmationText(deviceSerial, LocalizationService.Default);
    }

    /// <summary>
    /// Susun teks konfirmasi sesuai bahasa aktif.
    /// </summary>
    public static string BuildConfirmationText(string? deviceSerial, ITextProvider text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var target = string.IsNullOrWhiteSpace(deviceSerial)
            ? text.Get("value.yourDevice")
            : deviceSerial;

        return text.Get("confirm.wipe", target);
    }

    /// <summary>
    /// Label persetujuan yang harus dicentang pengguna di dialog konfirmasi.
    /// </summary>
    public static string BuildAcknowledgementLabel()
    {
        return BuildAcknowledgementLabel(LocalizationService.Default);
    }

    /// <summary>
    /// Label persetujuan sesuai bahasa aktif.
    /// </summary>
    public static string BuildAcknowledgementLabel(ITextProvider text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Get("ack.wipe");
    }
}
