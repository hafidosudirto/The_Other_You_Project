using System.Collections;
using UnityEngine;

/// <summary>
/// Skill QuickShot khusus Minion Range.
/// Versi ramping & independen — tidak bergantung pada Enemy_Bow_QuickShot boss.
/// Spawn projectile dari prefab (Minion_Projectile.prefab), terbang horizontal
/// lalu jatuh karena gravity, dan damage ditangani oleh ArrowDamage pada prefab.
///
/// Dipanggil dari BowAnimationEventRelay_MinionRange.AE_BowQuickShot_Release().
/// </summary>
[DisallowMultipleComponent]
public class MinionRange_Projectile : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Titik spawn projectile. Boleh dikosongkan; akan di-auto-find 'FirePoint'/'ArrowSpawnPoint'.")]
    public Transform firePoint;

    [Tooltip("Prefab projectile (panah). Biasanya = Minion_Projectile.prefab.")]
    public GameObject arrowPrefab;

    [Tooltip("Sprite renderer untuk deteksi arah hadap fallback.")]
    public SpriteRenderer facingSprite;

    [Header("Arrow Settings")]
    public float speed = 10f;
    [Tooltip("Waktu terbang lurus horizontal sebelum gravitasi mulai menarik panah ke bawah.")]
    public float straightTime = 0.4f;
    public float gravityStart = 3f;
    public float gravityEnd = 12f;
    public float destroyDelay = 0.25f;

    [Header("Hit Effects")]
    [Tooltip("Damage base panah. Dikirim ke ArrowDamage.SetStats().")]
    public float damage = 5f;
    public float knockback = 1f;
    public float stun = 0f;

    [Header("Debug")]
    public bool showDebug = true;

    private CharacterBase owner;
    private bool isCasting;

    public bool IsCasting => isCasting;

    public void SetOwner(CharacterBase o)
    {
        owner = o;
    }

    /// <summary>
    /// Tembak langsung tanpa menunggu animation event.
    /// Menghormati flag isCasting agar tidak double-fire.
    /// </summary>
    public void ForceShoot()
    {
        if (isCasting)
            return;

        if (firePoint == null)
            firePoint = FindChildRecursive(transform.root, "FirePoint")
                     ?? FindChildRecursive(transform.root, "ArrowSpawnPoint")
                     ?? transform;

        if (arrowPrefab == null)
        {
            Debug.LogWarning("[MinionRange_Projectile] ForceShoot gagal. arrowPrefab belum di-assign.", this);
            return;
        }

        // Fallback jika GameObject inactive.
        if (!gameObject.activeInHierarchy)
        {
            ShootArrow();
            return;
        }

        StartCoroutine(CastRoutine());
    }

    /// <summary>
    /// Dipanggil dari Animation Event pada clip MinionRange_Attack.
    /// Idempotent: jika coroutine sudah jalan, panggilan kedua diabaikan.
    ///
    /// Aman terhadap GameObject inactive: jika StartCoroutine gagal (mis.
    /// komponen dipasang di GameObject non-aktif), langsung tembak tanpa
    /// coroutine sebagai fallback.
    /// </summary>
    public void ReleaseFromAnimationEvent()
    {
        if (isCasting)
            return;

        if (firePoint == null)
            firePoint = FindChildRecursive(transform.root, "FirePoint")
                     ?? FindChildRecursive(transform.root, "ArrowSpawnPoint")
                     ?? transform;

        if (arrowPrefab == null)
        {
            Debug.LogWarning("[MinionRange_Projectile] ReleaseFromAnimationEvent gagal. arrowPrefab belum di-assign.", this);
            return;
        }

        // Fallback: jika GameObject inactive, StartCoroutine akan throw.
        // Spawn langsung tanpa coroutine agar tidak crash.
        if (!gameObject.activeInHierarchy)
        {
            if (showDebug)
                Debug.LogWarning("[MinionRange_Projectile] GameObject inactive — spawn langsung tanpa coroutine.", this);

            ShootArrow();
            return;
        }

        StartCoroutine(CastRoutine());
    }

    private IEnumerator CastRoutine()
    {
        isCasting = true;

        // Tunggu 1 frame agar timing animation event & posisi firePoint konsisten.
        yield return null;

        ShootArrow();

        isCasting = false;
    }

    private void ShootArrow()
    {
        if (firePoint == null || arrowPrefab == null)
            return;

        GameObject arrowObj = Instantiate(arrowPrefab, firePoint.position, Quaternion.identity);
        if (arrowObj == null)
            return;

        // Pakai MinionArrowDamage (filter Player-only) — bukan ArrowDamage boss.
        MinionArrowDamage dmg = arrowObj.GetComponent<MinionArrowDamage>();
        if (dmg == null)
        {
            // Komponen belum ada di prefab — tambahkan secara runtime sebagai fallback.
            dmg = arrowObj.AddComponent<MinionArrowDamage>();
        }

        Rigidbody2D rb = arrowObj.GetComponent<Rigidbody2D>();

        float dir = GetFacingDirection();
        ApplyArrowFacing(arrowObj, dir);

        // Pastikan collider projectile adalah Trigger agar OnTriggerEnter2D terpanggil,
        // bukan OnCollisionEnter2D (yang akan memantulkan panah). Jika prefab
        // tidak mengaturnya sebagai Trigger, kita override saat spawn.
        EnsureTriggerCollider(arrowObj);

        if (dmg != null)
        {
            if (owner != null)
                dmg.SetOwner(owner);

            dmg.SetStats(damage, knockback, stun);
        }

        if (showDebug)
        {
            Debug.Log(
                $"<color=red>[MINION RANGED ATTACK]</color> {transform.root.name} menembak projectile dari {firePoint.name} (dir={dir}).",
                this
            );
        }

        if (rb != null)
            StartCoroutine(ArrowRoutine(rb, arrowObj, dir));
        else
            Destroy(arrowObj, 1f);
    }

    private IEnumerator ArrowRoutine(Rigidbody2D rb, GameObject arrowObj, float dir)
    {
        if (rb == null || arrowObj == null)
            yield break;

        // Fase lurus horizontal.
        float timer = 0f;
        while (timer < straightTime)
        {
            if (rb == null || arrowObj == null)
                yield break;

            rb.velocity = new Vector2(dir * speed, 0f);
            timer += Time.deltaTime;
            yield return null;
        }

        // Fase jatuh karena gravity.
        float t = 0f;
        while (t < 1f)
        {
            if (rb == null || arrowObj == null)
                yield break;

            float g = Mathf.Lerp(gravityStart, gravityEnd, t);
            rb.velocity = new Vector2(
                dir * speed,
                rb.velocity.y - g * Time.deltaTime
            );

            t += Time.deltaTime * 1.5f;
            yield return null;
        }

        if (rb == null || arrowObj == null)
            yield break;

        rb.velocity = Vector2.zero;
        yield return new WaitForSeconds(destroyDelay);

        if (arrowObj != null)
            Destroy(arrowObj);
    }

    private float GetFacingDirection()
    {
        if (owner != null)
            return owner.isFacingRight ? 1f : -1f;

        if (facingSprite != null)
            return facingSprite.flipX ? -1f : 1f;

        return 1f;
    }

    private void ApplyArrowFacing(GameObject arrowObj, float direction)
    {
        if (arrowObj == null)
            return;

        Vector3 scale = arrowObj.transform.localScale;
        arrowObj.transform.localScale = new Vector3(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z)
        );

        float z = direction > 0f ? 0f : 180f;
        arrowObj.transform.rotation = Quaternion.Euler(0f, 0f, z);

        SpriteRenderer[] renderers = arrowObj.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in renderers)
            sr.flipX = false;
    }

    /// <summary>
    /// Pastikan semua Collider2D pada projectile diset sebagai Trigger.
    /// Kalau tidak, panah akan terpantul oleh collider Player (OnCollisionEnter2D)
    /// bukan OnTriggerEnter2D, sehingga MinionArrowDamage tidak terpanggil.
    /// </summary>
    private void EnsureTriggerCollider(GameObject arrowObj)
    {
        if (arrowObj == null)
            return;

        Collider2D[] colliders = arrowObj.GetComponentsInChildren<Collider2D>(true);
        foreach (var col in colliders)
        {
            if (col != null && !col.isTrigger)
                col.isTrigger = true;
        }
    }

    private Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null)
            return null;
        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void OnDisable()
    {
        isCasting = false;
    }
}