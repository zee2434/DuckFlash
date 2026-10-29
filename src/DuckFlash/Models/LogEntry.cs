namespace DuckFlash.Models;

/// <summary>
/// Tingkat keparahan satu baris log.
/// </summary>
public enum LogSeverity
{
    Info,
    Success,
    Warning,
    Error
}

/// <summary>
/// Satu baris di panel log live.
/// </summary>
public sealed class LogEntry
{
    public LogEntry(
        string message,
        LogSeverity severity = LogSeverity.Info,
        bool isAdvancedOnly = false)
    {
        Timestamp = DateTime.Now;
        Message = message;
        Severity = severity;
        IsAdvancedOnly = isAdvancedOnly;
    }

    /// <summary>Waktu baris dicatat.</summary>
    public DateTime Timestamp { get; }

    /// <summary>Isi baris dalam bahasa Indonesia.</summary>
    public string Message { get; }

    /// <summary>Tingkat keparahan baris, menentukan warna di UI.</summary>
    public LogSeverity Severity { get; }

    /// <summary>True jika baris hanya relevan pada tampilan log Advance.</summary>
    public bool IsAdvancedOnly { get; }

    /// <summary>Baris lengkap dengan jam, siap ditampilkan.</summary>
    public string Formatted => $"[{Timestamp:HH:mm:ss}] {Message}";
}
