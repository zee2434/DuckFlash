using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Menjalankan perintah adb install lewat drag and drop.
/// </summary>
public sealed class InstallService
{
    private readonly AdbService _adbService;

    public InstallService(AdbService adbService)
    {
        _adbService = adbService;
    }

    /// <summary>
    /// Jalankan perintah adb install, tetapi abaikan error kalau sudah pernah
    /// terinstal (perintahnya masih ekros OK).
    /// </summary>
    public async Task<CommandResult> InstallAsync(
        string packagePath,
        CancellationToken cancellationToken = default)
    {
        return await _adbService
            .ExecuteAsync(new[] { "install", packagePath }, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Susun teks konfirmasi dan persetujuan buat dialog konfirmasi.
    /// </summary>
    public static string BuildConfirmationText(string fileName)
    {
        return BuildConfirmationText(fileName, LocalizationService.Default);
    }

    /// <summary>
    /// Susun teks konfirmasi sesuai bahasa aktif.
    /// </summary>
    public static string BuildConfirmationText(string fileName, ITextProvider text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Get("confirm.install", fileName);
    }

    /// <summary>
    /// Label persetujuan di dialog konfirmasi untuk aksi ini.
    /// </summary>
    public static string BuildAcknowledgementLabel()
    {
        return BuildAcknowledgementLabel(LocalizationService.Default);
    }

    /// <summary>
    /// Label persetujuan di dialog konfirmasi sesuai bahasa aktif.
    /// </summary>
    public static string BuildAcknowledgementLabel(ITextProvider text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text.Get("ack.install");
    }
}
