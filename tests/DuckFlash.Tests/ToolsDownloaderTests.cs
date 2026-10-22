using System.IO.Compression;
using System.Net;
using System.Text;
using DuckFlash.Models;
using DuckFlash.Services;
using Xunit;

namespace DuckFlash.Tests;

public class ToolsDownloaderTests
{
    [Fact]
    public async Task CheckStatusAsync_NoToolsInstalled_ReturnsNotInstalled()
    {
        var root = CreateTempDirectory();
        try
        {
            using var downloader = new ToolsDownloader(root, CreateClient(new byte[] { 1, 2, 3 }));

            var status = await downloader.CheckStatusAsync();

            Assert.False(status.IsInstalled);
            Assert.Equal(root, status.LocalPath);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task CheckStatusAsync_AdbExists_ReturnsInstalled()
    {
        var root = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "adb.exe"), "dummy");

            using var downloader = new ToolsDownloader(root, CreateClient(Array.Empty<byte>()));

            var status = await downloader.CheckStatusAsync();

            Assert.True(status.IsInstalled);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task EnsureInstalledAsync_AlreadyInstalled_SkipsDownload()
    {
        var root = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(root, "adb.exe"), "dummy");

            var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(Array.Empty<byte>())
            });

            using var downloader = new ToolsDownloader(root, CreateClient(handler));
            var requested = 0;
            handler.OnRequest = () => requested++;

            var status = await downloader.EnsureInstalledAsync();

            Assert.True(status.IsInstalled);
            Assert.Equal(0, requested);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task EnsureInstalledAsync_DownloadsAndExtractsTools()
    {
        var root = CreateTempDirectory();
        try
        {
            var zipBytes = BuildFakePlatformToolsZip();

            var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(zipBytes)
            });

            using var downloader = new ToolsDownloader(root, CreateClient(handler));
            var progressReports = new List<DownloadProgressEventArgs>();
            downloader.ProgressChanged += (_, e) => progressReports.Add(e);

            var status = await downloader.EnsureInstalledAsync();

            Assert.True(status.IsInstalled);
            Assert.True(File.Exists(Path.Combine(root, "adb.exe")));
            Assert.True(File.Exists(Path.Combine(root, "fastboot.exe")));

            // Progress harus pernah sampai 100 dan ada laporan dalam bahasa Indonesia.
            Assert.Contains(progressReports, r => r.Percentage == 100);
            Assert.Contains(progressReports, r => r.Message is not null && r.Message.Contains("platform-tools"));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task EnsureInstalledAsync_RaisesProgressDuringDownload()
    {
        var root = CreateTempDirectory();
        try
        {
            var zipBytes = BuildFakePlatformToolsZip();

            var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(zipBytes)
            });

            using var downloader = new ToolsDownloader(root, CreateClient(handler));
            var progressReports = new List<DownloadProgressEventArgs>();
            downloader.ProgressChanged += (_, e) => progressReports.Add(e);

            await downloader.EnsureInstalledAsync();

            // Selain laporan 100%, harus ada laporan progress download juga.
            Assert.True(progressReports.Count > 1);
            Assert.Contains(progressReports, r => r.Percentage > 0 && r.Percentage < 100);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task EnsureInstalledAsync_DownloadFails_ReturnsErrorMessage()
    {
        var root = CreateTempDirectory();
        try
        {
            var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.NotFound));

            using var downloader = new ToolsDownloader(root, CreateClient(handler));

            var status = await downloader.EnsureInstalledAsync();

            Assert.False(status.IsInstalled);
            Assert.NotNull(status.ErrorMessage);
            Assert.Contains("Gagal memasang platform-tools", status.ErrorMessage);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task EnsureInstalledAsync_UnexpectedZipStructure_ReturnsErrorMessage()
    {
        var root = CreateTempDirectory();
        try
        {
            // Zip yang isinya tidak punya folder "platform-tools".
            var zipBytes = BuildZipWithEntry("acak.txt", "isi");

            var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(zipBytes)
            });

            using var downloader = new ToolsDownloader(root, CreateClient(handler));

            var status = await downloader.EnsureInstalledAsync();

            Assert.False(status.IsInstalled);
            Assert.NotNull(status.ErrorMessage);
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public async Task EnsureInstalledAsync_Cancelled_ThrowsAndCleansUp()
    {
        var root = CreateTempDirectory();
        try
        {
            var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(BuildFakePlatformToolsZip())
            });

            using var downloader = new ToolsDownloader(root, CreateClient(handler));
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                downloader.EnsureInstalledAsync(cts.Token));

            // Berkas zip sisa harus dibersihkan.
            Assert.False(File.Exists(Path.Combine(root + ".download", "platform-tools.zip")));
            Assert.False(Directory.Exists(root + ".download"));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Fact]
    public void DefaultInstallRoot_UsesLocalAppData()
    {
        using var downloader = new ToolsDownloader();

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var expected = Path.Combine(localAppData, "DuckFlash", "platform-tools");

        Assert.Equal(expected, downloader.InstallRoot);
    }

    [Fact]
    public void DownloadUrl_PointsToGoogleOfficialRepository()
    {
        Assert.Equal(
            "https://dl.google.com/android/repository/platform-tools-latest-windows.zip",
            ToolsDownloader.DownloadUrl);
    }

    private static HttpClient CreateClient(byte[] content)
    {
        return CreateClient(new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(content)
        }));
    }

    private static HttpClient CreateClient(HttpMessageHandler handler)
    {
        return new HttpClient(handler);
    }

    private static byte[] BuildFakePlatformToolsZip()
    {
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            void AddEntry(string name)
            {
                var entry = archive.CreateEntry(name);
                using var stream = entry.Open();
                var bytes = Encoding.UTF8.GetBytes("dummy");
                stream.Write(bytes, 0, bytes.Length);
            }

            AddEntry("platform-tools/adb.exe");
            AddEntry("platform-tools/fastboot.exe");
            AddEntry("platform-tools/source.properties");
        }

        return buffer.ToArray();
    }

    private static byte[] BuildZipWithEntry(string name, string content)
    {
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry(name);
            using var stream = entry.Open();
            var bytes = Encoding.UTF8.GetBytes(content);
            stream.Write(bytes, 0, bytes.Length);
        }

        return buffer.ToArray();
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "duckflash-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        // Bersihkan juga folder saudara yang dipakai downloader saat ekstraksi.
        foreach (var candidate in new[] { path, path + ".download", path + ".staging" })
        {
            try
            {
                if (Directory.Exists(candidate))
                {
                    Directory.Delete(candidate, recursive: true);
                }
            }
            catch (IOException)
            {
                // Test cleanup tidak boleh menggagalkan test.
            }
        }
    }

    /// <summary>
    /// Handler HTTP palsu supaya test tidak menyentuh jaringan.
    /// </summary>
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public FakeHandler(HttpResponseMessage response)
        {
            _response = response;
        }

        public Action? OnRequest { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            OnRequest?.Invoke();
            return Task.FromResult(_response);
        }
    }
}
