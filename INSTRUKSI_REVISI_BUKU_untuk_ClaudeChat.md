# Instruksi Revisi Buku PA (BAB 3 & BAB 4) — untuk Claude Chat

> **Cara pakai:** salin SELURUH isi file ini ke Claude Chat bersama file buku
> (`BAB_3_Pengembangan_Sistem.docx`, `BAB_4_Eksperimen_dan_Analisis.docx`).
> Dokumen ini adalah **sumber kebenaran** isi HFSM hasil sinkronisasi dengan kode
> Unity (per 22 Juni 2026). Jika ada konflik antara draft buku lama dan dokumen
> ini, **ikuti dokumen ini**.

Identitas: NRP 5222600090 — Mochamad Hafido Sudirto.
Judul (sesuai penyesuaian Bu Tia): **"Perancangan dan Implementasi Mekanik Gameplay
Karakter Pemain pada Game Action Combat 2D *The Other You*"**. Fokus = **mekanik
karakter pemain**, BUKAN AI/NPC/DDA.

---

## 0. KONTEKS REVISI (kenapa harus diubah)

Revisi dosen yang harus dijawab:
- **Bu Tia (Artiarini):** "Implementasi HFSM masih sangat belum terlihat pada BAB 3
  & BAB 4 — revisi buku total"; "pemberian nomor sub-bab masih berantakan";
  "screenshot tidak dilampirkan, buat ulang"; "implementasi FSM tidak muncul
  visualnya, kurang detail".
- **Pak Zakha (format):** tidak ada paragraf menjorok; istilah asing *italic*;
  tiap gambar/tabel dirujuk & dibahas **minimal 3 kalimat**; bagian bawah halaman
  tidak boleh kosong (maks 2 baris); *heading 1* / awal lampiran di halaman ganjil;
  sumber di bawah nama gambar `(Sumber: ...)` bukan link; penomoran gambar/tabel
  tanpa spasi setelah titik; sub-bab 1.6 memuat poin DAFTAR PUSTAKA.
- **Catatan rekan (berlaku umum):** kode jangan tangkapan-layar—jadikan
  **pseudocode**, source asli taruh di **Lampiran**; tabel jangan lewat 1 halaman &
  beri kolom No.; jangan ada halaman kosong (terutama BAB 3); BAB 5 kesimpulan
  harus memuat **data kuantitatif** dari pengujian.

**INTI MASALAH yang ditemukan:** draft buku, draft revisi sebelumnya, dan kode
Unity dulu memakai struktur HFSM yang BERBEDA-BEDA. Sekarang sudah dikunci satu
struktur kanonik (Bab 1 di bawah). Pakai itu di SELURUH buku (BAB 3 & 4) supaya
konsisten dengan demo yang akan dilihat penguji.

---

## 1. STRUKTUR HFSM KANONIK (WAJIB dipakai konsisten)

Hierarki dua tingkat, **7 leaf state** di bawah **3 superstate**:

```
Karakter Pemain (HFSM)
├── Locomotion (superstate)        → kondisi hidup & terkendali, tidak beraksi
│     ├── Locomotion/Idle          → diam (kondisi awal)
│     ├── Locomotion/Move          → bergerak mengikuti input arah
│     └── Locomotion/Dash          → dash/menghindar
├── Combat (superstate)            → sedang melakukan aksi tempur
│     ├── Combat/Sword             → aksi dengan senjata Sword
│     └── Combat/Bow               → aksi dengan senjata Bow (termasuk charge Full Draw)
└── Interrupt (superstate)         → kondisi yang menyela aksi
      ├── Interrupt/Staggered      → terkena serangan (terhuyung)
      └── Interrupt/Dead           → HP nol (kalah)
```

Catatan penting: **superstate tidak pernah menjadi state aktif sendiri** — mesin
selalu mendarat di salah satu *leaf state*. Superstate hanya untuk pengelompokan
dan jalur nama (mis. "Combat/Sword").

---

## 2. PRINSIP KEJUJURAN (HARUS tercermin di tulisan — jangan over-claim)

