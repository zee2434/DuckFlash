using DuckFlash.Models;
using DuckFlash.Services;
using Xunit;

namespace DuckFlash.Tests;

/// <summary>
/// Uji aksi unlock/lock bootloader. Aksi ini destruktif, jadi harus ada
/// konfirmasi eksplisit dengan peringatan data terhapus.
/// </summary>
public class BootloaderServiceTests
{
    [Fact]
    public async Task RunAsync_Unlock_UsesFlashingUnlockArgument()
    {
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });
        var service = new BootloaderService(new FastbootService(runner, new FakeToolPaths()));

        await service.RunAsync(BootloaderAction.Unlock);

        Assert.Contains("flashing", runner.LastArguments);
        Assert.Contains("unlock", runner.LastArguments);
    }

    [Fact]
    public async Task RunAsync_Lock_UsesFlashingLockArgument()
    {
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });
        var service = new BootloaderService(new FastbootService(runner, new FakeToolPaths()));

        await service.RunAsync(BootloaderAction.Lock);

        Assert.Contains("flashing", runner.LastArguments);
        Assert.Contains("lock", runner.LastArguments);
    }

    [Fact]
    public async Task RunAsync_WithSerial_AddsDashSArgument()
    {
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });
        var fastboot = new FastbootService(runner, new FakeToolPaths()) { DeviceSerial = "SERIAL123" };
        var service = new BootloaderService(fastboot);

        await service.RunAsync(BootloaderAction.Unlock);

        Assert.Equal(new[] { "-s", "SERIAL123", "flashing", "unlock" }, runner.LastArguments);
    }

    [Fact]
    public void ValidateAction_NoFastbootDevice_IsBlocked()
    {
        var service = new BootloaderService(new FastbootService(new FakeProcessRunner(), new FakeToolPaths()));

        var reason = service.ValidateAction(BootloaderAction.Unlock, "tidak diketahui", fastbootDetected: false);

        Assert.NotNull(reason);
        Assert.Contains("fastboot", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAction_AlreadyUnlocked_IsBlocked()
    {
        var service = new BootloaderService(new FastbootService(new FakeProcessRunner(), new FakeToolPaths()));

        var reason = service.ValidateAction(BootloaderAction.Unlock, "unlocked");

        Assert.NotNull(reason);
        Assert.Contains("sudah terbuka", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAction_AlreadyLocked_IsBlocked()
    {
        var service = new BootloaderService(new FastbootService(new FakeProcessRunner(), new FakeToolPaths()));

        var reason = service.ValidateAction(BootloaderAction.Lock, "locked");

        Assert.NotNull(reason);
        Assert.Contains("sudah terkunci", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateAction_LockedBootloader_UnlockIsAllowed()
    {
        var service = new BootloaderService(new FastbootService(new FakeProcessRunner(), new FakeToolPaths()));

        var reason = service.ValidateAction(BootloaderAction.Unlock, "locked");

        Assert.Null(reason);
    }

    [Fact]
    public void BuildConfirmationText_MentionsDataErasure()
    {
        var text = BootloaderService.BuildConfirmationText(BootloaderAction.Unlock, "SERIAL123");

        Assert.Contains("SERIAL123", text);
        Assert.Contains("MENGHAPUS SEMUA DATA", text);
    }

    [Fact]
    public void BuildConfirmationText_Lock_AlsoMentionsDataErasure()
    {
        var text = BootloaderService.BuildConfirmationText(BootloaderAction.Lock, "SERIAL123");

        Assert.Contains("MENGHAPUS SEMUA DATA", text);
    }
}
