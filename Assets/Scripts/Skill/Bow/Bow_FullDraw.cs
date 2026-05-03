using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class Bow_FullDraw : MonoBehaviour, ISkill, IEnergySkill
{
    [Header("Charge UI")]
    [Tooltip("UI charge bar kecil. Isi dengan object ChargeBarRoot yang memiliki script BowChargeBar.")]
    public BowChargeBar chargeBar;

    [Header("Full Draw / Referensi")]
    [Tooltip("Titik keluarnya panah. Biasanya isi dengan MuzzlePoint.")]
    [FormerlySerializedAs("firePoint")]
    public Transform titikTembak;

    [Tooltip("Prefab panah Full Draw biasa. Dipakai saat charge belum melewati batas Piercing.")]
    [FormerlySerializedAs("arrowPrefab")]
    public GameObject prefabPanahFullDraw;

    [Tooltip("Prefab panah Piercing. Dipakai saat charge sudah melewati batas Piercing.")]
    public GameObject prefabPanahPiercing;

    [Tooltip("Player pemilik skill ini.")]
    [FormerlySerializedAs("player")]
    public Player pemain;

    [Tooltip("Pengendali animasi player yang berada di object Visual.")]
    [FormerlySerializedAs("anim")]
    public PlayerAnimation animasiPemain;

    [Tooltip("SpriteRenderer visual player. Dipakai untuk membaca arah hadap jika arah dari Player tidak sinkron.")]
    [FormerlySerializedAs("facingSprite")]
    public SpriteRenderer facingSprite;

    [Header("Charge Settings")]
    [Tooltip("Durasi charge sampai penuh.")]
    [FormerlySerializedAs("maxChargeTime")]
    public float durasiChargePenuh = 1.2f;

    [Tooltip("Batas masuk Piercing jika BowChargeBar tidak tersedia. Jika BowChargeBar tersedia, nilai PiercingThreshold dari charge bar yang dipakai.")]
    [Range(0.1f, 1f)]
    [FormerlySerializedAs("fullChargeThreshold")]
    public float batasPiercingFallback = 0.5f;

    [Tooltip("Jika aktif, gerakan player dikunci selama proses charge.")]
    public bool kunciGerakSaatCharge = true;

    [Header("Animation Event")]
    [Tooltip("Jika aktif, panah hanya keluar ketika Animation Event AE_BowFullDraw_Release memanggil ReleaseFromAnimationEvent.")]
    public bool pakaiAnimationEvent = true;

    [Tooltip("Batas waktu menunggu Animation Event setelah tombol dilepas.")]
    public float batasWaktuMenungguReleaseEvent = 0.45f;

    [Tooltip("Jika aktif, panah tetap ditembakkan jika Animation Event gagal terpanggil.")]
    public bool fallbackTembakJikaEventGagal = false;

    [Header("Full Draw Non-Piercing")]
    [Tooltip("Kecepatan minimal panah saat dilepas cepat sebelum melewati batas Piercing.")]
    [FormerlySerializedAs("minArrowSpeed")]
    public float kecepatanPanahMinimum = 8f;

    [Tooltip("Kecepatan maksimal panah Full Draw sebelum berubah menjadi Piercing.")]
    [FormerlySerializedAs("maxArrowSpeed")]
    public float kecepatanPanahMaksimum = 18f;

    [Tooltip("Damage minimal Full Draw.")]
    [FormerlySerializedAs("minDamage")]
    public float damageMinimum = 4f;

    [Tooltip("Damage maksimal Full Draw sebelum berubah menjadi Piercing.")]
    [FormerlySerializedAs("maxDamage")]
    public float damageMaksimum = 10f;

    [Tooltip("Knockback minimal Full Draw.")]
    [FormerlySerializedAs("minKnockback")]
    public float dorongMundurMinimum = 2f;

    [Tooltip("Knockback maksimal Full Draw sebelum berubah menjadi Piercing.")]
    [FormerlySerializedAs("maxKnockback")]
    public float dorongMundurMaksimum = 6f;

    [Tooltip("Durasi stun/stagger minimal Full Draw. Nilai ini dikirim ke ArrowDamage.")]
    [FormerlySerializedAs("minStun")]
    public float lumpuhMinimum = 0.05f;

    [Tooltip("Durasi stun/stagger maksimal Full Draw. Nilai ini dikirim ke ArrowDamage.")]
    [FormerlySerializedAs("maxStun")]
    public float lumpuhMaksimum = 0.15f;

    [Header("Piercing Mode")]
    [Tooltip("Kecepatan panah Piercing.")]
    [FormerlySerializedAs("piercingSpeed")]
    public float kecepatanPiercing = 20f;

    [Tooltip("Damage panah Piercing.")]
    [FormerlySerializedAs("piercingDamage")]
    public float damagePiercing = 10f;

    [Tooltip("Knockback panah Piercing. Isi 0 jika Piercing tidak ingin mendorong musuh.")]
    [FormerlySerializedAs("piercingKnockback")]
    public float dorongMundurPiercing = 0f;

    [Tooltip("Durasi stun/stagger panah Piercing.")]
    [FormerlySerializedAs("piercingStun")]
    public float lumpuhPiercing = 0.25f;

    [Header("Piercing Lurus")]
    [Tooltip("Jika aktif, Piercing terbang lurus horizontal dan tidak memakai gravity, arrow feel, TitikKaki, atau raycast tanah.")]
    public bool piercingFullLurus = true;

    [Tooltip("Berapa lama panah Piercing lurus hidup sebelum dihapus otomatis.")]
    public float umurPiercingLurus = 0.9f;

    [Tooltip("Jika aktif, velocity Y Piercing selalu dipaksa 0 agar benar-benar horizontal.")]
    public bool paksaPiercingHorizontal = true;

    [Header("Arrow Setup")]
    [Tooltip("Durasi fase terbang utama untuk Full Draw non-piercing.")]
    public float durasiTerbang = 0.55f;

    [Tooltip("Jika aktif, TrailRenderer pada prefab panah dibersihkan saat spawn.")]
    public bool resetTrailSaatSpawn = true;

    [Header("Arrow Feel Full Draw")]
    [Tooltip("Panah mulai turun setelah berapa detik.")]
    public float mulaiTurun = 0.09f;

    [Tooltip("Dorongan Y awal agar panah tidak terlalu datar.")]
    public float angkatSedikit = 0.4f;

    [Tooltip("Kekuatan lengkung turun panah.")]
    public float lengkungTurun = 7f;

    [Tooltip("Jika aktif, rotasi panah mengikuti arah gerak selama terbang.")]
    public bool rotasiIkutArah = true;

    [Header("Landing Berdasarkan TitikKaki")]
    [Tooltip("Jika aktif, Full Draw non-piercing memakai TitikKaki player sebagai garis tanah visual.")]
    public bool pakaiTitikKakiUntukJatuh = true;

    [Tooltip("Titik kaki player. Isi dengan object TitikKaki di bawah Player_W2.")]
    public Transform titikKaki;

    [Tooltip("Offset Y dari TitikKaki untuk menentukan garis tanah visual.")]
    public float offsetYJatuhDariTitikKaki = 0.04f;

    [Tooltip("Jika aktif, panah dipaksa nancep ke garis TitikKaki jika belum sempat mendarat sampai fase cari tanah selesai.")]
    public bool paksaNancepKeTitikKakiSaatTimeout = true;

    [Header("Landing Cadangan / Raycast Tanah")]
    [Tooltip("Layer tanah yang dipakai sebagai cadangan jika tidak memakai TitikKaki.")]
    public LayerMask lapisanTanah;

    [Tooltip("Panah harus maju minimal sejauh ini sebelum boleh dicek untuk mendarat.")]
    public float jarakMinimumSebelumBolehJatuh = 0.5f;

    [Tooltip("Panah baru boleh dicek mendarat kalau kecepatan Y sudah lebih kecil dari nilai ini.")]
    public float batasKecepatanTurun = -0.1f;

    [Tooltip("Raycast ke tanah dimulai setinggi ini di atas posisi panah.")]
    public float tinggiRaycastTanah = 1.0f;

    [Tooltip("Jarak maksimum raycast ke bawah untuk mencari tanah.")]
    public float jarakRaycastTanah = 1.5f;

    [Tooltip("Panah dianggap menyentuh tanah jika jaraknya ke tanah sebesar ini atau kurang.")]
    public float jarakDekatTanahUntukMendarat = 0.06f;

    [Tooltip("Normal minimum agar permukaan dianggap tanah.")]
    [Range(0f, 1f)]
    public float minimumNormalTanah = 0.6f;

    [Header("Nancep Full Draw")]
    [Tooltip("Offset kecil saat panah sudah nancep. Y naikkan jika panah terlalu masuk tanah.")]
    public Vector2 offsetSaatNancep = new Vector2(0f, 0.05f);

    [Tooltip("Sudut utama panah Full Draw saat nancep.")]
    public float sudutNancepFullDraw = 25f;

    [Tooltip("Sudut minimum agar panah tidak terlihat rebah.")]
    public float sudutMinimumNancep = 15f;

    [Tooltip("Sudut maksimum agar panah tidak terlalu tegak.")]
    public float sudutMaksimumNancep = 40f;

    [Tooltip("Jika aktif, trail panah dimatikan ketika panah sudah nancep.")]
    public bool matikanTrailSaatNancep = true;

    [Header("Fase Cari Tanah")]
    [Tooltip("Jika aktif, panah tidak langsung hilang saat durasi terbang habis, tetapi mencari tanah lebih dahulu.")]
    public bool cariTanahSetelahDurasiTerbang = true;

    [Tooltip("Batas waktu tambahan untuk mencari tanah setelah durasi terbang utama habis.")]
    public float batasWaktuCariTanah = 0.4f;

    [Tooltip("Kecepatan horizontal saat fase turun akhir untuk mencari tanah.")]
    public float pengaliKecepatanSaatTurunAkhir = 0.8f;

    [Tooltip("Jika aktif, panah dipaksa nancep ke tanah raycast jika fase cari tanah habis tetapi raycast menemukan tanah.")]
    public bool paksaNancepJikaRaycastTanahDitemukan = true;

    [Header("Hilang Setelah Nancep")]
    [Tooltip("Berapa lama panah tetap terlihat setelah nancep.")]
    public float waktuTerlihatSetelahNancep = 0.8f;

    [Header("Timing")]
    [Tooltip("Cooldown Full Draw.")]
    [FormerlySerializedAs("cooldown")]
    public float jedaSkill = 0.35f;

    [Tooltip("Lama gerak player dikunci setelah panah dilepas.")]
    [FormerlySerializedAs("postReleaseLock")]
    public float lockSetelahTembak = 0.08f;

    [Header("Energy")]
    [Tooltip("Biaya energi Full Draw normal. Dibayar saat mulai hold.")]
    [FormerlySerializedAs("energyCost")]
    [SerializeField, Min(0f)]
    private float biayaEnergi = 18f;

    [Tooltip("Biaya energi total ketika Full Draw mencapai Piercing / full charge.")]
    [SerializeField, Min(0f)]
    private float biayaEnergiFullCharge = 30f;

    [Tooltip("Jika aktif, regen energi dihentikan sejak mulai hold Full Draw sampai panah benar-benar terlepas.")]
    [SerializeField]
    private bool blokirRegenEnergiSelamaCasting = true;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    public float EnergyCost => Mathf.Max(biayaEnergi, biayaEnergiFullCharge);
    public bool PayEnergyInSkillBase => false;

    private CharacterBase pemilikEnergi;
    private SkillBase skillBase;
    private Rigidbody2D rbPemain;

    private Coroutine routineCharge;
    private Coroutine routineCooldown;
    private Coroutine routineTungguReleaseEvent;
    private Coroutine routineSelesaiRelease;

    private bool sedangCooldown;
    private bool sedangCharge;
    private bool sedangRelease;
    private bool menungguReleaseEvent;
    private bool releaseSudahDitembakkan;
    private bool sudahMasukPiercing;
    private bool regenEnergiSedangDiblokir;

    private float timerCharge;
    private float pendingChargePercent;
    private bool pendingPakaiPiercing;

    private KeyCode tombolCharge = KeyCode.None;

    private void Awake()
    {
        AutoAssignReferences();
    }

    [ContextMenu("Auto Assign References")]
    public void AutoAssignReferences()
    {
        if (pemain == null)
            pemain = GetComponentInParent<Player>(true);

        if (pemilikEnergi == null)
            pemilikEnergi = pemain != null ? pemain : GetComponentInParent<CharacterBase>(true);

        if (skillBase == null)
            skillBase = GetComponentInParent<SkillBase>(true);

        if (rbPemain == null && pemain != null)
            rbPemain = pemain.GetComponent<Rigidbody2D>();

        if (animasiPemain == null)
        {
            if (pemain != null)
                animasiPemain = pemain.GetComponentInChildren<PlayerAnimation>(true);
            else
                animasiPemain = transform.root.GetComponentInChildren<PlayerAnimation>(true);
        }

        if (facingSprite == null)
        {
            if (pemain != null)
                facingSprite = pemain.GetComponentInChildren<SpriteRenderer>(true);
            else
                facingSprite = transform.root.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (titikKaki == null && pemain != null)
            titikKaki = CariChildDenganNama(pemain.transform, "TitikKaki");

        if (chargeBar == null)
            chargeBar = FindObjectOfType<BowChargeBar>();
    }

    public void TriggerSkill(int slotID)
    {
        if (sedangCooldown || sedangCharge || sedangRelease)
            return;

        AutoAssignReferences();

        if (!ReferensiLengkap())
            return;

        if (pemain.lockMovement)
            return;

        if (!CobaKurangiEnergi(biayaEnergi))
            return;

        SetRegenEnergiDiblokir(true);

        tombolCharge = AmbilTombolSlot(slotID);

        sedangCharge = true;
        sedangRelease = false;
        menungguReleaseEvent = false;
        releaseSudahDitembakkan = false;
        sudahMasukPiercing = false;

        timerCharge = 0f;
        pendingChargePercent = 0f;
        pendingPakaiPiercing = false;

        if (kunciGerakSaatCharge)
        {
            pemain.lockMovement = true;
            HentikanGerakPemain();
        }

        MulaiCooldown();

        if (chargeBar != null)
        {
            chargeBar.SetCharge01(0f);
            chargeBar.Show();
        }

        SetAnimatorFullCharge(false);

        if (animasiPemain != null)
            animasiPemain.TriggerBowChargeStart();

        if (routineCharge != null)
            StopCoroutine(routineCharge);

        routineCharge = StartCoroutine(RoutineCharge());

        if (debugLog)
            Debug.Log("[Bow_FullDraw] Mulai charge. Regen energi diblokir.", this);
    }

    private IEnumerator RoutineCharge()
    {
        while (sedangCharge)
        {
            timerCharge += Time.deltaTime;
            timerCharge = Mathf.Min(timerCharge, Mathf.Max(0.01f, durasiChargePenuh));

            float charge01 = Mathf.Clamp01(timerCharge / Mathf.Max(0.01f, durasiChargePenuh));

            if (chargeBar != null)
                chargeBar.SetCharge01(charge01);

            bool sekarangPiercing = charge01 >= AmbilBatasPiercing();

            if (sekarangPiercing != sudahMasukPiercing)
            {
                sudahMasukPiercing = sekarangPiercing;
                SetAnimatorFullCharge(sudahMasukPiercing);
            }

            if (tombolCharge != KeyCode.None)
            {
                if (!Input.GetKey(tombolCharge))
                {
                    MulaiRelease();
                    yield break;
                }
            }
            else
            {
                if (timerCharge >= durasiChargePenuh)
                {
                    MulaiRelease();
                    yield break;
                }
            }

            yield return null;
        }

        routineCharge = null;
    }

    private void MulaiRelease()
    {
        if (!sedangCharge)
            return;

        sedangCharge = false;
        sedangRelease = true;
        menungguReleaseEvent = pakaiAnimationEvent;
        releaseSudahDitembakkan = false;

        pendingChargePercent = Mathf.Clamp01(timerCharge / Mathf.Max(0.01f, durasiChargePenuh));
        pendingPakaiPiercing = pendingChargePercent >= AmbilBatasPiercing();

        if (pendingPakaiPiercing)
        {
            float biayaNormal = Mathf.Max(0f, biayaEnergi);
            float biayaFull = Mathf.Max(biayaNormal, biayaEnergiFullCharge);
            float biayaTambahanFullCharge = Mathf.Max(0f, biayaFull - biayaNormal);

            if (!CobaKurangiEnergi(biayaTambahanFullCharge))
            {
                Debug.LogWarning("[Bow_FullDraw] Energi tidak cukup untuk biaya tambahan Piercing / Full Charge.", this);
                BatalkanCastDanRelease();
                return;
            }
        }

        if (chargeBar != null)
            chargeBar.Hide();

        SetAnimatorFullCharge(pendingPakaiPiercing);

        if (animasiPemain != null)
            animasiPemain.TriggerBowChargeRelease();

        if (debugLog)
        {
            string mode = pendingPakaiPiercing ? "Piercing / Full Charge" : "Full Draw Normal";
            Debug.Log("[Bow_FullDraw] Release dimulai. Mode: " + mode + ", Charge: " + pendingChargePercent, this);
        }

        if (!pakaiAnimationEvent)
        {
            ReleaseFromAnimationEvent();
            return;
        }

        if (routineTungguReleaseEvent != null)
            StopCoroutine(routineTungguReleaseEvent);

        routineTungguReleaseEvent = StartCoroutine(RoutineTungguReleaseEvent());
    }

    private IEnumerator RoutineTungguReleaseEvent()
    {
        float timer = 0f;

        while (menungguReleaseEvent && timer < batasWaktuMenungguReleaseEvent)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (releaseSudahDitembakkan)
        {
            routineTungguReleaseEvent = null;
            yield break;
        }

        if (fallbackTembakJikaEventGagal)
        {
            Debug.LogWarning("[Bow_FullDraw] Animation Event AE_BowFullDraw_Release tidak terpanggil. Fallback menembakkan panah.", this);
            ReleaseFromAnimationEvent();
        }
        else
        {
            Debug.LogWarning("[Bow_FullDraw] Animation Event AE_BowFullDraw_Release tidak terpanggil. Release dibatalkan agar lockMovement tidak macet.", this);
            SelesaikanRelease();
        }

        routineTungguReleaseEvent = null;
    }

    public void ReleaseFromAnimationEvent()
    {
        if (!sedangRelease || releaseSudahDitembakkan)
            return;

        menungguReleaseEvent = false;
        releaseSudahDitembakkan = true;

        if (pendingPakaiPiercing)
            TembakkanPiercing();
        else
            TembakkanFullDraw(pendingChargePercent);

        if (routineSelesaiRelease != null)
            StopCoroutine(routineSelesaiRelease);

        routineSelesaiRelease = StartCoroutine(RoutineSelesaiRelease());
    }

    private IEnumerator RoutineSelesaiRelease()
    {
        if (lockSetelahTembak > 0f)
            yield return new WaitForSeconds(lockSetelahTembak);

        SelesaikanRelease();
        routineSelesaiRelease = null;
    }

    private void SelesaikanRelease()
    {
        if (pemain != null)
            pemain.lockMovement = false;

        sedangCharge = false;
        sedangRelease = false;
        menungguReleaseEvent = false;
        releaseSudahDitembakkan = false;
        sudahMasukPiercing = false;

        pendingChargePercent = 0f;
        pendingPakaiPiercing = false;

        SetAnimatorFullCharge(false);
        SetRegenEnergiDiblokir(false);

        if (debugLog)
            Debug.Log("[Bow_FullDraw] Release selesai. Regen energi dibuka kembali.", this);
    }

    private void BatalkanCastDanRelease()
    {
        if (pemain != null)
            pemain.lockMovement = false;

        sedangCharge = false;
        sedangRelease = false;
        menungguReleaseEvent = false;
        releaseSudahDitembakkan = false;
        sudahMasukPiercing = false;

        pendingChargePercent = 0f;
        pendingPakaiPiercing = false;

        if (chargeBar != null)
            chargeBar.Hide();

        SetAnimatorFullCharge(false);
        SetRegenEnergiDiblokir(false);
    }

    private void MulaiCooldown()
    {
        if (routineCooldown != null)
            StopCoroutine(routineCooldown);

        routineCooldown = StartCoroutine(RoutineCooldown());
    }

    private IEnumerator RoutineCooldown()
    {
        sedangCooldown = true;

        if (jedaSkill > 0f)
            yield return new WaitForSeconds(jedaSkill);

        sedangCooldown = false;
        routineCooldown = null;
    }

    private void TembakkanFullDraw(float chargePercent)
    {
        float batas = Mathf.Max(0.0001f, AmbilBatasPiercing());
        float faktor = Mathf.Clamp01(chargePercent / batas);

        float speed = Mathf.Lerp(kecepatanPanahMinimum, kecepatanPanahMaksimum, faktor);
        float damage = Mathf.Lerp(damageMinimum, damageMaksimum, faktor);
        float knockback = Mathf.Lerp(dorongMundurMinimum, dorongMundurMaksimum, faktor);
        float stun = Mathf.Lerp(lumpuhMinimum, lumpuhMaksimum, faktor);

        BuatPanah(prefabPanahFullDraw, speed, damage, knockback, stun, false);
    }

    private void TembakkanPiercing()
    {
        BuatPanah(prefabPanahPiercing, kecepatanPiercing, damagePiercing, dorongMundurPiercing, lumpuhPiercing, true);
    }

    private void BuatPanah(GameObject prefab, float speed, float damage, float knockback, float stun, bool piercing)
    {
        if (prefab == null || titikTembak == null)
        {
            Debug.LogWarning("[Bow_FullDraw] Prefab panah atau titik tembak belum diisi.", this);
            return;
        }

        GameObject panahObj = Instantiate(prefab, titikTembak.position, Quaternion.identity);

        if (panahObj == null)
        {
            Debug.LogWarning("[Bow_FullDraw] Gagal membuat panah.", this);
            return;
        }

        Rigidbody2D rb = panahObj.GetComponent<Rigidbody2D>();
        ArrowDamage damagePanah = panahObj.GetComponent<ArrowDamage>();

        if (rb == null)
        {
            Debug.LogWarning("[Bow_FullDraw] Prefab panah tidak memiliki Rigidbody2D.", panahObj);
            Destroy(panahObj);
            return;
        }

        float arah = AmbilArahHadap();

        rb.gravityScale = 0f;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;

        TerapkanArahAwalPanah(panahObj, arah);

        if (resetTrailSaatSpawn)
            ResetTrailPanah(panahObj);

        if (damagePanah != null)
        {
            damagePanah.SetOwner(pemilikEnergi);
            damagePanah.SetStats(damage, knockback, stun, piercing, false);
        }

        CatatDataFullDraw();

        if (debugLog)
        {
            string mode = piercing ? "Piercing / Full Charge" : "Full Draw Normal";
            Debug.Log("[Bow_FullDraw] Panah dibuat. Mode: " + mode + ", speed: " + speed + ", damage: " + damage + ", stun: " + stun, this);
        }

        if (piercing && piercingFullLurus)
            StartCoroutine(RoutinePiercingLurus(rb, panahObj, speed, arah));
        else
            StartCoroutine(RoutinePanahFullDraw(rb, panahObj, speed, arah));
    }

    private IEnumerator RoutinePanahFullDraw(Rigidbody2D rb, GameObject panahObj, float speed, float arah)
    {
        if (rb == null || panahObj == null)
            yield break;

        float timer = 0f;
        float posisiAwalX = rb.position.x;
        float vy = angkatSedikit;
        bool sudahNancep = false;

        float waktuMulaiTurun = Mathf.Clamp(mulaiTurun, 0f, Mathf.Max(0.01f, durasiTerbang - 0.01f));

        while (timer < durasiTerbang && !sudahNancep)
        {
            if (rb == null || panahObj == null)
                yield break;

            if (timer < waktuMulaiTurun)
                vy = angkatSedikit;
            else
                vy -= lengkungTurun * Time.deltaTime;

            rb.velocity = new Vector2(arah * speed, vy);

            if (rotasiIkutArah)
                TerapkanRotasiPanah(rb);

            if (BolehCekMendarat(rb, posisiAwalX))
            {
                if (CobaNancep(rb, panahObj, arah))
                {
                    sudahNancep = true;
                    break;
                }
            }

            timer += Time.deltaTime;
            yield return null;
        }

        if (!sudahNancep && cariTanahSetelahDurasiTerbang)
        {
            float timerCariTanah = 0f;

            while (timerCariTanah < batasWaktuCariTanah && !sudahNancep)
            {
                if (rb == null || panahObj == null)
                    yield break;

                float vxAkhir = arah * speed * pengaliKecepatanSaatTurunAkhir;
                float vyAkhir = rb.velocity.y - (lengkungTurun * 2f) * Time.deltaTime;

                rb.velocity = new Vector2(vxAkhir, vyAkhir);

                if (rotasiIkutArah)
                    TerapkanRotasiPanah(rb);

                if (BolehCekMendarat(rb, posisiAwalX))
                {
                    if (CobaNancep(rb, panahObj, arah))
                    {
                        sudahNancep = true;
                        break;
                    }
                }

                timerCariTanah += Time.deltaTime;
                yield return null;
            }
        }

        if (!sudahNancep && pakaiTitikKakiUntukJatuh && paksaNancepKeTitikKakiSaatTimeout)
        {
            float yTanahVisual;

            if (AmbilYTanahVisual(out yTanahVisual))
            {
                float yTarget = yTanahVisual + offsetYJatuhDariTitikKaki;
                PaksaPanahNancep(rb, panahObj, arah, yTarget);
                sudahNancep = true;
            }
        }

        if (!sudahNancep && paksaNancepJikaRaycastTanahDitemukan)
        {
            if (CobaPaksaNancepDenganRaycast(rb, panahObj, arah))
                sudahNancep = true;
        }

        if (!sudahNancep)
        {
            if (panahObj != null)
                Destroy(panahObj);

            yield break;
        }

        yield return new WaitForSeconds(waktuTerlihatSetelahNancep);

        if (panahObj != null)
            Destroy(panahObj);
    }

    private IEnumerator RoutinePiercingLurus(Rigidbody2D rb, GameObject panahObj, float speed, float arah)
    {
        if (rb == null || panahObj == null)
            yield break;

        float timer = 0f;

        rb.gravityScale = 0f;
        rb.angularVelocity = 0f;

        float sudutAwal = arah > 0f ? 0f : 180f;
        rb.transform.rotation = Quaternion.Euler(0f, 0f, sudutAwal);

        while (timer < umurPiercingLurus)
        {
            if (rb == null || panahObj == null)
                yield break;

            if (paksaPiercingHorizontal)
                rb.velocity = new Vector2(arah * speed, 0f);
            else
                rb.velocity = new Vector2(arah * speed, rb.velocity.y);

            timer += Time.deltaTime;
            yield return null;
        }

        if (panahObj != null)
            Destroy(panahObj);
    }

    private bool BolehCekMendarat(Rigidbody2D rb, float posisiAwalX)
    {
        if (rb == null)
            return false;

        float jarakDitempuh = Mathf.Abs(rb.position.x - posisiAwalX);

        if (jarakDitempuh < jarakMinimumSebelumBolehJatuh)
            return false;

        if (rb.velocity.y > batasKecepatanTurun)
            return false;

        return true;
    }

    private bool CobaNancep(Rigidbody2D rb, GameObject panahObj, float arah)
    {
        if (pakaiTitikKakiUntukJatuh)
            return CobaNancepDiTitikKaki(rb, panahObj, arah);

        return CobaNancepDiTanahRaycast(rb, panahObj, arah);
    }

    private bool CobaNancepDiTitikKaki(Rigidbody2D rb, GameObject panahObj, float arah)
    {
        if (rb == null || panahObj == null)
            return false;

        float yTanahVisual;

        if (!AmbilYTanahVisual(out yTanahVisual))
            return false;

        float yTarget = yTanahVisual + offsetYJatuhDariTitikKaki;

        if (rb.position.y > yTarget)
            return false;

        PaksaPanahNancep(rb, panahObj, arah, yTarget);
        return true;
    }

    private bool CobaNancepDiTanahRaycast(Rigidbody2D rb, GameObject panahObj, float arah)
    {
        if (rb == null || panahObj == null)
            return false;

        Vector2 origin = new Vector2(rb.position.x, rb.position.y + tinggiRaycastTanah);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, jarakRaycastTanah, lapisanTanah);

        if (hit.collider == null)
            return false;

        if (hit.normal.y < minimumNormalTanah)
            return false;

        float jarakKeTanah = rb.position.y - hit.point.y;

        if (jarakKeTanah > jarakDekatTanahUntukMendarat)
            return false;

        PaksaPanahNancep(rb, panahObj, arah, hit.point.y);
        return true;
    }

    private bool CobaPaksaNancepDenganRaycast(Rigidbody2D rb, GameObject panahObj, float arah)
    {
        if (rb == null || panahObj == null)
            return false;

        Vector2 origin = new Vector2(rb.position.x, rb.position.y + tinggiRaycastTanah);
        RaycastHit2D hit = Physics2D.Raycast(origin, Vector2.down, jarakRaycastTanah * 2f, lapisanTanah);

        if (hit.collider == null)
            return false;

        if (hit.normal.y < minimumNormalTanah)
            return false;

        PaksaPanahNancep(rb, panahObj, arah, hit.point.y);
        return true;
    }

    private void PaksaPanahNancep(Rigidbody2D rb, GameObject panahObj, float arah, float yTanah)
    {
        if (rb == null || panahObj == null)
            return;

        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;

        Collider2D col = panahObj.GetComponent<Collider2D>();

        if (col != null)
            col.enabled = false;

        if (matikanTrailSaatNancep)
            MatikanTrailPanah(panahObj);

        Vector3 posisiAkhir = panahObj.transform.position;
        posisiAkhir.x += offsetSaatNancep.x * arah;
        posisiAkhir.y = yTanah + offsetSaatNancep.y;

        float min = Mathf.Min(sudutMinimumNancep, sudutMaksimumNancep);
        float max = Mathf.Max(sudutMinimumNancep, sudutMaksimumNancep);
        float sudutFinal = Mathf.Clamp(sudutNancepFullDraw, min, max);

        float rotasiZ = arah > 0f ? -sudutFinal : 180f + sudutFinal;

        panahObj.transform.position = posisiAkhir;
        panahObj.transform.rotation = Quaternion.Euler(0f, 0f, rotasiZ);
    }

    private bool AmbilYTanahVisual(out float yTanahVisual)
    {
        if (titikKaki != null)
        {
            yTanahVisual = titikKaki.position.y;
            return true;
        }

        if (pemain != null)
        {
            yTanahVisual = pemain.transform.position.y;
            return true;
        }

        yTanahVisual = 0f;
        return false;
    }

    private void TerapkanRotasiPanah(Rigidbody2D rb)
    {
        if (rb == null)
            return;

        Vector2 velocity = rb.velocity;

        if (velocity.sqrMagnitude <= 0.0001f)
            return;

        float sudut = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg;
        rb.transform.rotation = Quaternion.Euler(0f, 0f, sudut);
    }

    private void TerapkanArahAwalPanah(GameObject panahObj, float arah)
    {
        if (panahObj == null)
            return;

        Vector3 scale = panahObj.transform.localScale;
        panahObj.transform.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));

        float sudutAwal = arah > 0f ? 0f : 180f;
        panahObj.transform.rotation = Quaternion.Euler(0f, 0f, sudutAwal);

        SpriteRenderer[] renderers = panahObj.GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer sr in renderers)
            sr.flipX = false;
    }

    private void ResetTrailPanah(GameObject panahObj)
    {
        if (panahObj == null)
            return;

        TrailRenderer[] trails = panahObj.GetComponentsInChildren<TrailRenderer>(true);

        foreach (TrailRenderer trail in trails)
        {
            trail.Clear();
            trail.emitting = true;
        }
    }

    private void MatikanTrailPanah(GameObject panahObj)
    {
        if (panahObj == null)
            return;

        TrailRenderer[] trails = panahObj.GetComponentsInChildren<TrailRenderer>(true);

        foreach (TrailRenderer trail in trails)
            trail.emitting = false;
    }

    private float AmbilArahHadap()
    {
        if (facingSprite != null)
            return facingSprite.flipX ? -1f : 1f;

        if (pemain != null)
            return pemain.isFacingRight ? 1f : -1f;

        if (pemilikEnergi != null)
            return pemilikEnergi.isFacingRight ? 1f : -1f;

        return 1f;
    }

    private float AmbilBatasPiercing()
    {
        if (chargeBar != null)
            return chargeBar.PiercingThreshold;

        return batasPiercingFallback;
    }

    private void SetAnimatorFullCharge(bool value)
    {
        if (animasiPemain != null)
            animasiPemain.SetBowFullCharge(value);
    }

    private void SetRegenEnergiDiblokir(bool blokir)
    {
        if (!blokirRegenEnergiSelamaCasting)
            return;

        if (regenEnergiSedangDiblokir == blokir)
            return;

        regenEnergiSedangDiblokir = blokir;

        if (pemilikEnergi == null)
            pemilikEnergi = pemain != null ? pemain : GetComponentInParent<CharacterBase>(true);

        if (pemilikEnergi != null)
            pemilikEnergi.SetEnergyRegenBlocked(blokir);
    }

    private bool ReferensiLengkap()
    {
        bool lengkap = true;

        if (pemain == null)
        {
            Debug.LogWarning("[Bow_FullDraw] Player belum diisi.", this);
            lengkap = false;
        }

        if (titikTembak == null)
        {
            Debug.LogWarning("[Bow_FullDraw] Titik Tembak belum diisi.", this);
            lengkap = false;
        }

        if (prefabPanahFullDraw == null)
        {
            Debug.LogWarning("[Bow_FullDraw] Prefab Panah Full Draw belum diisi.", this);
            lengkap = false;
        }

        if (prefabPanahPiercing == null)
        {
            Debug.LogWarning("[Bow_FullDraw] Prefab Panah Piercing belum diisi.", this);
            lengkap = false;
        }

        if (animasiPemain == null)
        {
            Debug.LogWarning("[Bow_FullDraw] Animasi Pemain belum diisi.", this);
            lengkap = false;
        }

        return lengkap;
    }

    private bool CobaKurangiEnergi(float biaya)
    {
        biaya = Mathf.Max(0f, biaya);

        if (biaya <= 0f)
            return true;

        if (pemilikEnergi == null)
            pemilikEnergi = pemain != null ? pemain : GetComponentInParent<CharacterBase>(true);

        if (pemilikEnergi == null)
        {
            Debug.LogWarning("[Bow_FullDraw] Pemilik energi tidak ditemukan.", this);
            return false;
        }

        if (!pemilikEnergi.TrySpendEnergy(biaya))
        {
            Debug.LogWarning("[Bow_FullDraw] Energi tidak cukup. Butuh " + biaya + ".", this);
            return false;
        }

        return true;
    }

    private void HentikanGerakPemain()
    {
        if (pemain == null)
            return;

        if (rbPemain == null)
            rbPemain = pemain.GetComponent<Rigidbody2D>();

        if (rbPemain != null)
            rbPemain.velocity = Vector2.zero;
    }

    private KeyCode AmbilTombolSlot(int slotID)
    {
        if (skillBase != null)
        {
            switch (slotID)
            {
                case 0: return skillBase.slot1Key;
                case 1: return skillBase.slot2Key;
                case 2: return skillBase.slot3Key;
                case 3: return skillBase.slot4Key;
            }
        }

        switch (slotID)
        {
            case 0: return KeyCode.Alpha1;
            case 1: return KeyCode.Alpha2;
            case 2: return KeyCode.Alpha3;
            case 3: return KeyCode.Alpha4;
            default: return KeyCode.None;
        }
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

    private void CatatDataFullDraw()
    {
        DataTracker tracker = DataTracker.Instance;

        if (tracker == null)
            return;

        MethodInfo method = tracker.GetType().GetMethod("RecordBowFullDraw");

        if (method != null)
        {
            method.Invoke(tracker, null);
            return;
        }

        tracker.RecordAction(PlayerActionType.Offensive, WeaponType.Bow);
    }

    private void OnDisable()
    {
        if (routineCharge != null)
            StopCoroutine(routineCharge);

        if (routineCooldown != null)
            StopCoroutine(routineCooldown);

        if (routineTungguReleaseEvent != null)
            StopCoroutine(routineTungguReleaseEvent);

        if (routineSelesaiRelease != null)
            StopCoroutine(routineSelesaiRelease);

        if (pemain != null)
            pemain.lockMovement = false;

        sedangCooldown = false;
        sedangCharge = false;
        sedangRelease = false;
        menungguReleaseEvent = false;
        releaseSudahDitembakkan = false;
        sudahMasukPiercing = false;

        timerCharge = 0f;
        pendingChargePercent = 0f;
        pendingPakaiPiercing = false;

        if (chargeBar != null)
        {
            chargeBar.SetCharge01(0f);
            chargeBar.HideImmediate();
        }

        SetAnimatorFullCharge(false);
        SetRegenEnergiDiblokir(false);

        if (animasiPemain != null)
            animasiPemain.SetCharging(false);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        durasiChargePenuh = Mathf.Max(0.01f, durasiChargePenuh);
        batasWaktuMenungguReleaseEvent = Mathf.Max(0f, batasWaktuMenungguReleaseEvent);

        biayaEnergi = Mathf.Max(0f, biayaEnergi);
        biayaEnergiFullCharge = Mathf.Max(0f, biayaEnergiFullCharge);

        if (biayaEnergiFullCharge < biayaEnergi)
            biayaEnergiFullCharge = biayaEnergi;

        kecepatanPanahMinimum = Mathf.Max(0f, kecepatanPanahMinimum);
        kecepatanPanahMaksimum = Mathf.Max(kecepatanPanahMinimum, kecepatanPanahMaksimum);
        kecepatanPiercing = Mathf.Max(0f, kecepatanPiercing);

        damageMinimum = Mathf.Max(0f, damageMinimum);
        damageMaksimum = Mathf.Max(damageMinimum, damageMaksimum);
        damagePiercing = Mathf.Max(0f, damagePiercing);

        dorongMundurMinimum = Mathf.Max(0f, dorongMundurMinimum);
        dorongMundurMaksimum = Mathf.Max(dorongMundurMinimum, dorongMundurMaksimum);
        dorongMundurPiercing = Mathf.Max(0f, dorongMundurPiercing);

        lumpuhMinimum = Mathf.Max(0f, lumpuhMinimum);
        lumpuhMaksimum = Mathf.Max(lumpuhMinimum, lumpuhMaksimum);
        lumpuhPiercing = Mathf.Max(0f, lumpuhPiercing);
    }

    private void OnDrawGizmosSelected()
    {
        if (titikTembak != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(titikTembak.position, 0.05f);
        }

        if (titikKaki != null)
        {
            Gizmos.color = Color.green;
            Vector3 garis = titikKaki.position + Vector3.up * offsetYJatuhDariTitikKaki;
            Gizmos.DrawLine(garis + Vector3.left * 0.8f, garis + Vector3.right * 0.8f);
        }
    }
#endif
}