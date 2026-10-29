using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using DuckFlash.Models;
using DuckFlash.Services;

namespace DuckFlash.Views;

/// <summary>
/// Dialog untuk menjelajahi file dan folder di perangkat Android.
/// </summary>
public partial class RemotePathPickerWindow : Window
{
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<RemoteFileEntry>>> _listDirectoryAsync;
    private readonly ITextProvider _text;
    private readonly bool _foldersOnly;
    private readonly ObservableCollection<RemoteFileEntry> _entries = new();
    private string _currentPath = "/sdcard";

    public IReadOnlyList<string> SelectedPaths { get; private set; } = Array.Empty<string>();

    public RemotePathPickerWindow(
        Func<string, CancellationToken, Task<IReadOnlyList<RemoteFileEntry>>> listDirectoryAsync,
        ITextProvider text,
        bool foldersOnly)
    {
        InitializeComponent();

        _listDirectoryAsync = listDirectoryAsync;
        _text = text;
        _foldersOnly = foldersOnly;
        EntriesListBox.SelectionMode = foldersOnly
            ? System.Windows.Controls.SelectionMode.Single
            : System.Windows.Controls.SelectionMode.Extended;

        Title = _text.Get("dialog.remotePicker.title");
        DescriptionText.Text = _text.Get(
            foldersOnly ? "hint.remotePicker.folder" : "hint.remotePicker.fileOrFolder");
        UpButton.Content = _text.Get("button.remotePicker.up");
        RefreshButton.Content = _text.Get("button.remotePicker.refresh");
        CurrentFolderButton.Content = _text.Get("button.remotePicker.currentFolder");
        SelectButton.Content = _text.Get("button.remotePicker.select");
        CancelButton.Content = _text.Get("button.remotePicker.cancel");
        EntriesListBox.ItemsSource = _entries;

        Loaded += async (_, _) => await LoadDirectoryAsync(_currentPath);
    }

    private async void UpButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var normalizedPath = AdbFileTransferService.NormalizeRemotePath(PathTextBox.Text);
            var parentPath = GetParentPath(normalizedPath);

            if (parentPath is not null)
            {
                await LoadDirectoryAsync(parentPath);
            }
        }
        catch (ArgumentException ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadDirectoryAsync(PathTextBox.Text);
    }

    private void CurrentFolderButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedPaths = new[] { _currentPath };
        DialogResult = true;
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedEntries = EntriesListBox.SelectedItems
            .Cast<RemoteFileEntry>()
            .Where(entry => !_foldersOnly || entry.IsDirectory)
            .ToArray();

        if (selectedEntries.Length > 0)
        {
            SelectedPaths = selectedEntries.Select(entry => entry.Path).ToArray();
            DialogResult = true;
        }
    }

    private async void EntriesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EntriesListBox.SelectedItem is not RemoteFileEntry entry)
        {
            return;
        }

        if (entry.IsDirectory)
        {
            await LoadDirectoryAsync(entry.Path);
        }
        else if (!_foldersOnly)
        {
            SelectedPaths = new[] { entry.Path };
            DialogResult = true;
        }
    }

    private void EntriesListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_foldersOnly)
        {
            foreach (var entry in EntriesListBox.SelectedItems
                         .Cast<RemoteFileEntry>()
                         .Where(entry => !entry.IsDirectory)
                         .ToArray())
            {
                EntriesListBox.SelectedItems.Remove(entry);
            }
        }
    }

    private async void PathTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await LoadDirectoryAsync(PathTextBox.Text);
        }
    }

    private async Task LoadDirectoryAsync(string path)
    {
        try
        {
            var entries = await _listDirectoryAsync(path, CancellationToken.None);
            _currentPath = AdbFileTransferService.NormalizeRemotePath(path);
            PathTextBox.Text = _currentPath;
            ErrorText.Text = string.Empty;
            _entries.Clear();

            foreach (var entry in entries)
            {
                _entries.Add(entry);
            }
        }
        catch (Exception ex)
        {
            ErrorText.Text = ex.Message;
        }
    }

    private static string? GetParentPath(string path)
    {
        if (path == "/")
        {
            return null;
        }

        var separator = path.LastIndexOf('/');
        return separator <= 0 ? "/" : path[..separator];
    }
}
