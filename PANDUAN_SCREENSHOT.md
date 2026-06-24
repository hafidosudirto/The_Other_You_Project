# Panduan Screenshot Bukti HFSM (untuk Sidang)

Tujuan: menghasilkan satu set foto yang membuktikan HFSM pemain **bekerja dan
terlihat**. Ikuti urutan ini. Estimasi 10–15 menit.

> Catatan: HFSM ini **observasional** (membaca status, tidak mengubah gameplay).
> Itu fakta yang jujur dan tetap layak ditunjukkan — lihat `LAPORAN_IMPLEMENTASI_HFSM.md`.

---

## A. Persiapan (sekali saja)

### A.1 Pasang komponen HUD
1. Buat GameObject kosong di scene gameplay: **GameObject → Create Empty**, beri
   nama `HFSM_Debug`. (Menaruhnya di objek terpisah membuat HUD tetap jalan
   walau prefab player berganti senjata.)
2. Klik `HFSM_Debug` → **Add Component** → cari **Player State Hud** → tambahkan.
3. Biarkan semua referensi kosong — komponen mencari sendiri saat Play
   (`controller`, `followTarget`, `worldCamera` auto-isi).
4. Opsional: di komponen, set **Panel Corner** = `TopLeft`, **Base Font Size** =
   `14` (naikkan ke 18–20 bila layar besar).

> `PlayerStateController` sudah terpasang di prefab Player (yang membuat field
> debug muncul di Inspector). Jika belum, tambahkan juga komponen tersebut ke
> GameObject Player.

### A.2 Atur Game View
1. Buka **Game** view → set **Aspect/Resolution** ke nilai tetap, mis.
   **1920×1080 (16:9)**, **Scale = 1×**. Ini membuat panel & label terbaca tajam.
2. Pastikan **Maximize On Play** MATI agar bisa lihat Inspector + Game bersamaan
   bila perlu.

### A.3 Nyalakan HUD
- Tekan **Play**, lalu tekan **F1** untuk menyalakan HUD (default mati).
  (Atau centang **Enable Hud** di komponen `PlayerStateHud` sebelum Play.)

Warna label = kategori state: **hijau** Locomotion · **oranye** Combat · **merah** Interrupt.

---

## B. Daftar Screenshot yang Harus Diambil

Ambil 11 foto berikut. Label di kepala kini memakai **bahasa awam** (mis.
"● Serangan Pedang"); bukti teknis jalur state tetap ada di panel baris **"Status FSM"**
dan di Inspector. Warna: hijau = gerak, oranye = tempur, merah = terganggu.

| # | Nama file saran | Isi yang harus terlihat | Cara memunculkan |
|---|---|---|---|
| 1 | `01_struktur_folder.png` | Project window: `Assets/Scripts/Player/StateMachine/` dengan 7 file (`IPlayerState`, `PlayerBaseState`, `PlayerStateMachine`, `PlayerStates`, `PlayerStateController`, `PlayerStateHud`, `PlayerStateLabels`). | Tidak perlu Play. Buka folder di Project window. |
| 2 | `02_inspector_debug.png` | Inspector `PlayerStateController` saat Play: grup **Debug (read-only)** terisi (`currentStateDebug`, `lastTransitionDebug`, `lastTriggerDebug`, `recentTransitionsDebug`). | Play, pilih GameObject Player di Hierarchy, gerak + serang sebentar, screenshot Inspector. |
| 3 | `03_idle.png` | Label **hijau** `● Diam`; panel `Sekarang: Diam` / `Status FSM: Locomotion/Idle`. | Diam, tidak menekan tombol apa pun. |
| 4 | `04_move.png` | Label **hijau** `● Bergerak` (`Locomotion/Move`). | Tekan kiri/kanan (A/D atau panah). |
| 5 | `05_dash.png` | Label **hijau** `● Menghindar (Dash)` (`Locomotion/Dash`), penyebab "menekan tombol dash". | Tekan **Left Shift** (dash). Foto saat dash berlangsung — singkat, coba beberapa kali / Pause cepat. |
| 6 | `06_combat_sword.png` | Label **oranye** `● Serangan Pedang` (`Combat/Sword`), penyebab "memakai skill pedang". | Pakai **Sword**, lakukan serangan (Slash Combo / Charged Strike / Whirlwind). |
| 7 | `07_combat_bow.png` | Label **oranye** `● Serangan Panah` (`Combat/Bow`), penyebab "memakai skill panah". | Pakai **Bow**, tembak skill non-charge (QuickShot / Spread / Piercing). |
| 8 | `08_combat_bow_charge.png` | Label **oranye** `● Serangan Panah` (`Combat/Bow`), penyebab **"menahan / charge panah (Full Draw)"**. | Pakai **Bow**, **tahan** skill **Full Draw** saat charge, foto di tengah charge. Menunjukkan detail fase charge lewat baris Penyebab. |
| 9 | `09_staggered.png` | Label **merah** `● Terhuyung` (`Interrupt/Staggered`). | Lihat trik B.1 di bawah (paling mudah & stabil). |
| 10 | `10_dead.png` | Label **merah** `● Kalah` (`Interrupt/Dead`). | Lihat trik B.2 di bawah. |
| 11 | `11_panel_riwayat.png` | Panel pojok layar: "Aksi terakhir" terisi beberapa baris (mis. `Bergerak → Serangan Pedang`) + "Penyebab" terakhir. | Setelah Diam→Bergerak→Serang→Skill dll., foto panelnya. |

