# DuckFlash v1.0.0

Rilis pertama DuckFlash.

Tool desktop Windows untuk membantu pemula mengoprek Android (custom ROM, custom
recovery) **tanpa perlu mengetik command `adb` atau `fastboot`**. Tinggal klik tombolnya.

<img src="docs/screenshot-main.png" alt="Tampilan DuckFlash" width="900" />

## Unduh

| File | Keterangan |
|---|---|
| `DuckFlash.exe` | Aplikasi single-file, sudah self-contained |
| `DuckFlash.exe.sha256` | Hash SHA256 untuk mengecek file tidak rusak |

Aplikasi ini **tidak perlu diinstal** dan **tidak butuh .NET runtime**. Cukup
unduh `DuckFlash.exe`, lalu jalankan.

## Yang bisa dilakukan

- **Pasang platform-tools sendiri** — unduh dari repository resmi Google, ekstrak ke
  `%LocalAppData%\DuckFlash\platform-tools`. Tidak perlu download manual.
- **Pasang Driver USB Google** — unduh dari repository resmi Google, pasang lewat
  `pnputil` dengan hak Administrator (elevasi hanya diminta saat fitur ini dipakai).
- **Cek perangkat** — `adb devices` dan `fastboot devices`, plus info lengkap:
  model, codename, versi Android, status bootloader, slot A/B aktif.
- **Multi-device** — kalau ada lebih dari satu perangkat, pilih serialnya; semua
  command otomatis memakai flag `-s <serial>`.
- **Flash `.img`** ke partisi `recovery`, `boot`, `vendor_boot`, `init_boot`,
  `vbmeta`, atau `dtbo`.
- **Sideload `.zip`** dari mode recovery.
- **Install `.apk`**.
- **Unlock / lock bootloader**, dengan peringatan besar bahwa semua data akan terhapus
  dan konfirmasi eksplisit.
- **Reboot** ke system, recovery, bootloader, atau fastbootd.
- **Wizard Instalasi Custom ROM** — 7 langkah: cek perangkat → unlock bootloader →
  flash recovery → hapus data → reboot ke recovery → sideload ROM → reboot ke Android.
- **Debloater** — hapus aplikasi yang tidak diperlukan dengan centang
  (`adb shell pm uninstall --user 0`).
- **Alat Perangkat** — ambil screenshot ke file PNG, ekspor logcat ke file teks,
  cadangkan/pulihkan aplikasi (file APK).
- **Cek versi terbaru** langsung dari GitHub Releases.
- **Bahasa Indonesia / English** — ganti di menu Pengaturan.
- **Preview command** — command `adb`/`fastboot` yang dijalankan ditampilkan live
  di panel log, plus penjelasan error dalam bahasa Indonesia kalau command gagal.

## Keamanan anti-brick

- Flash **diblokir** kalau bootloader terdeteksi terkunci, dengan alasan yang
  ditampilkan ke pengguna.
- File divalidasi sebelum jalan: ekstensi, ukuran, dan file kosong ditolak.
- **SHA256** file ditampilkan, plus kolom opsional untuk membandingkan dengan hash
  dari developer.
- Dialog konfirmasi sebelum flash menyebut **nama partisi dan nama file** dengan jelas.
- Aksi destruktif (unlock/lock bootloader, hapus data) menampilkan peringatan besar
  bahwa data akan terhapus, plus centang persetujuan eksplisit.

## ⚠️ Disclaimer Risiko

**Pengguna sendiri yang bertanggung jawab atas semua risiko yang muncul.**

Memasang custom ROM, flash partisi, unlock bootloader, atau menghapus data bisa
membuat perangkat Anda mati total (brick), kehilangan semua data, atau tidak bisa
menyala sama sekali. Beberapa perangkat bisa saja brick karena kesalahan pengguna,
file yang rusak, atau kabel USB yang putus di tengah jalan.

- Selalu **backup data** Anda sebelum melakukan flashing apa pun.
- Pastikan file yang Anda flash **cocok dengan model perangkat** Anda.
- Pastikan hash SHA256 file cocok dengan hash yang dipublikasikan developer.
- Gunakan file dari sumber tepercaya.

Proyek ini menyediakan alat bantu, bukan jaminan hasil.

## ⚠️ Catatan Antivirus

Aplikasi seperti DuckFlash (yang menjalankan `adb`/`fastboot`, mengubah partisi, dan
membuat proses elevate privilege) **sering dianggap mencurigakan** oleh antivirus,
terutama Kaspersky, ESET, dan beberapa antivirus lainnya.

Ini adalah **false positive** yang dikenal. Aplikasi ini open source dan bisa Anda
periksa sendiri kodenya. Kalau antivirus memblokir, tambahkan pengecualian untuk file
`DuckFlash.exe` sesuai panduan antivirus Anda.

## Yang belum ada di v1.0.0

- **ADB wireless pairing** (`adb pair` / `adb connect`) — belum ada, harus manual
  lewat command prompt.
- **Backup data aplikasi** — fitur cadangan hanya menyimpan file APK, bukan isi
  aplikasi (foto, pesan, setelan).
- **Hanya Windows** — belum ada versi macOS atau Linux.
- **Pemasangan driver USB butuh hak Administrator**.

## SHA256

```
SHA256  DuckFlash.exe
```

(nilai lengkap ada di file `DuckFlash.exe.sha256` yang diunggah bersama)

## Lisensi

MIT.
