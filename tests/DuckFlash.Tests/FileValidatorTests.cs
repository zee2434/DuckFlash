using DuckFlash.Models;
using DuckFlash.Services;
using Xunit;

namespace DuckFlash.Tests;

/// <summary>
/// Uji validasi file sebelum flash atau sideload.
/// </summary>
public class FileValidatorTests : IDisposable
{
    private readonly string _tempDirectory;

    public FileValidatorTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "duckflash-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
        catch (IOException)
        {
            // Cleanup tidak boleh menggagalkan test.
        }
    }

    [Fact]
    public void ValidateFlashImage_ValidFile_ReturnsDetails()
    {
        var path = CreateFile("recovery.img", "isi image partisi");

        var result = FileValidator.ValidateFlashImage(path);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal("recovery.img", result.FileName);
        Assert.True(result.FileSize > 0);
        Assert.Equal(64, result.Sha256.Length);
        Assert.True(result.IsFlashImage);
        Assert.False(result.IsSideloadPackage);
    }

    [Fact]
    public void ValidateFlashImage_NullPath_IsRejected()
    {
        var result = FileValidator.ValidateFlashImage(null);

        Assert.False(result.IsValid);
        Assert.Equal("File belum dipilih.", result.ErrorMessage);
    }

    [Fact]
    public void ValidateFlashImage_MissingFile_IsRejected()
    {
        var path = Path.Combine(_tempDirectory, "hilang.img");

        var result = FileValidator.ValidateFlashImage(path);

        Assert.False(result.IsValid);
        Assert.Contains("tidak ditemukan", result.ErrorMessage);
    }

    [Fact]
    public void ValidateFlashImage_WrongExtension_IsRejected()
    {
        var path = CreateFile("recovery.zip", "bukan image");

        var result = FileValidator.ValidateFlashImage(path);

        Assert.False(result.IsValid);
        Assert.Contains(".img", result.ErrorMessage);
    }

    [Fact]
    public void ValidateFlashImage_EmptyFile_IsRejected()
    {
        var path = CreateFile("kosong.img", string.Empty);

        var result = FileValidator.ValidateFlashImage(path);

        Assert.False(result.IsValid);
        Assert.Contains("kosong", result.ErrorMessage);
    }

    [Fact]
    public void ValidateSideloadPackage_ValidZip_ReturnsDetails()
    {
        var path = CreateZip("update.zip");

        var result = FileValidator.ValidateSideloadPackage(path);

        Assert.True(result.IsValid, result.ErrorMessage);
        Assert.Equal("update.zip", result.FileName);
        Assert.True(result.IsSideloadPackage);
        Assert.False(result.IsFlashImage);
    }

    [Fact]
    public void ValidateSideloadPackage_NotAZip_IsRejected()
    {
        var path = CreateFile("palsu.zip", "ini bukan zip sama sekali");

        var result = FileValidator.ValidateSideloadPackage(path);

        Assert.False(result.IsValid);
        Assert.Contains("zip", result.ErrorMessage);
    }

    [Fact]
    public void ValidateSideloadPackage_WrongExtension_IsRejected()
    {
        var path = CreateFile("update.img", "bukan zip");

        var result = FileValidator.ValidateSideloadPackage(path);

        Assert.False(result.IsValid);
        Assert.Contains(".zip", result.ErrorMessage);
    }

    [Fact]
    public void ComputeSha256_MatchesKnownValue()
    {
        var path = CreateFile("test.img", "abc");

        var hash = FileValidator.ComputeSha256(path);

        // SHA256 dari string "abc" yang sudah diketahui.
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", hash);
    }

    [Fact]
    public void ComputeSha256_IsLowercaseHex()
    {
        var path = CreateFile("test.img", "abc");

        var hash = FileValidator.ComputeSha256(path);

        Assert.Equal(hash.ToLowerInvariant(), hash);
        Assert.Equal(64, hash.Length);
    }

    [Fact]
    public void IsValidZip_RealZip_ReturnsTrue()
    {
        var path = CreateZip("nyata.zip");

        Assert.True(FileValidator.IsValidZip(path));
    }

    [Fact]
    public void IsValidZip_NotAZip_ReturnsFalse()
    {
        var path = CreateFile("bukan.zip", "bukan zip");

        Assert.False(FileValidator.IsValidZip(path));
    }

    [Fact]
    public void FlashPartition_OnlyAllowsListedPartitions()
    {
        Assert.True(FlashPartition.IsAllowed("recovery"));
        Assert.True(FlashPartition.IsAllowed("boot"));
        Assert.True(FlashPartition.IsAllowed("vendor_boot"));
        Assert.True(FlashPartition.IsAllowed("init_boot"));
        Assert.True(FlashPartition.IsAllowed("vbmeta"));
        Assert.True(FlashPartition.IsAllowed("dtbo"));

        Assert.False(FlashPartition.IsAllowed("system"));
        Assert.False(FlashPartition.IsAllowed("super"));
        Assert.False(FlashPartition.IsAllowed("cache"));
        Assert.False(FlashPartition.IsAllowed(null));
        Assert.False(FlashPartition.IsAllowed(string.Empty));
    }

    private string CreateFile(string name, string content)
    {
        var path = Path.Combine(_tempDirectory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private string CreateZip(string name)
    {
        var path = Path.Combine(_tempDirectory, name);

        using var archive = System.IO.Compression.ZipFile.Open(
            path, System.IO.Compression.ZipArchiveMode.Create);

        var entry = archive.CreateEntry("payload.txt");
        using var writer = new StreamWriter(entry.Open());
        writer.Write("isi paket");

        return path;
    }
}
