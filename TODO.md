# TODO

Daftar tugas proyek. Kerjakan **dari atas ke bawah, satu task per sesi**. Centang `[x]` setelah task selesai dan `dotnet build` lolos. Konteks teknis dan aturan ada di `CLAUDE.md`.

Legenda: `[ ]` belum, `[x]` selesai.

## Milestone 0: Fondasi

- [x] **0.1** Buat solution WPF .NET 8 (`DuckFlash`) dengan struktur folder sesuai `CLAUDE.md`, pasang `CommunityToolkit.Mvvm`, dan proyek tes `DuckFlash.Tests`
- [x] **0.2** Buat `IProcessRunner` + implementasi `ProcessRunner` (async, tangkap stdout dan stderr, `CancellationToken`, event output per baris)
- [x] **0.3** Buat `ToolsDownloader`: unduh dan ekstrak platform-tools ke `%LocalAppData%\DuckFlash\platform-tools`, dengan progress bar dan pengecekan apakah sudah terpasang
- [x] **0.4** Layout utama UI: area tombol aksi di kiri, panel log live di kanan, status bar di bawah

## Milestone 1: MVP (v1.0)

- [x] **1.1** `AdbService` dan `FastbootService`: jalankan command, kembalikan `CommandResult` (exit code, stdout, stderr)
- [x] **1.2** Tombol **Check ADB** (`adb devices`) dan **Check Fastboot** (`fastboot devices`) dengan status jelas (terdeteksi / tidak ada / unauthorized)
- [x] **1.3** Log live di UI + tombol **Copy log** dan **Clear log**
- [x] **1.4** Preview command: tampilkan command lengkap sebelum dijalankan dan catat di log
- [x] **1.5** Panel info perangkat: model, codename, versi Android, status bootloader (locked/unlocked), slot A/B (dari `adb shell getprop` dan `fastboot getvar all`)
- [x] **1.6** Kolom drag and drop file `.img` + dropdown partisi (`recovery`, `boot`, `vendor_boot`, `init_boot`, `vbmeta`, `dtbo`) + tombol **Flash**
- [x] **1.7** Validasi file: ekstensi, ukuran, dan tampilkan SHA256 + kolom opsional bandingkan hash
- [x] **1.8** Pengaman anti-brick: blokir flash saat bootloader locked, dialog konfirmasi dengan nama partisi dan file
- [x] **1.9** Drag and drop file `.zip` + tombol **Sideload** (`adb sideload`) dengan validasi dan konfirmasi
- [x] **1.10** Menu reboot: system, recovery, bootloader, fastbootd
- [x] **1.11** `ErrorTranslator`: pemetaan error umum ke penjelasan Indonesia + solusi (daftar di `Resources/`), tampil di UI saat command gagal
- [x] **1.12** Fitur **Install Driver USB**: unduh Google USB Driver, pasang via `pnputil` dengan elevasi Administrator, plus saran otomatis jika perangkat terbaca Windows tapi tidak muncul di `adb devices`
- [x] **1.13** Unit test untuk `ErrorTranslator`, parser output `getprop`/`getvar`, dan validasi file (pakai mock `IProcessRunner`)
- [x] **1.14** `README.md` (deskripsi, cara pakai, disclaimer risiko, catatan antivirus), lisensi MIT, dan script publish single-file `.exe`
- [ ] **1.15** **Uji manual oleh pemilik proyek** di perangkat nyata (mulai dari `adb devices`, lalu info perangkat, baru flashing), lalu rilis v1.0 di GitHub Releases

## Milestone 2: v1.1

- [x] **2.1** Pemilih multi-device (`-s <serial>`) di semua command
- [x] **2.2** Unlock/lock bootloader dengan peringatan besar data akan terhapus dan konfirmasi eksplisit
- [x] **2.3** Install APK lewat drag and drop (`adb install`)
- [x] **2.4** Auto-update: cek versi terbaru dari GitHub Releases dan beri notifikasi
- [x] **2.5** Dukungan bahasa Indonesia/English (resource file + pilihan bahasa di pengaturan)

## Milestone 3: v2.0

- [x] **3.1** Wizard "Install Custom ROM" langkah demi langkah: cek perangkat, unlock, flash recovery, wipe, sideload, reboot
- [x] **3.2** Debloater: daftar paket yang bisa dicentang, memakai `adb shell pm uninstall --user 0`
- [ ] **3.3** ADB wireless pairing (`adb pair`, `adb connect`)
- [x] **3.4** Backup/restore sederhana, screenshot, dan ekspor logcat

## Catatan sesi

Tulis di sini hal yang perlu diingat sesi berikutnya (keputusan desain, bug yang belum beres, dll):

- **Sesi 0.1 (fondasi)**
  - SDK .NET 8 (8.0.426) dipasang via `winget install Microsoft.DotNet.SDK.8`. XML SDK dari
    `Program Files\dotnet` awalnya tidak punya SDK, hanya runtime.
  - `NuGet.config` dibuat di root repo dengan `<clear />` + `nuget.org`. Config NuGet
    machine-wide di mesin ini bermasalah (`C:\Program Files (x86)\NuGet\Config\
    Microsoft.VisualStudio.FallbackLocation.config` gagal dibaca: "unauthorized access"),
    jadi restore harus memakai `--configfile NuGet.config`.
  - `Resources/` sudah dipakai di `.csproj` (`Content Include="Resources\**\*.json"`).
    `Resources/README.md` menjelaskan isi folder. File `error-mappings.json` menyusul di 1.11.
  - `Models/` sudah berisi `DeviceInfo`, `CommandResult`, dan `FlashPartition` (daftar partisi
    yang diizinkan: recovery, boot, vendor_boot, init_boot, vbmeta, dtbo).
  - `MainWindow` masih placeholder; layout asli dikerjakan di task 0.4.
  - Perintah publish single-file sudah diuji dan menghasilkan `DuckFlash.exe` (154 MB).
    Restore untuk publish perlu `dotnet restore -r win-x64` lebih dulu.
  - Belum ada `IProcessRunner`; mocks untuk testing belum bisa dibuat sampai task 0.2.

