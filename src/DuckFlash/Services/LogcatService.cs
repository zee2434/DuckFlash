using System.Text;
using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Ekspor logcat perangkat ke file teks, dan bersihkan buffer logcat.
///
/// Logcat sering dipakai untuk cari penyebab aplikasi crash atau booting
/// yang gagal, jadi file hasil ekspor dikirim ke komputer agar bisa dibuka
/// di editor teks apa pun.
/// </summary>
public sealed class LogcatService
{
    private readonly AdbService _adbService;
    private readonly ITextProvider _text;

    public LogcatService(AdbService adbService)
        : this(adbService, LocalizationService.Default)
    {
    }

    public LogcatService(AdbService adbService, ITextProvider text)
    {
        _adbService = adbService;
        _text = text;
    }

    /// <summary>
    /// Tulis logcat perangkat ke <paramref name="destinationPath"/>.
    /// File hanya ditulis kalau pembacaan logcat berhasil.
    /// </summary>
    public async Task<CommandResult> ExportAsync(
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArgumentException(
                _text.Get("validate.fileMissing"),
                nameof(destinationPath));
        }

        // -d = dump: keluarkan log yang tersimpan lalu berhenti, bukan mode live.
        var result = await _adbService
            .ExecuteAsync(new[] { "logcat", "-d", "-v", "threadtime" }, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            return result;
        }

        var fullPath = Path.GetFullPath(destinationPath);
        var folder = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(folder))
        {
            Directory.CreateDirectory(folder);
        }

        await File.WriteAllTextAsync(
            fullPath,
            result.StandardOutput,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)).ConfigureAwait(false);

        return result;
    }

    /// <summary>
    /// Bersihkan buffer logcat di perangkat supaya log baru mulai dari nol.
    /// </summary>
    public async Task<CommandResult> ClearAsync(CancellationToken cancellationToken = default)
    {
        return await _adbService
            .ExecuteAsync(new[] { "logcat", "-c" }, cancellationToken)
            .ConfigureAwait(false);
    }
}
