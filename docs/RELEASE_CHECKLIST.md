# Checklist Rilis v1.0.0

Semua yang sudah Otomatis (sudah dikerjakan dan terverifikasi) ditandai ✅.
Yang masih manual ditandai ⬜ — bagian itu butuh perangkat nyata atau akun GitHub Anda.

## Sudah selesai dan terverifikasi ✅

- [x] Versi aplikasi dikunci di `1.0.0` (`src/DuckFlash/DuckFlash.csproj`)
- [x] `dotnet build -c Release` lolos, 0 warning / 0 error
- [x] 274 unit test lulus (`dotnet vstest`, test live download dikecualikan)
- [x] `.exe` single-file dipublish: `artifacts/publish/DuckFlash.exe` (155,4 MB)
- [x] SHA256 dihitung dan disimpan di `artifacts/publish/DuckFlash.exe.sha256`
- [x] Metadata versi di `.exe` terbaca benar (ProductVersion `1.0.0`)
- [x] Aplikasi hasil publish dijalankan sungguhan: window terbuka, platform-tools
      terdeteksi, logo tampil, tidak ada error XAML
- [x] Label "Application version / Versi aplikasi" tampil di menu Pengaturan
- [x] Release notes ditulis di `docs/RELEASE_NOTES.md`
- [x] `README.md` diperbarui (fitur lengkap, batasan jujur, link repo dikoreksi)
- [x] Sisa file uji (`app-stdout.txt`, `app-stderr.txt`) masuk `.gitignore`

## Chek SHA256 hasil publish

Jalankan dari folder `artifacts\publish`:

```powershell
Get-FileHash .\DuckFlash.exe -Algorithm SHA256
```

Harus sama dengan isi file `DuckFlash.exe.sha256`:

```
43ff332781ffc070c426a55f9d8c8243a19ca7b6b786f2e48f986c8fa5c1513e
```

Kalau berbeda, publish ulang dengan `.\scripts\publish.ps1`.

---

## Yang masih manual ⬜

### 1. Init git dan push ke GitHub

Mesin ini belum punya `git` terpasang dan folder ini belum berupa git repo.

```powershell
# Instal git dulu (winget install --id Git.Git -e)
git init
git add .
git commit -m "DuckFlash v1.0.0"
git remote add origin https://github.com/Dikdikduck/DuckFlash.git
git branch -M main
git push -u origin main
```

Pastikan `artifacts/` tidak ikut ter-commit — sudah ada di `.gitignore`.

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

Catatan: `RELEASE_NOTES.md` memakai path relatif (`docs/screenshot-main.png`) untuk
gambar. GitHub.me-resolve path relatif terhadap branch default, jadi gambarnya muncul
kalau file-nya sudah ada di `main`. Kalau tidak muncul, hapus baris `<img ...>` itu
dan lampirkan `docs/screenshot-main.png` sebagai asset rilis (lalu pakai URL asset-nya).

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
