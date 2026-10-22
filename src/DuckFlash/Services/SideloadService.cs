using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Menjalankan adb sideload untuk memasang package zip.
/// </summary>
public sealed class SideloadService
{
    private readonly AdbService _adbService;
    private readonly ITextProvider _text;

    public SideloadService(AdbService adbService)
        : this(adbService, LocalizationService.Default)
    {
    }

    public SideloadService(AdbService adbService, ITextProvider text)
    {
        _adbService = adbService;
        _text = text;
    }

    /// <summary>
    /// Periksa apakah sideload boleh dijalankan.
    /// </summary>
    public string? ValidateRequest(string? packagePath)
    {
        var validation = FileValidator.Validate(
            packagePath,
            FileValidator.SideloadExtension,
            requireZipContent: true,
            _text);

        return validation.IsValid ? null : validation.ErrorMessage;
    }

    /// <summary>
    /// Jalankan adb sideload. Pemanggil WAJIB memvalidasi lebih dulu dan
    /// menampilkan konfirmasi ke pengguna.
    /// </summary>
    public async Task<CommandResult> SideloadAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        var validation = FileValidator.Validate(
            packagePath,
            FileValidator.SideloadExtension,
            requireZipContent: true,
            _text);

        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                validation.ErrorMessage ?? "File sideload tidak valid.");
        }

        return await _adbService
            .ExecuteAsync(new[] { "sideload", validation.FilePath }, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Susun teks konfirmasi untuk dialog.
    /// </summary>
    public static string BuildConfirmationText(FileValidationResult validation)
    {
        return BuildConfirmationText(validation, LocalizationService.Default);
    }

    /// <summary>
    /// Susun teks konfirmasi untuk dialog sesuai bahasa aktif.
    /// </summary>
    public static string BuildConfirmationText(FileValidationResult validation, ITextProvider text)
    {
        ArgumentNullException.ThrowIfNull(validation);
        ArgumentNullException.ThrowIfNull(text);

        return text.Get(
            "confirm.sideload",
            validation.FileName,
            FlashService.FormatSize(validation.FileSize),
            validation.Sha256);
    }
}
