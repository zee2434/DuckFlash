# Checklist Rilis v1.0.0

Semua yang sudah Otomatis (sudah dikerjakan dan terverifikasi) ditandai ✅.
Yang masih manual ditandai ⬜ — bagian itu butuh perangkat nyata atau akun GitHub Anda.

## Sudah selesai dan terverifikasi ✅

- [x] Versi aplikasi dikunci di `1.0.0` (`src/DuckFlash/DuckFlash.csproj`)
- [x] `dotnet build -c Release` lolos, 0 warning / 0 error
- [x] 274 unit test lulus (`dotnet vstest`, test live download dikecualikan)
- [x] `.exe` single-file dipublish: `artifacts/publish/DuckFlash.exe` (155,4 MB)
- [x] SHA256 dihitung dan disimpan di `artifacts/publish/DuckFlash.exe.sha256`
      (`6d56a9c1...`, hasil publish ulang setelah koreksi nama akun)
- [x] Metadata `.exe` terverifikasi: `ProductVersion 1.0.0`, `FileVersion 1.0.0.0`,
      `CompanyName Dikdikduck`
- [x] Metadata versi di `.exe` terbaca benar (ProductVersion `1.0.0`)
- [x] Aplikasi hasil publish dijalankan sungguhan: window terbuka, platform-tools
      terdeteksi, logo tampil, tidak ada error XAML
- [x] Label "Application version / Versi aplikasi" tampil di menu Pengaturan
- [x] Release notes ditulis di `docs/RELEASE_NOTES.md`
- [x] `README.md` diperbarui (fitur lengkap, batasan jujur, link repo dikoreksi)
- [x] Sisa file uji (`app-stdout.txt`, `app-stderr.txt`) masuk `.gitignore`
- [x] Nama akun GitHub diselaraskan ke `Dikdikduck` di semua file (README, LICENSE,
      `CLAUDE.md`, `UpdateService.cs`, csproj, test) — sebelumnya `Dikdiduck`,
      yang membuat fitur "Cek Versi Terbaru" menembak API repo yang tidak ada
- [x] `git` 2.55.0 dan `gh` 2.103.0 terpasang
- [x] Repo lokal di-init (branch `main`), commit pertama `15c381f`, tag `v1.0.0`

## Chek SHA256 hasil publish

Jalankan dari folder `artifacts\publish`:

```powershell
Get-FileHash .\DuckFlash.exe -Algorithm SHA256
```

Harus sama dengan isi file `DuckFlash.exe.sha256`:

```
6d56a9c11f1f1f727c669f8395ff21acb46a6e4caa8f2e2fa9d2ea4caad065fd
```

Kalau berbeda, publish ulang dengan `.\scripts\publish.ps1`.

---

## Yang masih manual ⬜

### 1. Init git dan push ke GitHub

`git` dan `gh` (GitHub CLI) sudah terpasang, folder sudah `git init`, sudah ada
commit pertama (`15c381f`) dan tag lokal `v1.0.0`. Yang tersisa: buat repo di
GitHub lalu push.

**1a. Buat repo (repo-nya belum ada — URL `Dikdikduck/DuckFlash` masih 404):**

Di browser, buka <https://github.com/new>, atau pakai `gh`:

```powershell
gh repo create DuckFlash --public --description "Tool sederhana untuk oprek Android (custom ROM, custom recovery) tanpa mengetik command adb/fastboot." --source . --remote origin --push
```

`gh` akan minta login lebih dulu kalau belum authenticated:

```powershell
gh auth login
```

**1b. Kalau memakai `git` langsung (bukan `gh`):**

```powershell
git remote add origin https://github.com/Dikdikduck/DuckFlash.git
git push -u origin main
git push origin v1.0.0
```

Pastikan `artifacts/` tidak ikut ter-commit — sudah ada di `.gitignore`, dan
repo lokal yang sudah dibuat benar-benar 1,9 MB (tanpa `.exe`).

### 2. Uji di perangkat nyata (WAJIB sebelum rilis)

CLAUDE.md melarang menjalankan `fastboot flash`, `fastboot erase`,
`fastboot oem unlock`, `fastboot flashing unlock/lock`, atau `adb sideload`
di perangkat nyata tanpa pengawasan manual. Uji berikut harus Anda lakukan sendiri:

**Tahap aman (tidak merusak):**
- [ ] Jalankan `DuckFlash.exe`, klik **Periksa Status** → platform-tools terdeteksi
- [ ] Klik **Check ADB** → perangkat Anda terdeteksi, tidak `unauthorized`
- [ ] Panel info perangkat benar: model, codename, versi Android, status bootloader, slot A/B
- [ ] Klik **Check Fastboot** setelah reboot ke bootloader → perangkat terdeteksi
- [ ] Pilih device di dropdown kalau ada lebih dari satu, pastikan `-s <serial>` muncul di log
- [ ] **Alat Perangkat → Ambil Screenshot** → file PNG muncul dan gambarnya benar
- [ ] **Alat Perangkat → Ekspor Logcat** → file teks terisi dan terbaca
- [ ] **Pasang Driver USB** (kalau perangkat belum terbaca) → UAC muncul, driver terpasang
- [ ] Ganti bahasa ke English di Pengaturan → seluruh UI berubah
- [ ] **Cek Versi Terbaru** → menampilkan versi 1.0.0

