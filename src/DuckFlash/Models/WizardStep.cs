namespace DuckFlash.Models;

/// <summary>
/// Langkah-langkah dalam wizard install custom ROM, sesuai urutan yang
/// benar untuk memasang custom ROM dari nol.
/// </summary>
public enum WizardStep
{
    /// <summary>Cek perangkat yang terhubung lewat ADB atau fastboot.</summary>
    CheckDevice,

    /// <summary>Buka kunci bootloader supaya partisi bisa ditulis.</summary>
    UnlockBootloader,

    /// <summary>Flash file recovery ke partisi recovery.</summary>
    FlashRecovery,

    /// <summary>Hapus data pengguna (userdata) supaya install benar-benar bersih.</summary>
    WipeData,

    /// <summary>Reboot perangkat ke mode recovery supaya ROM bisa di-sideload.</summary>
    RebootToRecovery,

    /// <summary>Sideload package ROM ke perangkat.</summary>
    SideloadRom,

    /// <summary>Reboot perangkat ke Android setelah ROM terpasang.</summary>
    RebootToSystem
}

/// <summary>
/// Status satu langkah wizard, supaya pengguna tahu langkah mana yang sudah
/// selesai, sedang berjalan, gagal, atau dilewati.
/// </summary>
public enum WizardStepState
{
    /// <summary>Belum dijalankan.</summary>
    Pending,

    /// <summary>Sedang dijalankan.</summary>
    Running,

    /// <summary>Berhasil.</summary>
    Done,

    /// <summary>Gagal, pengguna harus memperbaiki lalu mengulang.</summary>
    Failed,

    /// <summary>
    /// Menunggu tindakan pengguna, misalnya colokkan kembali kabel USB setelah
    /// perangkat direboot ke mode bootloader.
    /// </summary>
    Waiting,

    /// <summary>Tidak perlu dijalankan, jadi dilewati.</summary>
    Skipped
}
