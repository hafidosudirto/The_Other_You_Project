using UnityEngine;
using System.Collections;

public class Bow_FullDraw : MonoBehaviour, ISkill
{
    [Header("Refs")]
    public Transform firePoint;
    public GameObject arrowPrefab;
    public Player player;
    public PlayerAnimation anim;

    [Header("Charge Settings")]
    public float maxChargeTime = 3f;
    public float minArrowSpeed = 6f;
    public float maxArrowSpeed = 20f;

    [Header("Combat Settings")]
    public int baseDamage = 1;
    public float baseKnockback = 2f;
    public float baseStun = 0.15f;
    public Color arrowColor = Color.yellow;

    // --- BAGIAN YANG DISESUAIKAN DENGAN QUICK SHOT ---

    [Header("Flight Curve")]
    // Sama konsepnya dengan QuickShot
    public float straightTime = 0.45f;   // dulu: curveStartTime
    public float gravityStart = 3f;
    public float gravityEnd = 12f;

    [Header("Cleanup")]
    public float destroyDelay = 0.25f;   // panah berhenti sebentar lalu hancur

    private bool isOnCooldown;
    public float cooldown = 0.2f;        // disamakan dengan QuickShot

    // -------------------------------------------------

    private bool isCharging = false;
    private float chargeTimer = 0f;

    public void TriggerSkill(int slot)
    {
        // Tidak bisa cast kalau sedang charge atau cooldown
        if (isCharging || isOnCooldown)
            return;

        StartCoroutine(CooldownRoutine());

        isCharging = true;
        chargeTimer = 0f;

        if (anim) anim.TriggerBowChargeStart();

        StartCoroutine(ChargeRoutine());
    }

    IEnumerator CooldownRoutine()
    {
        isOnCooldown = true;
        yield return new WaitForSeconds(cooldown);
        isOnCooldown = false;
    }

    IEnumerator ChargeRoutine()
    {
        while (isCharging)
        {
            chargeTimer += Time.deltaTime;

            // Auto-release jika sudah penuh
            if (chargeTimer >= maxChargeTime)
            {
                Release();
                yield break;
            }

            yield return null;
        }
    }

    void Release()
    {
        isCharging = false;

        float chargePercent = Mathf.Clamp01(chargeTimer / maxChargeTime);

        if (anim) anim.TriggerBowChargeRelease();

        FireArrow(chargePercent);
    }

    void FireArrow(float charge)
    {
        if (!arrowPrefab || !firePoint) return;

        GameObject obj = Instantiate(arrowPrefab, firePoint.position, Quaternion.identity);
        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
        ArrowDamage dmg = obj.GetComponent<ArrowDamage>();

        if (!rb)
        {
            DebugHub.Warning("[FullDraw] Arrow missing Rigidbody2D!");
            return;
        }

        // 1) Arah berdasarkan facing player
        bool faceRight = player.isFacingRight;
        float direction = faceRight ? 1f : -1f;

        // 2) Kecepatan berdasarkan charge
        float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, charge);

        // velocity awal (fase lurus akan diatur di coroutine)
        rb.velocity = new Vector2(direction * speed, 0f);

        // 3) Warna dan flip sprite
        if (sr != null)
        {
            sr.color = arrowColor;
            sr.flipX = direction < 0;
        }

        // 4) Pastikan scale X mengikuti arah
        Vector3 scale = obj.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        obj.transform.localScale = scale;

        // 5) Damage
        if (dmg != null)
        {
            dmg.owner = player;
            dmg.SetStats(
                baseDamage,
                baseKnockback * charge,
                baseStun + (0.1f * charge),
                false,
                false
            );
        }

        DebugHub.Bow($"[FullDraw] Spawn dir={direction} | speed={speed}");

        // 6) Pola terbang + destroy (disamakan gaya-nya dengan QuickShot)
        StartCoroutine(ArrowRoutine(rb, obj, speed, direction));
    }

    private IEnumerator ArrowRoutine(Rigidbody2D rb, GameObject arrowObj, float speed, float dir)
    {
        float timer = 0f;

        // --------------------
        // STRAIGHT PHASE
        // --------------------
        while (timer < straightTime)
        {
            if (rb == null) yield break;

            rb.velocity = new Vector2(dir * speed, 0f);

            timer += Time.deltaTime;
            yield return null;
        }

        // --------------------
        // CURVE PHASE
        // --------------------
        float t = 0f;
        while (t < 1f)
        {
            if (rb == null) yield break;

            float g = Mathf.Lerp(gravityStart, gravityEnd, t);

            rb.velocity = new Vector2(
                dir * speed,
                rb.velocity.y - g * Time.deltaTime
            );

            t += Time.deltaTime * 1.5f;
            yield return null;
        }

        // Hentikan panah sebelum dihancurkan
        if (rb != null)
            rb.velocity = Vector2.zero;

        // --------------------
        // DELAYED DESTROY
        // --------------------
        yield return new WaitForSeconds(destroyDelay);

        if (arrowObj != null)
            Destroy(arrowObj);
    }
}
