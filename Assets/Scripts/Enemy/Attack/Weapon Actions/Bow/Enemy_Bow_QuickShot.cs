using System.Collections;
using UnityEngine;

public class Enemy_Bow_QuickShot : MonoBehaviour, ISkill
{
    [Header("BT / Range")]
    public float minRange = 1.5f;
    public float skillRange = 5f;
    public float rangeTolerance = 0.2f;

    [Header("References")]
    public Transform firePoint;
    public GameObject arrowPrefab;
    public SpriteRenderer facingSprite;

    [Header("Arrow Settings")]
    public float speed = 10f;

    [Header("Hit Effects")]
    public float quickDamage = 1f;
    public float knockback = 1.5f;
    public float stun = 0.1f;

    [Header("Flight Curve")]
    public float straightTime = 0.45f;
    public float gravityStart = 3f;
    public float gravityEnd = 12f;

    [Header("Cleanup")]
    public float destroyDelay = 0.25f;

    [Header("Timing")]
    public float cooldown = 0.35f;
    public float postShotLock = 0.05f;
    public float releaseTimeout = 0.65f;

    public bool IsActive => isCasting;

    private EnemyAI ai;
    private EnemyCombatController combat;
    private EnemyAnimation enemyAnim;
    private CharacterBase owner;
    private Rigidbody2D ownerRb;

    private bool isCasting;
    private bool waitingAnimationRelease;
    private bool cooldownRunning;
    private bool skillLockClaimed;

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
        if (isCasting) return false;
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
        isCasting = true;
        waitingAnimationRelease = true;
        skillLockClaimed = true;

        combat?.InvokeSkillStart();
        StopOwnerMovement();
        StartCoroutine(CooldownRoutine());

        if (enemyAnim != null)
            enemyAnim.PlayQuickShot();

        float timer = 0f;
        while (waitingAnimationRelease && timer < releaseTimeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (waitingAnimationRelease)
        {
            waitingAnimationRelease = false;
            ShootArrow();
        }

        yield return new WaitForSeconds(postShotLock);

        if (skillLockClaimed)
        {
            combat?.InvokeSkillEnd();
            skillLockClaimed = false;
        }

        isCasting = false;
    }

    public void ReleaseFromAnimationEvent()
    {
        if (!isCasting || !waitingAnimationRelease)
            return;

        waitingAnimationRelease = false;
        ShootArrow();
    }

    private IEnumerator CooldownRoutine()
    {
        cooldownRunning = true;
        yield return new WaitForSeconds(cooldown);
        cooldownRunning = false;
    }

    private void ShootArrow()
    {
        if (firePoint == null || arrowPrefab == null)
            return;

        GameObject arrowObj = Instantiate(arrowPrefab, firePoint.position, Quaternion.identity);
        if (arrowObj == null)
            return;

        Rigidbody2D rb = arrowObj.GetComponent<Rigidbody2D>();
        ArrowDamage dmg = arrowObj.GetComponent<ArrowDamage>();

        float dir = GetFacingDirection();
        ApplyArrowFacing(arrowObj, dir);

        if (dmg != null)
        {
            dmg.SetOwner(owner);
            dmg.SetStats(quickDamage, knockback, stun, false, false);
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

        float timer = 0f;
        while (timer < straightTime)
        {
            if (rb == null || arrowObj == null)
                yield break;

            rb.velocity = new Vector2(dir * speed, 0f);
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
        waitingAnimationRelease = false;
        cooldownRunning = false;

        if (skillLockClaimed)
        {
            combat?.InvokeSkillEnd();
            skillLockClaimed = false;
        }

        isCasting = false;
    }
}