- **Sesi 0.2 (ProcessRunner)**
  - `Services/IProcessRunner.cs`: `RunAsync(fileName, arguments, CancellationToken)` mengembalikan
    `CommandResult`. Dua event: `CommandStarting` (preview command, dipanggil SEBELUM proses jalan)
    dan `OutputReceived` (per baris, dengan info stream stdout atau stderr).
  - `Services/ProcessRunner.cs`: pakai `System.Diagnostics.Process` dengan `CreateNoWindow = true`,
    `RedirectStandardOutput/Error = true`. PENTING: output `fastboot` hampir semua ke stderr,
    jadi keduanya wajib ditangkap dan dipisah di `CommandResult`.
  - Cancel: token dibatalkan -> proses di-`Kill(entireProcessTree: true)` lalu
    `OperationCanceledException` dilempar.
  - `ProcessStartException` (Services/ProcessStartException.cs) dilempar kalau executable tidak ada.
  - `QuoteArgument`Handles path/argumen yang mengandung spasi atau tanda kutip.
  - `ProcessRunner` sengaja TANPA constructor parameter; `fileName` datang dari pemanggil
    (service adb/fastboot di 1.1 yang tahu path platform-tools).
  - `InternalsVisibleTo("DuckFlash.Tests")` ditambahkan ke csproj supaya `QuoteArgument` (internal)
    bisa diuji.
  - CATATAN: di proyek WPF, implicit using TIDAK menyertakan `System.IO` -> harus `using System.IO;`
    eksplisit di file yang memakai `StreamReader`.
  - Tes memakai `cmd.exe` sebagai pengganti adb/fastboot (tidak butuh platform-tools terpasang).
    PENTING: `cmd.exe /c echo` menyisakan spasi di akhir baris, jadi assertion harus pakai `.Trim()`.

- **Sesi 0.3 (ToolsDownloader)**
  - `Services/IToolsDownloader.cs`: `CheckStatusAsync` (cek apakah sudah terpasang) dan
    `EnsureInstalledAsync` (pastikan ada, download kalau belum). Event `ProgressChanged`
    melaporkan persen 0-100 untuk progress bar.
  - `Services/ToolsDownloader.cs`: unduh dari
    `https://dl.google.com/android/repository/platform-tools-latest-windows.zip` pakai
    `HttpClient` streaming (`ResponseHeadersRead`), ekstrak pakai `ZipFile.ExtractToDirectory`.
  - Lokasi default: `%LocalAppData%\DuckFlash\platform-tools`. `AdbPath` dan `FastbootPath`
    mengembalikan path penuh ke exe (tidak bergantung PATH).
  - PENTING (bug yang sudah diperbaiki): folder install dihapus sebelum hasil ekstraksi
    dipindahkan ke sana. Karena itu zip dan folder staging HARUS berada di folder SAUDARA
    (`<root>.download` dan `<root>.staging`), bukan di dalam `<root>`. Kalau tidak, folder
    staging ikut terhapus dan ekstraksi selalu gagal.
  - `Models/ToolsStatus.cs` dan `Models/DownloadProgressEventArgs.cs` dibuat untuk ini.
  - `System.IO` dan `System.Net.Http` ditambahkan sebagai global `<Using>` di csproj karena
    WPF implicit usings tidak menyertakannya. `<Using>` harus DI LUAR `<PropertyGroup>`.
  - Verifikasi manual: platform-tools berhasil terpasang ke lokasi default, dan
    `adb.exe version` / `fastboot.exe --version` jalan dari path penuh
    (adb 37.0.1-15733141, fastboot 37.0.1-15733141).
  - `tests/DuckFlash.Tests/ToolsDownloaderLiveTests.cs` menguji unduhan sungguhan ke server
    Google. Test ini butuh internet, jadi kalau CI offline harus di-skip.
  - BELUM ada wiring ke UI. Progress bar dan tombol "Pasang platform-tools" baru dibuat
    di task 0.4 (layout utama) dan dipakai di task 1.2.

