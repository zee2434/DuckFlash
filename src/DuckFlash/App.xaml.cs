using System.Windows;
using DuckFlash.ViewModels;
using DuckFlash.Views;

namespace DuckFlash;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var viewModel = new MainWindowViewModel();

        var window = new MainWindow
        {
            DataContext = viewModel
        };

        MainWindow = window;
        window.Show();

        // Cek apakah platform-tools sudah terpasang begitu aplikasi dibuka.
        viewModel.InitializeCommand.Execute(null);
    }
}
