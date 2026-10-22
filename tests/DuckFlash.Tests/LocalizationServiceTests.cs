using DuckFlash.Models;
using Xunit;

namespace DuckFlash.Services;

/// <summary>
/// Uji LocalizationService: pemuat katalog, format argumen, fallback bahasa,
/// dan penanganan kunci yang tidak ditemukan.
/// </summary>
public class LocalizationServiceTests
{
    [Fact]
    public void DefaultCatalogsAreLoadedFromEmbeddedResources()
    {
        var service = new LocalizationService();

        Assert.Equal(AppLanguage.Indonesian, service.CurrentLanguage);
        Assert.Equal("Platform-tools", service.Get("section.platformTools"));
        Assert.Equal("Perangkat", service.Get("section.device"));
    }

    [Fact]
    public void EnglishCatalogIsUsedWhenLanguageSwitched()
    {
        var service = new LocalizationService();
        service.CurrentLanguage = AppLanguage.English;

        Assert.Equal("Device", service.Get("section.device"));
        Assert.Equal("Flash image (.img)", service.Get("section.flash"));
    }

    [Fact]
    public void GetWithFormatArgsWorks()
    {
        var service = new LocalizationService();
        var result = service.Get("status.platformTools.installed", "C:\\path");

        Assert.Equal("Platform-tools: terpasang di C:\\path", result);
    }

    [Fact]
    public void MissingKeyInCurrentLanguageFallsBackToIndonesian()
    {
        var service = new LocalizationService(
            new Dictionary<AppLanguage, IReadOnlyDictionary<string, string>>
            {
                [AppLanguage.Indonesian] = new Dictionary<string, string>
                {
                    ["satu"] = "Satu",
                    ["dua"] = "Dua"
                },
                [AppLanguage.English] = new Dictionary<string, string>
                {
                    ["one"] = "One"
                }
            });

        service.CurrentLanguage = AppLanguage.English;

        Assert.Equal("One", service.Get("one"));
        Assert.Equal("Dua", service.Get("dua"));
    }

    [Fact]
    public void UnknownKeyReturnsKeyItself()
    {
        var service = new LocalizationService();
        Assert.Equal("nama.yang.tidak.ada", service.Get("nama.yang.tidak.ada"));
    }

    [Fact]
    public void CurrentLanguageSetterOnlyAcceptsAvailableLanguages()
    {
        var service = new LocalizationService();
        var unavailable = (AppLanguage)999;

        service.CurrentLanguage = unavailable;
        Assert.Equal(AppLanguage.Indonesian, service.CurrentLanguage);

        service.CurrentLanguage = AppLanguage.English;
        Assert.Equal(AppLanguage.English, service.CurrentLanguage);
    }

    [Fact]
    public void GetLanguageNameReturnsFriendlyNames()
    {
        var service = new LocalizationService();
        Assert.Equal("Bahasa Indonesia", service.GetLanguageName(AppLanguage.Indonesian));
        Assert.Equal("English", service.GetLanguageName(AppLanguage.English));
    }

    [Fact]
    public void GetOrDefaultReturnsFallbackWhenKeyMissing()
    {
        var service = new LocalizationService();
        Assert.Equal("Gunakan fallback", service.GetOrDefault("tidak.ada", "Gunakan fallback"));
    }
}