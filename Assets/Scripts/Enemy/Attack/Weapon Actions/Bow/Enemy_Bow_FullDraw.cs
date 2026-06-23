using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

public class Enemy_Bow_FullDraw : MonoBehaviour, ISkill
{
    public enum FullDrawMode
    {
        Normal,
        FullChargePiercing
    }

    [Header("BT / Range")]
    public float minRange = 3.5f;
    public float skillRange = 8f;
    public float rangeTolerance = 0.25f;

    [Header("Full Charge / Piercing Range")]
    public float fullChargeMinRange = 3.5f;
    public float fullChargeSkillRange = 9f;
    public float fullChargeRangeTolerance = 0.25f;

    [Header("References")]
    public Transform firePoint;

    [FormerlySerializedAs("prefabPanahFullDraw")]
    public GameObject arrowPrefab;

    [Tooltip("Prefab panah untuk FullDraw penuh / Piercing.")]
    public GameObject piercingArrowPrefab;

    public SpriteRenderer facingSprite;

    [Header("Charge Settings")]
    public float maxChargeTime = 1.2f;

    [Tooltip("Persentase charge untuk FullDraw biasa. Harus lebih kecil dari fullChargeThreshold.")]
    [Range(0.05f, 0.95f)]
    public float normalReleaseChargePercent = 0.45f;

    [Tooltip("Ambang charge yang dianggap FullDraw penuh / Piercing.")]
    [Range(0.1f, 1f)]
    public float fullChargeThreshold = 0.95f;

    [Header("Normal FullDraw Stats")]
    public float minArrowSpeed = 8f;
    public float maxArrowSpeed = 18f;

    public float minDamage = 4f;
    public float maxDamage = 10f;

    public float minKnockback = 2f;
    public float maxKnockback = 6f;

    public float minStun = 0.05f;
    public float maxStun = 0.15f;

    [Header("Full Charge / Piercing Stats")]
    public float piercingArrowSpeed = 20f;
    public float piercingDamage = 10f;
    public float piercingKnockback = 0f;
    public float piercingStun = 0.25f;

    [Header("Piercing Straight Flight")]
    public bool piercingStraightFlight = true;
    public float piercingLifetime = 0.9f;
    public bool forcePiercingHorizontal = true;

    [Header("Normal FullDraw Flight Curve")]
    public float flightDuration = 0.55f;
    public float fallStartTime = 0.09f;
    public float initialLift = 0.4f;
    public float fallCurveStrength = 7f;
    public bool rotateArrowToVelocity = true;

    [Header("Cleanup")]
    public float destroyDelay = 0.25f;

    [Header("Timing")]
    public float cooldown = 0.8f;
    public float postReleaseLock = 0.08f;
    public float releaseTimeout = 0.7f;

    [Header("Animation")]
    public bool useAnimationEvent = true;

    [Header("SFX Timing")]
    [Tooltip("Aktifkan jika SFX Full Draw musuh ingin dikendalikan dari script ini.")]
    public bool playSfxFromScript = true;

    [Tooltip("Suara tarikan busur diputar satu kali saat charge Full Draw dimulai.")]
    public bool playBowDrawOnChargeStart = true;

    [Tooltip("Suara panah meluncur diputar saat panah Full Draw dilepas.")]
    public bool playLaunchSfxOnRelease = true;

    [Tooltip("Suara panah menancap diputar jika panah tidak mengenai target sampai akhir durasi terbang.")]
    public bool playGroundMissSfx = true;

    [Tooltip("Suara hit diputar oleh projectile saat panah mengenai target.")]
    public bool playHitSfx = true;

    [Header("Default Mode")]
    [Tooltip("Dipakai jika node lama masih memanggil Trigger() tanpa menentukan Normal atau FullCharge.")]
    public FullDrawMode defaultMode = FullDrawMode.Normal;

    public bool IsActive => isCharging || waitingReleaseEvent || isReleasing;

