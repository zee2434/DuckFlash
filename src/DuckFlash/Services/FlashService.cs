using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Mengirim file .img ke partisi yang dipilih memakai fastboot flash.
/// Semua aturan anti-brick dicek sebelum command dikirim.
/// </summary>
public sealed class FlashService
{
    private readonly FastbootService _fastbootService;
    private readonly ITextProvider _text;

    public FlashService(FastbootService fastbootService)
        : this(fastbootService, LocalizationService.Default)
    {
    }

    public FlashService(FastbootService fastbootService, ITextProvider text)
    {
        _fastbootService = fastbootService;
        _text = text;
    }

    /// <summary>
    /// Periksa apakah flash boleh dijalankan. Mengembalikan null kalau aman,
    /// atau pesan alasan dalam bahasa aktif kalau tidak aman.
    /// </summary>
    public string? ValidateRequest(FlashRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Aturan 1: partisi harus dari daftar terbatas.
        if (!FlashPartition.IsAllowed(request.Partition))
        {
            return _text.Get("validate.partitionNotAllowed", request.Partition);
        }

        // Aturan 2: bootloader harus tidak terkunci.
        if (request.BootloaderState.Equals("locked", StringComparison.OrdinalIgnoreCase))
        {
            return _text.Get("validate.bootloaderLocked");
        }

        // Aturan 3: file harus ada dan valid.
        if (!File.Exists(request.ImagePath))
        {
            return _text.Get("validate.fileNotFound", request.FileName);
        }

        var validation = FileValidator.Validate(
            request.ImagePath,
            FileValidator.FlashExtension,
            requireZipContent: false,
            _text);

        if (!validation.IsValid)
        {
            return validation.ErrorMessage;
        }

        return null;
    }

    /// <summary>
    /// Jalankan fastboot flash. Pemanggil WAJIB memanggil
    /// <see cref="ValidateRequest"/> lebih dulu dan menampilkan konfirmasi ke pengguna.
    /// </summary>
    public async Task<CommandResult> FlashAsync(
        FlashRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Serial dari request adalah acuan, supaya flash selalu mengarah ke
        // perangkat yang memang dipilih pengguna.
        if (!string.IsNullOrWhiteSpace(request.DeviceSerial))
        {
            _fastbootService.DeviceSerial = request.DeviceSerial;
        }

        var arguments = new List<string> { "flash", request.Partition, request.ImagePath };

        return await _fastbootService.ExecuteAsync(arguments, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Susun teks konfirmasi untuk dialog, nama partisi dan nama file ditulis jelas.
    /// </summary>
    public static string BuildConfirmationText(FlashRequest request)
    {
        return BuildConfirmationText(request, LocalizationService.Default);
    }

    /// <summary>
    /// Susun teks konfirmasi untuk dialog sesuai bahasa aktif.
    /// </summary>
    public static string BuildConfirmationText(FlashRequest request, ITextProvider text)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(text);

        return text.Get(
            "confirm.flash",
            request.Partition,
            request.FileName,
            FormatSize(request.FileSize),
            request.Sha256);
    }

    /// <summary>
    /// Format ukuran file dalam bahasa Indonesia.
    /// </summary>
    public static string FormatSize(long bytes)
    {
        if (bytes >= 1024 * 1024 * 1024)
        {
            return $"{bytes / (1024d * 1024d * 1024d):0.00} GB";
        }

        if (bytes >= 1024 * 1024)
        {
            return $"{bytes / (1024d * 1024d):0.00} MB";
        }

        if (bytes >= 1024)
        {
            return $"{bytes / 1024d:0.00} KB";
        }

        return $"{bytes} byte";
    }
}
