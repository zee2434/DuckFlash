using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Asal stream output yang diterima dari sebuah proses.
/// </summary>
public enum ProcessOutputSource
{
    StandardOutput,
    StandardError
}

/// <summary>
/// Satu baris output dari proses beserta asal stream-nya.
/// </summary>
public sealed class ProcessOutputLineEventArgs : EventArgs
{
    public ProcessOutputLineEventArgs(string line, ProcessOutputSource source)
    {
        Line = line;
        Source = source;
    }

    /// <summary>Isi baris tanpa karakter akhir baris.</summary>
    public string Line { get; }

    /// <summary>Stream asal baris ini.</summary>
    public ProcessOutputSource Source { get; }
}

/// <summary>
/// Dikirim sebelum proses dijalankan, berisi command lengkapnya.
/// </summary>
public sealed class ProcessCommandEventArgs : EventArgs
{
    public ProcessCommandEventArgs(string commandLine)
    {
        CommandLine = commandLine;
    }

    /// <summary>Command lengkap yang akan dijalankan, siap ditampilkan ke pengguna.</summary>
    public string CommandLine { get; }
}

/// <summary>
/// Menjalankan proses eksternal. Semua pemanggilan adb/fastboot di aplikasi ini
/// harus melewati interface ini agar bisa dimock pada unit test.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Naik setiap baris output yang dibaca dari stdout atau stderr.
    /// </summary>
    event EventHandler<ProcessOutputLineEventArgs>? OutputReceived;

    /// <summary>
    /// Naik tepat sebelum proses dijalankan, dipakai untuk preview command di log.
    /// </summary>
    event EventHandler<ProcessCommandEventArgs>? CommandStarting;

    /// <summary>
    /// Menjalankan proses dan menunggu sampai selesai.
    /// </summary>
    /// <param name="fileName">Path lengkap ke executable.</param>
    /// <param name="arguments">Argument yang diteruskan ke proses.</param>
    /// <param name="cancellationToken">Membatalkan proses yang sedang berjalan.</param>
    /// <returns>Exit code beserta stdout dan stderr.</returns>
    Task<CommandResult> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default);
}
