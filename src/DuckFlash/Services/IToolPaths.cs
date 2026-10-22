namespace DuckFlash.Services;

/// <summary>
/// Sumber path penuh ke executable platform-tools.
/// Aplikasi memanggil adb.exe dan fastboot.exe dari folder ini dengan path penuh,
/// sehingga tidak bergantung pada PATH.
/// </summary>
public interface IToolPaths
{
    /// <summary>Path penuh ke adb.exe, atau null kalau platform-tools belum terpasang.</summary>
    string? AdbPath { get; }

    /// <summary>Path penuh ke fastboot.exe, atau null kalau platform-tools belum terpasang.</summary>
    string? FastbootPath { get; }

    /// <summary>Folder tempat platform-tools terpasang.</summary>
    string LocalPath { get; }
}
