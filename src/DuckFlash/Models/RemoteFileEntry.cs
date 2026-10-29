namespace DuckFlash.Models;

/// <summary>
/// Satu item yang ditampilkan saat menjelajahi filesystem perangkat.
/// </summary>
public sealed class RemoteFileEntry
{
    public string Name { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }
}
