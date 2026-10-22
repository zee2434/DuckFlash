using System.Windows;
using Microsoft.Win32;
using DuckFlash.ViewModels;

namespace DuckFlash.Views;

/// <summary>
/// Code-behind jendela alat perangkat. TIDAK punya logika yang diminta
/// pengguna: hanya membuka dialog pilih file/folder lalu meneruskan path
/// ke ViewModel.
/// </summary>
public partial class DeviceToolsWindow : Window
{
    private readonly DeviceToolsViewModel _viewModel;

    public DeviceToolsWindow(DeviceToolsViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void CaptureScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Simpan screenshot",
            Filter = "Gambar PNG (*.png)|*.png",
            FileName = BuildFileName("screenshot", ".png")
        };

        if (dlg.ShowDialog(this) == true)
        {
            await _viewModel.CaptureScreenshotAsync(dlg.FileName);
        }
    }

    private async void ExportLogcatButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Simpan logcat",
            Filter = "File teks (*.txt)|*.txt",
            FileName = BuildFileName("logcat", ".txt")
        };

        if (dlg.ShowDialog(this) == true)
        {
            await _viewModel.ExportLogcatAsync(dlg.FileName);
        }
    }

    private async void ClearLogcatButton_Click(object sender, RoutedEventArgs e)
    {
        await _viewModel.ClearLogcatAsync();
    }

    private async void BackupButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Pilih folder tujuan cadangan"
        };

        if (dlg.ShowDialog(this) == true)
        {
            await _viewModel.BackupAsync(dlg.FolderName);
        }
    }

    private async void RestoreButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog
        {
            Title = "Pilih folder cadangan"
        };

        if (dlg.ShowDialog(this) == true)
        {
            await _viewModel.RestoreAsync(dlg.FolderName);
        }
    }

    /// <summary>
    /// Susun nama file bawaan yang sudah mengandung tanggal dan jam.
    /// </summary>
    private static string BuildFileName(string prefix, string extension)
    {
        return $"{prefix}-{DateTime.Now:yyyyMMdd-HHmmss}{extension}";
    }
}
