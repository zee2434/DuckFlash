namespace DuckFlash.Services;

/// <summary>
/// Dialog konfirmasi dan error ke pengguna. Dipisah dari ViewModel supaya
/// unit test bisa memakai implementasi palsu tanpa membuka jendela.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Tampilkan pesan error. Implementasi sungguhan memakai MessageBox.
    /// </summary>
    void ShowError(string title, string message);

    /// <summary>
    /// Minta konfirmasi pengguna. True kalau pengguna menekan OK.
    /// </summary>
    bool Confirm(string title, string message);

    /// <summary>
    /// Minta konfirmasi eksplisit untuk aksi destruktif. Implementasi sungguhan
    /// membuka dialog dengan peringatan besar dan centang wajib.
    /// True kalau pengguna benar-benar mengonfirmasi.
    /// </summary>
    bool ConfirmDestructive(string title, string message, string acknowledgementLabel);
}
