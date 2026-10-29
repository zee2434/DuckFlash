using DuckFlash.Models;
using DuckFlash.Services;
using Xunit;

namespace DuckFlash.Tests;

public class RebootServiceTests
{
    [Theory]
    [InlineData(RebootTarget.System, "reboot")]
    [InlineData(RebootTarget.Recovery, "reboot recovery")]
    [InlineData(RebootTarget.Bootloader, "reboot bootloader")]
    [InlineData(RebootTarget.Fastbootd, "reboot fastboot")]
    public async Task RebootAsync_FastbootMode_UsesFastbootCommand(
        RebootTarget target,
        string expectedArguments)
    {
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });
        var toolPaths = new FakeToolPaths();
        var adb = new AdbService(runner, toolPaths);
        var fastboot = new FastbootService(runner, toolPaths)
        {
            DeviceSerial = "SERIAL123"
        };
        var service = new RebootService(adb, fastboot);

        await service.RebootAsync(target, isFastboot: true);

        Assert.Equal(toolPaths.FastbootPath, runner.LastFileName);
        Assert.Equal(
            new[] { "-s", "SERIAL123" }.Concat(expectedArguments.Split(' ')),
            runner.LastArguments);
    }

    [Fact]
    public async Task RebootAsync_AdbMode_KeepsAdbCommand()
    {
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });
        var toolPaths = new FakeToolPaths();
        var adb = new AdbService(runner, toolPaths)
        {
            DeviceSerial = "SERIAL123"
        };
        var fastboot = new FastbootService(runner, toolPaths);
        var service = new RebootService(adb, fastboot);

        await service.RebootAsync(RebootTarget.Recovery);

        Assert.Equal(toolPaths.AdbPath, runner.LastFileName);
        Assert.Equal(new[] { "-s", "SERIAL123", "reboot", "recovery" }, runner.LastArguments);
    }
}
