using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Menjalankan command adb. Semua pemanggilan melewati <see cref="IProcessRunner"/>
/// supaya bisa dimock pada unit test.
/// </summary>
public sealed class AdbService
{
    private readonly IProcessRunner _processRunner;
    private readonly IToolPaths _toolPaths;

    /// <summary>
    /// Serial device yang dipakai untuk semua command. Wajib diisi kalau ada
    /// lebih dari satu perangkat terdeteksi.
    /// </summary>
    public string? DeviceSerial { get; set; }

    public AdbService(IProcessRunner processRunner, IToolPaths toolPaths)
    {
        _processRunner = processRunner;
        _toolPaths = toolPaths;
    }

    /// <summary>Naik setiap baris output dari proses adb.</summary>
    public event EventHandler<ProcessOutputLineEventArgs>? OutputReceived
    {
        add => _processRunner.OutputReceived += value;
        remove => _processRunner.OutputReceived -= value;
    }

    /// <summary>Naik sebelum command dijalankan, untuk preview di log.</summary>
    public event EventHandler<ProcessCommandEventArgs>? CommandStarting
    {
        add => _processRunner.CommandStarting += value;
        remove => _processRunner.CommandStarting -= value;
    }

    /// <summary>Path penuh ke adb.exe yang dipakai service ini.</summary>
    public string AdbPath => _toolPaths.AdbPath
        ?? throw new ToolsNotInstalledException(
            "platform-tools belum terpasang. Klik \"Pasang Platform-tools\" terlebih dahulu.");

    /// <summary>
    /// Jalankan command adb dengan argumen yang diberikan.
    /// </summary>
    /// <param name="arguments">Argumen command, misalnya "devices" atau "shell getprop".</param>
    /// <param name="cancellationToken">Membatalkan proses yang sedang berjalan.</param>
    /// <returns>Exit code beserta stdout dan stderr.</returns>
    public Task<CommandResult> RunAsync(
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
    {
        return _processRunner.RunAsync(AdbPath, BuildArguments(arguments), cancellationToken);
    }

    /// <summary>
    /// Jalankan command adb dan langsung kembalikan hasilnya, awaited.
    /// </summary>
    public async Task<CommandResult> ExecuteAsync(
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
    {
        return await RunAsync(arguments, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Jalankan command adb untuk satu perangkat tertentu. Serial selalu ikut
    /// lewat flag -s, dipakai saat membaca detail perangkat di mode multi-device.
    /// </summary>
    public Task<CommandResult> ExecuteForSerialAsync(
        string serial,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
    {
        return _processRunner.RunAsync(AdbPath, BuildArguments(arguments, serial), cancellationToken);
    }

    /// <summary>
    /// Jalankan command adb tanpa flag -s. Dipakai untuk perintah yang memang
    /// berlaku ke semua perangkat, misalnya "devices".
    /// </summary>
    public Task<CommandResult> ExecuteGlobalAsync(
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
    {
        return _processRunner.RunAsync(AdbPath, BuildArguments(arguments, null, skipSerial: true), cancellationToken);
    }

    /// <summary>
    /// Susun argumen lengkap, menyertakan flag -s bila serial perangkat diisi.
    /// </summary>
    private List<string> BuildArguments(
        IEnumerable<string> arguments,
        string? serialOverride = null,
        bool skipSerial = false)
    {
        var result = new List<string>();

        var serial = serialOverride ?? DeviceSerial;

        // Multi-device: kalau lebih dari satu perangkat, serial wajib ikut.
        if (!skipSerial && !string.IsNullOrWhiteSpace(serial))
        {
            result.Add("-s");
            result.Add(serial);
        }

        result.AddRange(arguments ?? Array.Empty<string>());
        return result;
    }
}
