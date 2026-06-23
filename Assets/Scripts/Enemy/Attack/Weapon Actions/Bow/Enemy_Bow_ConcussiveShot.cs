using System.Collections;
using UnityEngine;

public class Enemy_Bow_ConcussiveShot : MonoBehaviour, ISkill
{
    public enum EnemyConcussiveJumpMode
    {
        SmoothJump,
        VanishPopJump
    }

    [Header("BT / Range")]
    public float minRange = 0f;
    public float skillRange = 4.5f;
    public float rangeTolerance = 0.35f;

    [Header("Mode")]
    [Tooltip("ON = enemy menembakkan panah visual ke tanah dekat player, lalu ledakan muncul.")]
    public bool useArrowVisual = true;

    [Tooltip("ON = ledakan diarahkan ke posisi tanah player saat release.")]
    public bool aimExplosionAtPlayer = true;

    [Header("References")]
    public Transform firePoint;
    public GameObject arrowPrefab;
    public GameObject hitAreaPrefab;
    public SpriteRenderer facingSprite;

    [Tooltip("Isi dengan child Visual milik enemy. Yang naik adalah visual, bukan root enemy.")]
    public Transform visualRoot;

    [Tooltip("Sprite renderer visual enemy. Dipakai untuk vanish pop.")]
    public SpriteRenderer[] visualRenderers;

    [Tooltip("Opsional. Jika ada titik kaki enemy, dipakai sebagai fallback tinggi tanah.")]
    public Transform titikKaki;

    [Header("Special Jump")]
    public EnemyConcussiveJumpMode jumpMode = EnemyConcussiveJumpMode.VanishPopJump;

    [Tooltip("Tinggi visual enemy saat muncul di udara.")]
    public float jumpHeight = 1.45f;

    [Tooltip("Durasi visual naik menuju posisi atas.")]
    public float jumpUpTime = 0.18f;

    [Tooltip("Durasi diam sebentar di atas sebelum release.")]
    public float hoverTime = 0.08f;

    [Tooltip("Durasi visual turun kembali.")]
    public float jumpDownTime = 0.32f;

    [Header("Vanish Pop")]
    [Tooltip("ON = visual baru naik setelah AE_BowConcussive_StartPop dipanggil dari animation clip.")]
    public bool pakaiStartPopAnimationEvent = true;

    [Tooltip("Jika AE_BowConcussive_StartPop tidak terpanggil, visual tetap lanjut otomatis setelah durasi ini.")]
    public float startPopEventTimeout = 0.35f;

    [Tooltip("Fallback jika tidak memakai StartPop Animation Event.")]
    public float fallbackTahanBlinkDiTanah = 0.16f;

    [Tooltip("Jika ON, visual dibuat transparan saat vanish pop.")]
    public bool fadeVisualSaatSpecialJump = true;

    public float durasiFadeHilang = 0.06f;
    public float durasiTidakTerlihat = 0.04f;
    public float durasiFadeMuncul = 0.08f;

    [Header("Animation Event")]
    [Tooltip("ON = release utama menunggu AE_BowConcussive_Release. Jika event gagal, fallback tetap menembak.")]
    public bool pakaiAnimationEvent = true;

    [Tooltip("Jika AE_BowConcussive_Release tidak terpanggil, release dilakukan otomatis setelah durasi ini.")]
    public float releaseEventTimeout = 0.65f;

    [Tooltip("Jeda setelah release sebelum visual mulai turun.")]
    public float delayBeforeFall = 0.1f;

    [Header("Arrow Setup")]
    public float arrowSpeed = 22f;

    [Tooltip("Jarak toleransi impact. Semakin besar, ledakan semakin cepat dianggap mengenai target.")]
    public float arrowArriveDistance = 0.18f;

    [Tooltip("Batas waktu panah visual sebelum dipaksa meledak di target.")]
    public float arrowFlightTimeout = 0.45f;

