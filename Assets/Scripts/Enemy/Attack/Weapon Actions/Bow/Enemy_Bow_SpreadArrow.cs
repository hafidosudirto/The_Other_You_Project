using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_Bow_SpreadArrow : MonoBehaviour
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

    [Header("Skill Conditions")]
    public float skillRange = 8f;
    public float minRange = 2f;
    public float rangeTolerance = 1f;
    public float cooldown = 3f;

    [Header("Referensi")]
    public Transform titikTembak;
    public GameObject prefabPanah;
    public CharacterBase enemyCharacter;
    public Animator enemyAnimator;
    public SpriteRenderer facingSprite;

    [Header("Animasi")]
    public string triggerAnimasi = "SpreadArrow";
    public bool pakaiAnimationEvent = true;
    public float fallbackDelayTembak = 0.25f;

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

    // State Internal AI
    private bool sedangCast;
    private bool panahSudahDitembakkan;
    private Coroutine routineCast;
    private float lastUseTime = -999f;

    // --- PROPERTI & FUNGSI WAJIB UNTUK BEHAVIOR TREE NODE ---
    public bool IsActive => sedangCast;

    public bool IsInRange(float distance)
    {
        return distance <= (skillRange + rangeTolerance) && distance >= (minRange - rangeTolerance);
    }

    public bool CanTrigger(float distance)
    {
        if (sedangCast) return false;
        if (Time.time < lastUseTime + cooldown) return false;
        return true;
    }

    // Dipanggil oleh Behavior Tree Node
    public void Trigger()
    {
        if (sedangCast) return;

        lastUseTime = Time.time;

        if (titikTembak == null || prefabPanah == null)
        {
            Debug.LogWarning("[Enemy_Bow_SpreadArrow] Titik tembak atau prefab kosong!");
            return;
        }

        routineCast = StartCoroutine(RoutineCast());
    }
    // ---------------------------------------------------------

    private void Awake()
    {
        if (enemyCharacter == null)
            enemyCharacter = GetComponentInParent<CharacterBase>();

        if (enemyAnimator == null && enemyCharacter != null)
            enemyAnimator = enemyCharacter.GetComponentInChildren<Animator>();

        if (facingSprite == null && enemyCharacter != null)
            facingSprite = enemyCharacter.GetComponentInChildren<SpriteRenderer>();
    }

    private IEnumerator RoutineCast()
    {
        sedangCast = true;
        panahSudahDitembakkan = false;

        if (enemyAnimator != null && !string.IsNullOrEmpty(triggerAnimasi))
            enemyAnimator.SetTrigger(triggerAnimasi);

        if (!pakaiAnimationEvent)
        {
            yield return new WaitForSeconds(fallbackDelayTembak);
            ReleaseSpreadArrow();
        }

        // Timeout untuk mengamankan lock AI jika event tidak terpanggil
        float timeout = 1.0f;
        float timer = 0f;
        while (!panahSudahDitembakkan && timer < timeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (!panahSudahDitembakkan)
            ReleaseSpreadArrow();

        sedangCast = false;
        routineCast = null;
    }

    // Dipanggil dari Animation Event pada klip SpreadArrow milik musuh
    public void ReleaseSpreadArrow()
    {
        if (panahSudahDitembakkan) return;
        panahSudahDitembakkan = true;

        List<DataPanah> formasi = BangunFormasiPanah();
        float arah = AmbilArahHadap();

        foreach (var data in formasi)
        {
            SpawnPanah(data, arah);
        }
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
        Vector3 posisiSpawn = titikTembak.position + new Vector3(0f, data.offsetY, 0f);
        GameObject panahObj = Instantiate(prefabPanah, posisiSpawn, Quaternion.identity);

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
            arrowDamage.owner = enemyCharacter;
            arrowDamage.SetStats(damageSpread, dorongMundur, lumpuhSingkat, panahMenembus, false);
        }

        StartCoroutine(RoutinePanah(rb, panahObj, arah, data.sudut, data.bonusVy));
    }

    private IEnumerator RoutinePanah(Rigidbody2D rb, GameObject panahObj, float arah, float sudutAwal, float bonusVy)
    {
        if (rb == null || panahObj == null) yield break;

        bool modeSpreadLurus = polaTembak == PolaTembak.SpreadTetap && spreadTetapLurusSepertiPiercing;

        if (modeSpreadLurus)
        {
            Vector2 velocityLurus = new Vector2(arah * kecepatanPanah, bonusVy);
            float t = 0f;
            while (t < durasiTerbang)
            {
                if (rb == null || panahObj == null) yield break;
                rb.velocity = velocityLurus;
                if (rotasiIkutArah) TerapkanRotasiPanah(rb);

                t += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            float timer = 0f;
            float waktuMulaiTurun = Mathf.Clamp(mulaiTurun, 0f, Mathf.Max(0.01f, durasiTerbang - 0.01f));
            Vector2 arahAwal = HitungArahDariSudut(arah, sudutAwal);

            float vx = arahAwal.x * kecepatanPanah;
            float vy = arahAwal.y * kecepatanPanah + angkatSedikit + bonusVy;

            while (timer < durasiTerbang)
            {
                if (rb == null || panahObj == null) yield break;

                if (timer >= waktuMulaiTurun)
                    vy -= lengkungTurun * Time.deltaTime;

                rb.velocity = new Vector2(vx, vy);
                if (rotasiIkutArah) TerapkanRotasiPanah(rb);

                timer += Time.deltaTime;
                yield return null;
            }
        }

        if (panahObj != null)
            Destroy(panahObj);
    }

    private Vector2 HitungArahDariSudut(float arahHadap, float sudut)
    {
        Vector2 dir = Quaternion.Euler(0f, 0f, sudut) * Vector2.right;
        if (arahHadap < 0f) dir = new Vector2(-dir.x, dir.y);
        return dir.normalized;
    }

    private void TerapkanRotasiPanah(Rigidbody2D rb)
    {
        if (rb == null || rb.velocity.sqrMagnitude <= 0.0001f) return;
        float sudut = Mathf.Atan2(rb.velocity.y, rb.velocity.x) * Mathf.Rad2Deg;
        rb.transform.rotation = Quaternion.Euler(0f, 0f, sudut);
    }

    private void TerapkanArahAwalPanah(GameObject panahObj, float arah)
    {
        if (panahObj == null) return;

        Vector3 scale = panahObj.transform.localScale;
        panahObj.transform.localScale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        panahObj.transform.rotation = Quaternion.Euler(0f, 0f, arah > 0f ? 0f : 180f);

        foreach (SpriteRenderer sr in panahObj.GetComponentsInChildren<SpriteRenderer>(true))
            sr.flipX = false;
    }

    private float AmbilArahHadap()
    {
        if (facingSprite != null) return facingSprite.flipX ? -1f : 1f;
        if (enemyCharacter != null) return enemyCharacter.isFacingRight ? 1f : -1f;
        return 1f;
    }
}