    private NodeManager ai;
    private EnemyCombatController combat;
    private EnemyAnimation enemyAnim;
    private CharacterBase owner;
    private Rigidbody2D ownerRb;

    private bool isCharging;
    private bool waitingReleaseEvent;
    private bool isReleasing;
    private bool cooldownRunning;
    private bool skillLockClaimed;
    private bool releaseAlreadyFired;

    private float chargeTimer;
    private float pendingChargePercent;
    private bool pendingPiercing;
    private FullDrawMode pendingMode;

    private Coroutine castRoutine;
    private Coroutine cooldownRoutine;

    private void Awake()
    {
        ai = GetComponentInParent<NodeManager>();
        combat = GetComponentInParent<EnemyCombatController>();
        owner = GetComponentInParent<CharacterBase>();
        ownerRb = GetComponentInParent<Rigidbody2D>();

        if (ai != null)
            enemyAnim = ai.GetComponentInChildren<EnemyAnimation>(true);

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
        return IsInRangeForMode(distanceToPlayer, defaultMode);
    }

    public bool IsInRangeForMode(float distanceToPlayer, FullDrawMode mode)
    {
        if (mode == FullDrawMode.FullChargePiercing)
        {
            return distanceToPlayer >= (fullChargeMinRange - fullChargeRangeTolerance) &&
                   distanceToPlayer <= (fullChargeSkillRange + fullChargeRangeTolerance);
        }

        return distanceToPlayer >= (minRange - rangeTolerance) &&
               distanceToPlayer <= (skillRange + rangeTolerance);
    }

    public bool CanTrigger(float distanceToPlayer)
    {
        return CanTriggerMode(distanceToPlayer, defaultMode);
    }

    public bool CanTriggerNormal(float distanceToPlayer)
    {
        return CanTriggerMode(distanceToPlayer, FullDrawMode.Normal);
    }

    public bool CanTriggerFullCharge(float distanceToPlayer)
    {
        return CanTriggerMode(distanceToPlayer, FullDrawMode.FullChargePiercing);
    }

    public bool CanTriggerMode(float distanceToPlayer, FullDrawMode mode)
    {
        if (cooldownRunning) return false;
        if (isCharging || waitingReleaseEvent || isReleasing) return false;
        if (combat != null && combat.IsBusy) return false;
        if (!IsInRangeForMode(distanceToPlayer, mode)) return false;
        if (firePoint == null) return false;

        if (mode == FullDrawMode.FullChargePiercing)
        {
            if (piercingArrowPrefab == null && arrowPrefab == null)
                return false;
        }
        else
        {
            if (arrowPrefab == null)
                return false;
        }

        return true;
    }

    public void Trigger()
    {
        TriggerMode(defaultMode);
    }

    public void TriggerNormal()
    {
        TriggerMode(FullDrawMode.Normal);
    }

    public void TriggerFullCharge()
    {
        TriggerMode(FullDrawMode.FullChargePiercing);
    }

    public void TriggerSkill(int slot)
    {
        Trigger();
    }

    public void TriggerSkillNormal()
    {
        TriggerNormal();
    }

    public void TriggerSkillFullCharge()
    {
        TriggerFullCharge();
    }

    public void TriggerMode(FullDrawMode mode)
    {
        float distance = GetDistanceToPlayer();

        if (!CanTriggerMode(distance, mode))
            return;

        pendingMode = mode;

        if (castRoutine != null)
            StopCoroutine(castRoutine);

        castRoutine = StartCoroutine(CastRoutine(mode));
    }

