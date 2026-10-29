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
    /// Reboot perangkat melalui tool yang sesuai dengan mode koneksinya.
    /// </summary>
    public async Task<CommandResult> RebootAsync(
        RebootTarget target,
        CancellationToken cancellationToken = default,
        bool isFastboot = false)
    {
        var argument = target.ToArgument();

        if (isFastboot)
        {
            var arguments = target switch
            {
                RebootTarget.System => new[] { "reboot" },
                RebootTarget.Recovery => new[] { "reboot", argument },
                RebootTarget.Bootloader => new[] { "reboot", argument },
                RebootTarget.Fastbootd => new[] { "reboot", "fastboot" },
                _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Target reboot tidak dikenal.")
            };

            return await _fastbootService
                .ExecuteAsync(arguments, cancellationToken)
                .ConfigureAwait(false);
        }

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
