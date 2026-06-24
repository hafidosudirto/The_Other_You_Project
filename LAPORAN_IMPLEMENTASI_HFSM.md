# Laporan Implementasi HFSM Pemain

**Project:** The Other You — Unity 2022.3.31f1 (2D action combat)
**Lingkup:** Hierarchical Finite State Machine (HFSM) untuk **karakter pemain**.
**Lokasi kode:** `Assets/Scripts/Player/StateMachine/`
**Sifat implementasi:** **Observasional / read-only** (lihat bagian *Keterbatasan Jujur*).

Dokumen ini berisi FAKTA dari kode yang berjalan — bukan klaim. Setiap pernyataan
dapat ditelusuri ke file dan kondisi yang konkret.

> **Catatan penempatan di buku PA:** isi dokumen ini disiapkan sebagai bahan
> **Bab 3 (Perancangan & Implementasi)**. Bab 2-4 buku tidak ditulis di sini.

---

## 1. Ringkasan satu paragraf (untuk dibaca saat sidang)

HFSM pemain adalah mesin status berlapis dua tingkat yang **membaca** kondisi
pemain yang sudah ada (flag aksi, HP, stagger, kecepatan Rigidbody2D) lalu
menyimpulkan **state aktif** pemain setiap frame. Keputusan transisi
**dipusatkan** di satu kelas (`PlayerStateController`) memakai urutan prioritas
yang jelas. State aktif divisualisasikan secara langsung di Game View lewat
`PlayerStateHud`, sehingga satu screenshot gameplay sudah membuktikan FSM
bekerja. HFSM ini **tidak mengubah** perilaku gameplay; ia bersifat
observasional agar aman dipasang di atas sistem combat yang sudah berjalan.

---

## 2. Daftar Kelas dan Tanggung Jawabnya

| Kelas | File | Jenis | Tanggung jawab |
|---|---|---|---|
| `IPlayerState` | `IPlayerState.cs` | interface | Kontrak state: properti `Name` + siklus hidup `Enter()` / `Tick()` / `Exit()`. |
| `PlayerBaseState` | `PlayerBaseState.cs` | abstract class | Implementasi dasar `IPlayerState`. Menyimpan `context` (controller) + `Parent` (induk hierarki). Menyediakan `FullPath` (mis. `"Combat/Sword"`). `Enter/Tick/Exit` virtual kosong. |
| `PlayerStateMachine` | `PlayerStateMachine.cs` | class C# murni | Driver mesin status. `ChangeState(next)` menjalankan `Exit()` lama → set `CurrentState` → `Enter()` baru; mengabaikan bila state sama. `Tick()` meneruskan ke state aktif. **Tidak tahu aturan transisi.** |
| `LocomotionState`, `InterruptState`, `CombatState` | `PlayerStates.cs` | super-state | Node induk hierarki. Hanya untuk pengelompokan + `FullPath`; tidak pernah menjadi state aktif sendirian. |
| `IdleState`, `MoveState`, `DashState` | `PlayerStates.cs` | leaf (Locomotion) | Diam / bergerak / dash. |
| `CombatSwordState`, `CombatBowState` | `PlayerStates.cs` | leaf (Combat) | Aksi tempur dengan Sword / Bow. |
| `StaggeredState`, `DeadState` | `PlayerStates.cs` | leaf (Interrupt) | Ter-stagger vs mati. |
| `PlayerStateController` | `PlayerStateController.cs` | MonoBehaviour | **Otak transisi.** Tiap `Update` membaca kondisi pemain, memilih state tujuan lewat prioritas, mencatat transisi ke field debug Inspector, dan menyetir `PlayerStateMachine`. Memegang cache instance state + buffer riwayat. |
| `PlayerStateHud` | `PlayerStateHud.cs` | MonoBehaviour | **Visualisasi.** Membaca controller (read-only) lalu menggambar via `OnGUI`: label state di atas kepala + panel detail pojok layar (lebar otomatis). Tanpa setup Canvas/prefab. Default mati, toggle `F1`. |
| `PlayerStateLabels` | `PlayerStateLabels.cs` | static class | **Lapisan terjemahan.** Mengubah istilah teknis (jalur state & pemicu) menjadi bahasa mekanik awam, mis. `Combat/Sword` → "Serangan Pedang", `skill bow (charge Full Draw)` → "menahan / charge panah". Satu sumber kebenaran label. |

