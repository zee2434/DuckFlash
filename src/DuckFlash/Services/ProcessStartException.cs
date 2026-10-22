namespace DuckFlash.Services;

/// <summary>
/// Dilempar ketika proses gagal dijalankan sama sekali, misalnya executable
/// tidak ditemukan atau tidak punya izin eksekusi.
/// </summary>
public sealed class ProcessStartException : Exception
{
    public ProcessStartException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
