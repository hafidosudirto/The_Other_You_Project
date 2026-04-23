using System.Collections;
using UnityEngine;

public class Enemy_Bow_ConcussiveShot : MonoBehaviour, ISkill
{
    [Header("BT / Range")]
    public float minRange = 0f;
    public float skillRange = 3.25f;
    public float rangeTolerance = 0.2f;

    [Header("Mode")]
    [Tooltip("OFF = langsung spawn hit area. ON = panah visual digerakkan via coroutine internal.")]
    public bool useArrowVisual = false;

    [Header("References")]
    public Transform firePoint;
    public GameObject arrowPrefab;
    public GameObject hitAreaPrefab;
    public SpriteRenderer facingSprite;

    [Header("Explosion Settings")]
    public float damage = 10f;
    public float knockback = 6f;
    public float stun = 0.35f;
    public float explosionRadius = 1.4f;

    [Header("Mini-Jump")]
    public float jumpHeight = 2f;
    public float jumpUpTime = 0.5f;
    public float hoverTime = 0.2f;
    public float jumpDownTime = 0.3f;
    public float delayBeforeFall = 0.05f;

    [Header("Ground Fallback")]
    public Vector2 hitAreaOffset = new Vector2(1.2f, -0.2f);
    public LayerMask groundMask;

    [Header("Visual Arrow Flight")]
    public float visualArrowSpeed = 12f;
    public float visualArrowGravity = 8f;
    [Range(0f, 1f)] public float visualInitialDownFactor = 0.22f;
    public float visualRayDistance = 0.15f;
    public float visualArmDelay = 0.05f;
    public float visualLifeTime = 3f;
    public float visualAngleOffset = 0f;
    public float visualOverlapRadius = 0.08f;
    public LayerMask visualImpactMask = ~0;

    [Header("Timing")]
    public float cooldown = 1.0f;

    public bool IsActive => isCasting;

    private EnemyAI ai;
    private EnemyCombatController combat;
    private EnemyAnimation enemyAnim;
    private CharacterBase owner;
    private Rigidbody2D ownerRb;

    private bool isCasting;
    private bool releaseExecuted;
    private bool cooldownRunning;
    private bool skillLockClaimed;