---

## 3. Daftar State (Hierarki 2 Tingkat)

```
Locomotion (super)
├── Locomotion/Idle        ← pemain diam
├── Locomotion/Move        ← pemain bergerak
└── Locomotion/Dash        ← pemain dash/menghindar (Dash.IsDashing)

Combat (super)
├── Combat/Sword           ← aksi tempur saat senjata aktif = Sword
└── Combat/Bow             ← aksi tempur saat senjata aktif = Bow (charge Full Draw via pemicu)

Interrupt (super)
├── Interrupt/Staggered    ← isStaggered == true
└── Interrupt/Dead         ← currentHP <= 0
```

Catatan: mesin **selalu** mendarat pada *leaf state*. Super-state hanya membentuk
jalur (`FullPath`) dan pengelompokan, tidak pernah menjadi `CurrentState`.

---

## 4. Cara Kerja Transisi (Centralized Transition)

Semua keputusan ada di `PlayerStateController.SelectTargetState()`, dievaluasi
**setiap `Update()`** dari snapshot flag (read-only). Urutan prioritas:

| # | Kondisi yang dibaca | State tujuan | Pemicu (dicatat ke `lastTriggerDebug`) |
|---|---|---|---|
| 1 | `character == null` | `Locomotion/Idle` | `character null (fallback)` |
| 2 | `currentHP <= 0` | `Interrupt/Dead` | `currentHP <= 0` |
| 3 | `isStaggered == true` | `Interrupt/Staggered` | `isStaggered == true` |
| 4 | `lockMovement \|\| isAttacking` | **Combat (sub-state)** → lihat tabel 4a | — |
| 5 | `Dash.IsDashing == true` | `Locomotion/Dash` | `sedang dash` |
| 6 | `velocity² > threshold²` | `Locomotion/Move` | `velocity > threshold` |
| 7 | (sisanya) | `Locomotion/Idle` | `diam (default)` |

**Tabel 4a — pemilihan sub-state Combat** (`SelectCombatSubState()`):

| # | Kondisi | State tujuan | Pemicu |
|---|---|---|---|
| 1 | `weaponType == Bow` | `Combat/Bow` | `skill bow aktif` / `skill bow (charge Full Draw)` bila `EnergyRegenBlocked` |
| 2 | (selain Bow) | `Combat/Sword` | `skill sword aktif` |

**Eksekusi transisi** (`PlayerStateMachine.ChangeState`): `Exit()` state lama →
ganti `CurrentState` → `Enter()` state baru. Pada fase ini `Enter/Tick/Exit`
sengaja kosong (observasional), jadi transisi tidak menimbulkan efek gameplay.

**Pencatatan transisi** (`RecordTransition`): mengisi field Inspector
`currentStateDebug`, `previousStateDebug`, `lastTransitionDebug`,
`lastTriggerDebug`, dan menambah ke `recentTransitionsDebug` (maks 6 baris).
Transisi noise `Idle ↔ Move` difilter dari riwayat agar log tetap bermakna;
transisi dari placeholder awal/akhir (`"(belum mulai)"`, `"(stopped)"`)
diabaikan. `OnDisable` mereset field agar teks runtime tidak tersimpan ke scene.

---

## 5. Hubungan HFSM dengan Mekanik Pemain

HFSM **mengamati** sinyal berikut. Kolom "Dikendalikan oleh" menunjukkan siapa
yang sebenarnya menulis sinyal itu — **bukan** HFSM.

