using DuckFlash.Models;
using DuckFlash.Services;

namespace DuckFlash.Tests;

/// <summary>
/// UpdateService palsu untuk unit test. Tidak pernah menyentuh jaringan.
/// </summary>
public sealed class FakeUpdateService : IUpdateService
{
    /// <summary>Hasil yang dikembalikan saat CheckAsync dipanggil.</summary>
    public UpdateInfo Result { get; set; } = new()
    {
        IsCheckSuccessful = true,
        IsUpdateAvailable = false,
        LatestVersion = "v1.0.0",
        CurrentVersion = "1.0.0"
    };

    /// <summary>Berapa kali CheckAsync dipanggil.</summary>
    public int CallCount { get; private set; }

    public Task<UpdateInfo> CheckAsync(CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(Result);
    }
}
