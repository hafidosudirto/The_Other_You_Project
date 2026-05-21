using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class Bow_QuickShot : MonoBehaviour, ISkill, IEnergySkill
{
    [Header("Quick Shot / Referensi")]
    [Tooltip("Titik keluarnya panah Quick Shot. Biasanya isi dengan MuzzlePoint.")]
    [FormerlySerializedAs("firePoint")]
    public Transform titikTembak;

    [Tooltip("Prefab panah Quick Shot. Prefab wajib memiliki Rigidbody2D, Collider2D, dan ArrowDamage.")]
    [FormerlySerializedAs("arrowPrefab")]
    public GameObject prefabPanah;

    [Tooltip("Player pemilik skill ini.")]
    [FormerlySerializedAs("player")]
    public Player pemain;

    [Tooltip("Pengendali animasi player yang berada di object Visual.")]
    [FormerlySerializedAs("anim")]
    public PlayerAnimation animasiPemain;

    [Tooltip("SpriteRenderer visual player. Dipakai untuk membaca arah hadap jika arah dari Player tidak sinkron.")]
    public SpriteRenderer facingSprite;

    [Header("Animation Event")]
    [Tooltip("Jika aktif, panah hanya keluar saat Animation Event AE_BowQuickShot_Release memanggil ReleaseFromAnimationEvent.")]
    public bool pakaiAnimationEvent = true;

    [Tooltip("Batas waktu menunggu Animation Event. Jika event tidak terpanggil, skill dibatalkan agar lockMovement tidak macet.")]
    public float batasWaktuMenungguEvent = 0.35f;

    [Tooltip("Jika aktif, panah tetap ditembakkan saat event gagal terpanggil. Untuk debugging awal, sebaiknya dimatikan.")]
    public bool fallbackTembakJikaEventGagal = false;

    [Header("Arrow Setup")]
    [Tooltip("Kecepatan gerak panah ke depan.")]
    [FormerlySerializedAs("speed")]
    public float kecepatanPanah = 15f;

    [Tooltip("Durasi fase terbang utama sebelum panah masuk fase mencari tanah.")]
    [FormerlySerializedAs("lifeTime")]
    public float durasiTerbang = 0.55f;

    [Tooltip("Jika aktif, TrailRenderer pada prefab panah dibersihkan saat spawn agar tidak menyisakan garis lama.")]
    public bool resetTrailSaatSpawn = true;

    [Header("Hit Effect")]
    [Tooltip("Damage Quick Shot.")]
    [FormerlySerializedAs("quickDamage")]
    public float damageQuickShot = 1f;

    [Tooltip("Kekuatan knockback Quick Shot.")]
    [FormerlySerializedAs("knockback")]
    public float dorongMundur = 10f;

    [Tooltip("Durasi stun atau lumpuh singkat Quick Shot.")]
    [FormerlySerializedAs("stun")]
    public float lumpuhSingkat = 0.1f;

    [Header("Arrow Feel")]
    [Tooltip("Panah mulai turun setelah berapa detik.")]
    public float mulaiTurun = 0.09f;

    [Tooltip("Dorongan Y awal agar panah tidak terlalu datar.")]
    public float angkatSedikit = 0.4f;

    [Tooltip("Kekuatan lengkung turun panah.")]
    public float lengkungTurun = 6f;

    [Tooltip("Jika aktif, rotasi panah mengikuti arah gerak selama terbang.")]
    public bool rotasiIkutArah = true;

    [Header("Landing Berdasarkan TitikKaki")]
    [Tooltip("Jika aktif, Quick Shot memakai TitikKaki player sebagai garis tanah visual.")]
    public bool pakaiTitikKakiUntukJatuh = true;

    [Tooltip("Titik kaki player. Isi dengan object TitikKaki di bawah Player_W2.")]
    public Transform titikKaki;

    [Tooltip("Offset Y dari TitikKaki untuk menentukan garis tanah visual.")]
    public float offsetYJatuhDariTitikKaki = 0.04f;

    [Tooltip("Jika aktif, panah tetap dipaksa nancep ke garis TitikKaki jika belum sempat mendarat sampai fase cari tanah selesai.")]
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

    [Tooltip("Panah dianggap sudah menyentuh tanah jika jaraknya ke tanah sebesar ini atau kurang.")]
    public float jarakDekatTanahUntukMendarat = 0.06f;

    [Tooltip("Normal minimum agar permukaan dianggap tanah.")]
    [Range(0f, 1f)]
    public float minimumNormalTanah = 0.6f;

    [Header("Nancep di Tanah")]
    [Tooltip("Offset kecil saat panah sudah nancep. Y naikkan jika panah terlalu masuk tanah.")]
    public Vector2 offsetSaatNancep = new Vector2(0f, 0.065f);

    [Tooltip("Sudut utama panah saat nancep. Nilai 55 sampai 70 membuat panah terlihat menancap, bukan rebah.")]
    public float sudutNancep = 35f;

    [Tooltip("Sudut minimum agar panah tidak terlihat rebah.")]
    public float sudutMinimumNancep = 35f;

    [Tooltip("Sudut maksimum agar panah tidak terlalu tegak.")]
    public float sudutMaksimumNancep = 40f;

    [Tooltip("Jika aktif, trail panah dimatikan ketika panah sudah nancep.")]
    public bool matikanTrailSaatNancep = true;

    [Header("Fase Cari Tanah")]
    [Tooltip("Jika aktif, panah tidak langsung hilang saat durasi terbang habis, tetapi mencari tanah lebih dahulu.")]
    public bool cariTanahSetelahDurasiTerbang = true;

    [Tooltip("Batas waktu tambahan untuk mencari tanah setelah durasi terbang utama habis.")]
    public float batasWaktuCariTanah = 0.45f;

    [Tooltip("Kecepatan horizontal saat fase turun akhir untuk mencari tanah.")]
    public float pengaliKecepatanSaatTurunAkhir = 0.8f;

    [Tooltip("Jika aktif, panah akan dipaksa nancep ke tanah raycast jika fase cari tanah habis tetapi raycast menemukan tanah.")]
    public bool paksaNancepJikaRaycastTanahDitemukan = true;

    [Header("Hilang Setelah Nancep")]
    [Tooltip("Berapa lama panah tetap terlihat setelah nancep.")]
    public float waktuTerlihatSetelahNancep = 0.8f;

    [Header("Timing")]
    [Tooltip("Cooldown Quick Shot.")]
    [FormerlySerializedAs("cooldown")]
    public float jedaSkill = 0.35f;

    [Tooltip("Lama gerak player dikunci setelah panah dilepas.")]
    [FormerlySerializedAs("postShotLock")]
    public float lockSetelahTembak = 0.08f;

    [Header("Energy")]
    [Tooltip("Biaya energi Quick Shot.")]
    [FormerlySerializedAs("energyCost")]
    [SerializeField, Min(0f)]
    private float biayaEnergi = 8f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;

    public float EnergyCost => biayaEnergi;
    public bool PayEnergyInSkillBase => false;

    private CharacterBase pemilikEnergi;
    private Rigidbody2D rbPemain;

    private Coroutine routineCast;
    private Coroutine routineCooldown;

    private bool sedangCooldown;
    private bool sedangCast;
    private bool menungguReleaseEvent;
    private bool panahSudahDilepas;

    private void Awake()
    {
        AutoAssignReferences();
    }

#if UNITY_EDITOR
#if UNITY_EDITOR
    private void OnValidate()
    {
        // Sengaja dikosongkan agar nilai sudut di Inspector tidak kejang-kejang.
        // Validasi sudut dilakukan saat runtime di PaksaPanahNancep().
    }
#endif
#endif

    [ContextMenu("Auto Assign References")]
    public void AutoAssignReferences()
    {
        if (pemain == null)
            pemain = GetComponentInParent<Player>(true);

        if (pemilikEnergi == null)
            pemilikEnergi = pemain != null ? pemain : GetComponentInParent<CharacterBase>(true);

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
    }

    public void TriggerSkill(int slotID)
    {
        if (sedangCooldown || sedangCast)
            return;

        AutoAssignReferences();

        if (!ReferensiLengkap())
            return;

        if (pemain.lockMovement)
            return;

        if (!CobaKurangiEnergi())
            return;

        sedangCast = true;
        menungguReleaseEvent = pakaiAnimationEvent;
        panahSudahDilepas = false;

        pemain.lockMovement = true;
        HentikanGerakPemain();

        MulaiCooldown();

        if (animasiPemain != null)
            animasiPemain.PlayQuickShot();

        if (routineCast != null)
            StopCoroutine(routineCast);

        routineCast = StartCoroutine(RoutineCastQuickShot());
    }

    private IEnumerator RoutineCastQuickShot()
    {
        if (!pakaiAnimationEvent)
        {
            ReleaseFromAnimationEvent();
            yield break;
        }

        float timer = 0f;

        while (menungguReleaseEvent && timer < batasWaktuMenungguEvent)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (panahSudahDilepas)
            yield break;

        if (fallbackTembakJikaEventGagal)
        {
            Debug.LogWarning("[Bow_QuickShot] Animation Event tidak terpanggil. Fallback menembakkan panah.", this);
            ReleaseFromAnimationEvent();
        }
        else
        {
            Debug.LogWarning("[Bow_QuickShot] Animation Event AE_BowQuickShot_Release tidak terpanggil. Cast dibatalkan agar lockMovement tidak macet.", this);
            SelesaikanCast();
        }
    }

    public void ReleaseFromAnimationEvent()
    {
        if (!sedangCast || panahSudahDilepas)
            return;

        menungguReleaseEvent = false;
        panahSudahDilepas = true;

        TembakkanPanah();

        StartCoroutine(RoutineSelesaiSetelahRelease());
    }

    private IEnumerator RoutineSelesaiSetelahRelease()
    {
        if (lockSetelahTembak > 0f)
            yield return new WaitForSeconds(lockSetelahTembak);

        SelesaikanCast();
    }

    private void SelesaikanCast()
    {
        if (pemain != null)
            pemain.lockMovement = false;

        sedangCast = false;
        menungguReleaseEvent = false;
        panahSudahDilepas = false;
        routineCast = null;
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

    private void TembakkanPanah()
    {
        if (prefabPanah == null || titikTembak == null)
        {
            Debug.LogWarning("[Bow_QuickShot] Prefab panah atau titik tembak belum diisi.", this);
            return;
        }

        GameObject panahObj = Instantiate(prefabPanah, titikTembak.position, Quaternion.identity);
        if (panahObj == null)
        {
            Debug.LogWarning("[Bow_QuickShot] Gagal membuat panah.", this);
            return;
        }

        Rigidbody2D rb = panahObj.GetComponent<Rigidbody2D>();
        ArrowDamage damagePanah = panahObj.GetComponent<ArrowDamage>();

        if (rb == null)
        {
            Debug.LogWarning("[Bow_QuickShot] Prefab panah tidak memiliki Rigidbody2D.", panahObj);
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
            damagePanah.SetStats(damageQuickShot, dorongMundur, lumpuhSingkat, false, false);
        }

        if (debugLog)
            Debug.Log("[Bow_QuickShot] Panah ditembakkan. Arah: " + arah, this);

        StartCoroutine(RoutinePanah(rb, panahObj, arah));
    }

    private IEnumerator RoutinePanah(Rigidbody2D rb, GameObject panahObj, float arah)
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

            rb.velocity = new Vector2(arah * kecepatanPanah, vy);

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

                float vxAkhir = arah * kecepatanPanah * pengaliKecepatanSaatTurunAkhir;
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
        float sudutFinal = Mathf.Clamp(sudutNancep, min, max);

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

    private bool ReferensiLengkap()
    {
        bool lengkap = true;

        if (pemain == null)
        {
            Debug.LogWarning("[Bow_QuickShot] Player belum diisi.", this);
            lengkap = false;
        }

        if (titikTembak == null)
        {
            Debug.LogWarning("[Bow_QuickShot] Titik Tembak belum diisi.", this);
            lengkap = false;
        }

        if (prefabPanah == null)
        {
            Debug.LogWarning("[Bow_QuickShot] Prefab Panah belum diisi.", this);
            lengkap = false;
        }

        if (animasiPemain == null)
        {
            Debug.LogWarning("[Bow_QuickShot] Animasi Pemain belum diisi.", this);
            lengkap = false;
        }

        if (pakaiTitikKakiUntukJatuh && titikKaki == null)
        {
            Debug.LogWarning("[Bow_QuickShot] TitikKaki belum diisi. Quick Shot masih bisa jalan, tetapi patokan nancep visual kurang akurat.", this);
        }

        return lengkap;
    }

    private bool CobaKurangiEnergi()
    {
        if (biayaEnergi <= 0f)
            return true;

        if (pemilikEnergi == null)
            pemilikEnergi = pemain != null ? pemain : GetComponentInParent<CharacterBase>(true);

        if (pemilikEnergi == null)
        {
            Debug.LogWarning("[Bow_QuickShot] Pemilik energi tidak ditemukan.", this);
            return false;
        }

        if (!pemilikEnergi.TrySpendEnergy(biayaEnergi))
        {
            Debug.LogWarning("[Bow_QuickShot] Energi tidak cukup. Butuh " + biayaEnergi + ".", this);
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
        if (pemain != null)
            pemain.lockMovement = false;

        sedangCast = false;
        menungguReleaseEvent = false;
        panahSudahDilepas = false;
        sedangCooldown = false;

        routineCast = null;
        routineCooldown = null;
    }

#if UNITY_EDITOR
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