using UnityEngine;

// Enemy adalah turunan CharacterBase.
// Semua sistem dasar HP, damage, stagger, dan riposte sudah ada di CharacterBase.
// Script ini hanya menyiapkan nilai default musuh dan menetapkan mode Kinematic.
//
// Enemy menggunakan movement manual sehingga Rigidbody2D dibuat Kinematic.
// Knockback tetap bisa berjalan karena CharacterBase menangani knockback manual.

[RequireComponent(typeof(Rigidbody2D))]
public class Enemy : CharacterBase
{
    protected override void Awake()
    {
        // Inisialisasi dari CharacterBase:
        // mengambil Rigidbody2D, set flag dasar, isi HP, dll.
        base.Awake();

        // Default stat musuh. Bisa diubah per prefab.
        maxHP = 50f;
        currentHP = maxHP;
        attack = 8f;
        defense = 2f;
        moveSpeed = 2f;

        // Enemy memakai Rigidbody2D Kinematic agar tidak saling mendorong Player.
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }
    }
}
