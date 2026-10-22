using System.Windows;
using DuckFlash.ViewModels;

namespace DuckFlash.Views;

/// <summary>
/// Code-behind jendela debloater. TIDAK punya logika yang diminta pengguna:
/// semua pembacaan daftar aplikasi dan penghapusan ada di ViewModel/service.
/// </summary>
public partial class DebloatWindow : Window
{
    public DebloatWindow(DebloatViewModel viewModel)
    {
        InitializeComponent();

        DataContext = viewModel;
    }
}
