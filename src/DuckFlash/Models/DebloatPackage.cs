using CommunityToolkit.Mvvm.ComponentModel;

namespace DuckFlash.Models;

/// <summary>
/// Satu aplikasi yang terpasang di perangkat dan bisa dicentang pengguna
/// untuk dihapus lewat adb.
/// </summary>
public sealed class DebloatPackage : ObservableObject
{
    private bool _isSelected;

    public DebloatPackage(string packageName, bool isSystem)
    {
        PackageName = packageName;
        IsSystem = isSystem;
    }

    /// <summary>Nama package, misalnya com.android.chrome.</summary>
    public string PackageName { get; }

    /// <summary>True kalau aplikasi ini aplikasi bawaan sistem.</summary>
    public bool IsSystem { get; }

    /// <summary>True kalau pengguna mencentang aplikasi ini untuk dihapus.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    /// <summary>
    /// Nama package dengan awalan package: yang kadang ikut tercetak di
    /// keluaran adb.
    /// </summary>
    public string DisplayName => PackageName;
}
