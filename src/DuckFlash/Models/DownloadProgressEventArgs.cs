namespace DuckFlash.Models;

/// <summary>
/// Naik setiap kali ada progress dari download atau ekstraksi platform-tools.
/// </summary>
public sealed class DownloadProgressEventArgs : EventArgs
{
    public DownloadProgressEventArgs(int percentage, string? message)
    {
        Percentage = percentage;
        Message = message;
    }

    /// <summary>Indeks 0 sampai 100.</summary>
    public int Percentage { get; }

    /// <summary>Laporan status dalam bahasa Indonesia.</summary>
    public string? Message { get; }
}
