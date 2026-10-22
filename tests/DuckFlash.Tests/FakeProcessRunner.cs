using DuckFlash.Models;
using DuckFlash.Services;

namespace DuckFlash.Tests;

/// <summary>
/// ProcessRunner palsu untuk unit test. Tidak menjalankan proses sungguhan.
/// </summary>
public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly CommandResult _result;

    public FakeProcessRunner(CommandResult? result = null)
    {
        _result = result ?? new CommandResult { ExitCode = 0 };
    }

    /// <summary>Argument yang diterima pada pemanggilan terakhir.</summary>
    public IReadOnlyList<string> LastArguments { get; private set; } = Array.Empty<string>();

    /// <summary>Path executable yang diterima pada pemanggilan terakhir.</summary>
    public string? LastFileName { get; private set; }

    /// <summary>Berapa kali RunAsync dipanggil.</summary>
    public int CallCount { get; private set; }

    /// <summary>Exception yang dilempar pada pemanggilan berikutnya, kalau ada.</summary>
    public Exception? ThrowOnRun { get; set; }

    public event EventHandler<ProcessOutputLineEventArgs>? OutputReceived;

    public event EventHandler<ProcessCommandEventArgs>? CommandStarting;

    public Task<CommandResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastFileName = fileName;
        LastArguments = arguments.ToList();

        if (ThrowOnRun is not null)
        {
            throw ThrowOnRun;
        }

        // Simulasikan event preview command dan output, seperti runner sungguhan.
        CommandStarting?.Invoke(this, new ProcessCommandEventArgs(
            $"{fileName} {string.Join(' ', LastArguments)}"));

        foreach (var line in _result.StandardOutput.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            OutputReceived?.Invoke(this, new ProcessOutputLineEventArgs(
                line.TrimEnd('\r'), ProcessOutputSource.StandardOutput));
        }

        foreach (var line in _result.StandardError.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            OutputReceived?.Invoke(this, new ProcessOutputLineEventArgs(
                line.TrimEnd('\r'), ProcessOutputSource.StandardError));
        }

        return Task.FromResult(_result);
    }
}

/// <summary>
/// Sumber path palsu untuk unit test.
/// </summary>
public sealed class FakeToolPaths : IToolPaths
{
    public string? AdbPath { get; set; } = @"C:\Tools\platform-tools\adb.exe";

    public string? FastbootPath { get; set; } = @"C:\Tools\platform-tools\fastboot.exe";

    public string LocalPath { get; set; } = @"C:\Tools\platform-tools";
}