- **Sesi 0.4 (layout utama UI)**
  - `Views/MainWindow.xaml` sekarang layout sungguhan: **panel aksi di kiri** (Platform-tools,
    Perangkat, Flash, Reboot), **panel log live di kanan** (Copy log / Clear log),
    **status bar di bawah**. Dipisah `GridSplitter` agar lebarnya bisa diatur user.
  - `ViewModels/MainWindowViewModel.cs` sudah jadi ViewModel nyata: property
    `PlatformToolsStatus`, `DeviceStatus`, `StatusText`, `DownloadProgress`, `IsBusy`,
    `LogEntries` (ObservableCollection), plus command `Initialize`, `RefreshPlatformToolsStatus`,
    `InstallPlatformTools`, `CopyLog`, `ClearLog`.
  - `Models/LogEntry.cs` (pakai `LogSeverity`) + `Converters/BooleanToVisibilityConverter.cs`
    dibuat untuk kebutuhan ini.
  - Log dari thread proses dikumpulkan di `_pendingEntries` lalu di-flush ke ObservableCollection
    oleh `DispatcherTimer` (100 ms), jadi UI thread tidak pernah diblokir.
  - PENTING (bug yang sudah diperbaiki): memakai `<Grid>` dengan `RowDefinition Height="*"` +
    `Height="Auto"` membuat **status bar terdorong keluar jendela**. Penyebabnya isi kolom
    kiri (ScrollViewer dengan banyak tombol) tidak terikat batas tinggi. Solusinya: ganti
    root jadi `<DockPanel>` dengan `<StatusBar DockPanel.Dock="Bottom">`. Ini yang dipakai sekarang.
  - PENTING (XAML): nested markup extension seperti
    `Converter={StaticResource X}` di dalam `{}` binding GAGAL parse (error MC1000).
    Solusinya pakai property turunan di ViewModel (misal `CanRunCommand`), bukan converter.
    `BoolConverters` bawaan WPF juga sulit dipanggil dari XAML -> pakai converter sendiri.
  - CATATAN CommunityToolkit.Mvvm: `[RelayCommand] private async Task InitializeAsync()`
    menghasilkan command bernama `InitializeCommand` (suffix "Async" dibuang).
  - `[NotifyPropertyChangedFor(nameof(CanRunCommand))]` dipakai agar `CanRunCommand` ikut
    diperbarui saat `IsBusy` berubah.
  - Verifikasi visual: screenshot window via PrintWindow menunjukkan ketiga area tampil benar,
    status bar menampilkan "DuckFlash siap digunakan. | Platform-tools: terpasang di ...".
    PERHATIAN: `PrintWindow` dengan flag 0 hanya menangkap area client (title bar terpotong),
    dan monitor ini DPI 150% jadi ukuran bitmap harus memakai ukuran FISIK dari GetWindowRect.
  - Tombol Check ADB / Check Fastboot / Flash / Reboot sengaja masih nonaktif (IsEnabled=False)
    karena logikanya dibangun di task 1.1-1.10.

- **Sesi 1.1 (AdbService & FastbootService)**
  - `Services/AdbService.cs` dan `Services/FastbootService.cs`. Keduanya memakai `IProcessRunner`
    dan mengembalikan `CommandResult` (exit code, stdout, stderr terpisah).
  - `Services/IToolPaths.cs` interface baru: sumber path penuh ke adb.exe / fastboot.exe.
    `ToolsDownloader` sekarang implementasikan `IToolPaths` (punya `LocalPath`).
    JADI: `ToolsDownloader` = satu sumber kebenaran path platform-tools.
  - `Services/ToolsNotInstalledException.cs` dilempar kalau command dijalankan padahal
    platform-tools belum terpasang. Pesannya berbahasa Indonesia.
  - Multi-device: property `DeviceSerial` di service. Kalau diisi, argumen `["-s", serial]`
    disisipkan di depan argumen lain. Kalau kosong/spasi, flag tidak ditambahkan.
  - Event `OutputReceived` dan `CommandStarting` service meneruskan event dari runner
    (add/remove accessor), jadi UI cukup subscribe ke service.
  - `ProcessRunner` TIDAK implements IDisposable (Process dibuat per-panggilan RunAsync).
    Jadi `using var runner = new ProcessRunner()` GAGAL compile.
  - `tests/DuckFlash.Tests/FakeProcessRunner.cs` berisi mock `IProcessRunner` + `FakeToolPaths`.
    Dipakai lagi di task 1.13 dan seterusnya.
  - Verifikasi manual terhadap adb/fastboot sungguhan:
    `adb devices` -> exit 0, stdout "List of devices attached";
    `fastboot devices` -> exit 0, stdout kosong (tidak ada perangkat di fastboot);
    `adb shell getprop ...` -> exit 1 (benar, tidak ada perangkat terhubung).
  - BELUM dipakai dari ViewModel. Wiring ke tombol "Check ADB"/"Check Fastboot" ada di task 1.2.