HFSM ini bersifat **observasional / read-only**:
- HFSM **MEMBACA** kondisi yang sudah ada (senjata aktif, penanda penguncian gerak,
  penanda menyerang, status stagger, HP, kecepatan, status dash, status blokir regen
  energi) lalu **menyimpulkan** state yang sedang aktif.
- HFSM **TIDAK** mengubah gameplay, **TIDAK** meng-gate energy/cooldown, dan **TIDAK**
  memanggil skill/dash. Pengaturan energy, cooldown, dan eksekusi skill tetap berada
  di skrip mekanik (MoveKeyboard, Dash, Sword_*, Bow_*). HFSM hanya **mencerminkan**
  hasilnya.
- Karena itu, **JANGAN** menulis "transisi ke Combat terjadi jika energy mencukupi".
  Yang benar: "HFSM berpindah ke Combat **ketika mengamati** penanda aksi aktif
  (penguncian gerak / sedang menyerang); pengecekan energy dilakukan oleh skrip
  skill, bukan oleh HFSM."
- Saat ini metode Enter/Update/Exit tiap state **masih kosong** (belum mengubah
  perilaku). Rencana menjadikannya pengendali ditulis sebagai **Saran/future work**
  di BAB 5 (lihat Bab 7 dokumen ini), bukan diklaim sudah berjalan.

Kalimat aman untuk dipakai: *"Instrumen ini bersifat read-only terhadap mekanik
permainan dan tidak mengubah perilaku karakter; ia menjadi alat validasi state,
bukan bukti bahwa seluruh mekanik dikendalikan oleh HFSM."*

---

## 3. ISI UNTUK **BAB 3** (model ADDIE — sisipkan pada sub-bab yang sudah ada)

Pertahankan kerangka ADDIE buku: 3.1 Deskripsi Solusi · 3.2 Analyze · 3.3 Design ·
3.4 Development · 3.5 Implementation · 3.6 Evaluation. HFSM masuk di **3.3.2,
3.4.2, 3.5.4**. Rapikan penomoran sub-bab (keluhan Bu Tia) agar berurutan & konsisten.

### 3.3.2 Perancangan HFSM Karakter Pemain (DESIGN)

Buka dengan 1 paragraf: tujuan HFSM (menata kondisi karakter agar mekanik tidak
bertabrakan), alasan memilih *hierarchical* (aturan umum cukup ditulis sekali di
*superstate*). Lalu rujuk **Gambar 3.3** (diagram hierarki) dan bahas ≥3 kalimat.

**Gambar 3.3 — Diagram Hierarki HFSM Karakter Pemain** *(Sumber: Dokumentasi
Penulis)*. Gambar = pohon hierarki pada Bab 1 dokumen ini (akar → Locomotion/Combat/
Interrupt → leaf). Mahasiswa membuat diagramnya (boleh dari `DIAGRAM_HFSM.md`).

**Tabel 3.5 — Struktur dan Fungsi State HFSM Karakter Pemain**

| No. | State | Tipe | Fungsi |
|---|---|---|---|
| 1 | Locomotion | Superstate | Memayungi kondisi saat karakter hidup & terkendali, tidak sedang beraksi |
| 2 | Locomotion/Idle | Substate | Diam saat tidak ada input gerak; kondisi awal & tempat kembali |
| 3 | Locomotion/Move | Substate | Bergerak mengikuti input arah |
| 4 | Locomotion/Dash | Substate | Gerak cepat menghindar (dash) berjeda |
| 5 | Combat | Superstate | Memayungi kondisi saat karakter melakukan aksi tempur |
| 6 | Combat/Sword | Substate | Aksi dengan senjata *Sword* (Slash Combo, Charged Strike, Whirlwind, Riposte) |
| 7 | Combat/Bow | Substate | Aksi dengan senjata *Bow* (Quick Shot, Full Draw, Spread, dll); fase *charge* Full Draw ditandai pada pemicu |
| 8 | Interrupt | Superstate | Memayungi kondisi yang menyela aksi karakter |
| 9 | Interrupt/Staggered | Substate | Reaksi saat terkena serangan; menyela kondisi berjalan |
| 10 | Interrupt/Dead | Substate | Kondisi akhir saat HP nol; seluruh aksi diblokir |

