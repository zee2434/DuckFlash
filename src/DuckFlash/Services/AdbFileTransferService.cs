using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Menjelajahi filesystem perangkat dan memindahkan file/folder lewat ADB.
/// </summary>
public sealed class AdbFileTransferService
{
    private readonly AdbService _adbService;

    public AdbFileTransferService(AdbService adbService)
    {
        _adbService = adbService;
    }

    public async Task<IReadOnlyList<RemoteFileEntry>> ListDirectoryAsync(
        string remotePath,
        CancellationToken cancellationToken = default)
    {
        var normalizedPath = NormalizeRemotePath(remotePath);
        var result = await _adbService.ExecuteAsync(
            new[] { "shell", "ls", "-1Ap", QuoteRemoteShellArgument(normalizedPath) },
            cancellationToken).ConfigureAwait(false);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(GetFailureMessage(result, normalizedPath));
        }

        var entries = new List<RemoteFileEntry>();

        foreach (var rawLine in result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var name = rawLine.TrimEnd('\r');
            var isDirectory = name.EndsWith("/", StringComparison.Ordinal);
            name = isDirectory ? name[..^1] : name;

            if (string.IsNullOrWhiteSpace(name) || name is "." or "..")
            {
                continue;
            }

            entries.Add(new RemoteFileEntry
            {
                Name = name,
                Path = JoinRemotePath(normalizedPath, name),
                IsDirectory = isDirectory
            });
        }

        return entries
            .OrderByDescending(entry => entry.IsDirectory)
            .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task<CommandResult> PullAsync(
        IEnumerable<string> remotePaths,
        string localFolder,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localFolder);
        ArgumentNullException.ThrowIfNull(remotePaths);
        var fullLocalFolder = Path.GetFullPath(localFolder);

        if (!Directory.Exists(fullLocalFolder))
        {
            throw new DirectoryNotFoundException($"Folder tujuan tidak ditemukan: {fullLocalFolder}");
        }

        var sources = remotePaths
            .Select(NormalizeRemotePath)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (sources.Length == 0)
        {
            throw new ArgumentException("Pilih setidaknya satu file atau folder perangkat.", nameof(remotePaths));
        }

        return _adbService.ExecuteAsync(
            new[] { "pull" }.Concat(sources).Append(fullLocalFolder),
            cancellationToken);
    }

    public Task<CommandResult> PushAsync(
        IEnumerable<string> localPaths,
        string remoteFolder,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(localPaths);
        var sources = localPaths
            .Select(path =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(path);
                var fullPath = Path.GetFullPath(path);

                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                {
                    throw new FileNotFoundException("File atau folder sumber tidak ditemukan.", fullPath);
                }

                return fullPath;
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (sources.Length == 0)
        {
            throw new ArgumentException("Pilih setidaknya satu file atau folder lokal.", nameof(localPaths));
        }

        return _adbService.ExecuteAsync(
            new[] { "push" }.Concat(sources).Append(NormalizeRemotePath(remoteFolder)),
            cancellationToken);
    }

    public static string NormalizeRemotePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalized = path.Replace('\\', '/');

        if (!normalized.StartsWith("/", StringComparison.Ordinal)
            || normalized.Contains('\0')
            || normalized.Contains('\r')
            || normalized.Contains('\n'))
        {
            throw new ArgumentException("Path perangkat harus berupa path absolut yang valid.", nameof(path));
        }

        while (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized = normalized[..^1];
        }

        return normalized;
    }

    private static string JoinRemotePath(string parent, string name)
    {
        return parent == "/" ? $"/{name}" : $"{parent}/{name}";
    }

    private static string QuoteRemoteShellArgument(string argument)
    {
        return $"'{argument.Replace("'", "'\\''", StringComparison.Ordinal)}'";
    }

    private static string GetFailureMessage(CommandResult result, string path)
    {
        var output = result.CombinedOutput.Trim();
        return string.IsNullOrWhiteSpace(output)
            ? $"Tidak dapat membaca folder perangkat: {path} (exit code {result.ExitCode})."
            : $"Tidak dapat membaca folder perangkat {path}: {output}";
    }
}