- **Sesi 1.2 - 1.14 (MVP: UI, service, pengaman anti-brick)**
  - Service baru: `DeviceCheckService` (cek adb/fastboot + ambil detail perangkat),
    `DeviceOutputParser` (parse output `adb devices`, `fastboot devices`, `getprop`, `getvar`),
    `ErrorTranslator`, `FileValidator`, `FlashService`, `SideloadService`, `RebootService`,
    `DriverService`.
  - Model baru: `DeviceCheckStatus`/`DeviceCheckResult`, `RebootTarget`/`RebootTargetInfo`,
    `ErrorMapping`/`ErrorMappingFile`, `TranslatedError`, `FileValidationResult`, `FlashRequest`.
  - `Resources/error-mappings.json` berisi 11 pemetaan error (urutan penting: pola spesifik
    harus DILETAKKAN SEBELUM pola umum, misal `bootloader_locked` sebelum `remote_failed`).
  - PENTING (bug yang sudah diperbaiki): parser `getprop` awalnya meninggalkan kurung `[`
    di awal nilai, jadi `ro.product.model` terbaca `"[Pixel 7]"`. Perbaikannya: nilai di-trim
    lagi setelah `TrimStart('[')` dan `TrimEnd(']')`.
  - PENTING (bug yang sudah diperbaiki): `.exe` hasil publish single-file TIDAK ikut
    membawa `Resources/error-mappings.json`, jadi ErrorTranslator diam-diam jatuh ke
    pesan generik. Perbaikannya: JSON sekarang ditanam sebagai `EmbeddedResource`
    (`DuckFlash.Resources.error-mappings.json`) dan `ErrorTranslator` membacanya dari
    assembly DULU dari file di disk. Test `DefaultTranslator_MappingsAreEmbeddedInAssembly`
    menjaga hal ini.
  - PENTING (XAML): `CommandParameter` yang berisi string (misal `CommandParameter="Recovery"`)
    TIDAK bisa dipakai untuk command yangraftedargumen bertipe enum. CommunityToolkit
    melempar `ArgumentException` saat window dibuka, jadi aplikasi crash saat start.
    Perbaikannya: `RebootAsync` menerima `string`, lalu dikonversi dengan
    `Enum.TryParse<RebootTarget>`.
  - PENTING (XAML): komentar XML tidak boleh mengandung `--`. Tulisan
    `<!-- ---------- Judul ---------- -->` bikin error MC3000. Pakai `<!-- Judul -->` saja.
  - PENTING (test): `MessageBox.Show` di ViewModel bikin test HANG selamanya. Solusinya:
    dialog dipisah ke interface `IDialogService` (implementasi `MessageBoxDialogService`),
    dan test memakai `FakeDialogService`. Test yang memanggil command apa pun juga WAJIB
    memakai fake ini.
  - PENTING (test): log dari thread proses masuk antrean `_pendingEntries` dan di-flush
    oleh `DispatcherTimer`. Di unit test tidak ada loop dispatcher yang jalan, jadi log
    tidak pernah muncul. Perbaikannya: `FlushPendingEntries()` dipanggil di `finally`
    setiap command, dan `EnqueueLog` langsung menambahkan ke `LogEntries` kalau pemanggil
    sudah di thread UI.
  - `IToolsDownloader` sekarang mewarisi `IToolPaths`, jadi satu objek adalah sumber
    kebenaran untuk path platform-tools sekaligus untukmanagement unduhan.
  - `Converters/StringToVisibilityConverter.cs` dipakai untuk menyembunyikan panel
    detail perangkat kalau belum ada data.
  - Pengaman anti-brick aktif di ViewModel: `FlashService.ValidateRequest` memblokir
    partisi di luar daftar, bootloader terkunci, dan file tidak valid. Dialog konfirmasi
    (lewat `IDialogService`) selalu menyebut nama partisi dan nama file.
  - Verifikasi manual (lewat UI Automation, bukan flashing): aplikasi start, tombol
    "Check ADB" menjalankan `adb.exe devices` dari path penuh dan log menampilkan
    preview command (`> ...adb.exe devices`) + output asli + status Indonesia.
    "Check Fastboot" dan "Reboot Recovery" juga jalan; error `no devices/emulators found`
    diterjemahkan ke penjelasan Indonesia + solusi + detail error mentah.
  - Test: 136 test lulus (1 test live `ToolsDownloaderLiveTests` dikecualikan dari
    run cepat karena butuh internet).
  - Publish: `scripts\publish.ps1` membuat `artifacts\publish\DuckFlash.exe` (154 MB,
    single-file self-contained win-x64) plus `DuckFlash.exe.sha256`. Exe hasil publish
    sudah diverifikasi bisa dijalankan.
  - `README.md` + `LICENSE` (MIT) dibuat. Slot screenshot di README masih kosong,
    harus diisi pemilik proyek dengan screenshot aplikasi.
  - MASIH HARUS dikerjakan manual oleh pemilik proyek: task 1.15 (uji di perangkat
    nyata lalu rilis v1.0). Semua command berbahaya TIDAK pernah dijalankan oleh Claude.

- **Sesi 2.5 (bahasa Indonesia/English)**
  - `Resources/strings.id.json` + `Resources/strings.en.json`: katalog teks (~180 kunci per
    bahasa) untuk SEMUA teks yang tampil ke pengguna. Sumber kebenaran, bukan hardcode.
  - `Services/ITextProvider.cs` (interface `Get(key, params object?[] args)`) +
    `Services/LocalizationService.cs` sebagai implementasi. Service tidak lagi
    bergantung langsung ke `LocalizationService`, jadi mudah diuji.
  - `LocalizationService.Default` = teks Indonesia statis, dipakai service yang
    dikonstruksi tanpa ViewModel.
  - Semua teks dipindah ke katalog: ViewModel (~50 string), `DeviceCheckService`,
    `FlashService`, `SideloadService`, `InstallService`, `BootloaderService`,
    `ToolsDownloader`, `DriverService`, `UpdateService`, `MessageBoxDialogService`,
    `DeviceOutputParser`, `FileValidator`, plus seluruh XAML lewat `Text.*`.
  - `Models/LanguageOption.cs` + `SelectedLanguageOption` di ViewModel untuk ComboBox
    bahasa. `AppSettings` menyimpan pilihan bahasa; bahasa terakhir dibaca saat start.
  - PENTING (bug yang sudah diperbaiki): `LocalizationFile` awalnya tidak punya atribut
    `[JsonPropertyName]`. `System.Text.Json` pencocokan nama property itu SENSITIF HURUF
    BESAR secara default, jadi key `"strings"` tidak pernah terisi -> katalog kosong ->
    UI jatuh ke teks kunci mentah. Perbaikannya: tambahkan `[JsonPropertyName]` untuk
    `language`, `languageName`, `strings`.
  - PENTING (bug yang sudah diperbaiki): file `strings.id.json` / `strings.en.json`odi-MSBuild
    sebagai SATELLITE RESOURCE karena nama stem-nya (`strings.id`, `strings.en`) terbaca
    sebagai nama budaya. Akibatnya file-nya dikompilasi ke `en/DuckFlash.resources.dll` dan
    `id/DuckFlash.resources.dll`, BUKAN ke assembly utama -> .exe hasil publish single-file
    TIDAK ikut membawa katalog bahasa. `LogicalName` saja tidak cukup.
    Perbaikannya: tambah metadata `WithCulture="false"` pada item `EmbeddedResource`
    di `DuckFlash.csproj` (diterapkan juga ke `error-mappings*.json`).
    Test `LocalizationCoverageTests` membaca lewat `GetManifestResourceStream` supaya
    regresi ini ketahuan; `LocalizationService.LoadEmbedded` juga membaca dari assembly
    DULU dari file di disk.
  - Verifikasi: publish single-file lalu cek byte `.exe` memuat
    `DuckFlash.Resources.strings.id.json`, `DuckFlash.Resources.strings.en.json`, dan
    `DuckFlash.Resources.error-mappings.json`.
  - Test: 207 test lulus (termasuk 3 `LocalizationCoverageTests` yang memastikan kedua
    katalog punya kunci sama, tidak ada nilai kosong, dan placeholder format sama).
  - MASIH HARUS dikerjakan manual oleh pemilik proyek: isi slot screenshot di `README.md`.