    private Coroutine jumpRoutine;

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
        if (hitAreaPrefab == null) return false;
        if (firePoint == null) return false;
        return true;
    }

    public void Trigger()
    {
        float distance = GetDistanceToPlayer();
        if (!CanTrigger(distance))
            return;

        if (jumpRoutine != null)
            StopCoroutine(jumpRoutine);

        jumpRoutine = StartCoroutine(CastRoutine());
    }

    public void TriggerSkill(int slot)
    {
        Trigger();
    }

    private IEnumerator CastRoutine()
    {
        isCasting = true;
        releaseExecuted = false;
        skillLockClaimed = true;

        combat?.InvokeSkillStart();
        StopOwnerMovement();
        StartCoroutine(CooldownRoutine());

        if (enemyAnim != null)
            enemyAnim.PlayConcussiveShot();

        yield return StartCoroutine(JumpShotRoutine(owner != null ? owner.transform : transform));

        if (skillLockClaimed)
        {
            combat?.InvokeSkillEnd();
            skillLockClaimed = false;
        }

        isCasting = false;
        jumpRoutine = null;
    }

    private IEnumerator JumpShotRoutine(Transform target)
    {
        float peak = jumpHeight;
        float lastOffset = 0f;

        float t = 0f;
        while (t < jumpUpTime)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / Mathf.Max(0.01f, jumpUpTime));
            float eased = Mathf.Sin(n * Mathf.PI * 0.5f);

            float newOffset = Mathf.Lerp(0f, peak, eased);
            float delta = newOffset - lastOffset;
            target.Translate(0f, delta, 0f);
            lastOffset = newOffset;

            yield return null;
        }

        if (hoverTime > 0f)
            yield return new WaitForSeconds(hoverTime);

        ExecuteRelease();

        if (delayBeforeFall > 0f)
            yield return new WaitForSeconds(delayBeforeFall);

        t = 0f;
        while (t < jumpDownTime)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / Mathf.Max(0.01f, jumpDownTime));
            float eased = 1f - Mathf.Cos(n * Mathf.PI * 0.5f);

            float newOffset = Mathf.Lerp(peak, 0f, eased);
            float delta = newOffset - lastOffset;
            target.Translate(0f, delta, 0f);
            lastOffset = newOffset;

            yield return null;
        }
    }

    public void ReleaseFromAnimationEvent()
    {
        if (!isCasting)
            return;

        // Untuk versi enemy ini, release utama tetap dikontrol oleh coroutine mini-jump.
        // Event animasi tidak wajib, tetapi tetap aman bila dipanggil.
    }

    private void ExecuteRelease()
    {
        if (releaseExecuted)
            return;

        releaseExecuted = true;

        if (!useArrowVisual || arrowPrefab == null)
        {
            SpawnHitAreaDirectGroundRaycast();
            return;
        }

        StartCoroutine(VisualArrowRoutine());
    }

    private void SpawnHitAreaDirectGroundRaycast()
    {
        float dir = GetFacingDirection();

        Vector2 rayOrigin = (Vector2)firePoint.position + new Vector2(0.6f * dir, 0f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 10f, groundMask);

        Vector3 spawnPos;
        if (hit.collider != null)
            spawnPos = hit.point;
        else
            spawnPos = firePoint.position + new Vector3(hitAreaOffset.x * dir, hitAreaOffset.y, 0f);

        SpawnHitArea(spawnPos);
    }

    private IEnumerator VisualArrowRoutine()
    {
        GameObject arrowObj = Instantiate(arrowPrefab, firePoint.position, Quaternion.identity);
        if (arrowObj == null)
            yield break;

        float dir = GetFacingDirection();
        ApplyArrowFacing(arrowObj, dir);

        Vector2 velocity = new Vector2(
            visualArrowSpeed * dir,
            -visualArrowSpeed * visualInitialDownFactor
        );

        float lifeTimer = 0f;
        float armTimer = 0f;
        bool exploded = false;

        while (!exploded && arrowObj != null)
        {
            float dt = Time.deltaTime;
            lifeTimer += dt;
            armTimer += dt;

            if (lifeTimer >= visualLifeTime)
            {
                if (arrowObj != null)
                    Destroy(arrowObj);
                yield break;
            }

            Vector3 previous = arrowObj.transform.position;

            velocity.y -= visualArrowGravity * dt;
            arrowObj.transform.position += (Vector3)(velocity * dt);

            if (velocity.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
                arrowObj.transform.rotation = Quaternion.Euler(0f, 0f, angle + visualAngleOffset);
            }

            if (armTimer < visualArmDelay)
            {
                yield return null;
                continue;
            }

            Vector2 rayDir = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : new Vector2(dir, 0f);
            float rayLength = Vector2.Distance(previous, arrowObj.transform.position) + visualRayDistance;

            RaycastHit2D groundHit = Physics2D.Raycast(previous, rayDir, rayLength, groundMask);
            if (groundHit.collider != null)
            {
                SpawnHitArea(groundHit.point);
                exploded = true;
                break;
            }

            Collider2D[] overlaps = Physics2D.OverlapCircleAll(
                arrowObj.transform.position,
                visualOverlapRadius,
                visualImpactMask
            );

            for (int i = 0; i < overlaps.Length; i++)
            {
                Collider2D c = overlaps[i];
                if (c == null) continue;

                CharacterBase target = c.GetComponent<CharacterBase>() ??
                                       c.GetComponentInParent<CharacterBase>();

                if (target != null)
                {
                    if (owner != null && target == owner)
                        continue;

                    SpawnHitArea(arrowObj.transform.position);
                    exploded = true;
                    break;
                }

                int otherMask = 1 << c.gameObject.layer;
                if ((groundMask.value & otherMask) != 0)
                {
                    SpawnHitArea(arrowObj.transform.position);
                    exploded = true;
                    break;
                }
            }

            yield return null;
        }

        if (arrowObj != null)
            Destroy(arrowObj);
    }

    private void SpawnHitArea(Vector3 position)
    {
        if (hitAreaPrefab == null)
            return;

        GameObject area = Instantiate(hitAreaPrefab, position, Quaternion.identity);
        ConcussiveHitArea ch = area.GetComponent<ConcussiveHitArea>();
        if (ch != null)
            ch.Setup(owner, damage, knockback, stun, explosionRadius);
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
        releaseExecuted = false;
        cooldownRunning = false;

        if (skillLockClaimed)
        {
            combat?.InvokeSkillEnd();
            skillLockClaimed = false;
        }

        isCasting = false;
        jumpRoutine = null;
    }
}