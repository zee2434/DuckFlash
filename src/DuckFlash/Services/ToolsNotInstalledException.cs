namespace DuckFlash.Services;

/// <summary>
/// Dilempar ketika command adb/fastboot dijalankan padahal platform-tools
/// belum terpasang di folder lokal aplikasi.
/// </summary>
public sealed class ToolsNotInstalledException : Exception
{
    public ToolsNotInstalledException(string message)
        : base(message)
    {
    }
}
