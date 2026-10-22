using DuckFlash.Models;
using DuckFlash.Services;
using Xunit;

namespace DuckFlash.Tests;

/// <summary>
/// Uji ErrorTranslator. Pemetaan dimuat dari Resources/error-mappings.json,
/// jadi test ini sekaligus memverifikasi isi file tersebut.
/// </summary>
public class ErrorTranslatorTests
{
    [Fact]
    public void DefaultTranslator_LoadsMappingsFromResources()
    {
        var translator = new ErrorTranslator();

        Assert.True(translator.MappingCount > 0, "Daftar pemetaan error tidak termuat.");
    }

    [Fact]
    public void DefaultTranslator_MappingsAreEmbeddedInAssembly()
    {
        // Pemetaan harus tertanam di dalam assembly, supaya tetap terbaca saat
        // aplikasi dipublish sebagai .exe single-file.
        var assembly = typeof(ErrorTranslator).Assembly;

        var names = assembly.GetManifestResourceNames();

        Assert.Contains("DuckFlash.Resources.error-mappings.json", names);
    }

    [Theory]
    [InlineData("error: device unauthorized", "device_unauthorized")]
    [InlineData("no permissions; see [http://developer.android.com/tools/device.html]", "no_permissions")]
    [InlineData("adb: waiting for any device", "waiting_for_any_device")]
    [InlineData("error: no devices/emulators found", "no_devices_found")]
    [InlineData("fastboot: no devices found", "fastboot_no_devices")]
    [InlineData("FAILED (remote: 'Flashing is not allowed')", "bootloader_locked")]
    [InlineData("FAILED (remote: 'unknown partition')", "partition_not_found")]
    [InlineData("error: device offline", "device_offline")]
    [InlineData("remote: cannot stat 'C:\\missing.img'", "file_not_found")]
    [InlineData("adb: sideload failed to install", "sideload_failed")]
    public void Translate_KnownError_ReturnsExpectedMapping(string raw, string expectedId)
    {
        var translator = new ErrorTranslator();

        var result = translator.Translate(raw);

        Assert.Equal(expectedId, result.MappingId);
        Assert.True(result.IsKnown);
        Assert.False(string.IsNullOrWhiteSpace(result.Title));
        Assert.False(string.IsNullOrWhiteSpace(result.Explanation));
        Assert.False(string.IsNullOrWhiteSpace(result.Solution));
    }

    [Fact]
    public void Translate_UnknownError_ReturnsGenericIndonesianMessage()
    {
        var translator = new ErrorTranslator();

        var result = translator.Translate("error: sesuatu yang sangat aneh terjadi");

        Assert.False(result.IsKnown);
        Assert.Equal("Perintah gagal", result.Title);
        Assert.Contains("error: sesuatu yang sangat aneh terjadi", result.RawMessage);
    }

    [Fact]
    public void Translate_EmptyMessage_ReturnsGenericMessage()
    {
        var translator = new ErrorTranslator();

        var result = translator.Translate(string.Empty);

        Assert.False(result.IsKnown);
        Assert.Equal("Perintah gagal", result.Title);
    }

    [Fact]
    public void Translate_NullMessage_ReturnsGenericMessage()
    {
        var translator = new ErrorTranslator();

        var result = translator.Translate((string?)null);

        Assert.False(result.IsKnown);
        Assert.Equal("Perintah gagal", result.Title);
    }

    [Fact]
    public void Translate_UsesStdoutAndStderrTogether()
    {
        var translator = new ErrorTranslator();

        var result = translator.Translate(new CommandResult
        {
            ExitCode = 1,
            StandardOutput = "List of devices attached",
            StandardError = "error: device unauthorized"
        });

        Assert.Equal("device_unauthorized", result.MappingId);
    }

    [Fact]
    public void Translate_IsCaseInsensitive()
    {
        var translator = new ErrorTranslator();

        var result = translator.Translate("ERROR: DEVICE UNAUTHORIZED");

        Assert.Equal("device_unauthorized", result.MappingId);
    }

    [Fact]
    public void Translate_MatchingIsFirstMatchWins()
    {
        var translator = new ErrorTranslator();

        // "flashing is not allowed" harus menang sebelum pola FAILED (remote:.
        var result = translator.Translate("FAILED (remote: 'Flashing is not allowed')");

        Assert.Equal("bootloader_locked", result.MappingId);
    }

    [Fact]
    public void Translate_GenericRemoteFailed_IsUsedWhenNothingMoreSpecificMatches()
    {
        var translator = new ErrorTranslator();

        var result = translator.Translate("FAILED (remote: 'something else entirely')");

        Assert.Equal("remote_failed", result.MappingId);
    }

    [Fact]
    public void CustomMappings_AreUsed()
    {
        var mappings = new[]
        {
            new ErrorMapping
            {
                Id = "custom",
                Patterns = new List<string> { "kode-khas" },
                Title = "Judul",
                Explanation = "Penjelasan",
                Solution = "Solusi"
            }
        };

        var translator = new ErrorTranslator(mappings);

        var result = translator.Translate("terjadi kode-khas di sini");

        Assert.Equal("custom", result.MappingId);
        Assert.Equal("Judul", result.Title);
    }

    [Fact]
    public void TranslatedError_Formatted_ContainsAllParts()
    {
        var translator = new ErrorTranslator();

        var result = translator.Translate("error: device unauthorized");

        Assert.Contains(result.Title, result.Formatted);
        Assert.Contains(result.Explanation, result.Formatted);
        Assert.Contains(result.Solution, result.Formatted);
    }
}
