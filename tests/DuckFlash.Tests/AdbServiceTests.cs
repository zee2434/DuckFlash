using DuckFlash.Models;
using DuckFlash.Services;
using Xunit;

namespace DuckFlash.Tests;

public class AdbServiceTests
{
    [Fact]
    public async Task ExecuteAsync_PassesArgumentsToRunner()
    {
        var runner = new FakeProcessRunner();
        var service = new AdbService(runner, new FakeToolPaths());

        await service.ExecuteAsync(new[] { "devices" });

        Assert.Equal(1, runner.CallCount);
        Assert.Equal(new[] { "devices" }, runner.LastArguments);
    }

    [Fact]
    public async Task ExecuteAsync_UsesFullPathToAdb()
    {
        var runner = new FakeProcessRunner();
        var paths = new FakeToolPaths { AdbPath = @"C:\Tools\platform-tools\adb.exe" };
        var service = new AdbService(runner, paths);

        await service.ExecuteAsync(new[] { "devices" });

        Assert.Equal(@"C:\Tools\platform-tools\adb.exe", runner.LastFileName);
    }

    [Fact]
    public async Task ExecuteAsync_NoSerial_DoesNotAddSerialFlag()
    {
        var runner = new FakeProcessRunner();
        var service = new AdbService(runner, new FakeToolPaths());

        await service.ExecuteAsync(new[] { "devices" });

        Assert.DoesNotContain("-s", runner.LastArguments);
    }

    [Fact]
    public async Task ExecuteAsync_WithSerial_AddsSerialFlagFirst()
    {
        var runner = new FakeProcessRunner();
        var service = new AdbService(runner, new FakeToolPaths())
        {
            DeviceSerial = "ABC123"
        };

        await service.ExecuteAsync(new[] { "devices" });

        Assert.Equal(new[] { "-s", "ABC123", "devices" }, runner.LastArguments);
    }

    [Fact]
    public async Task ExecuteAsync_WhitespaceSerial_IsIgnored()
    {
        var runner = new FakeProcessRunner();
        var service = new AdbService(runner, new FakeToolPaths())
        {
            DeviceSerial = "   "
        };

        await service.ExecuteAsync(new[] { "devices" });

        Assert.DoesNotContain("-s", runner.LastArguments);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCommandResult()
    {
        var expected = new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "List of devices attached"
        };

        var runner = new FakeProcessRunner(expected);
        var service = new AdbService(runner, new FakeToolPaths());

        var result = await service.ExecuteAsync(new[] { "devices" });

        Assert.True(result.IsSuccess);
        Assert.Equal("List of devices attached", result.StandardOutput);
    }

    [Fact]
    public void AdbPath_WhenToolsNotInstalled_Throws()
    {
        var runner = new FakeProcessRunner();
        var paths = new FakeToolPaths { AdbPath = null };
        var service = new AdbService(runner, paths);

        var exception = Assert.Throws<ToolsNotInstalledException>(() => service.AdbPath);

        Assert.Contains("platform-tools belum terpasang", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenToolsNotInstalled_Throws()
    {
        var runner = new FakeProcessRunner();
        var paths = new FakeToolPaths { AdbPath = null };
        var service = new AdbService(runner, paths);

        await Assert.ThrowsAsync<ToolsNotInstalledException>(() =>
            service.ExecuteAsync(new[] { "devices" }));
    }

    [Fact]
    public async Task OutputReceived_ForwardsFromRunner()
    {
        var runner = new FakeProcessRunner(new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "baris satu",
            StandardError = "baris dua"
        });

        var service = new AdbService(runner, new FakeToolPaths());
        var lines = new List<string>();
        service.OutputReceived += (_, e) => lines.Add(e.Line);

        await service.ExecuteAsync(new[] { "devices" });

        Assert.Contains("baris satu", lines);
        Assert.Contains("baris dua", lines);
    }

    [Fact]
    public async Task CommandStarting_ForwardsFromRunner()
    {
        var runner = new FakeProcessRunner();
        var service = new AdbService(runner, new FakeToolPaths());
        string? preview = null;
        service.CommandStarting += (_, e) => preview = e.CommandLine;

        await service.ExecuteAsync(new[] { "devices" });

        Assert.Equal(@"C:\Tools\platform-tools\adb.exe devices", preview);
    }
}