**Tabel 3.6 — Transisi State HFSM Karakter Pemain** (berbasis prioritas; HFSM
mengamati penanda lalu memilih state. Prioritas: Dead > Staggered > Combat > Dash >
Move > Idle).

| No. | State Asal | State Tujuan | Kondisi yang Diamati HFSM |
|---|---|---|---|
| 1 | (kondisi awal) | Locomotion/Idle | Karakter aktif tanpa input gerak |
| 2 | Locomotion/Idle | Locomotion/Move | Ada input arah (kecepatan di atas ambang) |
| 3 | Locomotion/Move | Locomotion/Idle | Input arah berhenti (kecepatan di bawah ambang) |
| 4 | Locomotion/Idle, Move | Locomotion/Dash | Dash sedang berjalan (penanda dash aktif) |
| 5 | Locomotion (semua) | Combat/Sword | Penanda aksi aktif & senjata aktif = Sword |
| 6 | Locomotion (semua) | Combat/Bow | Penanda aksi aktif & senjata aktif = Bow |
| 7 | Combat/Sword, Bow | Locomotion/Idle, Move | Aksi/skill selesai (penanda aksi mati) |
| 8 | Locomotion/Move, Combat | Interrupt/Staggered | Karakter terkena serangan (status stagger aktif) |
| 9 | Interrupt/Staggered | Locomotion/Idle | Pemulihan stagger selesai |
| 10 | Semua state | Interrupt/Dead | HP mencapai nol |

Catatan untuk teks pembahasan (bukan masuk tabel): energy & cooldown TIDAK menjadi
syarat transisi HFSM — itu diurus skrip skill/dash; HFSM hanya mengamati hasil
penandanya. Tuliskan ini eksplisit agar jujur.

**Tabel 3.7 — Relasi HFSM dengan Komponen Mekanik**

| No. | Komponen | Peran dalam Mekanik | Hubungan dengan HFSM |
|---|---|---|---|
| 1 | CharacterBase | Atribut & perilaku dasar (HP, energy, stagger) | Menyediakan data yang **dibaca** HFSM (HP, status stagger, status blokir regen) |
| 2 | Player | Identitas & penanda aksi karakter | Menyediakan senjata aktif & penanda (penguncian gerak, menyerang) dasar kondisi Combat |
| 3 | MoveKeyboard | Penggerak karakter | Kecepatan hasilnya **dibaca** untuk membedakan Idle/Move |
| 4 | Dash | Gerak cepat berjeda (energy + cooldown) | Status dash aktif **dibaca** untuk kondisi Locomotion/Dash |
| 5 | SkillBase / Sword_* / Bow_* | Eksekusi skill senjata | Menulis penanda aksi; HFSM membacanya menjadi Combat/Sword atau Combat/Bow |
| 6 | Energy (di CharacterBase) | Sumber daya skill | Status blokir regen **dibaca** untuk melabeli fase *charge* (bukan syarat transisi) |
| 7 | Cooldown & recovery | Pembatas tempo aksi | Diurus skrip mekanik; HFSM tidak menegakkan, hanya mencerminkan hasil |
| 8 | Stagger (di CharacterBase) | Reaksi terkena serangan | Status stagger **dibaca** untuk kondisi Interrupt/Staggered |
| 9 | HP (di CharacterBase) | Ketahanan karakter | HP nol **dibaca** sebagai syarat Interrupt/Dead |

Tutup 3.3.2 dengan 1 paragraf: HFSM = lapisan **koordinasi/observasi**; tiap
komponen tetap pelaksana perilaku & pemegang data.

### 3.4.2 Pengembangan Struktur HFSM (DEVELOP)

**Tabel 3.10 — Komponen Implementasi Struktur HFSM**

| No. | Komponen (file) | Peran |
|---|---|---|
| 1 | IPlayerState | Antarmuka kontrak state: Name + Enter/Tick/Exit |
| 2 | PlayerBaseState | Kelas dasar state; menyimpan relasi induk → membentuk hierarki & FullPath |
| 3 | PlayerStateMachine | Pengelola state aktif; menjalankan perpindahan (Exit lama → set → Enter baru) |
| 4 | PlayerStateController | Membaca penanda & kondisi karakter, memilih state sesuai prioritas (kebijakan transisi terpusat) |
| 5 | PlayerStates | Kumpulan state konkret (Idle, Move, Dash, Sword, Bow, Staggered, Dead + superstate) |
| 6 | PlayerStateHud | Visualisasi state di Game View (label di kepala + panel) — read-only |
| 7 | PlayerStateLabels | Lapisan terjemahan istilah teknis → bahasa awam untuk HUD |

