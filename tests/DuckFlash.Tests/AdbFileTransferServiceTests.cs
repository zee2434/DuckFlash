using DuckFlash.Models;
using DuckFlash.Services;
using Xunit;

namespace DuckFlash.Tests;

public class AdbFileTransferServiceTests
{
    [Fact]
    public async Task ListDirectoryAsync_ParsesFoldersAndFiles_AndQuotesRemotePath()
    {
        var runner = new FakeProcessRunner(new CommandResult
        {
            ExitCode = 0,
            StandardOutput = "notes.txt\nPictures/\n.hidden\n"
        });
        var service = new AdbFileTransferService(new AdbService(runner, new FakeToolPaths()));

        var entries = await service.ListDirectoryAsync("/sdcard/My Folder");

        Assert.Equal(
            new[] { "Pictures", ".hidden", "notes.txt" },
            entries.Select(entry => entry.Name));
        Assert.True(entries[0].IsDirectory);
        Assert.False(entries[1].IsDirectory);
        Assert.Equal("/sdcard/My Folder/Pictures", entries[0].Path);
        Assert.Equal(
            new[] { "shell", "ls", "-1Ap", "'/sdcard/My Folder'" },
            runner.LastArguments);
    }

    [Fact]
    public async Task ListDirectoryAsync_EscapesQuotesInRemotePath()
    {
        var runner = new FakeProcessRunner();
        var service = new AdbFileTransferService(new AdbService(runner, new FakeToolPaths()));

        await service.ListDirectoryAsync("/sdcard/user's folder");

        Assert.Equal(
            "'/sdcard/user'\\''s folder'",
            runner.LastArguments[^1]);
    }

    [Fact]
    public async Task PullAsync_PassesRemotePathAndLocalDestination()
    {
        var runner = new FakeProcessRunner();
        var service = new AdbFileTransferService(new AdbService(runner, new FakeToolPaths()));
        var localFolder = Path.Combine(Path.GetTempPath(), $"duckflash-pull-{Guid.NewGuid():N}");
        Directory.CreateDirectory(localFolder);

        try
        {
            await service.PullAsync(new[] { "/sdcard/DCIM" }, localFolder);

            Assert.Equal(
                new[] { "pull", "/sdcard/DCIM", Path.GetFullPath(localFolder) },
                runner.LastArguments);
        }
        finally
        {
            Directory.Delete(localFolder);
        }
    }

    [Fact]
    public async Task PushAsync_PassesLocalSourceAndRemoteFolder()
    {
        var runner = new FakeProcessRunner();
        var service = new AdbFileTransferService(new AdbService(runner, new FakeToolPaths()));
        var localFile = Path.Combine(Path.GetTempPath(), $"duckflash-push-{Guid.NewGuid():N}.txt");
        File.WriteAllText(localFile, "test");

        try
        {
            await service.PushAsync(new[] { localFile }, "/sdcard/Download/");

            Assert.Equal(
                new[] { "push", Path.GetFullPath(localFile), "/sdcard/Download" },
                runner.LastArguments);
        }
        finally
        {
            File.Delete(localFile);
        }
    }

    [Fact]
    public async Task PushAsync_MissingLocalSource_IsRejected()
    {
        var service = new AdbFileTransferService(
            new AdbService(new FakeProcessRunner(), new FakeToolPaths()));

        await Assert.ThrowsAsync<FileNotFoundException>(() =>
            service.PushAsync(
                new[] { Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")) },
                "/sdcard"));
    }

    [Fact]
    public async Task PullAsync_PassesMultipleRemoteSources()
    {
        var runner = new FakeProcessRunner();
        var service = new AdbFileTransferService(new AdbService(runner, new FakeToolPaths()));
        var localFolder = Path.Combine(Path.GetTempPath(), $"duckflash-pull-{Guid.NewGuid():N}");
        Directory.CreateDirectory(localFolder);

        try
        {
            await service.PullAsync(
                new[] { "/sdcard/DCIM", "/sdcard/Pictures/Trip" },
                localFolder);

            Assert.Equal(
                new[] { "pull", "/sdcard/DCIM", "/sdcard/Pictures/Trip", Path.GetFullPath(localFolder) },
                runner.LastArguments);
        }
        finally
        {
            Directory.Delete(localFolder);
        }
    }

    [Fact]
    public async Task PushAsync_PassesMultipleFilesAndFolders()
    {
        var runner = new FakeProcessRunner();
        var service = new AdbFileTransferService(new AdbService(runner, new FakeToolPaths()));
        var sourceFolder = Path.Combine(Path.GetTempPath(), $"duckflash-push-{Guid.NewGuid():N}");
        var sourceFile = $"{sourceFolder}.txt";
        Directory.CreateDirectory(sourceFolder);
        File.WriteAllText(sourceFile, "test");

        try
        {
            await service.PushAsync(new[] { sourceFolder, sourceFile }, "/sdcard/Download");

            Assert.Equal(
                new[] { "push", Path.GetFullPath(sourceFolder), Path.GetFullPath(sourceFile), "/sdcard/Download" },
                runner.LastArguments);
        }
        finally
        {
            Directory.Delete(sourceFolder);
            File.Delete(sourceFile);
        }
    }
}