- **Sesi 3.1 (wizard install custom ROM)**
  - `Models/WizardStep.cs`: enum `WizardStep` (CheckDevice, UnlockBootloader, FlashRecovery,
    WipeData, RebootToRecovery, SideloadRom, RebootToSystem) + `WizardStepState`
    (Pending, Running, Done, Failed, Waiting, Skipped).
  - `Models/WizardStepItem.cs`: item langkah yang `ObservableObject`, dipakai di ListBox
    sisi kiri wizard.
  - `Services/WipeService.cs`: `fastboot -w`. WAJIB konfirmasi destruktif sebelum dipanggil.
    `Validate(fastbootDetected)` mengembalikan alasan kalau perangkat tidak dalam mode fastboot.
  - `ViewModels/RomInstallWizardViewModel.cs`: state machine 7 langkah. Semua command tetap
    lewat service yang sudah ada (FlashService, SideloadService, BootloaderService,
    RebootService, WipeService), jadi aturan anti-brick tidak ditulis ulang.
  - `Views/RomInstallWizardWindow.xaml` + `.xaml.cs`: daftar langkah di kiri, area kerja
    di kanan (drop zone .img dan .zip, log, tombol Kembali/Jalankan/Berikutnya/Lewati).
    Code-behind hanya menangani file picker dan drag & drop, tidak ada logika.
  - `MainWindowViewModel.CreateRomInstallWizard()` membuat ViewModel wizard dengan service
    yang sama seperti jendela utama, jadi status perangkat dan bahasa aktif konsisten.
    Tombol "Mulai Instalasi Custom ROM" di `MainWindow.xaml` membuka wizard ini.
  - PENTING (duplikasi log): `AdbService.OutputReceived` dan `FastbootService.OutputReceived`
    KEDUANYA meneruskan event dari `ProcessRunner` yang sama. Kalau wizard subscribe ke
    keduanya, tiap baris output muncul dua kali. Fix: subscribe ke salah satu saja.
  - PENTING (XAML): property `Text.*` milik `UiText` tidak bisa dipakai di ViewModel
    yang hanya pegang `ITextProvider`. Solusinya, `RomInstallWizardViewModel` punya
    properti `Text` (UiText) yang dibangun dari `LocalizationService` yang sama.
  - Preview command tetap ditulis ke log wizard (`> ...fastboot.exe -w`) sesuai aturan CLAUDE.md.
  - Test: 228 test lulus. `RomInstallWizardViewModelTests` (20 test) memakai
    `FakeProcessRunner` + `FakeDialogService`, termasuk uji anti-brick: flash ditolak saat
    bootloader terkunci, flash ditolak tanpa file, wipe butuh konfirmasi destruktif,
    dan file .img/.zip divalidasi (ekstensi, ukuran, isi zip, SHA256).
    `WizardWindow_CreatesWithoutXamlErrors` membuat jendela di thread STA supaya error
    XAML ketahuan oleh test.
  - TIDAK ada command berbahaya yang dijalankan ke perangkat nyata. Semua uji lewat mock.

- **Sesi 3.2 (debloater)**
  - `Models/DebloatPackage.cs`: satu aplikasi yang bisa dicentang (`IsSelected`).
  - `Services/DebloatService.cs`: baca daftar aplikasi dengan
    `adb shell pm list packages -3` (aplikasi pengguna) dan `-s` (aplikasi sistem),
    hapus dengan `adb shell pm uninstall --user 0 <package>`.
  - PENTING (anti-brick): nama package divalidasi dulu (`IsValidPackageName`) supaya tidak
    ada karakterane yang bisa disisipkan ke shell perangkat.
  - `--user 0` hanya menghapus data untuk pengguna utama, jadi aplikasi sistem TIDAK hilang
    dan masih bisa dikembalikan. explained di teks konfirmasi.
  - `ViewModels/DebloatViewModel.cs`: daftar aplikasi + pencarian (cari nama package) +
    centang semua/hapus centang + jumlah yang dipilih. Penghapusan selalu lewat dialog
    konfirmasi destruktif (peringatan data tidak bisa dikembalikan).
  - `Views/DebloatWindow.xaml` + `.xaml.cs`: jendela daftar aplikasi dengan CheckBox per
    aplikasi, kotak pencarian, tombol Muat/Pilih Semua/Hapus Centangan/Hapus yang Dicentang,
    plus panel log. Tombol "Buka Debloater" ada di `MainWindow.xaml`.
  - PENTING (XAML): `IsEnabled="{Binding IsBusy, Converter={StaticResource InverseBool}}"`
    GAGAL (MC1000 = nested markup extension, dan converter-nya juga belum ada).
    Solusinya pakai properti turunan di ViewModel (`CanInteract => !IsBusy`), seperti
    `CanRunCommand` di jendela utama.
  - `tests/DuckFlash.Tests/DebloatServiceTests.cs`: uji parsing `pm list packages`, cek
    nama package, deteksi "Success", konfirmasi, dan ViewModel (penyaring, centang,
    konfirmasi wajib sebelum hapus). Pakai `ScriptedProcessRunner` (baru) yang bisa
    membalas berbeda tergantung argumen dan mencatat semua pemanggilan.
  - Test: 246 test lulus.
  - TIDAK ada aplikasi yang benar-benar dihapus dari perangkat nyata. Semua uji lewat mock.

