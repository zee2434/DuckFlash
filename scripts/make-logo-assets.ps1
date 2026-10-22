# Generate aset logo DuckFlash dari file desain di folder assets\.
#
# Sumber:
#   assets\DuckFlashLogo_notext_Transparent.png  (burung tanpa teks,latar transparan)
#   assets\DuckFlashLogo.jpg                     (logo lengkap dengan tulisan DUCKFLASH)
#
# Hasil:
#   src\DuckFlash\Resources\Images\logo.png      (256x256, dipakai di dalam aplikasi)
#   src\DuckFlash\Resources\Images\DuckFlash.ico (16/24/32/48/64/128/256, ikon .exe)
#   docs\logo.png                                (512x512, untuk README.md dan GitHub)
#
# Jalankan ulang script ini kalau file desain di assets\ berubah:
#   .\scripts\make-logo-assets.ps1

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path -Parent $PSScriptRoot
$assetsFolder = Join-Path $repoRoot "assets"
$imagesFolder = Join-Path $repoRoot "src\DuckFlash\Resources\Images"
$docsFolder = Join-Path $repoRoot "docs"

$transparentSource = Join-Path $assetsFolder "DuckFlashLogo_notext_Transparent.png"
$logoSource = Join-Path $assetsFolder "DuckFlashLogo.jpg"

$appPngPath = Join-Path $imagesFolder "logo.png"
$iconPath = Join-Path $imagesFolder "DuckFlash.ico"
$readmePngPath = Join-Path $docsFolder "logo.png"

foreach ($path in @($imagesFolder, $docsFolder)) {
    if (-not (Test-Path $path)) {
        New-Item -ItemType Directory -Path $path | Out-Null
    }
}

# Potong bagian transparan di tepi gambar supaya logo tidak terlihat "melayang".
function Get-ContentBounds {
    param([System.Drawing.Bitmap]$Bitmap)

    $rect = New-Object System.Drawing.Rectangle(0, 0, $Bitmap.Width, $Bitmap.Height)
    $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    try {
        $buffer = New-Object byte[] ($data.Stride * $Bitmap.Height)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $buffer, 0, $buffer.Length)
    }
    finally {
        $Bitmap.UnlockBits($data)
    }

    $minX = $Bitmap.Width
    $minY = $Bitmap.Height
    $maxX = -1
    $maxY = -1

    for ($y = 0; $y -lt $Bitmap.Height; $y++) {
        $rowStart = $y * $data.Stride

        for ($x = 0; $x -lt $Bitmap.Width; $x++) {
            $alpha = $buffer[$rowStart + ($x * 4) + 3]

            if ($alpha -gt 8) {
                if ($x -lt $minX) { $minX = $x }
                if ($x -gt $maxX) { $maxX = $x }
                if ($y -lt $minY) { $minY = $y }
                if ($y -gt $maxY) { $maxY = $y }
            }
        }
    }

    if ($maxX -lt 0) {
        return $null
    }

    return [System.Drawing.Rectangle]::new($minX, $minY, ($maxX - $minX + 1), ($maxY - $minY + 1))
}

