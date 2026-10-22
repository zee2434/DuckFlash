using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DuckFlash.ViewModels;

namespace DuckFlash.Views;

/// <summary>
/// Code-behind wizard, TIDAK punya logika yang diminta pengguna.
/// Hanya menangani event UI: file picker dan drag &amp; drop file.
/// Semua pengecekan file tetap dilakukan di ViewModel lewat FileValidator.
/// </summary>
public partial class RomInstallWizardWindow : Window
{
    private readonly RomInstallWizardViewModel _viewModel;

    public RomInstallWizardWindow(RomInstallWizardViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
    }

    #region File picker & drag & drop

    private void PickRecoveryButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Pilih file .img",
            Filter = "Image partisi (*.img)|*.img"
        };

        if (dlg.ShowDialog(this) == true)
        {
            _viewModel.SelectRecoveryFile(dlg.FileName);
        }
    }

    private void PickRomButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Pilih file .zip",
            Filter = "Arsip zip (*.zip)|*.zip"
        };

        if (dlg.ShowDialog(this) == true)
        {
            _viewModel.SelectRomPackage(dlg.FileName);
        }
    }

    private void DragOverHandler(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void DragLeaveHandler(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(90, 90, 90));
        }
    }

    private void RecoveryDropHandler(object sender, DragEventArgs e)
    {
        var path = FirstPathWithExtension(e, ".img");

        if (path is not null)
        {
            _viewModel.SelectRecoveryFile(path);
        }
    }

    private void RomDropHandler(object sender, DragEventArgs e)
    {
        var path = FirstPathWithExtension(e, ".zip");

        if (path is not null)
        {
            _viewModel.SelectRomPackage(path);
        }
    }

    /// <summary>
    /// Ambil path pertama yang punya ekstensi tertentu dari hasil drag &amp; drop.
    /// </summary>
    private static string? FirstPathWithExtension(DragEventArgs e, string extension)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return null;
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return null;
        }

        return paths.FirstOrDefault(path =>
            Path.GetExtension(path).Equals(extension, StringComparison.OrdinalIgnoreCase));
    }

    #endregion
}
