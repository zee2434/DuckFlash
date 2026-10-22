using System.Windows;

namespace DuckFlash.Services;

/// <summary>
/// Implementasi <see cref="IDialogService"/> memakai MessageBox WPF.
/// </summary>
public sealed class MessageBoxDialogService : IDialogService
{
    private readonly ITextProvider _text;

    public MessageBoxDialogService()
        : this(LocalizationService.Default)
    {
    }

    public MessageBoxDialogService(ITextProvider text)
    {
        _text = text;
    }

    /// <inheritdoc />
    public void ShowError(string title, string message)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    /// <inheritdoc />
    public bool Confirm(string title, string message)
    {
        return MessageBox.Show(message, title, MessageBoxButton.OKCancel, MessageBoxImage.Warning)
            == MessageBoxResult.OK;
    }

    /// <inheritdoc />
    public bool ConfirmDestructive(string title, string message, string acknowledgementLabel)
    {
        var window = new DestructiveDialogWindow();

        window.SetContent(
            title,
            message,
            _text.Get("warning.destructive"),
            acknowledgementLabel);

        return window.ShowDialog() == true;
    }
}