    [Tooltip("Waktu panah tetap terlihat di tanah sebelum ledakan. Gunakan 0 agar langsung meledak saat impact.")]
    public float delayExplosionAfterArrowArrive = 0f;

    [Tooltip("Jika aktif, target ledakan diperbarui selama panah terbang agar lebih pas ke posisi player.")]
    public bool updateTargetDuringArrowFlight = true;

    [Tooltip("Jika aktif, panah langsung di-snap ke titik target dan meledak ketika target sudah bisa dicapai dalam satu frame.")]
    public bool explodeAsSoonAsReachable = true;

    public bool rotateArrowToVelocity = true;
    public bool resetTrailSaatSpawn = true;

    [Header("Ground / Targeting")]
    public LayerMask groundMask;

    [Tooltip("Raycast tanah dimulai setinggi ini dari target ledakan.")]
    public float groundRaycastHeight = 2.5f;

    [Tooltip("Jarak raycast ke bawah untuk mencari tanah.")]
    public float groundRaycastDistance = 6f;

    [Tooltip("Offset akhir ledakan dari titik tanah.")]
    public Vector2 explosionGroundOffset = new Vector2(0f, 0.05f);

    [Tooltip("Fallback jika aimExplosionAtPlayer = false.")]
    public Vector2 hitAreaOffset = new Vector2(1.2f, -0.2f);

    [Header("Explosion Settings")]
    public float damage = 10f;
    public float knockback = 6f;
    public float stun = 0.35f;

    [Tooltip("Samakan dengan Player. Nilai lama enemy terlalu kecil dan sering tidak mengenai player.")]
    public float explosionRadius = 5f;

    [Header("Timing")]
    public float cooldown = 1.0f;

    [Header("SFX Timing")]
    [Tooltip("Aktifkan jika SFX Concussive Shot musuh ingin dikendalikan dari script ini.")]
    public bool playSfxFromScript = true;

    [Tooltip("Suara tarikan busur diputar satu kali saat casting Concussive Shot dimulai.")]
    public bool playBowDrawOnCastStart = true;

    [Tooltip("Suara panah meluncur opsi 2 diputar ketika enemy mulai melompat.")]
    public bool playJumpLaunchSfx = true;

    [Tooltip("Suara concussive meluncur diputar saat projectile concussive dilepas.")]
    public bool playConcussiveLaunchSfxOnRelease = true;

    [Tooltip("Suara ledakan diputar saat area ledakan concussive muncul.")]
    public bool playConcussiveExplodeSfx = true;

    [Tooltip("Suara hit diputar jika ledakan concussive mendeteksi target di dalam radius.")]
    public bool playHitSfxOnExplosionHit = true;

    private bool jumpSfxPlayed;

    [Header("Debug")]
    public bool debugLog = false;

    public bool IsActive => isCasting;

    private NodeManager ai;
    private EnemyCombatController combat;
    private EnemyAnimation enemyAnim;
    private CharacterBase owner;
    private Rigidbody2D ownerRb;

    private Coroutine castRoutine;
    private Coroutine cooldownRoutine;

    private bool isCasting;
    private bool releaseExecuted;
    private bool startPopReceived;
    private bool cooldownRunning;
    private bool skillLockClaimed;

    private Vector3 visualLocalAwal;
    private float visualYOffset;
    private Color[] warnaAwalVisual;

    private Vector3 cachedExplosionPosition;

    private void Awake()
    {
        AutoAssignReferences();
    }

    [ContextMenu("Auto Assign References")]
    public void AutoAssignReferences()
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

        if (visualRoot == null)
        {
            if (enemyAnim != null)
                visualRoot = enemyAnim.transform;
            else if (facingSprite != null)
                visualRoot = facingSprite.transform;
        }

