using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DuckFlash.ViewModels;

namespace DuckFlash.Views;

/// <summary>
/// Code-behind utama, TIDAK punya logika yang diminta pengguna.
/// Hanya menangani event UI: file picker dan drag &amp; drop file.
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel();
        DataContext = _viewModel;

        // Menjalankan sekali untuk mengecek platform-tools.
        _viewModel.InitializeCommand.Execute(null);
    }

    #region File picker & drag & drop

    private void PickImageButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Pilih file .img",
            Filter = "Image partisi (*.img)|*.img"
        };

        if (dlg.ShowDialog(this) == true)
        {
            _viewModel.HandleDroppedFile(dlg.FileName);
        }
    }

    private void PickPackageButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Pilih file .zip",
            Filter = "Arsip zip (*.zip)|*.zip"
        };

        if (dlg.ShowDialog(this) == true)
        {
            _viewModel.HandleDroppedFile(dlg.FileName);
        }
    }

    private void PickApkButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Pilih file .apk",
            Filter = "Paket Android (*.apk)|*.apk"
        };

        if (dlg.ShowDialog(this) == true)
        {
            _viewModel.HandleDroppedFile(dlg.FileName);
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

    private void ImageDropHandler(object sender, DragEventArgs e)
    {
        var path = FirstPathWithExtension(e, ".img");

        if (path is not null)
        {
            _viewModel.HandleDroppedFile(path);
        }
    }

    private void PackageDropHandler(object sender, DragEventArgs e)
    {
        var path = FirstPathWithExtension(e, ".zip");

        if (path is not null)
        {
            _viewModel.HandleDroppedFile(path);
        }
    }

    private void ApkDropHandler(object sender, DragEventArgs e)
    {
        var path = FirstPathWithExtension(e, ".apk");

        if (path is not null)
        {
            _viewModel.HandleDroppedFile(path);
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

    /// <summary>
    /// Buka wizard instalasi custom ROM. ViewModel-nya dibuat oleh
    /// MainWindowViewModel supaya service yang dipakai tetap sama.
    /// </summary>
    private void OpenRomWizardButton_Click(object sender, RoutedEventArgs e)
    {
        var wizard = new RomInstallWizardWindow(_viewModel.CreateRomInstallWizard())
        {
            Owner = this
        };

        wizard.ShowDialog();
    }

    /// <summary>
    /// Buka jendela debloater.
    /// </summary>
    private void OpenDebloatButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new DebloatWindow(_viewModel.CreateDebloatViewModel())
        {
            Owner = this
        };

        window.ShowDialog();
    }

    /// <summary>
    /// Buka jendela alat perangkat (screenshot, logcat, backup/restore).
    /// </summary>
    private void OpenDeviceToolsButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new DeviceToolsWindow(_viewModel.CreateDeviceToolsViewModel())
        {
            Owner = this
        };

        window.ShowDialog();
    }
}
