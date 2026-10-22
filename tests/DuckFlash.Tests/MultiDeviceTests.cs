using DuckFlash.Models;
using DuckFlash.Services;
using Xunit;

namespace DuckFlash.Tests;

/// <summary>
/// Uji dukungan multi-device: setiap command harus memakai flag -s
/// beserta serial perangkat yang dipilih pengguna.
/// </summary>
public class MultiDeviceTests
{
    [Fact]
    public async Task AdbService_WithSerial_AddsDashSArgument()
    {
        var runner = new FakeProcessRunner();
        var service = new AdbService(runner, new FakeToolPaths())
        {
            DeviceSerial = "SERIAL123"
        };

        await service.ExecuteAsync(new[] { "shell", "getprop" });

        Assert.Equal(new[] { "-s", "SERIAL123", "shell", "getprop" }, runner.LastArguments);
    }

    [Fact]
    public async Task AdbService_WithoutSerial_OmitsDashSArgument()
    {
        var runner = new FakeProcessRunner();
        var service = new AdbService(runner, new FakeToolPaths());

        await service.ExecuteAsync(new[] { "shell", "getprop" });

        Assert.Equal(new[] { "shell", "getprop" }, runner.LastArguments);
    }

    [Fact]
    public async Task AdbService_ExecuteForSerial_AlwaysAddsDashSArgument()
    {
        var runner = new FakeProcessRunner();
        var service = new AdbService(runner, new FakeToolPaths());

        await service.ExecuteForSerialAsync("OTHER", new[] { "shell", "getprop" });

        Assert.Equal(new[] { "-s", "OTHER", "shell", "getprop" }, runner.LastArguments);
    }

    [Fact]
    public async Task AdbService_ExecuteGlobal_OmitsDashSArgument()
    {
        var runner = new FakeProcessRunner();
        var service = new AdbService(runner, new FakeToolPaths())
        {
            DeviceSerial = "SERIAL123"
        };

        await service.ExecuteGlobalAsync(new[] { "devices" });

        Assert.Equal(new[] { "devices" }, runner.LastArguments);
    }

    [Fact]
    public async Task FastbootService_WithSerial_AddsDashSArgument()
    {
        var runner = new FakeProcessRunner();
        var service = new FastbootService(runner, new FakeToolPaths())
        {
            DeviceSerial = "SERIAL123"
        };

        await service.ExecuteAsync(new[] { "flash", "recovery", "C:\\image.img" });

        Assert.Equal(new[] { "-s", "SERIAL123", "flash", "recovery", "C:\\image.img" }, runner.LastArguments);
    }

    [Fact]
    public async Task FastbootService_ExecuteForSerial_AlwaysAddsDashSArgument()
    {
        var runner = new FakeProcessRunner();
        var service = new FastbootService(runner, new FakeToolPaths());

        await service.ExecuteForSerialAsync("OTHER", new[] { "getvar", "all" });

        Assert.Equal(new[] { "-s", "OTHER", "getvar", "all" }, runner.LastArguments);
    }

    [Fact]
    public async Task FastbootService_ExecuteGlobal_OmitsDashSArgument()
    {
        var runner = new FakeProcessRunner();
        var service = new FastbootService(runner, new FakeToolPaths())
        {
            DeviceSerial = "SERIAL123"
        };

        await service.ExecuteGlobalAsync(new[] { "devices" });

        Assert.Equal(new[] { "devices" }, runner.LastArguments);
    }

    [Fact]
    public async Task DeviceCheckService_GetAdbDeviceDetails_UsesSerialFlag()
    {
        var runner = new FakeProcessRunner(new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "[ro.product.model]: [Pixel 7]\n"
        });

        var service = new DeviceCheckService(
            new AdbService(runner, new FakeToolPaths()),
            new FastbootService(runner, new FakeToolPaths()),
            new FakeToolPaths());

        var details = await service.GetAdbDeviceDetailsAsync("SERIAL123");

        Assert.Equal(new[] { "-s", "SERIAL123", "shell", "getprop" }, runner.LastArguments);
        Assert.Equal("Pixel 7", details.Model);
    }

    [Fact]
    public async Task DeviceCheckService_GetFastbootDeviceDetails_UsesSerialFlag()
    {
        var runner = new FakeProcessRunner(new CommandResult
        {
            ExitCode = 0,
            StandardError = "product: panther\nunlocked: yes\ncurrent-slot: a\n"
        });

        var service = new DeviceCheckService(
            new AdbService(runner, new FakeToolPaths()),
            new FastbootService(runner, new FakeToolPaths()),
            new FakeToolPaths());

        var details = await service.GetFastbootDeviceDetailsAsync("SERIAL123");

        Assert.Equal(new[] { "-s", "SERIAL123", "getvar", "all" }, runner.LastArguments);
        Assert.Equal("unlocked", details.BootloaderState);
    }

    [Fact]
    public async Task DeviceCheckService_CheckAdb_DoesNotUseSerialFlag()
    {
        var runner = new FakeProcessRunner(new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "List of devices attached\nABC123\tdevice\n"
        });

        var service = new DeviceCheckService(
            new AdbService(runner, new FakeToolPaths()) { DeviceSerial = "ABC123" },
            new FastbootService(runner, new FakeToolPaths()) { DeviceSerial = "ABC123" },
            new FakeToolPaths());

        await service.CheckAdbAsync();

        // "devices" harus tetap tanpa -s supaya semua perangkat terlihat.
        Assert.Equal(new[] { "devices" }, runner.LastArguments);
    }
}
