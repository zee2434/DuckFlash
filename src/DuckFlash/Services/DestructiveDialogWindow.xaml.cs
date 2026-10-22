using System.ComponentModel;
using System.Windows;

namespace DuckFlash.Services;

/// <summary>
/// Jendela konfirmasi untuk aksi destruktif. Tombol lanjut hanya aktif
/// setelah pengguna mencentang kotak persetujuan.
/// </summary>
public partial class DestructiveDialogWindow : Window
{
    private readonly DestructiveDialogContent _content = new();

    public DestructiveDialogWindow()
    {
        InitializeComponent();

        DataContext = _content;
    }

    /// <summary>Isi jendela sebelum ditampilkan.</summary>
    public void SetContent(string title, string message, string warningText, string acknowledgmentLabel)
    {
        Title = title;

        _content.Title = title;
        _content.Message = message;
        _content.WarningText = warningText;
        _content.AcknowledgmentLabel = acknowledgmentLabel;
        _content.AcknowledgementChecked = false;

        // Tombol hanya aktif setelah persetujuan dicentang.
        ConfirmButton.IsEnabled = false;
    }

    /// <summary>True kalau pengguna mencentang persetujuan.</summary>
    public bool IsAcknowledged => AcknowledgementCheckBox.IsChecked == true;

    private void OnAcknowledgementChanged(object sender, RoutedEventArgs e)
    {
        // Baca langsung dari checkbox, karena urutan binding belum tentu sinkron.
        ConfirmButton.IsEnabled = AcknowledgementCheckBox.IsChecked == true;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        if (!IsAcknowledged)
        {
            return;
        }

        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

/// <summary>
/// Isi teks untuk <see cref="DestructiveDialogWindow"/>.
/// </summary>
public sealed class DestructiveDialogContent : INotifyPropertyChanged
{
    private bool _acknowledgementChecked;

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string WarningText { get; set; } = string.Empty;

    public string AcknowledgmentLabel { get; set; } = string.Empty;

    /// <summary>Status kotak persetujuan yang dicentang pengguna.</summary>
    public bool AcknowledgementChecked
    {
        get => _acknowledgementChecked;
        set
        {
            if (_acknowledgementChecked == value)
            {
                return;
            }

            _acknowledgementChecked = value;
            OnPropertyChanged(nameof(AcknowledgementChecked));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Dikirim supaya binding tahu datanya berubah.</summary>
    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
