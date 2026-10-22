using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Mengecek versi terbaru aplikasi di GitHub Releases.
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Cek apakah ada versi yang lebih baru. Tidak pernah melempar exception
    /// selain OperationCanceledException.
    /// </summary>
    Task<UpdateInfo> CheckAsync(CancellationToken cancellationToken = default);
}
