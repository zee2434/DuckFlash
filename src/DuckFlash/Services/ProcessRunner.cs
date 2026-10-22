using System.Diagnostics;
using System.IO;
using System.Text;
using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Implementasi <see cref="IProcessRunner"/> di atas System.Diagnostics.Process.
/// Output dibaca asinkron per baris dari stdout maupun stderr, dan proses bisa
/// dibatalkan lewat CancellationToken.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    /// <inheritdoc />
    public event EventHandler<ProcessOutputLineEventArgs>? OutputReceived;

    /// <inheritdoc />
    public event EventHandler<ProcessCommandEventArgs>? CommandStarting;

    /// <inheritdoc />
    public async Task<CommandResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var argumentList = (arguments ?? Array.Empty<string>()).ToList();
        var commandLine = BuildCommandLine(fileName, argumentList);

        // Preview command: pengguna harus melihat command aslinya sebelum jalan.
        CommandStarting?.Invoke(this, new ProcessCommandEventArgs(commandLine));

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = BuildArgumentString(argumentList),
                // Penting untuk aplikasi GUI: jangan munculkan konsol.
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = false,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            }
        };

        var stdout = new List<string>();
        var stderr = new List<string>();

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new ProcessStartException(
                $"Gagal menjalankan \"{fileName}\". Pastikan platform-tools sudah terpasang.",
                ex);
        }

        // fastboot menulis hampir semua outputnya ke stderr, jadi keduanya wajib ditangkap.
        var stdoutTask = ReadLinesAsync(process.StandardOutput, ProcessOutputSource.StandardOutput, stdout);
        var stderrTask = ReadLinesAsync(process.StandardError, ProcessOutputSource.StandardError, stderr);

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        // Tunggu kedua stream benar-benar habis agar tidak ada baris yang terlewat.
        await Task.WhenAll(stdoutTask, stderrTask).ConfigureAwait(false);

        return new CommandResult
        {
            ExitCode = process.ExitCode,
            StandardOutput = string.Join(Environment.NewLine, stdout),
            StandardError = string.Join(Environment.NewLine, stderr),
            CommandLine = commandLine
        };
    }

    private async Task ReadLinesAsync(
        StreamReader reader,
        ProcessOutputSource source,
        List<string> collected)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            collected.Add(line);
            OutputReceived?.Invoke(this, new ProcessOutputLineEventArgs(line, source));
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Proses sudah selesai antara waktu dibatalkan dan pemanggilan Kill.
        }
        catch (NotSupportedException)
        {
            // Proses sudah keluar, tidak ada yang perlu dibunuh.
        }
    }

    private static string BuildCommandLine(string fileName, IReadOnlyList<string> arguments)
    {
        var builder = new StringBuilder(fileName);

        foreach (var argument in arguments)
        {
            builder.Append(' ').Append(QuoteArgument(argument));
        }

        return builder.ToString();
    }

    private static string BuildArgumentString(IReadOnlyList<string> arguments)
    {
        var builder = new StringBuilder();

        foreach (var argument in arguments)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(QuoteArgument(argument));
        }

        return builder.ToString();
    }

    /// <summary>
    /// Memberi tanda kutip bila argumen mengandung spasi atau karakter khusus,
    /// supaya path file dengan spasi tidak merusak command.
    /// </summary>
    internal static string QuoteArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        if (!argument.Any(char.IsWhiteSpace) && !argument.Contains('"'))
        {
            return argument;
        }

        var builder = new StringBuilder("\"");

        foreach (var character in argument)
        {
            if (character == '"')
            {
                builder.Append('\\');
            }

            builder.Append(character);
        }

        builder.Append('"');
        return builder.ToString();
    }
}
