# Build DuckFlash menjadi .exe single-file yang bisa dibagikan.
#
# Hasil publish diletakkan di artifacts\publish\DuckFlash.exe
# File .exe bersifat self-contained, jadi tidak butuh .NET runtime di komputer pengguna.
#
# Cara pakai:
#   .\scripts\publish.ps1

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\DuckFlash\DuckFlash.csproj"
$outputPath = Join-Path $repoRoot "artifacts\publish"

Write-Host "Membersihkan folder output..." -ForegroundColor Cyan

if (Test-Path $outputPath) {
    Remove-Item $outputPath -Recurse -Force
}

Write-Host "Restore untuk runtime win-x64..." -ForegroundColor Cyan

dotnet restore $projectPath -r win-x64

Write-Host "Build dan publish..." -ForegroundColor Cyan

dotnet publish $projectPath `
    -c Release `
    -r win-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $outputPath

$exePath = Join-Path $outputPath "DuckFlash.exe"

if (-not (Test-Path $exePath)) {
    throw "Publish selesai tapi DuckFlash.exe tidak ditemukan di $outputPath"
}

Write-Host ""
Write-Host "Selesai." -ForegroundColor Green

$exeInfo = Get-Item $exePath
$sizeMb = [math]::Round($exeInfo.Length / 1MB, 1)

Write-Host "  File  : $exePath"
Write-Host "  Ukuran: $sizeMb MB"
Write-Host ""

# Hitung SHA256 supaya bisa diunggah ke GitHub Releases.
$hash = (Get-FileHash -Path $exePath -Algorithm SHA256).Hash.ToLowerInvariant()
$hashPath = Join-Path $outputPath "DuckFlash.exe.sha256"

Set-Content -Path $hashPath -Value "$hash  DuckFlash.exe" -Encoding ascii

Write-Host "  SHA256: $hash"
Write-Host "  Disimpan juga di: $hashPath"
Write-Host ""
Write-Host "Unggah DuckFlash.exe dan DuckFlash.exe.sha256 ke GitHub Releases." -ForegroundColor Yellow
