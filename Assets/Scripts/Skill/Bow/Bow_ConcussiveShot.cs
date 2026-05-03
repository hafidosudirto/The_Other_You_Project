using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class Bow_ConcussiveShot : MonoBehaviour, ISkill, IEnergySkill
{
    public enum ConcussiveJumpMode
    {
        SmoothJump,
        VanishPopJump
    }

    [Header("Mode")]
    [Tooltip("ON = panah visual ditembakkan, nancep, lalu meledak. OFF = langsung membuat ledakan.")]
    public bool useArrowVisual = true;

    [Header("References")]
    public Transform firePoint;
    public GameObject arrowPrefab;
    public GameObject hitAreaPrefab;
    public Player player;
    public PlayerAnimation anim;
    public SpriteRenderer facingSprite;

    [Tooltip("Isi dengan child Visual. Yang akan naik adalah Visual, bukan Player_W2.")]
    public Transform visualRoot;

    [Tooltip("Isi dengan SpriteRenderer pada Visual. Boleh satu saja.")]
    public SpriteRenderer[] visualRenderers;

    [Tooltip("Wajib diisi dengan TitikKaki agar panah nancep di garis tanah yang benar.")]
    public Transform titikKaki;

    [Tooltip("Isi dengan Shadow_PlayerBow. Shadow tidak akan ikut turun/naik.")]
    public Transform shadowPlayerBow;

    [Header("Special Jump")]
    public ConcussiveJumpMode jumpMode = ConcussiveJumpMode.VanishPopJump;

    [Tooltip("Tinggi visual karakter saat muncul di udara.")]
    public float jumpHeight = 1.45f;

    [Tooltip("Durasi naik menuju posisi atas.")]
    public float jumpUpTime = 0.18f;

    [Tooltip("Durasi diam sebentar di atas sebelum turun.")]
    public float hoverTime = 0.10f;

    [Tooltip("Durasi turun. Jangan terlalu kecil. Nilai aman: 0.32 sampai 0.45.")]
    public float jumpDownTime = 0.38f;

    [Tooltip("Visual naik sedikit sebelum menghilang.")]
    public float naikSedikitSebelumHilang = 0.22f;

    [Tooltip("Durasi visual memudar sampai hilang.")]
    public float durasiFadeHilang = 0.06f;

    [Tooltip("Durasi visual benar-benar tidak terlihat sebelum muncul di atas.")]
    public float durasiTidakTerlihat = 0.04f;

    [Tooltip("Durasi visual muncul kembali di atas.")]
    public float durasiFadeMuncul = 0.08f;

    [Tooltip("Jika ON, Visual dibuat transparan saat vanish pop.")]
    public bool fadeVisualSaatSpecialJump = true;

    [Tooltip("Jika ON, Shadow_PlayerBow akan diaktifkan saat skill.")]
    public bool gunakanShadowPlayerBow = true;

    [Tooltip("Jika ON, shadow dikembalikan ke kondisi awal setelah skill selesai.")]
    public bool matikanShadowSetelahSelesai = true;

    [Header("Animation Event")]
    [Tooltip("ON = panah keluar dari AE_BowConcussive_Release. OFF = panah keluar otomatis setelah visual berada di atas.")]
    public bool pakaiAnimationEvent = true;


    [Header("Special Jump / Clip Sync")]
    [Tooltip("ON = Visual baru dipindahkan ke atas setelah AE_BowConcussive_StartPop dipanggil dari Animation Clip.")]
    public bool pakaiStartPopAnimationEvent = true;

    [Tooltip("Jika AE_BowConcussive_StartPop tidak terpanggil, skrip akan lanjut otomatis setelah durasi ini.")]
    public float startPopEventTimeout = 0.35f;

    [Tooltip("Fallback jika tidak memakai Animation Event. Visual ditahan di tanah selama durasi ini sebelum pop ke atas.")]
    public float fallbackTahanBlinkDiTanah = 0.18f;


    [Tooltip("Kalau AE_BowConcussive_Release tidak terpanggil sampai waktu ini, skrip akan menembakkan panah lewat fallback.")]
    public float releaseEventTimeout = 0.75f;

    [Tooltip("Jeda setelah panah keluar sebelum visual mulai turun.")]
    public float delayBeforeFall = 0.12f;

    [Header("Arrow Setup")]
    public float arrowSpeed = 17f;

    [Tooltip("Sudut panah saat ditembakkan ke bawah. Nilai aman: 25 sampai 35.")]
    public float sudutTembakTurun = 30f;

    [Tooltip("Batas waktu panah terbang sebelum dipaksa nancep ke tanah.")]
    public float arrowFlightTime = 0.75f;

    public bool resetTrailSaatSpawn = true;

    [Header("Arrow Feel")]
    public float mulaiTurun = 0.08f;
    public float lengkungTurun = 7f;
    public bool rotasiIkutArah = true;

    [Header("Landing / Nancep")]
    public LayerMask groundMask;

    [Tooltip("Jika ON, Y tanah diambil dari TitikKaki saat skill mulai.")]
    public bool pakaiTitikKakiUntukJatuh = true;

    [Tooltip("Offset Y dari TitikKaki ketika panah nancep.")]
    public float offsetYJatuhDariTitikKaki = 0.04f;

    [Tooltip("Panah harus bergerak sejauh ini dulu sebelum boleh nancep.")]
    public float jarakMinimumSebelumBolehJatuh = 0.2f;

    [Tooltip("Raycast tanah dimulai setinggi ini dari posisi panah.")]
    public float tinggiRaycastTanah = 1f;

    [Tooltip("Jarak raycast ke bawah untuk mencari tanah.")]
    public float jarakRaycastTanah = 2.5f;

    [Tooltip("Offset posisi akhir saat panah nancep.")]
    public Vector2 offsetSaatNancep = new Vector2(0f, 0.05f);

    [Tooltip("Sudut panah saat nancep. Jika terlalu tegak, turunkan. Jika terlalu rebah, naikkan.")]
    public float sudutNancep = 28f;

    public bool matikanColliderSaatNancep = true;
    public bool matikanTrailSaatNancep = true;

    [Header("Ledakan Setelah Nancep")]
    public bool ledakkanSaatNancep = true;

    [Tooltip("Jeda kecil setelah panah nancep sebelum ledakan muncul.")]
    public float delayLedakanSetelahNancep = 0.05f;

    [Tooltip("Berapa lama panah tetap terlihat setelah nancep.")]
    public float waktuTerlihatSetelahNancep = 0.25f;

    [Header("Explosion Settings")]
    public float damage = 10f;
    public float knockback = 6f;
    public float stun = 0.35f;
    public float explosionRadius = 5f;

    [Header("Fallback Hit Area")]
    public Vector2 hitAreaOffset = new Vector2(1.2f, -0.2f);

    [Header("Camera Bump")]
    public bool enableCameraBump = true;
    public float cameraBumpStrength = 0.1f;
    public float cameraBumpDuration = 0.12f;

    [Header("Timing")]
    public float jedaSkill = 1.0f;

    [Header("Energy")]
    [SerializeField, Min(0f)] private float energyCost = 25f;

    [Header("Debug")]
    public bool debugLog = false;

    public float EnergyCost => energyCost;
    public bool PayEnergyInSkillBase => false;

    private CharacterBase character;
    private Rigidbody2D playerRb;

    private Coroutine castRoutine;
    private Coroutine cooldownRoutine;
    private Coroutine camRoutine;

    private bool isCasting;
    private bool isCooldown;
    private bool releaseExecuted;
    //private bool endRecoveryExecuted;
    private bool startPopReceived;
    private bool dataSudahTercatat;

    private Vector3 visualLocalAwal;
    private float visualYOffset;
    private float yTanahAwal;
    private bool punyaYTanahAwal;

    private bool shadowAwalnyaAktif;
    private Vector3 shadowLocalAwal;

    private Color[] warnaAwalVisual;

    private void Awake()
    {
        AutoAssignReferences();
    }

    [ContextMenu("Auto Assign References")]
    public void AutoAssignReferences()
    {
        if (player == null)
            player = GetComponentInParent<Player>(true);

        if (character == null)
            character = player != null ? player : GetComponentInParent<CharacterBase>(true);

        if (playerRb == null && player != null)
            playerRb = player.GetComponent<Rigidbody2D>();

        if (anim == null && player != null)
            anim = player.GetComponentInChildren<PlayerAnimation>(true);

        if (visualRoot == null && anim != null)
            visualRoot = anim.transform;

        if (facingSprite == null)
        {
            if (visualRoot != null)
                facingSprite = visualRoot.GetComponentInChildren<SpriteRenderer>(true);
            else if (player != null)
                facingSprite = player.GetComponentInChildren<SpriteRenderer>(true);
        }

        if ((visualRenderers == null || visualRenderers.Length == 0) && visualRoot != null)
            visualRenderers = visualRoot.GetComponentsInChildren<SpriteRenderer>(true);

        if (titikKaki == null && player != null)
            titikKaki = CariChildDenganNama(player.transform, "TitikKaki");

        if (shadowPlayerBow == null && player != null)
            shadowPlayerBow = CariChildDenganNama(player.transform, "Shadow_PlayerBow");
    }

    public void TriggerSkill(int slot)
    {
        if (isCooldown || isCasting)
            return;

        AutoAssignReferences();

        if (!ReferensiLengkap())
            return;

        if (player.lockMovement)
            return;

        if (!TryPayEnergy())
            return;

        isCasting = true;
        startPopReceived = false;
        releaseExecuted = false;
        //endRecoveryExecuted = false;
        dataSudahTercatat = false;
        visualYOffset = 0f;

        CacheAwal();
        CacheYTanahAwal();
        SiapkanShadow();

        player.lockMovement = true;
        StopOwnerMovement();

        MulaiCooldown();

        if (castRoutine != null)
            StopCoroutine(castRoutine);

        castRoutine = StartCoroutine(CastRoutine());

        if (anim != null)
            anim.PlayConcussiveShot();

        if (debugLog)
            Debug.Log("[Bow_ConcussiveShot] Cast dimulai.", this);
    }

    private IEnumerator CastRoutine()
    {
        if (jumpMode == ConcussiveJumpMode.VanishPopJump)
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
                Debug.LogWarning("[Bow_ConcussiveShot] AE_BowConcussive_Release tidak terpanggil. Panah ditembakkan lewat fallback.", this);
                ReleaseInternal();
            }
        }

        if (delayBeforeFall > 0f)
            yield return new WaitForSeconds(delayBeforeFall);

        yield return RoutineJumpDown();

        SelesaikanCast();
    }

    private IEnumerator RoutineSmoothJumpUp()
    {
        float t = 0f;
        float durasi = Mathf.Max(0.01f, jumpUpTime);

        while (t < durasi)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / durasi);
            float eased = Mathf.Sin(n * Mathf.PI * 0.5f);

            SetVisualYOffset(Mathf.Lerp(0f, jumpHeight, eased));
            yield return null;
        }

        SetVisualYOffset(jumpHeight);
    }

    private IEnumerator RoutineVanishPopJump()
    {
        // Fase 1:
        // Visual tetap di tanah agar sprite efek blink pada Animation Clip
        // tidak ikut terbawa ke atas.
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
            {
                Debug.LogWarning("[Bow_ConcussiveShot] AE_BowConcussive_StartPop tidak terpanggil. Pop dilakukan lewat fallback.", this);
            }
        }
        else
        {
            if (fallbackTahanBlinkDiTanah > 0f)
                yield return new WaitForSeconds(fallbackTahanBlinkDiTanah);
        }

        // Fase 2:
        // Setelah blink selesai, karakter benar-benar menghilang sebentar.
        float fadeOut = Mathf.Max(0.01f, durasiFadeHilang);
        float fadeIn = Mathf.Max(0.01f, durasiFadeMuncul);

        if (fadeVisualSaatSpecialJump)
        {
            float tFadeOut = 0f;

            while (tFadeOut < fadeOut)
            {
                tFadeOut += Time.deltaTime;
                float n = Mathf.Clamp01(tFadeOut / fadeOut);
                SetAlphaVisual(1f - n);
                yield return null;
            }

            SetAlphaVisual(0f);
        }

        if (durasiTidakTerlihat > 0f)
            yield return new WaitForSeconds(durasiTidakTerlihat);

        // Fase 3:
        // Visual baru dipindahkan ke atas setelah blink tanah selesai.
        SetVisualYOffset(jumpHeight);

        if (fadeVisualSaatSpecialJump)
        {
            float tFadeIn = 0f;

            while (tFadeIn < fadeIn)
            {
                tFadeIn += Time.deltaTime;
                float n = Mathf.Clamp01(tFadeIn / fadeIn);
                SetAlphaVisual(n);
                yield return null;
            }
        }

        SetAlphaVisual(1f);
    }
    private IEnumerator RoutineJumpDown()
    {
        float t = 0f;
        float durasi = Mathf.Max(0.01f, jumpDownTime);
        float startY = visualYOffset;

        while (t < durasi)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / durasi);

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

    public void ReleaseFromAnimationEvent()
    {
        if (!isCasting || releaseExecuted)
            return;

        ReleaseInternal();
    }

    public void StartPopFromAnimationEvent()
    {
        if (!isCasting)
            return;

        startPopReceived = true;

        if (debugLog)
            Debug.Log("[Bow_ConcussiveShot] AE_BowConcussive_StartPop diterima. Visual akan pop ke atas.", this);
    }

    public void EndRecoveryFromAnimationEvent()
    {
        //endRecoveryExecuted = true;
    }

    private void ReleaseInternal()
    {
        if (releaseExecuted)
            return;

        releaseExecuted = true;

        if (debugLog)
            Debug.Log("[Bow_ConcussiveShot] Release panah.", this);

        if (useArrowVisual)
            FireVisualArrow();
        else
            SpawnHitAreaDirect();
    }

    private void FireVisualArrow()
    {
        if (arrowPrefab == null || firePoint == null)
            return;

        float dir = AmbilArahHadap();

        Vector3 spawnPos = firePoint.position + Vector3.up * visualYOffset;

        GameObject arrow = Instantiate(arrowPrefab, spawnPos, Quaternion.identity);
        if (arrow == null)
            return;

        ConcussiveArrowVisual oldVisual = arrow.GetComponent<ConcussiveArrowVisual>();
        if (oldVisual != null)
            oldVisual.enabled = false;

        Rigidbody2D rb = arrow.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            Debug.LogWarning("[Bow_ConcussiveShot] Prefab panah tidak memiliki Rigidbody2D.", arrow);
            Destroy(arrow);
            return;
        }

        rb.gravityScale = 0f;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (resetTrailSaatSpawn)
            ResetTrailPanah(arrow);

        Vector2 arahTembak = AmbilArahTembakMenukik(dir);
        TerapkanRotasiPanahAwal(arrow, arahTembak);

        StartCoroutine(RoutinePanah(rb, arrow, dir, arahTembak));
    }

    private IEnumerator RoutinePanah(Rigidbody2D rb, GameObject arrow, float dir, Vector2 arahTembak)
    {
        float timer = 0f;
        float xAwal = rb.position.x;
        bool sudahNancep = false;

        while (timer < arrowFlightTime && !sudahNancep)
        {
            if (rb == null || arrow == null)
                yield break;

            Vector2 velocity = arahTembak * arrowSpeed;

            if (timer >= mulaiTurun)
                velocity.y -= lengkungTurun * (timer - mulaiTurun);

            rb.velocity = velocity;

            if (rotasiIkutArah)
                TerapkanRotasiIkutVelocity(rb);

            if (BolehCekNancep(rb, xAwal))
            {
                if (CobaNancep(rb, arrow, dir))
                {
                    sudahNancep = true;
                    break;
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        if (!sudahNancep)
        {
            if (punyaYTanahAwal)
            {
                PaksaNancep(rb, arrow, dir, yTanahAwal + offsetYJatuhDariTitikKaki);
                sudahNancep = true;
            }
            else if (CobaNancepDenganRaycast(rb, arrow, dir))
            {
                sudahNancep = true;
            }
        }

        if (!sudahNancep)
        {
            Destroy(arrow);
            yield break;
        }

        if (waktuTerlihatSetelahNancep > 0f)
            yield return new WaitForSeconds(waktuTerlihatSetelahNancep);

        if (arrow != null)
            Destroy(arrow);
    }

    private bool BolehCekNancep(Rigidbody2D rb, float xAwal)
    {
        if (rb == null)
            return false;

        float jarak = Mathf.Abs(rb.position.x - xAwal);
        return jarak >= jarakMinimumSebelumBolehJatuh;
    }

    private bool CobaNancep(Rigidbody2D rb, GameObject arrow, float dir)
    {
        if (rb == null || arrow == null)
            return false;

        if (pakaiTitikKakiUntukJatuh && punyaYTanahAwal)
        {
            float yTarget = yTanahAwal + offsetYJatuhDariTitikKaki;

            if (rb.position.y <= yTarget)
            {
                PaksaNancep(rb, arrow, dir, yTarget);
                return true;
            }
        }

        return CobaNancepDenganRaycast(rb, arrow, dir);
    }

    private bool CobaNancepDenganRaycast(Rigidbody2D rb, GameObject arrow, float dir)
    {
        if (rb == null || arrow == null)
            return false;

        Vector2 origin = new Vector2(rb.position.x, rb.position.y + tinggiRaycastTanah);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, jarakRaycastTanah, groundMask);

        if (hit.collider == null)
            return false;

        PaksaNancep(rb, arrow, dir, hit.point.y);
        return true;
    }

    private void PaksaNancep(Rigidbody2D rb, GameObject arrow, float dir, float yTanah)
    {
        if (rb == null || arrow == null)
            return;

        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;

        if (matikanColliderSaatNancep)
        {
            Collider2D col = arrow.GetComponent<Collider2D>();
            if (col != null)
                col.enabled = false;
        }

        if (matikanTrailSaatNancep)
            MatikanTrailPanah(arrow);

        Vector3 p = arrow.transform.position;
        p.x += offsetSaatNancep.x * dir;
        p.y = yTanah + offsetSaatNancep.y;
        arrow.transform.position = p;

        float sudut = Mathf.Abs(sudutNancep);
        float rotasiZ = dir > 0f ? -sudut : 180f + sudut;
        arrow.transform.rotation = Quaternion.Euler(0f, 0f, rotasiZ);

        if (debugLog)
            Debug.Log("[Bow_ConcussiveShot] Panah nancep, ledakan disiapkan.", this);

        if (ledakkanSaatNancep)
            StartCoroutine(SpawnExplosionAfterDelay(p));
    }

    private IEnumerator SpawnExplosionAfterDelay(Vector3 pos)
    {
        if (delayLedakanSetelahNancep > 0f)
            yield return new WaitForSeconds(delayLedakanSetelahNancep);

        SpawnHitAreaAt(pos);
    }

    private void SpawnHitAreaDirect()
    {
        float dir = AmbilArahHadap();

        Vector3 pos;

        if (punyaYTanahAwal)
            pos = new Vector3(firePoint.position.x + hitAreaOffset.x * dir, yTanahAwal, firePoint.position.z);
        else
            pos = firePoint.position + new Vector3(hitAreaOffset.x * dir, hitAreaOffset.y, 0f);

        SpawnHitAreaAt(pos);
    }

    private void SpawnHitAreaAt(Vector3 pos)
    {
        if (hitAreaPrefab == null)
            return;

        GameObject area = Instantiate(hitAreaPrefab, pos, Quaternion.identity);
        if (area == null)
            return;

        ConcussiveHitArea hitArea = area.GetComponent<ConcussiveHitArea>();
        if (hitArea != null)
            hitArea.Setup(player, damage, knockback, stun, explosionRadius);

        if (enableCameraBump)
        {
            if (camRoutine != null)
                StopCoroutine(camRoutine);

            camRoutine = StartCoroutine(CameraBumpRoutine());
        }

        NotifyDataTrackerConcussiveShot();
    }

    private Vector2 AmbilArahTembakMenukik(float dir)
    {
        float sudut = Mathf.Clamp(Mathf.Abs(sudutTembakTurun), 0f, 75f);
        float rad = sudut * Mathf.Deg2Rad;

        return new Vector2(dir * Mathf.Cos(rad), -Mathf.Sin(rad)).normalized;
    }

    private void TerapkanRotasiPanahAwal(GameObject arrow, Vector2 arah)
    {
        if (arrow == null)
            return;

        float sudut = Mathf.Atan2(arah.y, arah.x) * Mathf.Rad2Deg;
        arrow.transform.rotation = Quaternion.Euler(0f, 0f, sudut);

        Vector3 s = arrow.transform.localScale;
        arrow.transform.localScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
    }

    private void TerapkanRotasiIkutVelocity(Rigidbody2D rb)
    {
        if (rb == null)
            return;

        Vector2 v = rb.velocity;

        if (v.sqrMagnitude <= 0.0001f)
            return;

        float sudut = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        rb.transform.rotation = Quaternion.Euler(0f, 0f, sudut);
    }

    private void ResetTrailPanah(GameObject arrow)
    {
        TrailRenderer[] trails = arrow.GetComponentsInChildren<TrailRenderer>(true);

        foreach (TrailRenderer trail in trails)
        {
            trail.Clear();
            trail.emitting = true;
        }
    }

    private void MatikanTrailPanah(GameObject arrow)
    {
        TrailRenderer[] trails = arrow.GetComponentsInChildren<TrailRenderer>(true);

        foreach (TrailRenderer trail in trails)
            trail.emitting = false;
    }

    private void CacheAwal()
    {
        if (visualRoot != null)
            visualLocalAwal = visualRoot.localPosition;

        SimpanWarnaAwalVisual();
    }

    private void CacheYTanahAwal()
    {
        punyaYTanahAwal = false;
        yTanahAwal = 0f;

        if (titikKaki != null)
        {
            yTanahAwal = titikKaki.position.y;
            punyaYTanahAwal = true;
            return;
        }

        if (player != null)
        {
            yTanahAwal = player.transform.position.y;
            punyaYTanahAwal = true;
        }
    }

    private void SiapkanShadow()
    {
        if (shadowPlayerBow == null)
            return;

        shadowAwalnyaAktif = shadowPlayerBow.gameObject.activeSelf;
        shadowLocalAwal = shadowPlayerBow.localPosition;

        if (gunakanShadowPlayerBow)
            shadowPlayerBow.gameObject.SetActive(true);
    }

    private void PulihkanShadow()
    {
        if (shadowPlayerBow == null)
            return;

        shadowPlayerBow.localPosition = shadowLocalAwal;

        if (matikanShadowSetelahSelesai)
            shadowPlayerBow.gameObject.SetActive(shadowAwalnyaAktif);
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

        if (visualRenderers != null && warnaAwalVisual != null)
        {
            int total = Mathf.Min(visualRenderers.Length, warnaAwalVisual.Length);

            for (int i = 0; i < total; i++)
            {
                if (visualRenderers[i] != null)
                    visualRenderers[i].color = warnaAwalVisual[i];
            }
        }
    }

    private void SelesaikanCast()
    {
        PulihkanVisual();
        PulihkanShadow();
        StopOwnerMovement();

        if (player != null)
            player.lockMovement = false;

        isCasting = false;
        releaseExecuted = false;
        //endRecoveryExecuted = false;
        visualYOffset = 0f;
        castRoutine = null;

        if (debugLog)
            Debug.Log("[Bow_ConcussiveShot] Cast selesai.", this);
    }

    private void MulaiCooldown()
    {
        if (cooldownRoutine != null)
            StopCoroutine(cooldownRoutine);

        cooldownRoutine = StartCoroutine(CooldownRoutine());
    }

    private IEnumerator CooldownRoutine()
    {
        isCooldown = true;

        if (jedaSkill > 0f)
            yield return new WaitForSeconds(jedaSkill);

        isCooldown = false;
        cooldownRoutine = null;
    }

    private void StopOwnerMovement()
    {
        if (player == null)
            return;

        if (playerRb == null)
            playerRb = player.GetComponent<Rigidbody2D>();

        if (playerRb != null)
            playerRb.velocity = Vector2.zero;
    }

    private bool TryPayEnergy()
    {
        if (energyCost <= 0f)
            return true;

        if (character == null)
            character = player != null ? player : GetComponentInParent<CharacterBase>(true);

        if (character == null)
        {
            Debug.LogWarning("[Bow_ConcussiveShot] CharacterBase tidak ditemukan.", this);
            return false;
        }

        if (!character.TrySpendEnergy(energyCost))
        {
            Debug.LogWarning("[Bow_ConcussiveShot] Energy kurang. Butuh " + energyCost + ".", this);
            return false;
        }

        return true;
    }

    private float AmbilArahHadap()
    {
        if (facingSprite != null)
            return facingSprite.flipX ? -1f : 1f;

        if (player != null)
            return player.isFacingRight ? 1f : -1f;

        if (character != null)
            return character.isFacingRight ? 1f : -1f;

        return 1f;
    }

    private bool ReferensiLengkap()
    {
        bool lengkap = true;

        if (player == null)
        {
            Debug.LogWarning("[Bow_ConcussiveShot] Player belum diisi.", this);
            lengkap = false;
        }

        if (visualRoot == null)
        {
            Debug.LogWarning("[Bow_ConcussiveShot] Visual Root belum diisi. Isi dengan child Visual.", this);
            lengkap = false;
        }

        if (firePoint == null)
        {
            Debug.LogWarning("[Bow_ConcussiveShot] Fire Point belum diisi.", this);
            lengkap = false;
        }

        if (useArrowVisual && arrowPrefab == null)
        {
            Debug.LogWarning("[Bow_ConcussiveShot] Arrow Prefab belum diisi.", this);
            lengkap = false;
        }

        if (hitAreaPrefab == null)
        {
            Debug.LogWarning("[Bow_ConcussiveShot] Hit Area Prefab belum diisi.", this);
            lengkap = false;
        }

        if (titikKaki == null)
            Debug.LogWarning("[Bow_ConcussiveShot] TitikKaki belum diisi. Panah masih bisa nancep, tetapi posisi tanah kurang presisi.", this);

        return lengkap;
    }

    private void NotifyDataTrackerConcussiveShot()
    {
        if (dataSudahTercatat)
            return;

        dataSudahTercatat = true;

        DataTracker tracker = DataTracker.Instance;
        if (tracker == null)
            return;

        var method = tracker.GetType().GetMethod("RecordBowConcussiveShot");
        if (method != null)
        {
            method.Invoke(tracker, null);
            return;
        }

        tracker.RecordAction(PlayerActionType.Offensive, WeaponType.Bow);
    }

    private IEnumerator CameraBumpRoutine()
    {
        Camera cam = Camera.main;
        if (cam == null)
            yield break;

        Transform ct = cam.transform;
        Vector3 startPos = ct.position;

        float t = 0f;
        float durasi = Mathf.Max(0.01f, cameraBumpDuration);

        while (t < durasi)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / durasi);
            float offset = Mathf.Sin(n * Mathf.PI);

            ct.position = startPos + new Vector3(0f, offset * cameraBumpStrength, 0f);
            yield return null;
        }

        ct.position = startPos;
    }

    private Transform CariChildDenganNama(Transform root, string nama)
    {
        if (root == null)
            return null;

        if (root.name == nama)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform hasil = CariChildDenganNama(root.GetChild(i), nama);
            if (hasil != null)
                return hasil;
        }

        return null;
    }

    private void OnDisable()
    {
        if (castRoutine != null)
            StopCoroutine(castRoutine);

        if (cooldownRoutine != null)
            StopCoroutine(cooldownRoutine);

        if (camRoutine != null)
            StopCoroutine(camRoutine);

        PulihkanVisual();
        PulihkanShadow();

        if (player != null)
            player.lockMovement = false;

        isCasting = false;
        isCooldown = false;
        releaseExecuted = false;
        //endRecoveryExecuted = false;

        castRoutine = null;
        cooldownRoutine = null;
        camRoutine = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (firePoint != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 spawn = firePoint.position + Vector3.up * visualYOffset;
            Gizmos.DrawWireSphere(spawn, 0.06f);

            float dir = 1f;
            if (player != null)
                dir = player.isFacingRight ? 1f : -1f;

            Vector2 arah = AmbilArahTembakMenukik(dir);
            Gizmos.DrawLine(spawn, spawn + (Vector3)(arah * 1.2f));
        }

        if (titikKaki != null)
        {
            Gizmos.color = Color.green;
            Vector3 p = titikKaki.position + Vector3.up * offsetYJatuhDariTitikKaki;
            Gizmos.DrawLine(p + Vector3.left * 0.8f, p + Vector3.right * 0.8f);
        }
    }
#endif
}