Sertakan **pseudocode** kontrak state (JANGAN tangkapan layar kode; source asli ke
Lampiran):

```
antarmuka IPlayerState:
    Name                      // nama state, mis. "Sword"
    onEnter()                 // disiapkan saat state mulai aktif
    onUpdate()                // dijalankan berulang selama state aktif
    onExit()                  // dibersihkan saat state berakhir

setiap frame pada PlayerStateController:
    baca kondisi karakter (HP, stagger, senjata, penanda aksi, dash, kecepatan)
    pilih state tujuan berdasarkan prioritas:
        jika HP <= 0                          -> Interrupt/Dead
        selain itu jika sedang stagger        -> Interrupt/Staggered
        selain itu jika sedang beraksi:
            jika senjata = Bow                -> Combat/Bow
            selain itu                        -> Combat/Sword
        selain itu jika sedang dash           -> Locomotion/Dash
        selain itu jika bergerak              -> Locomotion/Move
        selain itu                            -> Locomotion/Idle
    jika state tujuan != state sekarang: lakukan perpindahan & catat transisi
```

### 3.5.4 Implementasi HFSM pada Karakter Pemain (IMPLEMENT)

Jelaskan: HFSM diterapkan pada karakter pemain saat gim berjalan, dengan
mempertahankan mekanik yang sudah ada. Untuk membuktikan state berpindah sesuai
rancangan dan **menampilkannya secara visual** (menjawab keluhan "FSM tidak muncul
visualnya"), disediakan dua instrumen:

1. **Instrumen di Inspector** (`PlayerStateController`) — Tabel 3.11.
2. **Indikator state di Game View** (`PlayerStateHud`) — label di atas kepala
   karakter (warna: hijau=Locomotion, oranye=Combat, merah=Interrupt) + panel
   ringkas berbahasa awam. Default mati, diaktifkan dengan tombol F1.

**Tabel 3.11 — Field Instrumen Observasi State pada PlayerStateController**

| No. | Field | Fungsi |
|---|---|---|
| 1 | currentStateDebug | State yang sedang aktif, mis. Combat/Sword |
| 2 | previousStateDebug | State sebelum transisi terakhir |
| 3 | lastTransitionDebug | Transisi terakhir, mis. Locomotion/Move -> Combat/Sword |
| 4 | lastTriggerDebug | Pemicu transisi terakhir, mis. "skill sword aktif" |
| 5 | recentTransitionsDebug | Riwayat maksimum enam transisi bermakna terakhir |

**Gambar 3.8 — Instrumen Observasi State HFSM (Inspector + indikator Game View)**
*(Sumber: Dokumentasi Penulis)*. Bahas ≥3 kalimat. Tegaskan sifat read-only
(pakai kalimat aman di Bab 2 dokumen ini).

---

## 4. ISI UNTUK **BAB 4** (Eksperimen & Analisis)

HFSM divalidasi terutama lewat **Black Box Testing** + tampilan state.

### 4.6.2 Hasil Black Box Testing — **Tabel 4.9: Validasi State HFSM**

Untuk tiap aksi, bandingkan state yang **diharapkan** vs **teramati** (dari indikator
HUD / currentStateDebug). Isi kolom Hasil setelah uji coba nyata.

| No. | Aksi Pemain | State Diharapkan | State Teramati (HUD) | Hasil |
|---|---|---|---|---|
| 1 | Diam tanpa input | Locomotion/Idle | … | … |
| 2 | Tekan arah kiri/kanan | Locomotion/Move | … | … |
| 3 | Tekan dash (Left Shift) | Locomotion/Dash | … | … |
| 4 | Serang dengan Sword | Combat/Sword | … | … |
| 5 | Tembak skill Bow | Combat/Bow | … | … |
| 6 | Tahan Full Draw (Bow) | Combat/Bow (pemicu: charge) | … | … |
| 7 | Terkena serangan musuh | Interrupt/Staggered | … | … |
| 8 | HP habis | Interrupt/Dead | … | … |

**Gambar 4.5 — Tampilan State HFSM (indikator Game View) pada Setiap Kondisi**
*(Sumber: Dokumentasi Penulis)*. Susun montase dari tangkapan layar pada
`PANDUAN_SCREENSHOT.md` (Diam, Bergerak, Dash, Serangan Pedang, Serangan Panah,
Terhuyung, Kalah). Bahas ≥3 kalimat.

### 4.7.2 Analisis Black Box Testing
Hubungkan kembali ke permasalahan: tunjukkan bahwa seluruh kondisi yang dirancang
benar-benar tercapai & teramati, sehingga mekanik karakter berjalan terkoordinasi.
Sajikan **persentase kesesuaian** (mis. 8/8 = 100%) sebagai data kuantitatif.

---

## 5. ATURAN FORMAT (terapkan di seluruh revisi)

1. Font Times New Roman 12; spasi 1,5; margin kiri 4 cm, lainnya 3 cm.
2. **Tanpa indentasi menjorok** pada paragraf.
3. Istilah asing **italic**: *state*, *superstate*, *substate*, *Hierarchical Finite
   State Machine* (HFSM), *Sword*, *Bow*, *dash*, *charge*, *read-only*, *Inspector*,
   *Game View*, dll. (singkatan baku & nama field kode boleh tegak).
4. Setiap **gambar & tabel** wajib **dirujuk dalam teks** dan **dibahas ≥3 kalimat**.
5. Penomoran: "Gambar 3.x" / "Tabel 3.x" (nomor bab.urut, **tanpa spasi setelah
   titik**). Caption gambar **di bawah** gambar, rata tengah, tebal; `(Sumber: …)`
   di bawah gambar. Caption tabel **di atas** tabel.
6. Setiap tabel beri **kolom No.**; satu tabel **tidak lewat 1 halaman**.
7. **Tidak ada bagian/halaman kosong** (maks 2 baris kosong di bawah).
8. Kode → **pseudocode**; *source code* asli letakkan di **Lampiran**.
9. Penomoran sub-bab konsisten & berurutan (perbaiki yang berantakan): 3.x → 3.x.y;
   bila perlu rincian: A. / B. lalu 1) / 2) lalu a) / b) atau *bullet*.
