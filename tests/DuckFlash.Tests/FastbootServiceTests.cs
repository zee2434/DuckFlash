using DuckFlash.Models;
using DuckFlash.Services;
using Xunit;

namespace DuckFlash.Tests;

public class FastbootServiceTests
{
    [Fact]
    public async Task ExecuteAsync_PassesArgumentsToRunner()
    {
        var runner = new FakeProcessRunner();
        var service = new FastbootService(runner, new FakeToolPaths());

        await service.ExecuteAsync(new[] { "devices" });

        Assert.Equal(1, runner.CallCount);
        Assert.Equal(new[] { "devices" }, runner.LastArguments);
    }

    [Fact]
    public async Task ExecuteAsync_UsesFullPathToFastboot()
    {
        var runner = new FakeProcessRunner();
        var paths = new FakeToolPaths { FastbootPath = @"C:\Tools\platform-tools\fastboot.exe" };
        var service = new FastbootService(runner, paths);

        await service.ExecuteAsync(new[] { "devices" });

        Assert.Equal(@"C:\Tools\platform-tools\fastboot.exe", runner.LastFileName);
    }

    [Fact]
    public async Task ExecuteAsync_WithSerial_AddsSerialFlagFirst()
    {
        var runner = new FakeProcessRunner();
        var service = new FastbootService(runner, new FakeToolPaths())
        {
            DeviceSerial = "XYZ789"
        };

        await service.ExecuteAsync(new[] { "getvar", "all" });

        Assert.Equal(new[] { "-s", "XYZ789", "getvar", "all" }, runner.LastArguments);
    }

    [Fact]
    public async Task ExecuteAsync_NoSerial_DoesNotAddSerialFlag()
    {
        var runner = new FakeProcessRunner();
        var service = new FastbootService(runner, new FakeToolPaths());

        await service.ExecuteAsync(new[] { "getvar", "all" });

        Assert.DoesNotContain("-s", runner.LastArguments);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCommandResultWithStderr()
    {
        // fastboot hampir selalu menulis output ke stderr.
        var expected = new CommandResult
        {
            ExitCode = 1,
            StandardError = "FAILED (remote: 'unknown command')"
        };

        var runner = new FakeProcessRunner(expected);
        var service = new FastbootService(runner, new FakeToolPaths());

        var result = await service.ExecuteAsync(new[] { "getvar", "all" });

        Assert.False(result.IsSuccess);
        Assert.Contains("FAILED", result.StandardError);
        Assert.Contains("FAILED", result.CombinedOutput);
    }

    [Fact]
    public void FastbootPath_WhenToolsNotInstalled_Throws()
    {
        var runner = new FakeProcessRunner();
        var paths = new FakeToolPaths { FastbootPath = null };
        var service = new FastbootService(runner, paths);

        var exception = Assert.Throws<ToolsNotInstalledException>(() => service.FastbootPath);

        Assert.Contains("platform-tools belum terpasang", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenToolsNotInstalled_Throws()
    {
        var runner = new FakeProcessRunner();
        var paths = new FakeToolPaths { FastbootPath = null };
        var service = new FastbootService(runner, paths);

        await Assert.ThrowsAsync<ToolsNotInstalledException>(() =>
            service.ExecuteAsync(new[] { "devices" }));
    }

    [Fact]
    public async Task OutputReceived_ForwardsFromRunner()
    {
        var runner = new FakeProcessRunner(new CommandResult
        {
            ExitCode = 0,
            StandardError = "fastboot output"
        });

        var service = new FastbootService(runner, new FakeToolPaths());
        var lines = new List<string>();
        service.OutputReceived += (_, e) => lines.Add(e.Line);

        await service.ExecuteAsync(new[] { "devices" });

        Assert.Contains("fastboot output", lines);
    }

    [Fact]
    public async Task CommandStarting_ForwardsFromRunner()
    {
        var runner = new FakeProcessRunner();
        var service = new FastbootService(runner, new FakeToolPaths());
        string? preview = null;
        service.CommandStarting += (_, e) => preview = e.CommandLine;

        await service.ExecuteAsync(new[] { "devices" });

        Assert.Equal(@"C:\Tools\platform-tools\fastboot.exe devices", preview);
    }
}
