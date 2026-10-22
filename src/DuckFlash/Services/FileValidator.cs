using System.Security.Cryptography;
using DuckFlash.Models;

namespace DuckFlash.Services;

/// <summary>
/// Memeriksa file sebelum flash atau sideload: ekstensi, ukuran, dan hash SHA256.
/// </summary>
public static class FileValidator
{
    /// <summary>Ekstensi yang boleh untuk fastboot flash.</summary>
    public const string FlashExtension = ".img";

    /// <summary>Ekstensi yang boleh untuk adb sideload.</summary>
    public const string SideloadExtension = ".zip";

    /// <summary>Ekstensi yang boleh untuk adb install.</summary>
    public const string ApkExtension = ".apk";

    /// <summary>File dengan ukuran 0 byte ditolak.</summary>
    public const long MinimumFileSize = 1;

    /// <summary>
    /// Batas atas yang masih masuk akal. File di atas ini hampir pasti bukan
    /// image partisi yang wajar, jadi ditolak supaya tidak salah pilih file.
    /// </summary>
    public const long MaximumFileSize = 4L * 1024 * 1024 * 1024;

    /// <summary>
    /// Validasi file untuk fastboot flash. Hanya .img yang diterima.
    /// </summary>
    public static FileValidationResult ValidateFlashImage(string? filePath)
    {
        return Validate(filePath, FlashExtension, requireZipContent: false);
    }

    /// <summary>
    /// Validasi file untuk adb sideload. Hanya .zip yang diterima.
    /// </summary>
    public static FileValidationResult ValidateSideloadPackage(string? filePath)
    {
        return Validate(filePath, SideloadExtension, requireZipContent: true);
    }

    /// <summary>
    /// Validasi file APK untuk adb install. Hanya .apk yang diterima.
    /// </summary>
    public static FileValidationResult ValidateApk(string? filePath)
    {
        return Validate(filePath, ApkExtension, requireZipContent: true);
    }

    private static FileValidationResult Validate(
        string? filePath,
        string expectedExtension,
        bool requireZipContent)
    {
        return Validate(filePath, expectedExtension, requireZipContent, LocalizationService.Default);
    }

    /// <summary>
    /// Validasi file dengan sumber teks tertentu, supaya pesan error bisa
    /// mengikuti bahasa yang dipilih pengguna.
    /// </summary>
    public static FileValidationResult Validate(
        string? filePath,
        string expectedExtension,
        bool requireZipContent,
        ITextProvider text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Invalid(string.Empty, text.Get("validate.fileMissing"));
        }

        var fullPath = Path.GetFullPath(filePath);
        var fileName = Path.GetFileName(fullPath);
        var extension = Path.GetExtension(fullPath);

        if (!File.Exists(fullPath))
        {
            return Invalid(fullPath, text.Get("validate.fileNotFound", fileName));
        }

        if (!extension.Equals(expectedExtension, StringComparison.OrdinalIgnoreCase))
        {
            return Invalid(fullPath, text.Get("validate.extension", expectedExtension, extension));
        }

        var info = new FileInfo(fullPath);

        if (info.Length < MinimumFileSize)
        {
            return Invalid(fullPath, text.Get("validate.emptyFile", fileName));
        }

        if (info.Length > MaximumFileSize)
        {
            return Invalid(fullPath, text.Get("validate.tooLarge", fileName));
        }

        if (requireZipContent && !IsValidZip(fullPath))
        {
            return Invalid(fullPath, text.Get("validate.notZip", fileName));
        }

        return new FileValidationResult
        {
            IsValid = true,
            FilePath = fullPath,
            FileName = fileName,
            FileSize = info.Length,
            Sha256 = ComputeSha256(fullPath),
            IsFlashImage = expectedExtension == FlashExtension,
            IsSideloadPackage = expectedExtension == SideloadExtension
        };
    }

    /// <summary>
    /// Hitung hash SHA256 file.
    /// </summary>
    public static string ComputeSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();

        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Cek apakah file benar-benar arsip zip yang bisa dibuka.
    /// </summary>
    public static bool IsValidZip(string filePath)
    {
        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(filePath);
            return archive.Entries.Count > 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static FileValidationResult Invalid(string filePath, string message)
    {
        return new FileValidationResult
        {
            IsValid = false,
            FilePath = filePath,
            ErrorMessage = message
        };
    }
}