- **Sesi 3.4 (alat perangkat: screenshot, logcat, backup/restore)**
  - Task 3.3 (ADB wireless) sempat dikerjakan, tapi pemilik proyek meminta DIHAPUS.
    Semua file dan katalog wireless sudah dibuang, 3.3 tetap `[ ]` di daftar tugas.
  - Service baru: `ScreenshotService`, `LogcatService`, `BackupService`.
  - `ScreenshotService`: `adb shell screencap -p /sdcard/duckflash_screenshot.png` ->
    `adb pull` -> `adb shell rm -f`. Cara ini dipakai karena isi PNG biner, sedangkan
    `IProcessRunner` hanya membaca output sebagai teks. (Alternatifnya `adb exec-out
    screencap -p` butuh dukungan output biner di `ProcessRunner`.)
  - `LogcatService`: `adb logcat -d -v threadtime` ditulis ke file teks, plus
    `adb logcat -c` untuk membersihkan buffer di perangkat.
  - `BackupService`: daftar aplikasi pengguna dari `pm list packages -3 -f`, lalu tiap
    APK ditarik dengan `adb pull` ke folder cadangan + file `backup-manifest.json`.
    Pemulihan memakai `adb install -r` (satu APK) atau `adb install-multiple -r` (split APK).
    PENTING: path APK di perangkat bisa mengandung tanda '=' (folder berhash), jadi
    parsing `pm list packages -f` HARUS memisah pada tanda '=' TERAKHIR, bukan pertama.
  - Keamanan: nama file dari manifest selalu dicek supaya tidak keluar dari folder
    cadangan (path traversal), dan pemulihan WAJIB ada konfirmasi karena memasang
    aplikasi di perangkat.
  - Batasan yang WAJIB disebut ke pengguna: backup ini hanya menyimpan file APK,
    data aplikasi (foto, pesan, setelan) TIDAK ikut dicadangkan dan tidak bisa
    dipulihkan. Ditulis di `hint.backupScope` dan di dialog konfirmasi pemulihan.
  - `ViewModels/DeviceToolsViewModel.cs` + `Views/DeviceToolsWindow.xaml`: tiga bagian
    (screenshot, logcat, backup/pemulihan) plus panel log. Dialog pilih file/folder
    ditangani di code-behind (itu urusan UI), semua logic di ViewModel/service.
  - `Microsoft.Win32.OpenFolderDialog` (baru di .NET 8) dipakai untuk memilih folder.
  - Model baru: `BackupPackage`, `BackupManifest`/`BackupManifestEntry`, `BackupResult`/`RestoreResult`.
  - Test: 271 test lulus. `tests/DuckFlash.Tests/DeviceToolsTests.cs` (25 test) memakai
    `ScriptedProcessRunner`; termasuk uji path traversal pada manifest, split APK, dan
    `DeviceToolsWindow_CreatesWithoutXamlErrors` (thread STA) supaya error XAML ketahuan.
  - TIDAK ada file yang benar-benar diambil dari perangkat dan tidak ada aplikasi yang
    benar-benar dipasang. Semua uji lewat runner palsu.
  - Catatan (bukan bug baru): jendela wizard/debloater/alat perangkat subscribe ke event
    `AdbService`, sedangkan `MainWindowViewModel` subscribe ke event `ProcessRunner` yang
    sama, jadi log di JENDELA UTAMA bisa menampilkan tiap baris dua kali selama jendela
    anak terbuka. Perlu dibersihkan di task terpisah.
  - MASIH HARUS dikerjakan manual oleh pemilik proyek: task 1.15 (uji di perangkat nyata
    lalu rilis v1.0) dan isi slot screenshot di `README.md`.
  - CATATAN: `README.md` SUDAH diperbarui di sesi logo. Bagian "Yang Tidak Bisa
    Dilakukan" sekarang hanya berisi hal yang memang belum ada: ADB wireless (3.3),
    backup data aplikasi (backup hanya menyimpan APK), hanya Windows, dan driver USB
    butuh hak Administrator. Wizard ROM, debloater, alat perangkat, install APK,
    multi-device, cek update, dan pilihan bahasa sudah dicatat semua. Slot screenshot
    masih kosong (menunggu rilis pertama). Link repo di README lama ada yang salah ketik
    (`Dikdiduck`), sekarang semuanya `Dikdikduck` sesuai nama akun GitHub pemilik proyek
    (`CLAUDE.md` dan `UpdateService` juga diselaraskan).

