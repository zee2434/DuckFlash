using System.ComponentModel;
using System.Runtime.CompilerServices;
using DuckFlash.Services;

namespace DuckFlash.ViewModels;

/// <summary>
/// Kumpulan teks UI yang sudah diterjemahkan. Implementasi INotifyPropertyChanged
/// supaya tampilan otomatis diperbarui saat pengguna mengganti bahasa.
/// </summary>
public sealed class UiText : INotifyPropertyChanged, ITextProvider
{
    private readonly LocalizationService _localization;

    public UiText(LocalizationService localization)
    {
        _localization = localization;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    #region Judul section

    public string AppTitle => Get("app.title");
    public string Ready => Get("app.ready");

    public string PlatformTools => Get("section.platformTools");
    public string Device => Get("section.device");
    public string Bootloader => Get("section.bootloader");
    public string FlashImage => Get("section.flash");
    public string SideloadPackage => Get("section.sideload");
    public string InstallPackage => Get("section.install");
    public string FileTransfer => Get("section.fileTransfer");
    public string Reboot => Get("section.reboot");
    public string Update => Get("section.update");
    public string Settings => Get("section.settings");
    public string Log => Get("section.log");
    public string RomWizard => Get("section.romWizard");
    public string Debloat => Get("section.debloat");
    public string DeviceTools => Get("section.deviceTools");

    #endregion

    #region Tombol

    public string CheckStatus => Get("button.checkStatus");
    public string InstallPlatformTools => Get("button.installPlatformTools");
    public string CheckAdb => Get("button.checkAdb");
    public string CheckFastboot => Get("button.checkFastboot");
    public string InstallDriver => Get("button.installDriver");
    public string UnlockBootloader => Get("button.unlockBootloader");
    public string LockBootloader => Get("button.lockBootloader");
    public string PickImage => Get("button.pickImage");
    public string PickPackage => Get("button.pickPackage");
    public string PickApk => Get("button.pickApk");
    public string Flash => Get("button.flash");
    public string Sideload => Get("button.sideload");
    public string InstallApk => Get("button.installApk");
    public string Pull => Get("button.pull");
    public string Push => Get("button.push");
    public string BrowseRemoteFileOrFolder => Get("button.browseRemoteFileOrFolder");
    public string BrowseRemoteFolder => Get("button.browseRemoteFolder");
    public string PickLocalFolder => Get("button.pickLocalFolder");
    public string BrowseLocalSources => Get("button.browseLocalSources");
    public string CompareHash => Get("button.compareHash");
    public string RebootSystem => Get("button.rebootSystem");
    public string RebootRecovery => Get("button.rebootRecovery");
    public string RebootBootloader => Get("button.rebootBootloader");
    public string RebootFastbootd => Get("button.rebootFastbootd");
    public string CheckUpdate => Get("button.checkUpdate");
    public string CopyLog => Get("button.copyLog");
    public string ClearLog => Get("button.clearLog");
    public string SimplifyLog => Get("button.simplifyLog");
    public string AdvanceLog => Get("button.advanceLog");
    public string StartRomWizard => Get("button.startRomWizard");
    public string NextStep => Get("button.nextStep");
    public string BackStep => Get("button.backStep");
    public string SkipStep => Get("button.skipStep");
    public string PickRecovery => Get("button.pickRecovery");
    public string PickRomPackage => Get("button.pickRom");
    public string RunStep => Get("button.runStep");
    public string OpenDebloat => Get("button.openDebloat");
    public string LoadPackages => Get("button.loadPackages");
    public string SelectAll => Get("button.selectAll");
    public string ClearSelection => Get("button.clearSelection");
    public string UninstallSelected => Get("button.uninstallSelected");
    public string SearchPackage => Get("label.searchPackage");
    public string ShowSystemApps => Get("label.showSystemApps");
    public string NoPackages => Get("value.noPackages");
    public string OpenDeviceTools => Get("button.openDeviceTools");
    public string CaptureScreenshot => Get("button.captureScreenshot");
    public string ExportLogcat => Get("button.exportLogcat");
    public string ClearLogcat => Get("button.clearLogcat");
    public string BackupApps => Get("button.backupApps");
    public string RestoreApps => Get("button.restoreApps");

    #endregion

    #region Label

    public string DeviceSelector => Get("label.deviceSelector");
    public string BootloaderState => Get("label.bootloaderState");
    public string ActiveSlot => Get("label.activeSlot");
    public string Partition => Get("label.partition");
    public string Sha256 => Get("label.sha256");
    public string ExpectedSha256 => Get("label.expectedSha256");
    public string Language => Get("label.language");
    public string AppVersion => Get("label.appVersion");
    public string ScreenshotTarget => Get("label.screenshotTarget");
    public string LogcatTarget => Get("label.logcatTarget");
    public string BackupFolder => Get("label.backupFolder");
    public string PullRemoteSource => Get("label.pullRemoteSource");
    public string PullLocalDestination => Get("label.pullLocalDestination");
    public string PushLocalSource => Get("label.pushLocalSource");
    public string PushRemoteDestination => Get("label.pushRemoteDestination");

    #endregion

    #region Petunjuk dan peringatan

    public string DropImageHint => Get("hint.dropImage");
    public string DropPackageHint => Get("hint.dropPackage");
    public string DropApkHint => Get("hint.dropApk");
    public string DropRomHint => Get("hint.dropRom");
    public string ScreenshotScope => Get("hint.screenshotScope");
    public string LogcatScope => Get("hint.logcatScope");
    public string BackupScope => Get("hint.backupScope");

    public string BootloaderWarning => Get("warning.bootloader");
    public string DestructiveWarning => Get("warning.destructive");

    public string DeviceSelectorTip => Get("tooltip.deviceSelector");
    public string PartitionTip => Get("tooltip.partition");

    #endregion

    #region Nilai bawaan

    public string NoImage => Get("value.noImage");
    public string NoPackage => Get("value.noPackage");
    public string NoApk => Get("value.noApk");

    #endregion

    /// <summary>
    /// Muat ulang semua teks setelah bahasa diganti.
    /// </summary>
    public void Refresh()
    {
        OnPropertyChanged(null as string);
    }

    public string Get(string key, params object?[] args)
    {
        return _localization.Get(key, args);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
