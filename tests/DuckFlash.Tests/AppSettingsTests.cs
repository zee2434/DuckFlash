using DuckFlash.Models;
using Xunit;

namespace DuckFlash.Services;

/// <summary>
/// Uji AppSettings: simpan dan muat ulang bahasa, plus toleransi terhadap
/// corrupt file di disk.
/// </summary>
public class AppSettingsTests
{
    private static string NewTempFile(string name)
    {
        var directory = Path.Combine(Path.GetTempPath(), "duckflash-tests", "appsettings");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, name);
    }

    [Fact]
    public void SaveAndLoadRoundTripPreservesLanguage()
    {
        var filePath = NewTempFile("settings.json");

        var settings = new AppSettings(filePath);
        settings.Language = AppLanguage.English;

        settings.Save();
        var loaded = settings.Load();

        Assert.Equal(AppLanguage.English, loaded.Language);
    }

    [Fact]
    public void LoadWithoutFileUsesDefaultLanguage()
    {
        var filePath = NewTempFile("settings-missing.json");
        var settings = new AppSettings(filePath);
        var loaded = settings.Load();
        Assert.Equal(AppLanguage.Indonesian, loaded.Language);
    }

    [Fact]
    public void CorruptFileDoesNotThrow()
    {
        var filePath = NewTempFile("corrupt.json");
        File.WriteAllText(filePath, "{ broken json");

        var settings = new AppSettings(filePath);
        var loaded = settings.Load();
        Assert.Equal(AppLanguage.Indonesian, loaded.Language);
    }

    [Fact]
    public void UnknownLanguageStringKeepsDefault()
    {
        var filePath = NewTempFile("unknown-lang.json");
        File.WriteAllText(filePath, "{\"language\":\"Klingon\"}");

        var settings = new AppSettings(filePath);
        var loaded = settings.Load();
        Assert.Equal(AppLanguage.Indonesian, loaded.Language);
    }

    [Fact]
    public void RoundTripWithDefaultLanguageKeepsIndonesian()
    {
        var filePath = NewTempFile("settings-default.json");

        var settings = new AppSettings(filePath) { Language = AppLanguage.Indonesian };
        settings.Save();
        var loaded = settings.Load();
        Assert.Equal(AppLanguage.Indonesian, loaded.Language);
    }
}