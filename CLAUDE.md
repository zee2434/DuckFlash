# CLAUDE.md

Konteks proyek untuk Claude Code. Baca file ini di awal setiap sesi, lalu baca `TODO.md` untuk tahu pekerjaan berikutnya.

## Tentang proyek

**DuckFlash** adalah aplikasi desktop Windows (`.exe`) yang memudahkan pemula mengoprek Android (custom ROM, custom recovery) tanpa mengetik command `adb` dan `fastboot` secara manual. Pengguna cukup klik tombol dan drag and drop file. Proyek ini open source dan akan dipublikasikan di GitHub.

Target pengguna: pemula. Semua teks UI, pesan error, dan penjelasan dalam **bahasa Indonesia** (dukungan English menyusul di v1.1).

## Stack

- C# dengan **.NET 8** dan **WPF**
- Pola **MVVM** dengan `CommunityToolkit.Mvvm`
- Publish sebagai **single-file, self-contained** `.exe` untuk `win-x64`:
  ```
  dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
  ```
- Jangan pakai Python/PyInstaller (sering false positive antivirus).
- Hindari dependency NuGet tambahan kalau tidak benar-benar perlu.

## Struktur folder

```
/src/DuckFlash/
  Models/        data murni (DeviceInfo, CommandResult, dll)
  Services/      logika: ToolsDownloader, AdbService, FastbootService, DriverService, ErrorTranslator
  ViewModels/    state dan command UI
  Views/         XAML
  Resources/     teks, ikon, daftar terjemahan error
/tests/DuckFlash.Tests/
TODO.md
CLAUDE.md
README.md
```

Logika tidak boleh ditaruh di code-behind XAML. Semua pemanggilan proses lewat `Services`.

## Aturan teknis penting

1. **Platform-tools tidak dibundel di repo.** Unduh dari `https://dl.google.com/android/repository/platform-tools-latest-windows.zip`, ekstrak ke `%LocalAppData%\DuckFlash\platform-tools`. Aplikasi memanggil `adb.exe` dan `fastboot.exe` dari folder itu dengan path penuh, tidak bergantung pada PATH.
2. **Jalankan proses dengan `System.Diagnostics.Process`**, output async, `CreateNoWindow = true`. Tangkap **stdout dan stderr**: `fastboot` menulis hampir semua outputnya ke **stderr**.
3. **Jangan blokir UI thread.** Semua pemanggilan adb/fastboot memakai `async/await` dan mendukung `CancellationToken`.
4. **Preview command.** Sebelum menjalankan perintah apa pun, tampilkan command lengkapnya di UI dan tulis ke log live. Pengguna harus bisa melihat command aslinya.
5. **Driver USB.** Jangan buat atau bundel driver sendiri. Unduh Google USB Driver dari repository resmi Google, lalu pasang lewat `pnputil /add-driver <inf> /install` dengan hak Administrator (minta elevasi hanya saat fitur ini dipakai).
6. **Multi-device.** Kalau lebih dari satu perangkat terdeteksi, wajib pakai `-s <serial>` pada setiap command.
7. **Penerjemah error.** Error mentah dari adb/fastboot dipetakan ke penjelasan Indonesia plus solusi di `ErrorTranslator`. Daftar pemetaan disimpan di `Resources/`, bukan hardcode tersebar. Wajib mencakup minimal: `device unauthorized`, `no permissions`, `waiting for any device`, `no devices/emulators found`, dan `FAILED (remote: ...)`.

## Aturan keamanan (anti-brick)

Fitur flashing berisiko membuat perangkat tidak bisa menyala. Aturan berikut wajib dipatuhi di kode:

- Blokir flash kalau bootloader terdeteksi **locked**, tampilkan alasannya.
- Validasi file sebelum jalan: `.img` untuk `fastboot flash`, `.zip` untuk `adb sideload`; tolak file kosong atau ukuran tidak masuk akal.
- Tampilkan **SHA256** file dan sediakan kolom opsional untuk membandingkan dengan hash dari developer.
- Dialog konfirmasi sebelum flash dengan **nama partisi dan nama file ditulis jelas**.
- Aksi destruktif (unlock/lock bootloader, wipe) harus ada peringatan besar bahwa **data akan terhapus** dan konfirmasi eksplisit.
- Partisi flash dipilih dari daftar terbatas: `recovery`, `boot`, `vendor_boot`, `init_boot`, `vbmeta`, `dtbo`.

## Aturan kerja untuk Claude

- Kerjakan **satu task per sesi/prompt** sesuai `TODO.md`. Jangan lanjut ke task berikutnya tanpa diminta.
- Setelah task selesai: pastikan `dotnet build` lolos tanpa error, centang task di `TODO.md` (`- [x]`), lalu ringkas perubahan.
- Untuk task besar, buat rencana singkat dulu sebelum menulis kode.
- **Jangan pernah menjalankan `fastboot flash`, `fastboot erase`, `fastboot oem unlock`, `fastboot flashing unlock/lock`, atau `adb sideload` pada perangkat nyata.** Pengujian flashing dilakukan manual oleh pemilik proyek. Untuk unit test, mock `Process` di belakang interface (misal `IProcessRunner`).
- Perintah aman untuk dicoba: `adb version`, `adb devices`, `fastboot --version`, `fastboot devices`, `getprop`, `getvar`.
- Jaga perubahan kecil dan terfokus. Jangan refactor file yang tidak terkait task.
- Komentar kode dan nama identifier dalam bahasa Inggris; teks yang tampil ke pengguna dalam bahasa Indonesia.

## Repo dan rilis

- Repo: `github.com/zee2434/DuckFlash`, nama file rilis `DuckFlash.exe`.
- Lisensi: MIT.
- `README.md` wajib memuat: deskripsi, screenshot, cara pakai, **disclaimer risiko flashing** (risiko ditanggung pengguna), dan catatan bahwa antivirus bisa memberi peringatan palsu pada tools seperti ini.
- Rilis lewat GitHub Releases (file `.exe` + checksum SHA256).
