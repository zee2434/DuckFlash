using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Menjalankan perintah reboot ke perangkat.
/// </summary>
public sealed class RebootService
{
    private readonly AdbService _adbService;
    private readonly FastbootService _fastbootService;

    public RebootService(AdbService adbService, FastbootService fastbootService)
    {
        _adbService = adbService;
        _fastbootService = fastbootService;
    }

    /// <summary>
    /// Reboot perangkat. Untuk Fastbootd, perintah dikirim lewat fastboot
    /// karena perangkat sedang dalam mode fastboot.
    /// </summary>
    public async Task<CommandResult> RebootAsync(
        RebootTarget target,
        CancellationToken cancellationToken = default)
    {
        var argument = target.ToArgument();

        if (target == RebootTarget.Fastbootd)
        {
            return await _fastbootService
                .ExecuteAsync(new[] { "reboot", argument }, cancellationToken)
                .ConfigureAwait(false);
        }

        return await _adbService
            .ExecuteAsync(new[] { "reboot", argument }, cancellationToken)
            .ConfigureAwait(false);
    }
}
