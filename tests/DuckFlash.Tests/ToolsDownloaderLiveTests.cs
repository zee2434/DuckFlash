using DuckFlash.Services;
using Xunit;
using Xunit.Abstractions;

namespace DuckFlash.Tests;

/// <summary>
/// Uji terhadap server Google sungguhan. Sengaja tidak memakai atribut Skip supaya
/// kegagalan unduhan terlihat jelas kalau dijalankan manual.
/// </summary>
public class ToolsDownloaderLiveTests
{
    private readonly ITestOutputHelper _output;

    public ToolsDownloaderLiveTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task EnsureInstalledAsync_RealDownload_InstallsTools()
    {
        var root = Path.Combine(Path.GetTempPath(), "duckflash-live-" + Guid.NewGuid().ToString("N"));

        try
        {
            using var downloader = new ToolsDownloader(root, null);
            downloader.ProgressChanged += (_, e) =>
                _output.WriteLine($"{e.Percentage}% {e.Message}");

            var status = await downloader.EnsureInstalledAsync();

            Assert.True(status.IsInstalled, status.ErrorMessage);
            Assert.True(File.Exists(Path.Combine(root, "adb.exe")));
            Assert.True(File.Exists(Path.Combine(root, "fastboot.exe")));

            // Ekstrak juga harus menghasilkan source.properties.
            Assert.True(File.Exists(Path.Combine(root, "source.properties")));
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(root, recursive: true);
                }
            }
            catch (IOException)
            {
                // Test cleanup tidak boleh menggagalkan test.
            }
        }
    }
}