10. Daftar Pustaka format **IEEE**, urut sesuai panduan.

---

## 6. CHECKLIST KONSISTENSI (verifikasi setelah revisi)

- [ ] Semua penyebutan state di BAB 3 & 4 memakai 7 leaf state kanonik (Bab 1).
- [ ] Tidak ada lagi struktur lama (Run, Sword Skill/Bow Skill terpisah, Combat
      tunggal, atau Charging/Attacking/Casting).
- [ ] Tidak ada klaim energy/cooldown sebagai syarat transisi HFSM (Bab 2 kejujuran).
- [ ] Gambar 3.3, 3.8, 4.5 ada, dirujuk, dibahas ≥3 kalimat, ada (Sumber: …).
- [ ] Tabel 3.5, 3.6, 3.7, 3.10, 3.11, 4.9 memakai isi dokumen ini + kolom No.
- [ ] Judul & narasi fokus **mekanik karakter pemain**, bukan AI/NPC.
- [ ] BAB 5 Kesimpulan memuat data kuantitatif (mis. % kesesuaian black box).

---

## 7. UNTUK BAB 5 (Saran / future work) — opsional tapi memperkuat

Sebutkan keterbatasan jujur: HFSM saat ini **observasional**. Rencana lanjutan
(*Fase 2*) adalah migrasi bertahap menjadi pengendali (mengisi Enter/Update/Exit
dan memindah kepemilikan penanda ke state), dimulai dari Locomotion, lalu Interrupt,
terakhir Combat — satu mekanik per langkah dengan uji regresi. Ini menunjukkan
arsitektur sudah modular & siap dikembangkan, tanpa meng-overclaim kondisi sekarang.