    private IEnumerator CastRoutine(FullDrawMode mode)
    {
        isCharging = true;
        waitingReleaseEvent = false;
        isReleasing = false;
        releaseAlreadyFired = false;
        skillLockClaimed = true;

        chargeTimer = 0f;
        pendingChargePercent = 0f;
        pendingPiercing = false;
        pendingMode = mode;

        combat?.InvokeSkillStart();
        StopOwnerMovement();

        if (cooldownRoutine != null)
            StopCoroutine(cooldownRoutine);

        cooldownRoutine = StartCoroutine(CooldownRoutine());

        if (playBowDrawOnChargeStart)
            PlayBowDrawSfx();

        if (enemyAnim != null)
        {
            enemyAnim.TriggerBowChargeStart();

            /*
             * Jika EnemyAnimation Anda sudah memiliki parameter isFullCharge,
             * method SetBowFullCharge dapat ditambahkan di EnemyAnimation.
             * Revisi ini tetap aman walaupun method tersebut belum ada.
             */
            TrySetEnemyFullChargeFlag(false);
        }

        float targetCharge = mode == FullDrawMode.FullChargePiercing
            ? 1f
            : Mathf.Min(normalReleaseChargePercent, fullChargeThreshold - 0.01f);

        targetCharge = Mathf.Clamp01(targetCharge);

        while (isCharging)
        {
            chargeTimer += Time.deltaTime;

            float charge01 = Mathf.Clamp01(chargeTimer / Mathf.Max(0.01f, maxChargeTime));
            bool reachedTarget = charge01 >= targetCharge;

            if (mode == FullDrawMode.FullChargePiercing)
                TrySetEnemyFullChargeFlag(charge01 >= fullChargeThreshold);

            if (reachedTarget)
            {
                BeginRelease(mode);
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

        if (waitingReleaseEvent && !releaseAlreadyFired)
        {
            waitingReleaseEvent = false;
            FireByPendingMode();
        }

        if (postReleaseLock > 0f)
            yield return new WaitForSeconds(postReleaseLock);

        FinishCast();
    }

    private void BeginRelease(FullDrawMode mode)
    {
        isCharging = false;
        isReleasing = true;
        waitingReleaseEvent = useAnimationEvent;
        releaseAlreadyFired = false;

        pendingChargePercent = Mathf.Clamp01(chargeTimer / Mathf.Max(0.01f, maxChargeTime));
        pendingPiercing =
            mode == FullDrawMode.FullChargePiercing ||
            pendingChargePercent >= fullChargeThreshold;

        TrySetEnemyFullChargeFlag(pendingPiercing);

        if (enemyAnim != null)
            enemyAnim.TriggerBowChargeRelease();

        if (!useAnimationEvent)
            ReleaseFromAnimationEvent();
    }

    public void ReleaseFromAnimationEvent()
    {
        if (!isReleasing || releaseAlreadyFired)
            return;

        waitingReleaseEvent = false;
        releaseAlreadyFired = true;

        FireByPendingMode();
    }

    public void ReleaseFullDraw()
    {
        ReleaseFromAnimationEvent();
    }

    public void Release()
    {
        ReleaseFromAnimationEvent();
    }

    public void Shoot()
    {
        ReleaseFromAnimationEvent();
    }

    private void FireByPendingMode()
    {
        if (pendingPiercing)
            FirePiercingArrow();
        else
            FireNormalFullDrawArrow(pendingChargePercent);
    }

    private void FireNormalFullDrawArrow(float charge)
    {
        float threshold = Mathf.Max(0.0001f, fullChargeThreshold);
        float factor = Mathf.Clamp01(charge / threshold);

        float speed = Mathf.Lerp(minArrowSpeed, maxArrowSpeed, factor);
        float damage = Mathf.Lerp(minDamage, maxDamage, factor);
        float knockback = Mathf.Lerp(minKnockback, maxKnockback, factor);
        float stun = Mathf.Lerp(minStun, maxStun, factor);

        CreateArrow(arrowPrefab, speed, damage, knockback, stun, false);
    }

    private void FirePiercingArrow()
    {
        GameObject prefab = piercingArrowPrefab != null ? piercingArrowPrefab : arrowPrefab;

        CreateArrow(
            prefab,
            piercingArrowSpeed,
            piercingDamage,
            piercingKnockback,
            piercingStun,
            true
        );
    }

    private void CreateArrow(
        GameObject prefab,
        float speed,
        float damage,
        float knockback,
        float stun,
        bool piercing
    )
    {
        if (firePoint == null || prefab == null)
            return;

        GameObject obj = Instantiate(prefab, firePoint.position, Quaternion.identity);

        if (obj == null)
            return;

        if (playLaunchSfxOnRelease)
            PlayFullDrawLaunchSfx(piercing);

        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        ArrowDamage dmg = obj.GetComponent<ArrowDamage>();

        float direction = GetFacingDirection();

        ApplyArrowFacing(obj, direction);

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        if (dmg != null)
        {
            dmg.SetOwner(owner);
            dmg.SetStats(
                damage,
                knockback,
                stun,
                piercing,
                false
            );
        }

        SetupProjectileSfx(obj);

        if (rb != null)
        {
            if (piercing && piercingStraightFlight)
                StartCoroutine(PiercingStraightRoutine(rb, obj, speed, direction));
            else
                StartCoroutine(NormalFullDrawArrowRoutine(rb, obj, speed, direction));
        }
        else
        {
            Destroy(obj, 1f);
        }
    }

    private IEnumerator NormalFullDrawArrowRoutine(
        Rigidbody2D rb,
        GameObject arrowObj,
        float speedValue,
        float dir
    )
    {
        if (rb == null || arrowObj == null)
            yield break;

        float timer = 0f;
        float vy = initialLift;
        float fallStart = Mathf.Clamp(
            fallStartTime,
            0f,
            Mathf.Max(0.01f, flightDuration - 0.01f)
        );

        while (timer < flightDuration)
        {
            if (rb == null || arrowObj == null)
                yield break;

            if (timer >= fallStart)
                vy -= fallCurveStrength * Time.deltaTime;

            rb.velocity = new Vector2(dir * speedValue, vy);

            if (rotateArrowToVelocity)
                ApplyArrowRotation(rb);

            timer += Time.deltaTime;
            yield return null;
        }

        if (rb == null || arrowObj == null)
            yield break;

        rb.velocity = Vector2.zero;

        yield return new WaitForSeconds(destroyDelay);

        PlayGroundMissIfArrowStillActive(arrowObj);

        if (arrowObj != null)
            Destroy(arrowObj);
    }

    private IEnumerator PiercingStraightRoutine(
        Rigidbody2D rb,
        GameObject arrowObj,
        float speedValue,
        float dir
    )
    {
        if (rb == null || arrowObj == null)
            yield break;

        float timer = 0f;

        rb.gravityScale = 0f;
        rb.angularVelocity = 0f;
        rb.transform.rotation = Quaternion.Euler(0f, 0f, dir > 0f ? 0f : 180f);

        while (timer < piercingLifetime)
        {
            if (rb == null || arrowObj == null)
                yield break;

            if (forcePiercingHorizontal)
                rb.velocity = new Vector2(dir * speedValue, 0f);
            else
                rb.velocity = new Vector2(dir * speedValue, rb.velocity.y);

            timer += Time.deltaTime;
            yield return null;
        }

        PlayGroundMissIfArrowStillActive(arrowObj);

        if (arrowObj != null)
            Destroy(arrowObj);
    }

    private IEnumerator CooldownRoutine()
    {
        cooldownRunning = true;
        yield return new WaitForSeconds(cooldown);
        cooldownRunning = false;
        cooldownRoutine = null;
    }

    private void FinishCast()
    {
        StopOwnerMovement();

        isCharging = false;
        waitingReleaseEvent = false;
        isReleasing = false;
        releaseAlreadyFired = false;

        pendingChargePercent = 0f;
        pendingPiercing = false;

        TrySetEnemyFullChargeFlag(false);

        if (skillLockClaimed)
        {
            combat?.InvokeSkillEnd();
            skillLockClaimed = false;
        }

        castRoutine = null;
    }

    private float GetDistanceToPlayer()
    {
        if (ai == null || ai.playerTransform == null)
            return Mathf.Infinity;

        return Vector2.Distance(transform.position, ai.playerTransform.position);
    }

    public float GetRangeForMode(FullDrawMode mode)
    {
        return mode == FullDrawMode.FullChargePiercing
            ? fullChargeSkillRange
            : skillRange;
    }

    public float GetMinRangeForMode(FullDrawMode mode)
    {
        return mode == FullDrawMode.FullChargePiercing
            ? fullChargeMinRange
            : minRange;
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
        if (arrowObj == null)
            return;

        Vector3 scale = arrowObj.transform.localScale;

        arrowObj.transform.localScale = new Vector3(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z)
        );

        arrowObj.transform.rotation = Quaternion.Euler(
            0f,
            0f,
            direction > 0f ? 0f : 180f
        );

        SpriteRenderer[] renderers = arrowObj.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].flipX = false;
    }

