using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using DuckFlash.Models;
using DuckFlash.Services;

namespace DuckFlash.Views;

/// <summary>
/// Memilih beberapa file dan folder lokal untuk dikirim ke perangkat.
/// </summary>
public partial class LocalPathPickerWindow : Window
{
    private readonly ITextProvider _text;
    private readonly ObservableCollection<LocalFileEntry> _entries = new();
    private string _currentPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public IReadOnlyList<string> SelectedPaths { get; private set; } = Array.Empty<string>();

    public LocalPathPickerWindow(ITextProvider text)
    {
        InitializeComponent();

        _text = text;
        Title = _text.Get("dialog.localPicker.title");
        DescriptionText.Text = _text.Get("hint.localPicker.multiSelect");
        UpButton.Content = _text.Get("button.remotePicker.up");
        RefreshButton.Content = _text.Get("button.remotePicker.refresh");
        SelectButton.Content = _text.Get("button.remotePicker.select");
        CancelButton.Content = _text.Get("button.remotePicker.cancel");
        EntriesListBox.ItemsSource = _entries;

        Loaded += async (_, _) => await LoadDirectoryAsync(_currentPath);
    }

    private async void UpButton_Click(object sender, RoutedEventArgs e)
    {
        var parentPath = Directory.GetParent(_currentPath)?.FullName;

        if (parentPath is not null)
        {
            await LoadDirectoryAsync(parentPath);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadDirectoryAsync(PathTextBox.Text);
    }

    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedPaths = EntriesListBox.SelectedItems
            .Cast<LocalFileEntry>()
            .Select(entry => entry.Path)
            .ToArray();

        if (SelectedPaths.Count > 0)
        {
            DialogResult = true;
        }
    }

    private async void EntriesListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (EntriesListBox.SelectedItem is LocalFileEntry { IsDirectory: true } entry)
        {
            await LoadDirectoryAsync(entry.Path);
        }
    }

    private void EntriesListBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SelectionCountText.Text = _text.Get(
            "status.localPicker.selected",
            EntriesListBox.SelectedItems.Count);
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
            var fullPath = Path.GetFullPath(path);
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException($"Folder tidak ditemukan: {fullPath}");
            }

            var entries = await Task.Run(() => Directory
                .EnumerateFileSystemEntries(fullPath)
                .Select(entryPath =>
                {
                    var isDirectory = Directory.Exists(entryPath);
                    return new LocalFileEntry
                    {
                        Name = Path.GetFileName(entryPath),
                        Path = entryPath,
                        IsDirectory = isDirectory
                    };
                })
                .OrderByDescending(entry => entry.IsDirectory)
                .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray());

            _currentPath = fullPath;
            PathTextBox.Text = _currentPath;
            ErrorText.Text = string.Empty;
            _entries.Clear();

            foreach (var entry in entries)
            {
                _entries.Add(entry);
            }

            EntriesListBox.SelectedItems.Clear();
        }
        catch (Exception ex) when (ex is ArgumentException
                                   or IOException
                                   or UnauthorizedAccessException
                                   or NotSupportedException)
        {
            ErrorText.Text = ex.Message;
        }
    }
}
