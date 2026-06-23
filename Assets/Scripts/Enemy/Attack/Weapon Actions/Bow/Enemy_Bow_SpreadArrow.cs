using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_Bow_SpreadArrow : MonoBehaviour, ISkill
{
    public enum PolaTembak
    {
        SpreadTetap,
        LurusBertingkat,
        KipasRingan
    }

    private struct DataPanah
    {
        public float offsetY;
        public float sudut;
        public float bonusVy;

        public DataPanah(float offsetY, float sudut, float bonusVy)
        {
            this.offsetY = offsetY;
            this.sudut = sudut;
            this.bonusVy = bonusVy;
        }
    }

    [Header("BT / Range")]
    public float skillRange = 8f;
    public float minRange = 2f;
    public float rangeTolerance = 1f;

    [Header("References")]
    public Transform titikTembak;
    public GameObject prefabPanah;
    public CharacterBase enemyCharacter;
    public Animator enemyAnimator;
    public SpriteRenderer facingSprite;

    [Header("Animation")]
    [Tooltip("Trigger Animator untuk memutar clip SpreadArrow. Pose backdash harus berada di dalam clip ini.")]
    public string triggerAnimasi = "SpreadArrow";

    [Tooltip("Aktifkan jika clip SpreadArrow memiliki Animation Event: StartBackDash, Release, dan EndRecovery.")]
    public bool pakaiAnimationEvent = true;

    [Tooltip("Fallback pelepasan panah jika Animation Event Release tidak terpanggil.")]
    public float fallbackDelayTembak = 0.25f;

    [Tooltip("Batas aman menunggu event AE_BowSpreadArrow_Release.")]
    public float releaseTimeout = 1.0f;

    [Tooltip("Batas aman menunggu event AE_BowSpreadArrow_EndRecovery.")]
    public float recoveryTimeout = 0.45f;

    [Header("Backdash Physics - Animation Event Only")]
    [Tooltip("Jika aktif, AE_BowSpreadArrow_StartBackDash memberi dorongan fisik mundur. Ini bukan trigger animasi baru.")]
    public bool enableBackDashImpulse = true;

    [Tooltip("Kecepatan dorongan mundur saat frame backdash pada clip SpreadArrow.")]
    public float backDashSpeed = 5f;

    [Tooltip("Durasi dorongan fisik mundur. Visual backdash tetap harus dibuat di clip SpreadArrow.")]
    public float backDashDuration = 0.12f;

    [Tooltip("Jika aktif, velocity Y dipertahankan saat backdash. Biasanya false untuk game 2D side-view datar.")]
    public bool preserveVerticalVelocityDuringBackDash = false;

    [Header("Fast Release After Backdash")]
    [Tooltip("Jika aktif, panah otomatis dilepas beberapa saat setelah AE_BowSpreadArrow_StartBackDash.")]
    public bool autoReleaseAfterBackDashEvent = true;

    [Tooltip("Delay panah setelah event backdash. Nilai kecil membuat panah keluar lebih cepat.")]
    public float releaseDelayAfterBackDashEvent = 0.02f;

    [Header("Pola Tembak")]
    public PolaTembak polaTembak = PolaTembak.LurusBertingkat;
    [Range(3, 5)] public int jumlahPanah = 3;

    [Header("Pola: Lurus Bertingkat")]
    public float jarakVertikalLurus = 0.22f;

    [Header("Pola: Spread Tetap")]
    public float bonusVyAtas = 1.15f;
    public float bonusVyTengah = 0f;
    public float bonusVyBawah = -1.15f;
    public float offsetSpawnSpreadTetap = 0f;
    public bool spreadTetapLurusSepertiPiercing = true;

    [Header("Pola: Kipas Ringan")]
    public float sudutKipas = 9f;
    public float offsetSpawnKipas = 0.06f;

    [Header("Arrow Setup")]
    public float kecepatanPanah = 16f;
    public float durasiTerbang = 0.65f;
    public bool rotasiIkutArah = true;

    [Header("Arrow Feel")]
    public float mulaiTurun = 0.09f;
    public float angkatSedikit = 0.4f;
    public float lengkungTurun = 7f;

    [Header("Hit Effect")]
    public float damageSpread = 5f;
    public float dorongMundur = 4f;
    public float lumpuhSingkat = 0.1f;
    public bool panahMenembus = false;

    [Header("Timing")]
    public float cooldown = 3f;

    [Header("SFX Timing")]
    [Tooltip("Aktifkan jika SFX Spread Arrow musuh ingin dikendalikan dari script ini.")]
    public bool playSfxFromScript = true;

    [Tooltip("Suara tarikan busur diputar satu kali saat casting Spread Arrow dimulai.")]
    public bool playBowDrawOnCastStart = true;

    [Tooltip("Suara panah meluncur opsi 2 diputar satu kali saat formasi Spread Arrow dilepas.")]
    public bool playSpreadLaunchSfxOnRelease = true;

    [Tooltip("Suara panah menancap diputar untuk panah yang tidak mengenai target sampai akhir durasi terbang.")]
    public bool playGroundMissSfx = true;

    [Tooltip("Suara hit diputar oleh projectile saat panah mengenai target.")]
    public bool playHitSfx = true;

    public bool IsActive => sedangCast;

    private NodeManager ai;
    private EnemyCombatController combat;
    private EnemyAnimation enemyAnim;
    private Rigidbody2D ownerRb;

    private bool sedangCast;
    private bool panahSudahDitembakkan;
    private bool recoverySelesai;
    private bool cooldownRunning;
    private bool skillLockClaimed;

    private Coroutine routineCast;
    private Coroutine routineBackDash;
    private Coroutine routineAutoReleaseAfterBackDash;

    private void Awake()
    {
        ai = GetComponentInParent<NodeManager>();
        combat = GetComponentInParent<EnemyCombatController>();

        enemyCharacter = enemyCharacter != null
            ? enemyCharacter
            : GetComponentInParent<CharacterBase>();

        ownerRb = GetComponentInParent<Rigidbody2D>();

        if (ai != null)
            enemyAnim = ai.GetComponentInChildren<EnemyAnimation>(true);

        if (enemyAnimator == null)
        {
            if (enemyAnim != null)
                enemyAnimator = enemyAnim.animator;
            else if (enemyCharacter != null)
                enemyAnimator = enemyCharacter.GetComponentInChildren<Animator>(true);
        }

        if (facingSprite == null)
        {
            if (ai != null)
                facingSprite = ai.GetComponentInChildren<SpriteRenderer>(true);
            else if (enemyCharacter != null)
                facingSprite = enemyCharacter.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (titikTembak == null)
        {
            Transform found = FindChildRecursive(transform.root, "FirePoint");

            if (found == null)
                found = FindChildRecursive(transform.root, "ArrowSpawnPoint");

            titikTembak = found;
        }
    }

    public bool IsInRange(float distance)
    {
        return distance <= (skillRange + rangeTolerance) &&
               distance >= (minRange - rangeTolerance);
    }

    public bool CanTrigger(float distance)
    {
        if (sedangCast) return false;
        if (cooldownRunning) return false;
        if (combat != null && combat.IsBusy) return false;
        if (!IsInRange(distance)) return false;
        if (titikTembak == null || prefabPanah == null) return false;

        return true;
    }

    public void Trigger()
    {
        float distance = GetDistanceToPlayer();

        if (!CanTrigger(distance))
            return;

        routineCast = StartCoroutine(RoutineCast());
    }

    public void TriggerSkill(int slot)
    {
        Trigger();
    }

    private IEnumerator RoutineCast()
    {
        sedangCast = true;
        panahSudahDitembakkan = false;
        recoverySelesai = false;
        skillLockClaimed = true;

        combat?.InvokeSkillStart();

        StopOwnerMovement();
        StartCoroutine(CooldownRoutine());

        if (playBowDrawOnCastStart)
            PlayBowDrawSfx();

        /*
         * Hanya memutar clip SpreadArrow.
         * Tidak ada trigger BackDash terpisah.
         * Pose/gerakan visual backdash harus dibuat langsung di dalam clip SpreadArrow.
         */
        if (enemyAnim != null)
        {
            enemyAnim.PlaySpreadArrow();
        }
        else if (enemyAnimator != null && !string.IsNullOrEmpty(triggerAnimasi))
        {
            enemyAnimator.ResetTrigger(triggerAnimasi);
            enemyAnimator.SetTrigger(triggerAnimasi);
        }

        /*
         * Jika tidak memakai Animation Event, skrip tetap melepas panah melalui fallback.
         * Backdash fisik paling ideal tetap dipanggil dari AE_BowSpreadArrow_StartBackDash
         * agar sinkron dengan pose di clip.
         */
        if (!pakaiAnimationEvent)
        {
            yield return new WaitForSeconds(fallbackDelayTembak);
            ReleaseSpreadArrow();
        }

        float releaseTimer = 0f;
        while (!panahSudahDitembakkan && releaseTimer < releaseTimeout)
        {
            releaseTimer += Time.deltaTime;
            yield return null;
        }

        if (!panahSudahDitembakkan)
            ReleaseSpreadArrow();

        float recoveryTimer = 0f;
        while (!recoverySelesai && recoveryTimer < recoveryTimeout)
        {
            recoveryTimer += Time.deltaTime;
            yield return null;
        }

        FinishSkill();
    }

    // =========================================================
    // ANIMATION EVENT: AE_BowSpreadArrow_StartBackDash
    // =========================================================
    // Event ini hanya memberi dorongan fisik mundur.
    // Animasi visual backdash tetap berasal dari pose di clip SpreadArrow.
    public void StartBackDash()
    {
        if (!sedangCast)
            return;

        if (enableBackDashImpulse && ownerRb != null)
        {
            if (routineBackDash != null)
                StopCoroutine(routineBackDash);

            routineBackDash = StartCoroutine(BackDashImpulseRoutine());
        }

        /*
         * Bagian percepatan release:
         * Panah dapat keluar sangat cepat setelah event backdash,
         * tanpa harus menunggu AE_BowSpreadArrow_Release jika event tersebut
         * berada terlalu jauh di dalam clip.
         */
        if (autoReleaseAfterBackDashEvent && !panahSudahDitembakkan)
        {
            if (routineAutoReleaseAfterBackDash != null)
                StopCoroutine(routineAutoReleaseAfterBackDash);

            routineAutoReleaseAfterBackDash = StartCoroutine(AutoReleaseAfterBackDashRoutine());
        }
    }

    public void StartBackDashFromAnimationEvent()
    {
        StartBackDash();
    }

    public void BeginBackDash()
    {
        StartBackDash();
    }

    public void BeginBackDashFromAnimationEvent()
    {
        StartBackDash();
    }

    private IEnumerator BackDashImpulseRoutine()
    {
        float timer = 0f;

        /*
         * Arah mundur adalah kebalikan dari arah hadap.
         * Jika enemy menghadap kanan, ia terdorong ke kiri.
         * Jika enemy menghadap kiri, ia terdorong ke kanan.
         */
        float backDirection = -AmbilArahHadap();

        while (timer < backDashDuration)
        {
            if (ownerRb == null)
                yield break;

            float velocityY = preserveVerticalVelocityDuringBackDash
                ? ownerRb.velocity.y
                : 0f;

            ownerRb.velocity = new Vector2(
                backDirection * backDashSpeed,
                velocityY
            );

            timer += Time.deltaTime;
            yield return null;
        }

        StopOwnerMovement();
        routineBackDash = null;
    }

    private IEnumerator AutoReleaseAfterBackDashRoutine()
    {
        float delay = Mathf.Max(0f, releaseDelayAfterBackDashEvent);

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (sedangCast && !panahSudahDitembakkan)
            ReleaseSpreadArrow();

        routineAutoReleaseAfterBackDash = null;
    }

    // =========================================================
    // ANIMATION EVENT: AE_BowSpreadArrow_Release
    // =========================================================
    public void ReleaseSpreadArrow()
    {
        if (!sedangCast)
            return;

        if (panahSudahDitembakkan)
            return;

        panahSudahDitembakkan = true;

        if (titikTembak == null || prefabPanah == null)
            return;

        if (playSpreadLaunchSfxOnRelease)
            PlaySpreadLaunchSfx();

        List<DataPanah> formasi = BangunFormasiPanah();
        float arah = AmbilArahHadap();

        for (int i = 0; i < formasi.Count; i++)
            SpawnPanah(formasi[i], arah);
    }

    public void ReleaseFromAnimationEvent()
    {
        ReleaseSpreadArrow();
    }

    public void ReleaseSpread()
    {
        ReleaseSpreadArrow();
    }

    public void Release()
    {
        ReleaseSpreadArrow();
    }

    public void Shoot()
    {
        ReleaseSpreadArrow();
    }

    // =========================================================
    // ANIMATION EVENT: AE_BowSpreadArrow_EndRecovery
    // =========================================================
    public void EndSpreadArrowRecovery()
    {
        recoverySelesai = true;
    }

    public void EndRecoveryFromAnimationEvent()
    {
        EndSpreadArrowRecovery();
    }

    public void EndRecovery()
    {
        EndSpreadArrowRecovery();
    }

    public void FinishRecovery()
    {
        EndSpreadArrowRecovery();
    }

    public void Finish()
    {
        EndSpreadArrowRecovery();
    }

    private void FinishSkill()
    {
        if (routineBackDash != null)
        {
            StopCoroutine(routineBackDash);
            routineBackDash = null;
        }

        if (routineAutoReleaseAfterBackDash != null)
        {
            StopCoroutine(routineAutoReleaseAfterBackDash);
            routineAutoReleaseAfterBackDash = null;
        }

        StopOwnerMovement();

        if (skillLockClaimed)
        {
            combat?.InvokeSkillEnd();
            skillLockClaimed = false;
        }

        sedangCast = false;
        recoverySelesai = false;
        routineCast = null;
    }

    private List<DataPanah> BangunFormasiPanah()
    {
        List<DataPanah> hasil = new List<DataPanah>();
        int total = Mathf.Clamp(jumlahPanah, 3, 5);

        switch (polaTembak)
        {
            case PolaTembak.SpreadTetap:
                if (total == 3)
                {
                    hasil.Add(new DataPanah(-offsetSpawnSpreadTetap, 0f, bonusVyAtas));
                    hasil.Add(new DataPanah(0f, 0f, bonusVyTengah));
                    hasil.Add(new DataPanah(offsetSpawnSpreadTetap, 0f, bonusVyBawah));
                }
                else if (total == 4)
                {
                    hasil.Add(new DataPanah(0f, 0f, bonusVyAtas));
                    hasil.Add(new DataPanah(0f, 0f, bonusVyAtas * 0.35f));
                    hasil.Add(new DataPanah(0f, 0f, bonusVyBawah * 0.35f));
                    hasil.Add(new DataPanah(0f, 0f, bonusVyBawah));
                }
                else
                {
                    hasil.Add(new DataPanah(0f, 0f, bonusVyAtas));
                    hasil.Add(new DataPanah(0f, 0f, bonusVyAtas * 0.45f));
                    hasil.Add(new DataPanah(0f, 0f, bonusVyTengah));
                    hasil.Add(new DataPanah(0f, 0f, bonusVyBawah * 0.45f));
                    hasil.Add(new DataPanah(0f, 0f, bonusVyBawah));
                }
                break;

            case PolaTembak.LurusBertingkat:
                for (int i = 0; i < total; i++)
                {
                    float y = (i - (total - 1) / 2f) * jarakVertikalLurus;
                    hasil.Add(new DataPanah(y, 0f, 0f));
                }
                break;

            case PolaTembak.KipasRingan:
                float stepSudut = total <= 1 ? 0f : (sudutKipas * 2f) / (total - 1);
                float stepOffset = total <= 1 ? 0f : (offsetSpawnKipas * 2f) / (total - 1);

                for (int i = 0; i < total; i++)
                {
                    float sudut = -sudutKipas + (stepSudut * i);
                    float y = -offsetSpawnKipas + (stepOffset * i);
                    hasil.Add(new DataPanah(y, sudut, 0f));
                }
                break;
        }

        return hasil;
    }

    private void SpawnPanah(DataPanah data, float arah)
    {
        if (titikTembak == null || prefabPanah == null)
            return;

        Vector3 posisiSpawn = titikTembak.position + new Vector3(0f, data.offsetY, 0f);
        GameObject panahObj = Instantiate(prefabPanah, posisiSpawn, Quaternion.identity);

        if (panahObj == null)
            return;

        Rigidbody2D rb = panahObj.GetComponent<Rigidbody2D>();
        ArrowDamage arrowDamage = panahObj.GetComponent<ArrowDamage>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.velocity = Vector2.zero;
        }

        TerapkanArahAwalPanah(panahObj, arah);

        if (arrowDamage != null)
        {
            arrowDamage.SetOwner(enemyCharacter);
            arrowDamage.SetStats(
                damageSpread,
                dorongMundur,
                lumpuhSingkat,
                panahMenembus,
                false
            );
        }

        SetupProjectileSfx(panahObj);

        if (rb != null)
            StartCoroutine(RoutinePanah(rb, panahObj, arah, data.sudut, data.bonusVy));
        else
            Destroy(panahObj, 1f);
    }

    private IEnumerator RoutinePanah(
        Rigidbody2D rb,
        GameObject panahObj,
        float arah,
        float sudutAwal,
        float bonusVy
    )
    {
        if (rb == null || panahObj == null)
            yield break;

        bool modeSpreadLurus =
            polaTembak == PolaTembak.SpreadTetap &&
            spreadTetapLurusSepertiPiercing;

        if (modeSpreadLurus)
        {
            Vector2 velocityLurus = new Vector2(arah * kecepatanPanah, bonusVy);
            float timer = 0f;

            while (timer < durasiTerbang)
            {
                if (rb == null || panahObj == null)
                    yield break;

                rb.velocity = velocityLurus;

                if (rotasiIkutArah)
                    TerapkanRotasiPanah(rb);

                timer += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            float timer = 0f;

            float waktuMulaiTurun = Mathf.Clamp(
                mulaiTurun,
                0f,
                Mathf.Max(0.01f, durasiTerbang - 0.01f)
            );

            Vector2 arahAwal = HitungArahDariSudut(arah, sudutAwal);

            float vx = arahAwal.x * kecepatanPanah;
            float vy = arahAwal.y * kecepatanPanah + angkatSedikit + bonusVy;

            while (timer < durasiTerbang)
            {
                if (rb == null || panahObj == null)
                    yield break;

                if (timer >= waktuMulaiTurun)
                    vy -= lengkungTurun * Time.deltaTime;

                rb.velocity = new Vector2(vx, vy);

                if (rotasiIkutArah)
                    TerapkanRotasiPanah(rb);

                timer += Time.deltaTime;
                yield return null;
            }
        }

        PlayGroundMissIfArrowStillActive(panahObj);

        if (panahObj != null)
            Destroy(panahObj);
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

    private Vector2 HitungArahDariSudut(float arahHadap, float sudut)
    {
        Vector2 dir = Quaternion.Euler(0f, 0f, sudut) * Vector2.right;

        if (arahHadap < 0f)
            dir = new Vector2(-dir.x, dir.y);

        return dir.normalized;
    }

    private void TerapkanRotasiPanah(Rigidbody2D rb)
    {
        if (rb == null || rb.velocity.sqrMagnitude <= 0.0001f)
            return;

        float sudut = Mathf.Atan2(rb.velocity.y, rb.velocity.x) * Mathf.Rad2Deg;
        rb.transform.rotation = Quaternion.Euler(0f, 0f, sudut);
    }

    private void TerapkanArahAwalPanah(GameObject panahObj, float arah)
    {
        if (panahObj == null)
            return;

        Vector3 scale = panahObj.transform.localScale;

        panahObj.transform.localScale = new Vector3(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z)
        );

        panahObj.transform.rotation = Quaternion.Euler(
            0f,
            0f,
            arah > 0f ? 0f : 180f
        );

        SpriteRenderer[] renderers = panahObj.GetComponentsInChildren<SpriteRenderer>(true);

        for (int i = 0; i < renderers.Length; i++)
            renderers[i].flipX = false;
    }

    private float AmbilArahHadap()
    {
        if (ai != null)
            return ai.IsFacingRight ? 1f : -1f;

        if (enemyCharacter != null)
            return enemyCharacter.isFacingRight ? 1f : -1f;

        if (facingSprite != null)
            return facingSprite.flipX ? -1f : 1f;

        return 1f;
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

    private void PlayBowDrawSfx()
    {
        if (!playSfxFromScript) return;
        if (SFXManager.Instance == null) return;

        SFXManager.Instance.ResetBowDrawGate();
        SFXManager.Instance.PlayBowDrawGuarded();
    }

    private void PlaySpreadLaunchSfx()
    {
        PlaySfx(SFXManager.Instance != null ? SFXManager.Instance.arrowLaunchCharged : null);
    }

    private void SetupProjectileSfx(GameObject arrowObj)
    {
        if (arrowObj == null) return;
        if (!playSfxFromScript) return;

        BowProjectileSFX reporter = arrowObj.GetComponent<BowProjectileSFX>();
        if (reporter == null)
            reporter = arrowObj.AddComponent<BowProjectileSFX>();

        reporter.Setup(enemyCharacter, playGroundMissSfx, playHitSfx);
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
        if (routineCast != null)
        {
            StopCoroutine(routineCast);
            routineCast = null;
        }

        if (routineBackDash != null)
        {
            StopCoroutine(routineBackDash);
            routineBackDash = null;
        }

        if (routineAutoReleaseAfterBackDash != null)
        {
            StopCoroutine(routineAutoReleaseAfterBackDash);
            routineAutoReleaseAfterBackDash = null;
        }

        panahSudahDitembakkan = false;
        recoverySelesai = false;
        cooldownRunning = false;

        if (skillLockClaimed)
        {
            combat?.InvokeSkillEnd();
            skillLockClaimed = false;
        }

        sedangCast = false;
    }
}