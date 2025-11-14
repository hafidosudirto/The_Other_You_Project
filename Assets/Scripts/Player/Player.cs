using UnityEngine;

public class Player : CharacterBase
{
    // Player adalah turunan dari CharacterBase.
    // CharacterBase menyediakan fitur inti seperti HP, damage, defense, flip, stagger, dan riposte.
    // Player mengambil semua itu agar tidak perlu menulis ulang logika dasar.

    // Script ini dibuat sederhana untuk menjaga arsitektur tetap bersih.
    // Logika umum disimpan di CharacterBase.
    // Player hanya menambahkan hal yang sifatnya khusus untuk Player saja.

    // Player akan dipakai di tiga prefab.
    // Player_W0 tidak memakai SkillBase.
    // Player_W1 memakai SkillBase dengan skill sword.
    // Player_W2 memakai SkillBase dengan skill bow.

    // Override Awake dipakai untuk memberikan konfigurasi awal Player.
    // Fungsi ini berjalan lebih awal sebelum Start di script lain.
    // Tempat yang tepat untuk mengubah nilai default Player tanpa mengganggu CharacterBase.

    protected override void Awake()
    {
        // base.Awake memanggil inisialisasi CharacterBase.
        // Ini memastikan Player memiliki HP penuh, akses Rigidbody2D, dan siap menerima input.
        base.Awake();

        // Tempat yang cocok untuk memberi buff atau modifikasi milik Player.
        // Contoh:
        // attack += 2;
        // moveSpeed += 1;
        // Dibiarkan kosong agar Player tetap netral secara default.
    }
}