### B.1 Trik memunculkan `Interrupt/Staggered` (mudah & stabil)
1. Saat Play, pilih GameObject Player di Hierarchy.
2. Di Inspector `Player`/`CharacterBase` → grup **Status Flags** → **centang
   `isStaggered`**.
3. HUD langsung berubah merah `● Terhuyung` (`Status FSM: Interrupt/Staggered`).
   Screenshot. Hapus centang untuk kembali normal.
   *(Alternatif organik: biarkan pemain terkena serangan musuh yang men-stagger.)*

### B.2 Trik memunculkan `Interrupt/Dead` tanpa player hilang
1. Saat Play, pilih GameObject Player.
2. Di Inspector → **Character Stats** → ubah **`currentHP`** menjadi **`0`**
   langsung di field (jangan lewat damage).
3. Karena HFSM membaca nilai HP secara polling sedangkan `Die()` hanya terpicu
   lewat `TakeDamage/SetHP`, mengetik `0` langsung di field **tidak**
   menghancurkan GameObject — sehingga label `● Kalah`
   (`Status FSM: Interrupt/Dead`, merah) tetap tampil untuk difoto. Screenshot,
   lalu kembalikan `currentHP` ke semula.
   *(Jika dibunuh musuh secara organik, state Dead hanya sekejap karena
   GameObject langsung di-destroy — ini perilaku game, bukan bug.)*

---

## C. Tips kualitas foto

- Aktifkan HUD (F1) **sebelum** memotret; pastikan label tidak ketutup UI lain.
- Untuk #2 (Inspector) dan #11 (panel), perbesar agar teks terbaca.
- Jika label kepala terlalu kecil, naikkan **Base Font Size** di `PlayerStateHud`
  atau **Head World Offset** Y bila label menimpa sprite.
- Untuk audiens paling awam, matikan **Show Technical Line** di `PlayerStateHud`
  agar baris "Status FSM" disembunyikan (sisakan bahasa awam saja).
- Lebar panel menyesuaikan otomatis, jadi teks tidak akan terpotong.
- Konsistenkan resolusi Game View di semua foto agar rapi saat ditempel di buku.

---

## D. Checklist akhir sebelum sidang

- [ ] 11 screenshot di atas sudah ada dan terbaca jelas.
- [ ] Foto struktur folder (7 file) terlampir.
- [ ] Bisa menjelaskan tabel transisi prioritas (Bab 4 `LAPORAN_IMPLEMENTASI_HFSM.md`)
      dan diagram (`DIAGRAM_HFSM.md`).
- [ ] Bisa menyatakan dengan jujur: "HFSM ini observasional — membaca status
      pemain dan memvisualkannya; kendali gameplay tetap di skrip combat."
- [ ] Bisa menunjuk satu foto gameplay (label di kepala) sebagai bukti FSM aktif.