# Salin isi gambar ke kanvas persegi dengan sedikit ruang kosong di sekeliling.
function New-SquaredBitmap {
    param(
        [System.Drawing.Bitmap]$Source,
        [int]$Size,
        [double]$PaddingRatio = 0.04
    )

    $bounds = Get-ContentBounds -Bitmap $Source

    if ($null -eq $bounds) {
        throw "Gambar sumber kosong atau seluruhnya transparan."
    }

    $canvas = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)

    try {
        $graphics.Clear([System.Drawing.Color]::Transparent)
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

        $inner = [int]($Size * (1 - (2 * $PaddingRatio)))
        $offset = [int](($Size - $inner) / 2)

        $graphics.DrawImage(
            $Source,
            (New-Object System.Drawing.Rectangle($offset, $offset, $inner, $inner)),
            $bounds,
            [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally {
        $graphics.Dispose()
    }

    return $canvas
}

# Satu entri ICO: BITMAPINFOHEADER + piksel BGRA (bottom-up) + mask 1bpp.
#
# Data entri langsung dimasukkan ke list. PENTING: jangan mengembalikan array
# byte dari fungsi PowerShell, karena PowerShell mengurai array saat
# menuliskan ke pipeline dan hasilnya jadi kosong.
function Add-IcoEntry {
    param(
        [System.Drawing.Bitmap]$Bitmap,
        [System.Collections.Generic.List[byte[]]]$Entries
    )

    $size = $Bitmap.Width
    $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $data = $Bitmap.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    try {
        $pixels = New-Object byte[] ($data.Stride * $size)
        [System.Runtime.InteropServices.Marshal]::Copy($data.Scan0, $pixels, 0, $pixels.Length)
    }
    finally {
        $Bitmap.UnlockBits($data)
    }

    $xorSize = $size * $size * 4
    $maskStride = [int]([Math]::Ceiling($size / 32.0) * 4)
    $andSize = $maskStride * $size

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)

    # BITMAPINFOHEADER: tinggi dua kali tinggi gambar (gambar XOR + mask AND).
    # PENTING: biPlanes dan biBitCount berukuran 2 byte, bukan 4.
    $writer.Write([uint32]40)              # biSize
    $writer.Write([int]$size)              # biWidth
    $writer.Write([int]($size * 2))        # biHeight
    $writer.Write([uint16]1)               # biPlanes
    $writer.Write([uint16]32)              # biBitCount
    $writer.Write([uint32]0)               # biCompression (BI_RGB)
    $writer.Write([uint32]($xorSize + $andSize)) # biSizeImage
    $writer.Write([int]0)                  # biXPelsPerMeter
    $writer.Write([int]0)                  # biYPelsPerMeter
    $writer.Write([uint32]0)               # biClrUsed
    $writer.Write([uint32]0)               # biClrImportant

    # Piksel: baris dari bawah ke atas.
    for ($y = $size - 1; $y -ge 0; $y--) {
        $rowStart = $y * $data.Stride
        $writer.Write($pixels, $rowStart, $size * 4)
    }

    # Mask: semua nol, alpha di channel BGRA yang dipakai Windows.
    $writer.Write((New-Object byte[] $andSize))

    $writer.Flush()

    $Entries.Add($stream.ToArray())

    $writer.Dispose()
    $stream.Dispose()
}

Write-Host "Membuat aset logo..." -ForegroundColor Cyan

# 1. Logo untuk di dalam aplikasi (PNG transparan 256x256).
$source = [System.Drawing.Bitmap]::FromFile($transparentSource)
try {
    $appBitmap = New-SquaredBitmap -Source $source -Size 256
    try {
        $appBitmap.Save($appPngPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $appBitmap.Dispose()
    }
}
finally {
    $source.Dispose()
}

Write-Host "  $appPngPath"

# 2. Ikon .exe multi ukuran.
$source = [System.Drawing.Bitmap]::FromFile($transparentSource)
try {
    $sizes = @(16, 24, 32, 48, 64, 128, 256)
    $entries = New-Object 'System.Collections.Generic.List[byte[]]'
    $bitmaps = @()

    foreach ($size in $sizes) {
        $bitmap = New-SquaredBitmap -Source $source -Size $size -PaddingRatio 0.02
        $bitmaps += $bitmap
        Add-IcoEntry -Bitmap $bitmap -Entries $entries
    }

    $stream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($stream)

    # ICONDIR: reserved dan type 2 byte, count 2 byte.
    $writer.Write([uint16]0)    # reserved
    $writer.Write([uint16]1)    # type: icon
    $writer.Write([uint16]$sizes.Count)

    $offset = 6 + (16 * $sizes.Count)

    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $size = $sizes[$i]
        $bytes = $entries[$i]

        $dimension = [byte]$(if ($size -ge 256) { 0 } else { $size })

        $writer.Write($dimension)     # width
        $writer.Write($dimension)     # height
        $writer.Write([byte]0)        # jumlah warna palet
        $writer.Write([byte]0)        # reserved
        $writer.Write([uint16]1)      # planes
        $writer.Write([uint16]32)     # bitcount
        $writer.Write([uint32]$bytes.Length)
        $writer.Write([uint32]$offset)

        $offset += $bytes.Length
    }

    foreach ($bytes in $entries) {
        $writer.Write($bytes, 0, $bytes.Length)
    }

    $writer.Flush()

    [System.IO.File]::WriteAllBytes($iconPath, $stream.ToArray())

    $writer.Dispose()
    $stream.Dispose()

    foreach ($bitmap in $bitmaps) {
        $bitmap.Dispose()
    }
}
finally {
    $source.Dispose()
}

Write-Host "  $iconPath"

# 3. Logo untuk README (versi dengan tulisan, latar putih).
$source = [System.Drawing.Bitmap]::FromFile($logoSource)
try {
    $readme = New-Object System.Drawing.Bitmap(512, 512, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [System.Drawing.Graphics]::FromImage($readme)

    try {
        $graphics.Clear([System.Drawing.Color]::White)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.DrawImage($source, 0, 0, 512, 512)
    }
    finally {
        $graphics.Dispose()
    }

    try {
        $readme.Save($readmePngPath, [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $readme.Dispose()
    }
}
finally {
    $source.Dispose()
}

Write-Host "  $readmePngPath"

Write-Host ""
Write-Host "Selesai." -ForegroundColor Green