    private void ApplyArrowRotation(Rigidbody2D rb)
    {
        if (rb == null || rb.velocity.sqrMagnitude <= 0.0001f)
            return;

        float angle = Mathf.Atan2(rb.velocity.y, rb.velocity.x) * Mathf.Rad2Deg;
        rb.transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void StopOwnerMovement()
    {
        if (ownerRb != null)
            ownerRb.velocity = Vector2.zero;
    }

    private void TrySetEnemyFullChargeFlag(bool value)
    {
        if (enemyAnim == null)
            return;

        /*
         * Aman meskipun EnemyAnimation belum memiliki method SetBowFullCharge.
         * Dipanggil memakai SendMessage agar tidak membuat compile error.
         */
        enemyAnim.SendMessage("SetBowFullCharge", value, SendMessageOptions.DontRequireReceiver);
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

    private void PlayBowDrawSfx()
    {
        if (!playSfxFromScript) return;
        if (SFXManager.Instance == null) return;

        SFXManager.Instance.ResetBowDrawGate();
        SFXManager.Instance.PlayBowDrawGuarded();
    }

    private void PlayFullDrawLaunchSfx(bool piercing)
    {
        AudioClip clip = piercing
            ? (SFXManager.Instance != null ? SFXManager.Instance.arrowLaunchCharged : null)
            : (SFXManager.Instance != null ? SFXManager.Instance.arrowLaunchNormal : null);

        PlaySfx(clip);
    }

    private void SetupProjectileSfx(GameObject arrowObj)
    {
        if (arrowObj == null) return;
        if (!playSfxFromScript) return;

        BowProjectileSFX reporter = arrowObj.GetComponent<BowProjectileSFX>();
        if (reporter == null)
            reporter = arrowObj.AddComponent<BowProjectileSFX>();

        reporter.Setup(owner, playGroundMissSfx, playHitSfx);
    }

    private void PlayGroundMissIfArrowStillActive(GameObject arrowObj)
    {
        if (arrowObj == null) return;
        if (!playSfxFromScript) return;

        BowProjectileSFX reporter = arrowObj.GetComponent<BowProjectileSFX>();
        if (reporter != null)
            reporter.PlayGroundMissIfNotPlayed();
    }

    private void PlaySfx(AudioClip clip)
    {
        if (!playSfxFromScript) return;
        if (clip == null) return;
        if (SFXManager.Instance == null) return;
        if (SFXManager.Instance.sfxSource == null) return;

        SFXManager.Instance.PlaySFX(clip);
    }

    private void OnDisable()
    {
        if (castRoutine != null)
        {
            StopCoroutine(castRoutine);
            castRoutine = null;
        }

        isCharging = false;
        waitingReleaseEvent = false;
        isReleasing = false;
        releaseAlreadyFired = false;

        pendingChargePercent = 0f;
        pendingPiercing = false;

        TrySetEnemyFullChargeFlag(false);

        if (skillLockClaimed)
        {
            combat?.InvokeSkillEnd();
            skillLockClaimed = false;
        }
    }
}