| Mekanik | Sinyal yang dibaca HFSM | State terkait | Dikendalikan oleh (penulis sinyal) |
|---|---|---|---|
| **Gerak** | `Rigidbody2D.velocity` (ambang `moveVelocityThreshold = 0.05`) | `Locomotion/Move`, `Locomotion/Idle` | `MoveKeyboard.cs` |
| **Dash** | `Dash.IsDashing` (read-only) | `Locomotion/Dash` | `Dash.cs` (MoveKeyboard external-lock + `rb.MovePosition`, biaya energy, cooldown `dashCooldown`) |
| **Sword** | `weaponType == Sword` + (`isAttacking`/`lockMovement`) | `Combat/Sword` | `Sword_SlashCombo`, `Sword_ChargedStrike`, `Sword_Whirlwind`, `Sword_Riposte` (menulis `isAttacking`; Riposte juga `lockMovement`) |
| **Bow** | `weaponType == Bow` + (`isAttacking`/`lockMovement`) | `Combat/Bow` | `Bow_QuickShot`, `Bow_ConcussiveShot`, `Bow_PiercingShot`, `Bow_SpreadArrow`, `Bow_FullDraw` (menulis `lockMovement`) |
| **Bow Full Draw (charge)** | `CharacterBase.EnergyRegenBlocked` | `Combat/Bow` (pemicu menyebut "charge Full Draw") | `Bow_FullDraw.cs` (memanggil `SetEnergyRegenBlocked(true)` selama charge) |
| **Energy** | `CharacterBase.EnergyRegenBlocked` | dipakai melabeli pemicu `Combat/Bow` | `CharacterBase` (regen) + skill (`TrySpendEnergy`, blokir regen). HFSM tidak menambah/mengurangi energy. |
| **Cooldown** | *(tidak dibaca)* | — | Per-skill (`Dash.dashCooldown`, timer di tiap skill). HFSM tidak menegakkan cooldown. |
| **HP** | `CharacterBase.currentHP` | `Interrupt/Dead` | `CharacterBase.TakeDamage/SetHP/Die`. HFSM tidak mengubah HP. |
| **Stagger** | `CharacterBase.isStaggered` | `Interrupt/Staggered` | `CharacterBase.ApplyStagger/ApplyStun/TryApplyStagger`. HFSM tidak menerapkan stagger. |

---

## 6. Visualisasi & Instrumen Debug

**a) Inspector (`PlayerStateController`, grup "Debug (read-only)")**
`currentStateDebug`, `previousStateDebug`, `lastTransitionDebug`,
`lastTriggerDebug`, `recentTransitionsDebug` — semua terisi otomatis saat Play.

**b) Game View (`PlayerStateHud` + `PlayerStateLabels`)**
- **Label di atas kepala** pemain memakai bahasa awam (mis. `"● Serangan Pedang"`),
  warna mengikuti kategori: hijau = Locomotion, oranye = Combat, merah = Interrupt.
- **Panel pojok layar** (lebar otomatis, tidak terpotong): "Sekarang" (nama
  awam), "Status FSM" (jalur teknis, bisa dimatikan via `showTechnicalLine`),
  "Penyebab" (sebab awam), "Sebelumnya", dan daftar "Aksi terakhir".
- Semua istilah teknis diterjemahkan oleh `PlayerStateLabels` agar dipahami
  penguji non-teknis, sementara baris "Status FSM" tetap menyediakan bukti teknis.
- **Default mati**; toggle dengan **F1** saat Play. Tidak mengubah gameplay.

---

## 7. Keterbatasan Jujur (observasional vs dikendalikan)

1. **Read-only.** HFSM **tidak** menulis flag/velocity/HP/energy dan **tidak**
   memanggil skill/dash. `Enter/Tick/Exit` kosong. Ini disengaja: tujuannya
   merepresentasikan & memvisualkan status pemain tanpa mengubah perilaku game
   yang sudah berjalan. Kendali sebenarnya tetap di `MoveKeyboard`, `Dash`, dan
   skill `Sword_*`/`Bow_*`.
2. **Super-state tidak pernah aktif sendiri** — hanya membentuk hierarki/`FullPath`.
3. **Dash terbaca lewat satu flag read-only.** `Dash.cs` memakai
   `MoveKeyboard.LockExternal` + `rb.MovePosition` (velocity ~0), jadi tidak bisa
   dibedakan dari `Idle` lewat kecepatan. Untuk itu ditambahkan properti
   read-only `Dash.IsDashing` yang dibaca HFSM agar muncul `Locomotion/Dash`.
   Properti ini tidak mengubah perilaku dash — tetap observasional.
