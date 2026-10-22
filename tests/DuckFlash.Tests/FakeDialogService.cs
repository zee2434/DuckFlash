using DuckFlash.Services;

namespace DuckFlash.Tests;

/// <summary>
/// Dialog palsu untuk unit test. Tidak pernah membuka MessageBox, jadi test
/// tidak bisa tersangkut menunggu pengguna.
/// </summary>
public sealed class FakeDialogService : IDialogService
{
    /// <summary>Judul dialog error terakhir yang dipanggil.</summary>
    public string? LastErrorTitle { get; private set; }

    /// <summary>Pesan dialog error terakhir yang dipanggil.</summary>
    public string? LastErrorMessage { get; private set; }

    /// <summary>Judul dialog konfirmasi terakhir yang dipanggil.</summary>
    public string? LastConfirmTitle { get; private set; }

    /// <summary>Pesan dialog konfirmasi terakhir yang dipanggil.</summary>
    public string? LastConfirmMessage { get; private set; }

    /// <summary>Judul dialog konfirmasi destruktif terakhir yang dipanggil.</summary>
    public string? LastDestructiveTitle { get; private set; }

    /// <summary>Pesan dialog konfirmasi destruktif terakhir yang dipanggil.</summary>
    public string? LastDestructiveMessage { get; private set; }

    /// <summary>Label persetujuan pada dialog konfirmasi destruktif terakhir.</summary>
    public string? LastDestructiveAcknowledgement { get; private set; }

    /// <summary>Jawaban yang diberikan saat konfirmasi biasa diminta.</summary>
    public bool ConfirmResult { get; set; }

    /// <summary>Jawaban yang diberikan saat konfirmasi destruktif diminta.</summary>
    public bool DestructiveConfirmResult { get; set; }

    /// <summary>Berapa kali dialog error dipanggil.</summary>
    public int ErrorCount { get; private set; }

    /// <summary>Berapa kali dialog konfirmasi dipanggil.</summary>
    public int ConfirmCount { get; private set; }

    /// <summary>Berapa kali dialog konfirmasi destruktif dipanggil.</summary>
    public int DestructiveConfirmCount { get; private set; }

    public void ShowError(string title, string message)
    {
        ErrorCount++;
        LastErrorTitle = title;
        LastErrorMessage = message;
    }

    public bool Confirm(string title, string message)
    {
        ConfirmCount++;
        LastConfirmTitle = title;
        LastConfirmMessage = message;
        return ConfirmResult;
    }

    public bool ConfirmDestructive(string title, string message, string acknowledgementLabel)
    {
        DestructiveConfirmCount++;
        LastDestructiveTitle = title;
        LastDestructiveMessage = message;
        LastDestructiveAcknowledgement = acknowledgementLabel;
        return DestructiveConfirmResult;
    }
}
