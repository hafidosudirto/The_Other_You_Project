using UnityEngine;

public class ConcussiveHitArea : MonoBehaviour
{
    [Header("Blast Setup")]
    [Tooltip("Damage ledakan Concussive. Biasanya nilai ini dikirim dari Bow_ConcussiveShot.cs lewat Setup().")]
    public float damageLedak = 10f;

    [Tooltip("Kekuatan dorong ledakan. Dorongannya dipaksa mendatar ke kiri / kanan.")]
    public float dorongLedak = 6f;

    [Tooltip("Durasi stun dari ledakan. Kalau ingin ubah reaksi musuh lebih jauh, cek CharacterBase.cs / Enemy.cs.")]
    public float stunLedak = 0.35f;

    [Tooltip("Jangkauan ledakan. Semakin besar, semakin luas area yang kena.")]
    public float radiusLedak = 1.4f;

    [Header("Owner")]
    [Tooltip("Pemilik ledakan ini. Biasanya diisi otomatis saat Concussive dibuat.")]
    public CharacterBase owner;

    [Header("Target Filter")]
    [Tooltip("Layer target yang boleh kena ledakan. Biasanya diarahkan ke Enemy.")]
    public LayerMask hitMask = ~0;

    [Header("Life Time")]
    [Tooltip("Berapa lama area ledak tetap ada setelah aktif.")]
    public float waktuHidup = 0.15f;

    private bool sudahAktif;

    // Dipanggil dari Bow_ConcussiveShot.cs
    public void Setup(CharacterBase ownerCharacter, float dmg, float kb, float st, float rad)
    {
        owner = ownerCharacter;
        damageLedak = dmg;
        dorongLedak = kb;
        stunLedak = st;
        radiusLedak = rad;
    }

    void OnEnable()
    {
        TriggerHit();

        if (waktuHidup > 0f)
            Destroy(gameObject, waktuHidup);
    }

    public void TriggerHit()
    {
        if (sudahAktif) return;
        sudahAktif = true;

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, radiusLedak, hitMask);
        foreach (var h in hits)
        {
            CharacterBase target = h.GetComponent<CharacterBase>();
            if (target == null)
                continue;

            if (owner != null && target == owner)
                continue;

            Vector2 hitPoint = h.ClosestPoint(transform.position);
            ApplyEffectsToTarget(target, hitPoint);
        }
    }

    private void ApplyEffectsToTarget(CharacterBase target, Vector2 hitPoint)
    {
        if (damageLedak > 0f)
        {
            GameObject source = owner != null ? owner.gameObject : gameObject;
            target.TakeDamage(damageLedak, source);
        }

        // Knockback ledak dipaksa horizontal
        if (dorongLedak > 0f)
        {
            float arahX = target.transform.position.x >= transform.position.x ? 1f : -1f;
            Vector2 arahDorong = new Vector2(arahX, 0f);
            target.ApplyKnockback(arahDorong, dorongLedak);
        }

        if (stunLedak > 0f)
        {
            target.ApplyStun(stunLedak);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, radiusLedak);
    }
#endif
}