        if ((visualRenderers == null || visualRenderers.Length == 0) && visualRoot != null)
            visualRenderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);

        if (titikKaki == null)
            titikKaki = FindChildRecursive(transform.root, "TitikKaki");
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
        AutoAssignReferences();

        float distance = GetDistanceToPlayer();

        if (!CanTrigger(distance))
            return;

        if (castRoutine != null)
            StopCoroutine(castRoutine);

        castRoutine = StartCoroutine(CastRoutine());
    }

    public void TriggerSkill(int slot)
    {
        Trigger();
    }

    private IEnumerator CastRoutine()
    {
        isCasting = true;
        releaseExecuted = false;
        startPopReceived = false;
        skillLockClaimed = true;
        jumpSfxPlayed = false;
        visualYOffset = 0f;

        CacheVisualAwal();
        cachedExplosionPosition = GetExplosionTargetPosition();

        combat?.InvokeSkillStart();
        StopOwnerMovement();

        if (cooldownRoutine != null)
            StopCoroutine(cooldownRoutine);

        cooldownRoutine = StartCoroutine(CooldownRoutine());

        if (playBowDrawOnCastStart)
            PlayBowDrawSfx();

        if (enemyAnim != null)
            enemyAnim.PlayConcussiveShot();

        if (debugLog)
            Debug.Log($"[Enemy_Bow_ConcussiveShot] Cast mulai. Target ledakan: {cachedExplosionPosition}", this);

        if (jumpMode == EnemyConcussiveJumpMode.VanishPopJump)
            yield return RoutineVanishPopJump();
        else
            yield return RoutineSmoothJumpUp();

        if (hoverTime > 0f)
            yield return new WaitForSeconds(hoverTime);

        if (!pakaiAnimationEvent)
        {
            ReleaseInternal();
        }
        else
        {
            float timer = 0f;

            while (!releaseExecuted && timer < releaseEventTimeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (!releaseExecuted)
            {
                if (debugLog)
                    Debug.LogWarning("[Enemy_Bow_ConcussiveShot] AE_BowConcussive_Release tidak terpanggil. Release fallback dijalankan.", this);

                ReleaseInternal();
            }
        }

        if (delayBeforeFall > 0f)
            yield return new WaitForSeconds(delayBeforeFall);

        yield return RoutineJumpDown();

        FinishCast();
    }

    private IEnumerator RoutineSmoothJumpUp()
    {
        PlayConcussiveJumpSfxOnce();

        float timer = 0f;
        float duration = Mathf.Max(0.01f, jumpUpTime);

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float n = Mathf.Clamp01(timer / duration);
            float eased = Mathf.Sin(n * Mathf.PI * 0.5f);

            SetVisualYOffset(Mathf.Lerp(0f, jumpHeight, eased));

            yield return null;
        }

        SetVisualYOffset(jumpHeight);
    }

    private IEnumerator RoutineVanishPopJump()
    {
        SetVisualYOffset(0f);
        SetAlphaVisual(1f);

        if (pakaiStartPopAnimationEvent)
        {
            float timer = 0f;

            while (!startPopReceived && timer < startPopEventTimeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (!startPopReceived && debugLog)
                Debug.LogWarning("[Enemy_Bow_ConcussiveShot] AE_BowConcussive_StartPop tidak terpanggil. Pop fallback dijalankan.", this);
        }
        else
        {
            if (fallbackTahanBlinkDiTanah > 0f)
                yield return new WaitForSeconds(fallbackTahanBlinkDiTanah);
        }

        float fadeOutDuration = Mathf.Max(0.01f, durasiFadeHilang);
        float fadeInDuration = Mathf.Max(0.01f, durasiFadeMuncul);

        if (fadeVisualSaatSpecialJump)
        {
            float timer = 0f;

            while (timer < fadeOutDuration)
            {
                timer += Time.deltaTime;

                float n = Mathf.Clamp01(timer / fadeOutDuration);
                SetAlphaVisual(1f - n);

                yield return null;
            }

            SetAlphaVisual(0f);
        }

        if (durasiTidakTerlihat > 0f)
            yield return new WaitForSeconds(durasiTidakTerlihat);

        PlayConcussiveJumpSfxOnce();

        SetVisualYOffset(jumpHeight);

        if (fadeVisualSaatSpecialJump)
        {
            float timer = 0f;

            while (timer < fadeInDuration)
            {
                timer += Time.deltaTime;

                float n = Mathf.Clamp01(timer / fadeInDuration);
                SetAlphaVisual(n);

                yield return null;
            }
        }

        SetAlphaVisual(1f);
    }

    private IEnumerator RoutineJumpDown()
    {
        float timer = 0f;
        float duration = Mathf.Max(0.01f, jumpDownTime);
        float startY = visualYOffset;

        while (timer < duration)
        {
            timer += Time.deltaTime;

            float n = Mathf.Clamp01(timer / duration);
            float eased = n * n * (3f - 2f * n);

            SetVisualYOffset(Mathf.Lerp(startY, 0f, eased));

            yield return null;
        }

        SetVisualYOffset(0f);
    }

    private void SetVisualYOffset(float yOffset)
    {
        visualYOffset = yOffset;

        if (visualRoot != null)
            visualRoot.localPosition = visualLocalAwal + Vector3.up * visualYOffset;
    }

    // =========================================================
    // ANIMATION EVENTS
    // =========================================================

    public void StartPopFromAnimationEvent()
    {
        if (!isCasting)
            return;

        startPopReceived = true;

        if (debugLog)
            Debug.Log("[Enemy_Bow_ConcussiveShot] AE_BowConcussive_StartPop diterima.", this);
    }

    public void StartPop()
    {
        StartPopFromAnimationEvent();
    }

    public void BeginPop()
    {
        StartPopFromAnimationEvent();
    }

    public void BeginPopFromAnimationEvent()
    {
        StartPopFromAnimationEvent();
    }

    public void ReleaseFromAnimationEvent()
    {
        if (!isCasting || releaseExecuted)
            return;

        ReleaseInternal();
    }

    public void ReleaseConcussive()
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

    public void EndRecoveryFromAnimationEvent()
    {
        // Disediakan untuk kompatibilitas animation event.
        // Penyelesaian utama tetap dikontrol coroutine agar tidak tersangkut jika event hilang.
    }

    public void EndConcussiveRecovery()
    {
        EndRecoveryFromAnimationEvent();
    }

    public void EndRecovery()
    {
        EndRecoveryFromAnimationEvent();
    }

    public void FinishRecovery()
    {
        EndRecoveryFromAnimationEvent();
    }

    public void Finish()
    {
        EndRecoveryFromAnimationEvent();
    }

    // =========================================================
    // RELEASE / HIT
    // =========================================================

    private void ReleaseInternal()
    {
        if (releaseExecuted)
            return;

        releaseExecuted = true;
        cachedExplosionPosition = GetExplosionTargetPosition();

        if (debugLog)
            Debug.Log($"[Enemy_Bow_ConcussiveShot] Release. Target ledakan: {cachedExplosionPosition}", this);

        if (playConcussiveLaunchSfxOnRelease)
            PlayConcussiveLaunchSfx();

        if (useArrowVisual && arrowPrefab != null)
            FireVisualArrow();
        else
            SpawnHitAreaAt(cachedExplosionPosition);
    }

    private void FireVisualArrow()
    {
        if (arrowPrefab == null || firePoint == null)
        {
            SpawnHitAreaAt(cachedExplosionPosition);
            return;
        }

        Vector3 spawnPos = GetFirePointWorldPosition();
        GameObject arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);

        if (arrow == null)
        {
            SpawnHitAreaAt(cachedExplosionPosition);
            return;
        }

        Rigidbody2D rb = arrow.GetComponent<Rigidbody2D>();

        if (resetTrailSaatSpawn)
            ResetTrailPanah(arrow);

        float dir = GetFacingDirection();
        ApplyArrowFacing(arrow, dir);

        if (rb == null)
        {
            StartCoroutine(ArrowTransformRoutine(arrow, cachedExplosionPosition));
            return;
        }

        rb.gravityScale = 0f;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;

        StartCoroutine(ArrowRigidbodyRoutine(rb, arrow, cachedExplosionPosition));
    }

    private IEnumerator ArrowRigidbodyRoutine(Rigidbody2D rb, GameObject arrow, Vector3 targetPos)
    {
        float timer = 0f;

        while (rb != null && arrow != null && timer < arrowFlightTimeout)
        {
            if (updateTargetDuringArrowFlight)
                targetPos = GetExplosionTargetPosition();

            Vector2 currentPos = arrow.transform.position;
            Vector2 target = targetPos;
            Vector2 toTarget = target - currentPos;

            float distance = toTarget.magnitude;
            float stepDistance = arrowSpeed * Time.deltaTime;

            /*
             * Ledakan langsung terjadi ketika target sudah dapat dicapai dalam satu frame.
             * Ini menghilangkan delay setelah panah turun.
             */
            if (distance <= arrowArriveDistance ||
                (explodeAsSoonAsReachable && distance <= stepDistance + arrowArriveDistance))
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.simulated = false;

                arrow.transform.position = targetPos;

                SpawnHitAreaAt(targetPos);

                Destroy(arrow, 0.05f);
                yield break;
            }

            Vector2 velocity = toTarget.normalized * arrowSpeed;
            rb.velocity = velocity;

            if (rotateArrowToVelocity)
                RotateArrowToVelocity(rb.transform, velocity);

            timer += Time.deltaTime;
            yield return null;
        }

        /*
         * Fallback:
         * Jika panah tidak pernah mencapai target karena kendala teknis,
         * ledakan tetap dipaksa muncul tepat di target terakhir.
         */
        if (updateTargetDuringArrowFlight)
            targetPos = GetExplosionTargetPosition();

        if (arrow != null)
            arrow.transform.position = targetPos;

        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.simulated = false;
        }

        SpawnHitAreaAt(targetPos);

        if (arrow != null)
            Destroy(arrow, 0.05f);
    }

    private IEnumerator ArrowTransformRoutine(GameObject arrow, Vector3 targetPos)
    {
        float timer = 0f;

        while (arrow != null && timer < arrowFlightTimeout)
        {
            if (updateTargetDuringArrowFlight)
                targetPos = GetExplosionTargetPosition();

            Vector3 currentPos = arrow.transform.position;
            Vector3 toTarget = targetPos - currentPos;

            float distance = toTarget.magnitude;
            float stepDistance = arrowSpeed * Time.deltaTime;

            /*
             * Versi non-Rigidbody:
             * Ledakan langsung terjadi ketika target sudah dapat dicapai dalam satu frame.
             */
            if (distance <= arrowArriveDistance ||
                (explodeAsSoonAsReachable && distance <= stepDistance + arrowArriveDistance))
            {
                arrow.transform.position = targetPos;

                SpawnHitAreaAt(targetPos);

                Destroy(arrow, 0.05f);
                yield break;
            }

            Vector3 step = toTarget.normalized * stepDistance;
            arrow.transform.position += step;

            if (rotateArrowToVelocity && step.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(step.y, step.x) * Mathf.Rad2Deg;
                arrow.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            timer += Time.deltaTime;
            yield return null;
        }

        if (updateTargetDuringArrowFlight)
            targetPos = GetExplosionTargetPosition();

        if (arrow != null)
            arrow.transform.position = targetPos;

        SpawnHitAreaAt(targetPos);

        if (arrow != null)
            Destroy(arrow, 0.05f);
    }

    private void SpawnHitAreaAt(Vector3 position)
    {
        if (hitAreaPrefab == null)
            return;

        GameObject area = Instantiate(hitAreaPrefab, position, Quaternion.identity);

        if (area == null)
            return;

        if (playConcussiveExplodeSfx)
            PlayConcussiveExplodeSfx();

        if (playHitSfxOnExplosionHit && HasExplosionHitTarget(position))
            PlayBowHitSfx();

        ConcussiveHitArea hitArea = area.GetComponent<ConcussiveHitArea>();

        if (hitArea != null)
            hitArea.Setup(owner, damage, knockback, stun, explosionRadius);

        /*
         * Fallback ukuran collider:
         * Jika prefab memiliki CircleCollider2D tetapi ConcussiveHitArea tidak
         * mengubah radius collider secara internal, radius tetap disesuaikan di sini.
         */
        CircleCollider2D circle = area.GetComponent<CircleCollider2D>();

        if (circle == null)
            circle = area.GetComponentInChildren<CircleCollider2D>(true);

        if (circle != null)
            circle.radius = explosionRadius;

        if (debugLog)
            Debug.Log($"[Enemy_Bow_ConcussiveShot] Hit area spawn di {position}, radius={explosionRadius}", area);
    }

    private Vector3 GetExplosionTargetPosition()
    {
        float dir = GetFacingDirection();

        Vector3 basePos;

        if (aimExplosionAtPlayer && ai != null && ai.playerTransform != null)
        {
            basePos = ai.playerTransform.position;
        }
        else if (firePoint != null)
        {
            basePos = firePoint.position + new Vector3(hitAreaOffset.x * dir, hitAreaOffset.y, 0f);
        }
        else
        {
            basePos = transform.position + new Vector3(hitAreaOffset.x * dir, hitAreaOffset.y, 0f);
        }

        float groundY = basePos.y;

        Vector2 rayOrigin = new Vector2(basePos.x, basePos.y + groundRaycastHeight);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, groundRaycastDistance, groundMask);

        if (hit.collider != null)
        {
            groundY = hit.point.y;
        }
        else if (aimExplosionAtPlayer && ai != null && ai.playerTransform != null)
        {
            groundY = ai.playerTransform.position.y;
        }
        else if (titikKaki != null)
        {
            groundY = titikKaki.position.y;
        }

        return new Vector3(
            basePos.x + explosionGroundOffset.x,
            groundY + explosionGroundOffset.y,
            transform.position.z
        );
    }

    private Vector3 GetFirePointWorldPosition()
    {
        if (firePoint == null)
            return transform.position;

        /*
         * Jika firePoint bukan anak visualRoot, tambahkan visualYOffset agar panah
         * tetap keluar dari posisi visual enemy yang sedang berada di atas.
         */
        if (visualRoot != null && !firePoint.IsChildOf(visualRoot))
            return firePoint.position + Vector3.up * visualYOffset;

        return firePoint.position;
    }

    // =========================================================
    // UTILITY
    // =========================================================

    private void PlayBowDrawSfx()
    {
        if (!playSfxFromScript) return;
        if (SFXManager.Instance == null) return;

        SFXManager.Instance.ResetBowDrawGate();
        SFXManager.Instance.PlayBowDrawGuarded();
    }

    private void PlayConcussiveJumpSfxOnce()
    {
        if (jumpSfxPlayed) return;
        if (!playJumpLaunchSfx) return;

        jumpSfxPlayed = true;
        PlaySfx(SFXManager.Instance != null ? SFXManager.Instance.arrowLaunchCharged : null);
    }

    private void PlayConcussiveLaunchSfx()
    {
        PlaySfx(SFXManager.Instance != null ? SFXManager.Instance.concussiveLaunch : null);
    }

    private void PlayConcussiveExplodeSfx()
    {
        PlaySfx(SFXManager.Instance != null ? SFXManager.Instance.concussiveExplode : null);
    }

    private void PlayBowHitSfx()
    {
        PlaySfx(SFXManager.Instance != null ? SFXManager.Instance.swordHit : null);
    }

    private bool HasExplosionHitTarget(Vector3 position)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(position, explosionRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
                continue;

            CharacterBase target = hits[i].GetComponentInParent<CharacterBase>();

            if (target != null && target != owner)
                return true;
        }

        return false;
    }

    private void PlaySfx(AudioClip clip)
    {
        if (!playSfxFromScript) return;
        if (clip == null) return;
        if (SFXManager.Instance == null) return;
        if (SFXManager.Instance.sfxSource == null) return;

        SFXManager.Instance.PlaySFX(clip);
    }

    private IEnumerator CooldownRoutine()
    {
        cooldownRunning = true;

        if (cooldown > 0f)
            yield return new WaitForSeconds(cooldown);

        cooldownRunning = false;
        cooldownRoutine = null;
    }

    private void FinishCast()
    {
        PulihkanVisual();
        StopOwnerMovement();

        if (skillLockClaimed)
        {
            combat?.InvokeSkillEnd();
            skillLockClaimed = false;
        }

        isCasting = false;
        releaseExecuted = false;
        startPopReceived = false;
        jumpSfxPlayed = false;
        visualYOffset = 0f;
        castRoutine = null;

        if (debugLog)
            Debug.Log("[Enemy_Bow_ConcussiveShot] Cast selesai.", this);
    }

    private void CacheVisualAwal()
    {
        if (visualRoot != null)
            visualLocalAwal = visualRoot.localPosition;

        SimpanWarnaAwalVisual();
    }

    private void SimpanWarnaAwalVisual()
    {
        if (visualRenderers == null)
            return;

        warnaAwalVisual = new Color[visualRenderers.Length];

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            if (visualRenderers[i] != null)
                warnaAwalVisual[i] = visualRenderers[i].color;
        }
    }

    private void SetAlphaVisual(float alpha)
    {
        if (visualRenderers == null)
            return;

        alpha = Mathf.Clamp01(alpha);

        for (int i = 0; i < visualRenderers.Length; i++)
        {
            SpriteRenderer sr = visualRenderers[i];

            if (sr == null)
                continue;

            Color c = sr.color;
            c.a = alpha;
            sr.color = c;
        }
    }

    private void PulihkanVisual()
    {
        if (visualRoot != null)
            visualRoot.localPosition = visualLocalAwal;

        if (visualRenderers == null || warnaAwalVisual == null)
            return;

        int total = Mathf.Min(visualRenderers.Length, warnaAwalVisual.Length);

        for (int i = 0; i < total; i++)
        {
            if (visualRenderers[i] != null)
                visualRenderers[i].color = warnaAwalVisual[i];
        }
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

    private void RotateArrowToVelocity(Transform arrowTransform, Vector2 velocity)
    {
        if (arrowTransform == null || velocity.sqrMagnitude <= 0.0001f)
            return;

        float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        arrowTransform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void ResetTrailPanah(GameObject arrow)
    {
        if (arrow == null)
            return;

        TrailRenderer[] trails = arrow.GetComponentsInChildren<TrailRenderer>(true);

        for (int i = 0; i < trails.Length; i++)
        {
            if (trails[i] == null)
                continue;

            trails[i].Clear();
            trails[i].emitting = true;
        }
    }

    private void StopOwnerMovement()
    {
        if (ownerRb != null)
            ownerRb.velocity = Vector2.zero;
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
        if (castRoutine != null)
        {
            StopCoroutine(castRoutine);
            castRoutine = null;
        }

        if (cooldownRoutine != null)
        {
            StopCoroutine(cooldownRoutine);
            cooldownRoutine = null;
        }

        PulihkanVisual();

        if (skillLockClaimed)
        {
            combat?.InvokeSkillEnd();
            skillLockClaimed = false;
        }

        isCasting = false;
        releaseExecuted = false;
        startPopReceived = false;
        jumpSfxPlayed = false;
        cooldownRunning = false;
        visualYOffset = 0f;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        Vector3 target = Application.isPlaying
            ? cachedExplosionPosition
            : transform.position + Vector3.right * hitAreaOffset.x + Vector3.up * hitAreaOffset.y;

        Gizmos.DrawWireSphere(target, explosionRadius);

        if (firePoint != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(firePoint.position, 0.08f);
            Gizmos.DrawLine(firePoint.position, target);
        }
    }
#endif
}