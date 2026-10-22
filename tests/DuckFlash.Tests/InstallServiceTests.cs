using DuckFlash.Models;
using DuckFlash.Services;
using Xunit;

namespace DuckFlash.Tests;

/// <summary>
/// Uji fitur install APK lewat drag and drop.
/// </summary>
public class InstallServiceTests
{
    [Fact]
    public async Task InstallAsync_UsesAdbInstallArgument()
    {
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });
        var service = new InstallService(new AdbService(runner, new FakeToolPaths()));

        await service.InstallAsync(@"C:\download\app.apk");

        Assert.Equal(new[] { "install", @"C:\download\app.apk" }, runner.LastArguments);
    }

    [Fact]
    public async Task InstallAsync_WithSerial_AddsDashSArgument()
    {
        var runner = new FakeProcessRunner(new CommandResult { ExitCode = 0 });
        var adb = new AdbService(runner, new FakeToolPaths()) { DeviceSerial = "SERIAL123" };
        var service = new InstallService(adb);

        await service.InstallAsync(@"C:\download\app.apk");

        Assert.Equal(new[] { "-s", "SERIAL123", "install", @"C:\download\app.apk" }, runner.LastArguments);
    }

    [Fact]
    public void ValidateApk_ValidApk_IsAccepted()
    {
        var path = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.apk");

        // APK memang format zip, jadi buat arsip zip yang sungguhan.
        using (var archive = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create))
        {
            archive.CreateEntry("AndroidManifest.xml");
        }

        try
        {
            var result = FileValidator.ValidateApk(path);

            Assert.True(result.IsValid);
            Assert.Equal(64, result.Sha256.Length);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ValidateApk_WrongExtension_IsRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "bukan apk");

        try
        {
            var result = FileValidator.ValidateApk(path);

            Assert.False(result.IsValid);
            Assert.Contains(".apk", result.ErrorMessage ?? string.Empty);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ValidateApk_MissingFile_IsRejected()
    {
        var path = Path.Combine(Path.GetTempPath(), $"duckflash-test-{Guid.NewGuid():N}.apk");

        var result = FileValidator.ValidateApk(path);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void BuildConfirmationText_MentionsFileName()
    {
        var text = InstallService.BuildConfirmationText("app.apk");

        Assert.Contains("app.apk", text);
    }
}
