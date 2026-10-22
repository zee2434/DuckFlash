using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Ambil screenshot perangkat lewat adb.
///
/// Cara kerjanya: <c>screencap</c> menulis PNG di perangkat, lalu file itu
/// ditarik ke komputer dengan <c>adb pull</c>. Cara ini dipakai karena isi
/// PNG bersifat biner, sedangkan output proses di aplikasi ini dibaca sebagai
/// teks.
/// </summary>
public sealed class ScreenshotService
{
    /// <summary>Path file screenshot sementara di perangkat.</summary>
    public const string RemoteFilePath = "/sdcard/duckflash_screenshot.png";

    private readonly AdbService _adbService;
    private readonly ITextProvider _text;

    public ScreenshotService(AdbService adbService)
        : this(adbService, LocalizationService.Default)
    {
    }

    public ScreenshotService(AdbService adbService, ITextProvider text)
    {
        _adbService = adbService;
        _text = text;
    }

    /// <summary>
    /// Ambil screenshot dan simpan ke <paramref name="destinationPath"/>.
    /// Mengembalikan hasil command penarikan file, jadi bisa dicek apakah
    /// file benar-benar berhasil diambil.
    /// </summary>
    public async Task<CommandResult> CaptureAsync(
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException(
                _text.Get("validate.fileMissing"),
                nameof(destinationPath));
        }

        var fullPath = Path.GetFullPath(destinationPath);
        var folder = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        // 1. Tulis PNG di perangkat.
        var capture = await _adbService
            .ExecuteAsync(
                new[] { "shell", "screencap", "-p", RemoteFilePath },
                cancellationToken)
            .ConfigureAwait(false);

        if (!capture.IsSuccess)
        {
            return capture;
        }

        // 2. Tarik file ke komputer.
        var pull = await _adbService
            .ExecuteAsync(new[] { "pull", RemoteFilePath, fullPath }, cancellationToken)
            .ConfigureAwait(false);

        // 3. Bersihkan file sementara di perangkat. Kegagalan di sini tidak
        //    penting, jadi hasilnya diabaikan.
        try
        {
            await _adbService
                .ExecuteAsync(new[] { "shell", "rm", "-f", RemoteFilePath }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception)
        {
            // File sisa di perangkat tidak mengganggu pengguna.
        }

        return pull;
    }
}