- **Sesi logo aplikasi**
  - Pemisahan file gambar:
    - `assets/` = sumber desain (asli, tidak dipakai runtime).
      `DuckFlashLogo.jpg` (1024px, ada wordmark) dan `DuckFlashLogo_notext_Transparent.png`
      (600px, bebek saja tanpa teks).
    - `docs/logo.png` = logo untuk README (512px, background putih).
    - `src/DuckFlash/Resources/Images/logo.png` = logo untuk UI (256px, transparan, di-crop
      dari alpha bounding box supaya bebeknya penuh).
    - `src/DuckFlash/Resources/Images/DuckFlash.ico` = ikon `.exe` (16/24/32/48/64/128/256).
  - Skrip `scripts/make-logo-assets.ps1` menghasilkan ulang tiga file turunan itu dari
    `assets/`. Aman dijalankan ulang kapan saja.
  - PENTING (WPF): gambar WPF harus build action `Resource` (bukan `Content`/`CopyToOutput`),
    kalau tidak file TIDAK ikut tertanam di dalam assembly dan `pack://application:,,,` gagal
    resolve saat aplikasi dijalankan sebagai single-file `.exe`.
  - PENTING (PowerShell): fungsi yang mengembalikan `byte[]` akan di-unroll jadi byte
    per-byte di pipeline. Untuk bikin ICO, byte[] harus dimasukkan ke `List[byte[]]`
    lewat `.Add()`, bukan dikembalikan dari fungsi.
  - PENTING (format ICO): field ICONDIR (`reserved`/`type`/`count`), ICONDIRENTRY
    (`planes`/`bitCount`), dan BITMAPINFOHEADER (`biPlanes`/`biBitCount`) lebarnya
    **2 byte (uint16)**, sedangkan `bytesInRes`/`imageOffset`/`biSizeImage` 4 byte (uint32).
    Kalau salah, `System.Drawing.Icon` menolak membacanya.
  - `<ApplicationIcon>Resources\Images\DuckFlash.ico</ApplicationIcon>` di `.csproj` =
    ikon file `DuckFlash.exe` di Explorer/taskbar. Semua window (utama, wizard, debloater,
    alat perangkat) juga sudah diberi `Icon="pack://application:,,,/DuckFlash;component/..."`.
  - Verifikasi: 271 test lulus, aplikasi sungguhan dijalankan dan logo terlihat di title bar
    serta header, `.exe` hasil publish carry ikon 32x32 yang valid dan resource `logo.png`
    di dalamnya. Publish terakhir: SHA256 `0efc429a21942ba694c33394bedc44882eac580c6d9250cf16d0a8f1a2fbbfc1`.

- **Sesi rilis v1.0.0 (persiapan)**
  - Versi aplikasi dikunci di `1.0.0` lewat `<Version>`, `<AssemblyVersion>`, dan
    `<FileVersion>` di `src/DuckFlash/DuckFlash.csproj`. PENTING: `<Version>` ini yang
    dibaca `UpdateService.ReadAssemblyVersion()`, jadi WAJIB menaikkan nomor ini di
    setiap rilis baru, kalau tidak fitur "Cek Versi Terbaru" akan terus menawarkan
    rilis yang sama.
  - `UpdateService.ReadAssemblyVersion()` diubah dari `private` jadi `public` supaya
    bisa dipakai ViewModel dan diuji.
  - Ditambahkan tampilan versi aplikasi di menu Pengaturan (`MainWindowViewModel.AppVersion` +
    label baru `label.appVersion` di `strings.id.json` dan `strings.en.json`).
    Dipakai supaya pengguna bisa menyebutkan versinya saat melapor bug.
  - Test baru: `AppVersion_MatchesAssemblyVersion`, `AppVersionLabel_TranslatedInBothLanguages`,
    `ReadAssemblyVersion_ReturnsSemanticVersion`. Total 274 test lulus.
  - File baru: `docs/RELEASE_NOTES.md` (deskripsi rilis untuk GitHub Releases) dan
    `docs/RELEASE_CHECKLIST.md` (checklist manual: init git, uji perangkat nyata,
    upload asset, isi screenshot README).
  - `app-stdout.txt` dan `app-stderr.txt` (sisa menjalankan aplikasi secara manual)
    sudah masuk `.gitignore`.
  - Publish: `artifacts/publish/DuckFlash.exe` (155,4 MB),
    SHA256 `43ff332781ffc070c426a55f9d8c8243a19ca7b6b786f2e48f986c8fa5c1513e`.
<<<<<<< HEAD
    ⚠️ Hash ini SUDAH TIDAK LAGI BERLAKU — `.exe` dipublish ulang beberapa kali
    setelah koreksi nama akun GitHub. Hash yang benar untuk diunggah:
    `148b836698ed5f2e478215a63b124542ec8448b4377fcdb4f05b06519b40a58f`.
=======
>>>>>>> 15c381f (DuckFlash v1.0.0)
  - Verifikasi: `.exe` hasil publish dijalankan sungguhan, window terbuka, platform-tools
    terdeteksi, logo tampil, ProductVersion terbaca `1.0.0`, dan label versi
    "Application version / 1.0.0" terdeteksi lewat UI Automation.
  - MASIH BELUM BISA dikerjakan di mesin ini (butuh git, akun GitHub, dan perangkat
    nyata): `git init` + push, `gh release create`, upload `.exe` ke GitHub Releases,
    dan pengujian flashing di perangkat nyata. Semua langkah itu ada di
    `docs/RELEASE_CHECKLIST.md`. Task 1.15 tetap `[ ]` sampai pemilik selesai mengujinya.

- **Sesi screenshot README**
  - Screenshot diambil dari `.exe` hasil publish (bukan `dotnet run`), jadi yang tampil
    benar-benar tampilan rilis. Bahasa UI di screenshot bahasa Indonesia.
  - Tiga gambar disimpan di `docs/`:
    `screenshot-main.png` (1946x1215), `screenshot-wizard.png` (1946x1215),
    `screenshot-device-tools.png` (1350x789). Semuanya dipasang di README bagian
    "## Tampilan".
  - Cara ambil screenshot di mesin ini (berguna kalau tampilan berubah nanti):
    jalankan `.exe`, pakai `PrintWindow` (user32) dengan flag 2 (PW_RENDERFULLCONTENT)
    supaya layer WPF ikut tergambar. PENTING: panggil
    `SetThreadDpiAwarenessContext(-4)` DULU sebelum `SetWindowPos`, kalau tidak
    koordinat resize jadi salah hitung karena DPI 150% di mesin ini (layar fisik
    1920x1200, logis 1280x800). Setelah itu `SetWindowPos` dengan ukuran FISIK
    (misal 2400x1300 = 1600x867 logical).
  - `PrintWindow` menghasilkan tepi hitam 1-2 px; dipangkas dengan memindai baris/kolom
    yang seluruhnya gelap di tepi gambar.
  - Bahasa aplikasi diubah ke Indonesia lewat `%LocalAppData%\DuckFlash\settings.json`
    (`{"Language": "Indonesian"}`) supaya screenshot berbahasa Indonesia.

