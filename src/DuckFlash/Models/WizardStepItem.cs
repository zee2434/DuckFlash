using CommunityToolkit.Mvvm.ComponentModel;

namespace DuckFlash.Models;

/// <summary>
/// Satu langkah dalam wizard install custom ROM. Mempunyai judul dan
/// deskripsi yang ditampilkan di bagian atas, serta status yang memberi tahu
/// pengguna langkah mana yang sudah selesai, sedang berjalan, atau gagal.
/// </summary>
public sealed class WizardStepItem : ObservableObject
{
    private string _title = string.Empty;
    private string _description = string.Empty;
    private WizardStepState _state = WizardStepState.Pending;

    /// <summary>Jenis langkah ini.</summary>
    public WizardStep Step { get; init; }

    /// <summary>Judul langkah, seperti "Cek Perangkat".</summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>Penjelasan singkat tentang langkah ini.</summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>Status langkah saat ini.</summary>
    public WizardStepState State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(CanProceed));
            }
        }
    }

    /// <summary>True kalau langkah ini sudah berhasil dijalankan.</summary>
    public bool CanProceed => State == WizardStepState.Done;
}