4. **Sub-state Combat dibedakan per senjata, bukan per skill.** HFSM membedakan
   `Combat/Sword` vs `Combat/Bow` dari `weaponType`, tetapi tidak membedakan skill
   individual — mis. QuickShot vs SpreadArrow keduanya `Combat/Bow`. Fase charge
   Full Draw hanya dilaporkan lewat teks pemicu, bukan state terpisah.
5. **Transisi berbasis polling per-frame** (membaca snapshot flag tiap `Update`),
   bukan event-driven. Sederhana, deterministik, dan cukup untuk observasi.
6. **Label "charge Full Draw" bergantung pada `EnergyRegenBlocked`** yang saat ini
   hanya di-set oleh `Bow_FullDraw`. Ini hanya memengaruhi teks pemicu pada
   `Combat/Bow`, bukan menambah state. Bila kelak ada skill bow lain yang
   memblokir regen, ia juga akan dilabeli "charge".

---

## 8. Kenapa Desain Ini Layak Disebut "Implementasi HFSM"

- **Hierarki nyata** (super-state → sub-state) dengan `FullPath` yang dilacak.
- **Pemisahan tanggung jawab**: kontrak (`IPlayerState`), basis (`PlayerBaseState`),
  driver (`PlayerStateMachine`), kebijakan transisi (`PlayerStateController`),
  visualisasi (`PlayerStateHud`) — masing-masing satu peran.
- **Transisi terpusat & berprioritas** yang mudah dijelaskan dan diaudit.
- **Bukti visual**: state aktif tampil di Game View dan di Inspector.
- **Jujur tentang batas**: observasional, bukan pengendali — dinyatakan terbuka.

---

## 9. Roadmap Fase 2 — Migrasi dari Observasi ke Kendali (Modular)

Arsitektur ini **sengaja** dibangun sebagai *observer* dulu (Fase 1) supaya bisa
dipasang di atas sistem combat yang sudah berjalan **tanpa risiko**. Kerangkanya
sudah modular dan siap dinaikkan menjadi **authoritative HFSM** (state benar-benar
mengendalikan) secara bertahap. Rencana ini bisa disampaikan ke penguji sebagai
arah pengembangan yang konkret.

**Prinsip migrasi.** Membalik arah kendali: `Enter/Tick/Exit` tiap state yang
saat ini kosong diisi perilaku nyata, dan **kepemilikan flag** (`lockMovement`,
`isAttacking`, blokir regen) dipindah dari skrip yang tersebar ke state terkait.
Memakai pola **wrapper/delegasi** agar API publik tetap kompatibel dengan
prefab/scene (selaras catatan arsitektur di `CharacterBase`).

**Urutan bertahap (satu mekanik per langkah, uji regresi tiap langkah):**

1. **Locomotion lebih dulu (risiko terendah).** `IdleState`/`MoveState`/`DashState`
   mengisi `Tick()` untuk mengatur input gerak & sinyal animasi; `MoveKeyboard`
   berubah jadi layanan yang dipanggil state, bukan penentu sendiri.
2. **Dash.** `DashState.Enter()` memicu rutin dash (kini di `Dash.cs`), state yang
   memegang durasi/cooldown; input hanya meminta transisi ke `DashState`.
3. **Interrupt.** `StaggeredState`/`DeadState.Enter()` aktif mengunci input &
   memutar animasi, bukan sekadar membaca `isStaggered`/`currentHP`.
4. **Combat (paling akhir, paling berisiko).** Sub-state `Combat/Sword` &
   `Combat/Bow` menjadi pemilik `isAttacking`/`lockMovement`; skrip skill
   `Sword_*`/`Bow_*` dipanggil oleh state, bukan menulis flag langsung.

**Mitigasi risiko.** Pertahankan jalur observasional sebagai fallback selama
migrasi; ubah satu sistem, uji, baru lanjut; tidak menyentuh skrip skill sampai
locomotion + interrupt stabil.

**Yang sudah siap pakai sekarang:** struktur kelas (interface/base/driver/
controller/states/HUD/labels), hierarki + `FullPath`, transisi terpusat, dan
visualisasi. Fase 2 **mengisi perilaku**, bukan membongkar struktur.