<<<<<<< HEAD

- **Sesi persiapan rilis: koreksi nama akun GitHub + repo lokal**
  - PENTING: pada sesi ini diasumsikan nama akun GitHub adalah `Dikdikduck`.
    Asumsi itu SALAH - nama yang benar `zee2434` (lihat sesi berikutnya di bawah).
    Semua perubahan `Dikdikduck` yang dilakukan sesi ini sudah ditimpa lagi
    menjadi `zee2434`.
  - Yang benar dari sesi ini: `UpdateService.cs` memang perlu URL repo yang
    benar, dan itu bug nyata (bukan cuma typo di dokumen) - kalau repo tidak ada,
    fitur "Cek Versi Terbaru" gagal diam-diam.
  - Sudah diselaraskan di: `README.md`, `LICENSE`, `CLAUDE.md` (baris "Repo dan rilis"),
    `src/DuckFlash/Services/UpdateService.cs`, `src/DuckFlash/DuckFlash.csproj`
    (`<Company>`/`<Authors>`), `docs/RELEASE_CHECKLIST.md`, dan dua file test.
  - Verifikasi setelah perubahan: `dotnet build -c Release` 0 warning / 0 error,
    274 test lulus semua.
  - `git` (2.55.0) dan `gh` (2.103.0) dipasang lewat winget. Git Credential
    Manager ikut terpasang, jadi auth bisa lewat browser.
  - Repo lokal sudah di-init di branch `main`, commit pertama `15c381f`
    ("DuckFlash v1.0.0"), tag lokal `v1.0.0` sudah dibuat.
    Isi yang di-commit 1,9 MB - `artifacts/` dan `*.exe` tidak ikut karena
    sudah ada di `.gitignore`. File gambar `docs/*.png` sengaja ikut commit
    karena README dan release notes memakainya.
  - `RELEASE_NOTES.md`: gambar utama diganti dari path relatif
    (`docs/screenshot-main.png`) ke URL absolut
    (`https://raw.githubusercontent.com/zee2434/DuckFlash/main/docs/screenshot-main.png`).
    Path relatif berisiko tidak muncul di halaman GitHub Releases karena
    me-render relatif terhadap URL rilis, bukan branch `main`.
  - MASIH MANUAL oleh pemilik proyek (tidak dikerjakan oleh Claude):
    1. `gh auth login` lalu `gh repo create DuckFlash --public --source . --remote origin --push`
       (atau buat repo di web, lalu `git push -u origin main`).
    2. Uji di perangkat nyata (lihat `docs/RELEASE_CHECKLIST.md` bagian 2).
    3. Unggah `DuckFlash.exe` + `DuckFlash.exe.sha256` ke GitHub Releases dengan
       tag `v1.0.0`, deskripsi dari `docs/RELEASE_NOTES.md`.
- **Sesi perbaikan rilis: username sebenarnya zee2434 + tag belum ter-push**
  - KOREKSI PENTING: nama akun GitHub yang benar adalah `zee2434`, bukan
    `Dikdikduck`. Semua jejak `Dikdikduck`/`Dikdiduck` sudah diganti ke
    `zee2434` di README, LICENSE, CLAUDE.md, `UpdateService.cs`, csproj,
    dan test. Verifikasi dengan menyorot string di dalam `.exe` hasil publish:
    `api.github.com/repos/zee2434/DuckFlash/releases/latest`.
  - PENYEBAB UTAMA "Releases kosong": tag `v1.0.0` hanya ada di repo LOKAL.
    `git ls-remote --tags origin` mengembalikan kosong - tidak ada tag di remote.
    GitHub hanya bisa membuat rilis dari tag yang sudah ada di remote, jadi
    halaman Releases tetap kosong sampai tag di-push.
  - Repo ternyata SUDAH dibuat dan `main` sudah ter-push (commit `11b7e3c`).
    Repo private (API publik balas 404 tanpa token).
  - File `DuckFlash-v1.0.0-win-x64.zip` sempat ter-staged di git. Sudah
    di-unstage dan politteks `DuckFlash-v*-win-x64.zip` ditambahkan ke
    `.gitignore` - file 63 MB ini adalah asset rilis, bukan source code.
  - Publish ulang dilakukan karena `UpdateService.cs` berubah. SHA256 terbaru
    `148b836698ed5f2e478215a63b124542ec8448b4377fcdb4f05b06519b40a58f`
    (hash sebelumnya `6d56a9c1...` dan yang paling awal `43ff3327...`
    sudah tidak berlaku - jangan dipakai).
  - `dotnet build -c Release` 0 warning / 0 error, 274 test lulus.
  - MASIH MANUAL oleh pemilik proyek:
    1. `git push origin v1.0.0` - push tag (WAJIB, tanpa ini Releases kosong).
    2. Upload `DuckFlash.exe` + `DuckFlash.exe.sha256` ke GitHub Releases
       dengan tag `v1.0.0`, deskripsi dari `docs/RELEASE_NOTES.md`.
    3. Ubah repo jadi public (kalau mau APK ini bisa diunduh orang lain).
    4. Uji di perangkat nyata (lihat `docs/RELEASE_CHECKLIST.md` bagian 2).
=======
>>>>>>> 15c381f (DuckFlash v1.0.0)