**Tahap berisiko (lakukan dengan hati-hati, punya file recovery siap):**
- [ ] Cadangkan aplikasi (Alat Perangkat → Cadangkan APK Aplikasi) → cek foldernya
- [ ] Debloater: centang 1 aplikasi yang tidak penting → **Hapus yang Dicentang** → berhasil
- [ ] Unlock bootloader → peringatan data terhapus muncul → konfirmasi → status jadi "terbuka"
- [ ] Flash file `.img` ke partisi `recovery` → dialog konfirmasi menyebut partisi + file → berhasil
- [ ] **Bandingkan Hash** → isi hash developer yang benar → hasilnya "cocok"; isi hash salah → hasilnya "TIDAK cocok"
- [ ] Sideload `.zip` dari recovery → berhasil
- [ ] Reboot ke system → Android hidup normal
- [ ] Wizard Custom ROM (Mulai Instalasi Custom ROM) → jalan dari langkah 1 sampai 7
- [ ] Reboot ke bootloader → `fastboot -w` (Hapus Data) → data hilang seperti yang tertulis di peringatan

### 3. Unggah ke GitHub Releases

Buat rilis baru di <https://github.com/Dikdikduck/DuckFlash/releases/new>:

- **Tag**: `v1.0.0`
- **Nama rilis**: `DuckFlash v1.0.0`
- **Target**: branch `main`
- **Deskripsi**: salin isi `docs/RELEASE_NOTES.md`
- **Upload asset**:
  - `artifacts/publish/DuckFlash.exe`
  - `artifacts/publish/DuckFlash.exe.sha256`
- Centang **Set as a pre-release** kalau belum siap, atau **Latest release** kalau sudah final.
- Opsional: lampirkan juga `docs/logo.png` sebagai tampilan di halaman rilis.

Alternatif pakai `gh` (setelah `gh auth login`):

```powershell
gh release create v1.0.0 artifacts/publish/DuckFlash.exe artifacts/publish/DuckFlash.exe.sha256 --title "DuckFlash v1.0.0" --notes-file docs/RELEASE_NOTES.md
```

Tambahkan `--latest` kalau ingin ditandai sebagai rilis terbaru.

Catatan gambar: `RELEASE_NOTES.md` memakai URL absolut
(`https://raw.githubusercontent.com/Dikdikduck/DuckFlash/main/docs/screenshot-main.png`).
Path relatif seperti `docs/screenshot-main.png` bisa gagal muncul di halaman rilis
karena me-render relatif terhadap URL rilis, bukan branch `main`. Kalau gambar tidak
tampil, cek dulu repo-nya sudah public dan branch `main` sudah ada file-nya.

### 4. Isi screenshot di README — SUDAH SELESAI ✅

- [x] Screenshot jendela utama → `docs/screenshot-main.png` (1946×1215)
- [x] Screenshot wizard instalasi custom ROM → `docs/screenshot-wizard.png` (1946×1215)
- [x] Screenshot Alat Perangkat → `docs/screenshot-device-tools.png` (1350×789)
- [x] Ketiganya sudah dipasang di README bagian "## Tampilan"

Screenshot diambil dari `.exe` hasil publish (bukan dari `dotnet run`), jadi yang tampil
benar-benar tampilan rilis. Bahasa UI di screenshot bahasa Indonesia.

Kalau nanti tampilan aplikasi berubah, ambil ulang dengan cara yang sama: jalankan
`artifacts\publish\DuckFlash.exe`, perkecil/besarkan jendela sesuai kebutuhan, lalu
simpan ulang ke `docs/`.

### 5. Setelah rilis

- [ ] Cek halaman rilis: `.exe` bisa diunduh, ukuran ~155 MB, hash sesuai
- [ ] Jalankan `.exe` yang diunduh di komputer/kolom lain → harus jalan tanpa install .NET
- [ ] Cek **Cek Versi Terbaru** → harus menampilkan "sudah terbaru (1.0.0)", bukan menawarkan v1.0.0

---

## Catatan versi berikutnya

Setiap rilis baru **wajib** menaikkan tiga nilai ini:

1. `<Version>` dan `<AssemblyVersion>`/`<FileVersion>` di `src/DuckFlash/DuckFlash.csproj`
2. Tag rilis GitHub, misal `v1.1.0`
3. Tanggal/isi `docs/RELEASE_NOTES.md`

Tanpa nomor 1, fitur **Cek Versi Terbaru** akan menganggap Anda masih di versi lama
dan terus menawarkan rilis yang sama.
