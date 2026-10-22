using System.Net;
using DuckFlash.Services;
using Xunit;

namespace DuckFlash.Tests;

/// <summary>
/// Uji pengecekan versi terbaru di GitHub Releases.
/// </summary>
public class UpdateServiceTests
{
    [Theory]
    [InlineData("v1.1.0", "1.0.0", true)]
    [InlineData("1.0.1", "1.0.0", true)]
    [InlineData("2.0.0", "1.9.9", true)]
    [InlineData("v1.0.0", "1.0.0", false)]
    [InlineData("1.0.0", "1.0.0", false)]
    [InlineData("v0.9.0", "1.0.0", false)]
    [InlineData("", "1.0.0", false)]
    [InlineData(null, "1.0.0", false)]
    public void IsNewerVersion_ComparesCorrectly(string? latest, string current, bool expected)
    {
        Assert.Equal(expected, UpdateService.IsNewerVersion(latest, current));
    }

    [Fact]
    public async Task CheckAsync_NewerVersionAvailable_ReportsUpdate()
    {
        const string json = """
        {
          "tag_name": "v1.2.0",
<<<<<<< HEAD
          "html_url": "https://github.com/zee2434/DuckFlash/releases/tag/v1.2.0",
=======
          "html_url": "https://github.com/Dikdikduck/DuckFlash/releases/tag/v1.2.0",
>>>>>>> 15c381f (DuckFlash v1.0.0)
          "assets": [
            { "name": "DuckFlash.exe", "browser_download_url": "https://example.com/DuckFlash.exe" }
          ]
        }
        """;

        var service = new UpdateService(CreateClient(HttpStatusCode.OK, json), "1.0.0");

        var result = await service.CheckAsync();

        Assert.True(result.IsCheckSuccessful);
        Assert.True(result.IsUpdateAvailable);
        Assert.Equal("v1.2.0", result.LatestVersion);
        Assert.Equal("https://example.com/DuckFlash.exe", result.DownloadUrl);
        Assert.Equal("1.0.0", result.CurrentVersion);
    }

    [Fact]
    public async Task CheckAsync_SameVersion_ReportsNoUpdate()
    {
        const string json = """
        {
          "tag_name": "v1.0.0",
<<<<<<< HEAD
          "html_url": "https://github.com/zee2434/DuckFlash/releases/tag/v1.0.0",
=======
          "html_url": "https://github.com/Dikdikduck/DuckFlash/releases/tag/v1.0.0",
>>>>>>> 15c381f (DuckFlash v1.0.0)
          "assets": []
        }
        """;

        var service = new UpdateService(CreateClient(HttpStatusCode.OK, json), "1.0.0");

        var result = await service.CheckAsync();

        Assert.True(result.IsCheckSuccessful);
        Assert.False(result.IsUpdateAvailable);
    }

    [Fact]
    public async Task CheckAsync_HttpError_ReportsIndonesianError()
    {
        var service = new UpdateService(CreateClient(HttpStatusCode.NotFound, "{}"), "1.0.0");

        var result = await service.CheckAsync();

        Assert.False(result.IsCheckSuccessful);
        Assert.Contains("GitHub", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task CheckAsync_NetworkFailure_ReportsIndonesianError()
    {
        var client = new HttpClient(new FailingHandler());
        var service = new UpdateService(client, "1.0.0");

        var result = await service.CheckAsync();

        Assert.False(result.IsCheckSuccessful);
        Assert.Contains("Gagal cek versi terbaru", result.ErrorMessage ?? string.Empty);
    }

    [Fact]
    public async Task CheckAsync_NoExeAsset_StillReportsRelease()
    {
        const string json = """
        {
          "tag_name": "v1.5.0",
<<<<<<< HEAD
          "html_url": "https://github.com/zee2434/DuckFlash/releases/tag/v1.5.0",
=======
          "html_url": "https://github.com/Dikdikduck/DuckFlash/releases/tag/v1.5.0",
>>>>>>> 15c381f (DuckFlash v1.0.0)
          "assets": [
            { "name": "checksums.txt", "browser_download_url": "https://example.com/checksums.txt" }
          ]
        }
        """;

        var service = new UpdateService(CreateClient(HttpStatusCode.OK, json), "1.0.0");

        var result = await service.CheckAsync();

        Assert.True(result.IsCheckSuccessful);
        Assert.Null(result.DownloadUrl);
<<<<<<< HEAD
        Assert.Equal("https://github.com/zee2434/DuckFlash/releases/tag/v1.5.0", result.ReleaseUrl);
=======
        Assert.Equal("https://github.com/Dikdikduck/DuckFlash/releases/tag/v1.5.0", result.ReleaseUrl);
>>>>>>> 15c381f (DuckFlash v1.0.0)
    }

    [Fact]
    public void ReadAssemblyVersion_ReturnsSemanticVersion()
    {
        var version = UpdateService.ReadAssemblyVersion();

        Assert.Matches(@"^\d+\.\d+\.\d+$", version);
    }

    private static HttpClient CreateClient(HttpStatusCode statusCode, string body)
    {
        return new HttpClient(new StubHandler(statusCode, body));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public StubHandler(HttpStatusCode statusCode, string body)
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body)
            });
        }
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw new HttpRequestException("koneksi ditolak");
        }
    }
}
