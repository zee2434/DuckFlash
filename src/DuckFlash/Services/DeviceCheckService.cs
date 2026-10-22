using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Memeriksa perangkat yang terhubung lewat adb atau fastboot, lalu diambil
/// detailnya (model, codename, versi Android, status bootloader, slot).
/// </summary>
public sealed class DeviceCheckService
{
    private readonly AdbService _adbService;
    private readonly FastbootService _fastbootService;
    private readonly IToolPaths _toolPaths;
    private readonly ITextProvider _text;

    public DeviceCheckService(
        AdbService adbService,
        FastbootService fastbootService,
        IToolPaths toolPaths)
        : this(adbService, fastbootService, toolPaths, LocalizationService.Default)
    {
    }

    public DeviceCheckService(
        AdbService adbService,
        FastbootService fastbootService,
        IToolPaths toolPaths,
        ITextProvider text)
    {
        _adbService = adbService;
        _fastbootService = fastbootService;
        _toolPaths = toolPaths;
        _text = text;
    }

    /// <summary>
    /// Jalankan <c>adb devices</c> dan laporkan statusnya dalam bahasa aktif.
    /// </summary>
    public async Task<DeviceCheckResult> CheckAdbAsync(CancellationToken cancellationToken = default)
    {
        if (_toolPaths.AdbPath is null)
        {
            return new DeviceCheckResult
            {
                Status = DeviceCheckStatus.ToolsMissing,
                Message = _text.Get("device.adbToolsMissing")
            };
        }

        CommandResult result;

        try
        {
            // "devices" berlaku ke semua perangkat, jadi tanpa flag -s.
            result = await _adbService.ExecuteGlobalAsync(new[] { "devices" }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new DeviceCheckResult
            {
                Status = DeviceCheckStatus.Error,
                Message = _text.Get("device.adbFailed", ex.Message)
            };
        }

        var devices = DeviceOutputParser.ParseAdbDevices(result.StandardOutput);
        var combined = result.CombinedOutput;

        // adb menulis pesan error ke stderr.
        if (combined.Contains("no permissions", StringComparison.OrdinalIgnoreCase))
        {
            return new DeviceCheckResult
            {
                Status = DeviceCheckStatus.Error,
                Message = _text.Get("device.adbNoPermission")
            };
        }

        if (combined.Contains("waiting for any device", StringComparison.OrdinalIgnoreCase)
            || combined.Contains("no devices/emulators found", StringComparison.OrdinalIgnoreCase))
        {
            return new DeviceCheckResult
            {
                Status = DeviceCheckStatus.NoDevice,
                Message = _text.Get("device.adbNoDevice")
            };
        }

        if (devices.Count == 0)
        {
            return new DeviceCheckResult
            {
                Status = DeviceCheckStatus.NoDevice,
                Message = _text.Get("device.adbNoDevice")
            };
        }

        var unauthorized = devices.FirstOrDefault(device =>
            device.Status.Equals("unauthorized", StringComparison.OrdinalIgnoreCase));

        if (unauthorized is not null)
        {
            return new DeviceCheckResult
            {
                Status = DeviceCheckStatus.Unauthorized,
                Message = _text.Get("device.adbUnauthorized"),
                Devices = devices
            };
        }

        var offline = devices.FirstOrDefault(device =>
            device.Status.Equals("offline", StringComparison.OrdinalIgnoreCase));

        if (offline is not null)
        {
            return new DeviceCheckResult
            {
                Status = DeviceCheckStatus.Offline,
                Message = _text.Get("device.adbOffline"),
                Devices = devices
            };
        }

        var usable = devices.Where(device =>
            device.Status.Equals("device", StringComparison.OrdinalIgnoreCase)).ToList();

        if (usable.Count == 0)
        {
            return new DeviceCheckResult
            {
                Status = DeviceCheckStatus.NoDevice,
                Message = _text.Get("device.adbNoUsableDevice"),
                Devices = devices
            };
        }

        var message = usable.Count == 1
            ? _text.Get("device.adbDetected", usable[0].Serial)
            : _text.Get("device.adbDetectedMany", usable.Count);

        return new DeviceCheckResult
        {
            Status = DeviceCheckStatus.Detected,
            Message = message,
            Devices = devices
        };
    }

    /// <summary>
    /// Jalankan <c>fastboot devices</c> dan laporkan statusnya dalam bahasa aktif.
    /// </summary>
    public async Task<DeviceCheckResult> CheckFastbootAsync(CancellationToken cancellationToken = default)
    {
        if (_toolPaths.FastbootPath is null)
        {
            return new DeviceCheckResult
            {
                Status = DeviceCheckStatus.ToolsMissing,
                Message = _text.Get("device.fastbootToolsMissing")
            };
        }

        CommandResult result;

        try
        {
            // "devices" berlaku ke semua perangkat, jadi tanpa flag -s.
            result = await _fastbootService.ExecuteGlobalAsync(new[] { "devices" }, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new DeviceCheckResult
            {
                Status = DeviceCheckStatus.Error,
                Message = _text.Get("device.fastbootFailed", ex.Message)
            };
        }

        var devices = DeviceOutputParser.ParseFastbootDevices(result.StandardOutput);
        var combined = result.CombinedOutput;

        if (combined.Contains("no permissions", StringComparison.OrdinalIgnoreCase))
        {
            return new DeviceCheckResult
            {
                Status = DeviceCheckStatus.Error,
                Message = _text.Get("device.fastbootNoPermission")
            };
        }

        if (devices.Count == 0)
        {
            return new DeviceCheckResult
            {
                Status = DeviceCheckStatus.NoDevice,
                Message = _text.Get("device.fastbootNoDevice")
            };
        }

        var message = devices.Count == 1
            ? _text.Get("device.fastbootDetected", devices[0].Serial)
            : _text.Get("device.fastbootDetectedMany", devices.Count);

        return new DeviceCheckResult
        {
            Status = DeviceCheckStatus.Detected,
            Message = message,
            Devices = devices
        };
    }

    /// <summary>
    /// Ambil detail perangkat yang terhubung lewat adb: model, codename, versi Android.
    /// </summary>
    public async Task<DeviceInfo> GetAdbDeviceDetailsAsync(
        string serial,
        CancellationToken cancellationToken = default)
    {
        var result = await _adbService
            .ExecuteForSerialAsync(serial, new[] { "shell", "getprop" }, cancellationToken)
            .ConfigureAwait(false);

        var properties = DeviceOutputParser.ParseGetProp(result.StandardOutput);
        var unknown = _text.Get("value.unknown");

        return new DeviceInfo
        {
            Serial = serial,
            Status = "device",
            IsAdb = true,
            Model = properties.GetValueOrDefault("ro.product.model", unknown),
            Codename = properties.GetValueOrDefault("ro.product.device", unknown),
            AndroidVersion = properties.GetValueOrDefault("ro.build.version.release", unknown)
        };
    }

    /// <summary>
    /// Ambil detail perangkat yang terhubung lewat fastboot: status bootloader dan slot.
    /// </summary>
    public async Task<DeviceInfo> GetFastbootDeviceDetailsAsync(
        string serial,
        CancellationToken cancellationToken = default)
    {
        var result = await _fastbootService
            .ExecuteForSerialAsync(serial, new[] { "getvar", "all" }, cancellationToken)
            .ConfigureAwait(false);

        // fastboot menulis getvar ke stderr, jadi digabung dua-duanya.
        var variables = DeviceOutputParser.ParseGetVar(result.CombinedOutput);
        var unknown = _text.Get("value.unknown");

        return new DeviceInfo
        {
            Serial = serial,
            Status = "fastboot",
            IsFastboot = true,
            Codename = variables.GetValueOrDefault("product", unknown),
            BootloaderState = DeviceOutputParser.ParseBootloaderState(variables, unknown),
            ActiveSlot = DeviceOutputParser.ParseActiveSlot(variables, unknown)
        };
    }
}
