using System.Collections;
using UnityEngine;

public class Enemy_Bow_FullDraw : MonoBehaviour, ISkill
{
    [Header("BT / Range")]
    public float minRange = 3.5f;
    public float skillRange = 8f;
    public float rangeTolerance = 0.25f;

    [Header("References")]
    public Transform firePoint;
    public GameObject arrowPrefab;
    public SpriteRenderer facingSprite;

    [Header("Charge Settings")]
    public float maxChargeTime = 1.5f;
    public float minArrowSpeed = 6f;
    public float maxArrowSpeed = 20f;

    [Header("Combat Settings")]
    public float baseDamage = 1f;
    public float baseKnockback = 2f;
    public float baseStun = 0.15f;

    [Header("Flight Curve")]
    public float straightTime = 0.45f;
    public float gravityStart = 3f;
    public float gravityEnd = 12f;

    [Header("Cleanup")]
    public float destroyDelay = 0.25f;

    [Header("Timing")]
    public float cooldown = 0.8f;
    public float postReleaseLock = 0.08f;
    public float releaseTimeout = 0.7f;

    public bool IsActive => isCharging || waitingReleaseEvent;

    private EnemyAI ai;
    private EnemyCombatController combat;
    private EnemyAnimation enemyAnim;
    private CharacterBase owner;
    private Rigidbody2D ownerRb;

    private bool isCharging;
    private bool waitingReleaseEvent;
    private bool cooldownRunning;
    private bool skillLockClaimed;

    private float chargeTimer;
    private float pendingChargePercent;

    private void Awake()
    {
        ai = GetComponentInParent<EnemyAI>();
        combat = GetComponentInParent<EnemyCombatController>();
        owner = GetComponentInParent<CharacterBase>();
        ownerRb = GetComponentInParent<Rigidbody2D>();
        if (ai != null)
        {
            enemyAnim = ai.GetComponentInChildren<EnemyAnimation>(true);
        }

        if (firePoint == null)
        {
            Transform found = FindChildRecursive(transform.root, "FirePoint");
            if (found == null)
                found = FindChildRecursive(transform.root, "ArrowSpawnPoint");

            firePoint = found;
        }

        if (facingSprite == null && ai != null)
            facingSprite = ai.GetComponentInChildren<SpriteRenderer>(true);
    }

    public bool IsInRange(float distanceToPlayer)
    {
        return distanceToPlayer >= (minRange - rangeTolerance) &&
               distanceToPlayer <= (skillRange + rangeTolerance);
    }

    public bool CanTrigger(float distanceToPlayer)
    {
        if (cooldownRunning) return false;
        if (isCharging || waitingReleaseEvent) return false;
        if (combat != null && combat.IsBusy) return false;
        if (!IsInRange(distanceToPlayer)) return false;
        if (firePoint == null || arrowPrefab == null) return false;
        return true;
    }

    public void Trigger()
    {
        float distance = GetDistanceToPlayer();
        if (!CanTrigger(distance))
            return;

        StartCoroutine(CastRoutine());
    }

    public void TriggerSkill(int slot)
    {
        Trigger();
    }

    private IEnumerator CastRoutine()
    {
        isCharging = true;
        waitingReleaseEvent = false;
        skillLockClaimed = true;
        chargeTimer = 0f;
        pendingChargePercent = 0f;

        combat?.InvokeSkillStart();
        StopOwnerMovement();
        StartCoroutine(CooldownRoutine());

        if (enemyAnim != null)
            enemyAnim.TriggerBowChargeStart();

        while (isCharging)
        {
            chargeTimer += Time.deltaTime;

            if (chargeTimer >= maxChargeTime)
            {
                BeginRelease();
                break;
            }

            yield return null;
        }

        float timer = 0f;
        while (waitingReleaseEvent && timer < releaseTimeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (waitingReleaseEvent)
        {
            waitingReleaseEvent = false;
            FireArrow(pendingChargePercent);
        }

        yield return new WaitForSeconds(postReleaseLock);

        if (skillLockClaimed)
        {
            combat?.InvokeSkillEnd();
            skillLockClaimed = false;
        }

        isCharging = false;
    }

    private void BeginRelease()
    {
        isCharging = false;
        waitingReleaseEvent = true;
        pendingChargePercent = Mathf.Clamp01(chargeTimer / Mathf.Max(0.01f, maxChargeTime));

        if (enemyAnim != null)
            enemyAnim.TriggerBowChargeRelease();
    }

    public void ReleaseFromAnimationEvent()
    {
        if (!waitingReleaseEvent)
            return;

        waitingReleaseEvent = false;
        FireArrow(pendingChargePercent);
    }

    private void FireArrow(float charge)
    {
        if (firePoint == null || arrowPrefab == null)
            return;

        GameObject obj = Instantiate(arrowPrefab, firePoint.position, Quaternion.identity);
        if (obj == null)
            return;

        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        ArrowDamage dmg = obj.GetComponent<ArrowDamage>();

        float direction = GetFacingDirection();
        ApplyArrowFacing(obj, direction);

        float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, charge);

        if (dmg != null)
        {
            dmg.SetOwner(owner);
            dmg.SetStats(
                baseDamage,
                baseKnockback * Mathf.Max(0.1f, charge),
                baseStun + (0.1f * charge),
                false,
                false
            );
        }

        if (rb != null)
            StartCoroutine(ArrowRoutine(rb, obj, speed, direction));
        else
            Destroy(obj, 1f);
    }

    private IEnumerator ArrowRoutine(Rigidbody2D rb, GameObject arrowObj, float speedValue, float dir)
    {
        if (rb == null || arrowObj == null)
            yield break;

        float timer = 0f;
        while (timer < straightTime)
        {
            if (rb == null || arrowObj == null)
                yield break;

            rb.velocity = new Vector2(dir * speedValue, 0f);
            timer += Time.deltaTime;
            yield return null;
        }

        float t = 0f;
        while (t < 1f)
        {
            if (rb == null || arrowObj == null)
                yield break;

            float g = Mathf.Lerp(gravityStart, gravityEnd, t);
            rb.velocity = new Vector2(
                dir * speedValue,
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

    private IEnumerator CooldownRoutine()
    {
        cooldownRunning = true;
        yield return new WaitForSeconds(cooldown);
        cooldownRunning = false;
    }

    private float GetDistanceToPlayer()
    {
        if (ai == null || ai.playerTransform == null)
            return Mathf.Infinity;

        return Vector2.Distance(transform.position, ai.playerTransform.position);
    }

    private float GetFacingDirection()
    {
        if (ai != null)
            return ai.IsFacingRight ? 1f : -1f;

        if (owner != null)
            return owner.isFacingRight ? 1f : -1f;

        if (facingSprite != null)
            return facingSprite.flipX ? -1f : 1f;

        return 1f;
    }

    private void ApplyArrowFacing(GameObject arrowObj, float direction)
    {
        if (arrowObj == null) return;

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

    private void StopOwnerMovement()
    {
        if (ownerRb != null)
            ownerRb.velocity = Vector2.zero;
    }

    private Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null) return null;
        if (root.name == targetName) return root;

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
        isCharging = false;
        waitingReleaseEvent = false;
        cooldownRunning = false;
        pendingChargePercent = 0f;

        if (skillLockClaimed)
        {
            combat?.InvokeSkillEnd();
            skillLockClaimed = false;
        }
    }
}