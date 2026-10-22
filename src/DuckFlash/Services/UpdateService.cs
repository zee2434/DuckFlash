using System.Text.Json;
using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Mengecek apakah ada versi DuckFlash yang lebih baru di GitHub Releases.
/// </summary>
public sealed class UpdateService : IUpdateService
{
    /// <summary>
    /// Endpoint API GitHub Releases. Dipakai tanpa token, jadi rate limit-nya
    /// cukup untuk pemakaian pribadi.
    /// </summary>
    private const string LatestReleaseApi =
        "https://api.github.com/repos/Dikdikduck/DuckFlash/releases/latest";

    private readonly HttpClient _httpClient;
    private readonly string _currentVersion;
    private readonly ITextProvider _text;

    public UpdateService(HttpClient? httpClient = null, string? currentVersion = null)
        : this(httpClient, currentVersion, LocalizationService.Default)
    {
    }

    public UpdateService(HttpClient? httpClient, string? currentVersion, ITextProvider text)
    {
        _httpClient = httpClient ?? new HttpClient();
        _currentVersion = currentVersion ?? ReadAssemblyVersion();
        _text = text;
    }

    /// <summary>
    /// Cek GitHub Releases. Tidak pernah melempar exception: kalau gagal,
    /// hasilnya berisi pesan error dalam bahasa Indonesia.
    /// </summary>
    public async Task<UpdateInfo> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseApi);
            request.Headers.UserAgent.ParseAdd("DuckFlash");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _httpClient
                .SendAsync(request, cancellationToken)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new UpdateInfo
                {
                    IsCheckSuccessful = false,
                    CurrentVersion = _currentVersion,
                    ErrorMessage = _text.Get("update.httpError", (int)response.StatusCode)
                };
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var latestVersion = ReadString(root, "tag_name");
            var releaseUrl = ReadString(root, "html_url");
            var downloadUrl = ReadAssetUrl(root);

            var isNewer = IsNewerVersion(latestVersion, _currentVersion);

            return new UpdateInfo
            {
                IsCheckSuccessful = true,
                IsUpdateAvailable = isNewer,
                LatestVersion = latestVersion,
                ReleaseUrl = releaseUrl,
                DownloadUrl = downloadUrl,
                CurrentVersion = _currentVersion
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new UpdateInfo
            {
                IsCheckSuccessful = false,
                CurrentVersion = _currentVersion,
                ErrorMessage = _text.Get("update.checkFailed", ex.Message)
            };
        }
    }

    /// <summary>
    /// Ambil URL file .exe dari daftar asset rilis.
    /// </summary>
    private static string? ReadAssetUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets)
            || assets.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var asset in assets.EnumerateArray())
        {
            var name = ReadString(asset, "name");
            var url = ReadString(asset, "browser_download_url");

            if (name is null || url is null)
            {
                continue;
            }

            if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                return url;
            }
        }

        return null;
    }

    private static string? ReadString(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            var text = value.GetString();

            return string.IsNullOrWhiteSpace(text) ? null : text;
        }

        return null;
    }

    /// <summary>
    /// Bandingkan dua nomor versi. Versi dengan awalan "v" tetap dianggap sama.
    /// </summary>
    public static bool IsNewerVersion(string? latestVersion, string currentVersion)
    {
        if (string.IsNullOrWhiteSpace(latestVersion))
        {
            return false;
        }

        var latest = ParseVersion(latestVersion);
        var current = ParseVersion(currentVersion);

        for (var index = 0; index < Math.Max(latest.Length, current.Length); index++)
        {
            var left = index < latest.Length ? latest[index] : 0;
            var right = index < current.Length ? current[index] : 0;

            if (left != right)
            {
                return left > right;
            }
        }

        return false;
    }

    /// <summary>
    /// Ubah nomor versi jadi daftar angka. Bagian yang bukan angka (misal "v1")
    /// diabaikan.
    /// </summary>
    private static int[] ParseVersion(string version)
    {
        var parts = version
            .Trim()
            .TrimStart('v', 'V')
            .Split('.', StringSplitOptions.RemoveEmptyEntries)
            .Select(part =>
            {
                var digits = new string(part.TakeWhile(char.IsDigit).ToArray());

                return int.TryParse(digits, out var number) ? number : 0;
            })
            .ToArray();

        return parts.Length == 0 ? new[] { 0 } : parts;
    }

    /// <summary>
    /// Ambil versi aplikasi dari metadata assembly.
    /// </summary>
    public static string ReadAssemblyVersion()
    {
        var version = typeof(UpdateService).Assembly.GetName().Version;

        if (version is null)
        {
            return "1.0.0";
        }

        return $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
