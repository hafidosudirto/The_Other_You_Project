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

    [Header("Flight Curve")]
    public float straightTime = 0.45f;
    public float gravityStart = 3f;
    public float gravityEnd = 12f;

    [Header("Cleanup")]
    public float destroyDelay = 0.25f;

    [Header("Timing")]
    public float cooldown = 0.35f;
    public float postReleaseLock = 0.08f;

    private bool isOnCooldown;
    private bool isCharging = false;
    private bool waitingReleaseEvent = false;
    private float chargeTimer = 0f;
    private float pendingChargePercent = 0f;

    public void TriggerSkill(int slot)
    {
        if (isCharging || isOnCooldown)
            return;

        if (!player || !firePoint || !arrowPrefab)
        {
            Debug.LogWarning("[FullDraw] Missing player / firePoint / arrowPrefab.");
            return;
        }

        if (player.lockMovement)
            return;

        StartCoroutine(CooldownRoutine());

        isCharging = true;
        waitingReleaseEvent = false;
        chargeTimer = 0f;

        player.lockMovement = true;
        StopOwnerMovement();

        if (anim)
            anim.TriggerBowChargeStart();

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

            if (chargeTimer >= maxChargeTime)
            {
                BeginRelease();
                yield break;
            }

            yield return null;
        }
    }

    private void BeginRelease()
    {
        isCharging = false;
        waitingReleaseEvent = true;
        pendingChargePercent = Mathf.Clamp01(chargeTimer / maxChargeTime);

        if (anim)
            anim.TriggerBowChargeRelease();
    }

    public void ReleaseFromAnimationEvent()
    {
        if (!waitingReleaseEvent)
            return;

        waitingReleaseEvent = false;
        FireArrow(pendingChargePercent);
        StartCoroutine(ReleaseRecoverRoutine());
    }

    private IEnumerator ReleaseRecoverRoutine()
    {
        yield return new WaitForSeconds(postReleaseLock);

        if (player != null)
            player.lockMovement = false;
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

        bool faceRight = player.isFacingRight;
        float direction = faceRight ? 1f : -1f;

        float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, charge);
        rb.velocity = new Vector2(direction * speed, 0f);

        if (sr != null)
        {
            sr.color = arrowColor;
            sr.flipX = direction < 0;
        }

        Vector3 scale = obj.transform.localScale;
        scale.x = Mathf.Abs(scale.x) * direction;
        obj.transform.localScale = scale;

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

        StartCoroutine(ArrowRoutine(rb, obj, speed, direction));
    }

    private IEnumerator ArrowRoutine(Rigidbody2D rb, GameObject arrowObj, float speed, float dir)
    {
        float timer = 0f;

        while (timer < straightTime)
        {
            if (rb == null) yield break;

            rb.velocity = new Vector2(dir * speed, 0f);

            timer += Time.deltaTime;
            yield return null;
        }

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

        if (rb != null)
            rb.velocity = Vector2.zero;

        yield return new WaitForSeconds(destroyDelay);

        if (arrowObj != null)
            Destroy(arrowObj);
    }

    private void StopOwnerMovement()
    {
        if (player == null) return;

        Rigidbody2D ownerRb = player.GetComponent<Rigidbody2D>();
        if (ownerRb != null)
            ownerRb.velocity = Vector2.zero;
    }

    private void OnDisable()
    {
        if (player != null)
            player.lockMovement = false;

        isCharging = false;
        waitingReleaseEvent = false;
        pendingChargePercent = 0f;
    }
}