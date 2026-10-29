namespace DuckFlash.Models;

/// <summary>
/// Satu item file system lokal yang ditampilkan pada pemilih multi-select.
/// </summary>
public sealed class LocalFileEntry
{
    public string Name { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }
}
