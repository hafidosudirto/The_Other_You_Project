using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bow_SpreadArrow : MonoBehaviour, ISkill, IEnergySkill
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

    [Header("Spread Arrow / Refs")]
    [Tooltip(
        "Titik keluarnya panah dari busur.\n" +
        "Biasanya isi dengan MuzzlePoint.\n\n" +
        "Kalau panah terlalu tinggi, terlalu rendah, terlalu maju, atau terlalu mundur,\n" +
        "atur posisi MuzzlePoint terlebih dahulu."
    )]
    public Transform titikTembak;

    [Tooltip(
        "Prefab panah untuk Skill 3.\n" +
        "Saran: pakai prefab panah lurus / basic.\n\n" +
        "Prefab sebaiknya punya:\n" +
        "- Rigidbody2D\n" +
        "- Collider2D\n" +
        "- ArrowDamage\n" +
        "- TrailRenderer jika ingin ada efek jejak."
    )]
    public GameObject prefabPanah;

    [Tooltip(
        "Referensi player pemilik skill.\n" +
        "Dipakai untuk lock movement, membaca arah hadap, back dash, dan mengambil titik kaki."
    )]
    public Player pemain;

    [Tooltip(
        "Referensi PlayerAnimation pada object Visual.\n" +
        "Dipakai untuk memanggil trigger SpreadArrow secara aman dan tidak mengganggu trigger skill lain."
    )]
    public PlayerAnimation animasiPemain;

    [Tooltip(
        "SpriteRenderer visual player.\n" +
        "Dipakai untuk membaca arah hadap kiri / kanan.\n\n" +
        "Kalau panah menembak ke arah yang salah, cek field ini."
    )]
    public SpriteRenderer facingSprite;

    [Tooltip(
        "Animator visual player.\n" +
        "Dipakai untuk memanggil animasi Skill 3."
    )]
    public Animator animatorVisual;

    [Header("Animasi")]
    [Tooltip(
        "Nama trigger Animator untuk skill ini.\n" +
        "Contoh: SpreadArrow.\n\n" +
        "Kalau kosong, script tidak memanggil animasi."
    )]
    public string triggerAnimasi = "SpreadArrow";

    [Tooltip(
        "Kalau aktif, panah dilepas lewat Animation Event ReleaseSpreadArrow().\n\n" +
        "Saran: aktif, karena sekarang Skill 3 sudah disesuaikan dengan animasi."
    )]
    public bool pakaiAnimationEventUntukTembak = true;

    [Tooltip(
        "Cadangan delay kalau belum memakai Animation Event.\n\n" +
        "Kalau pakai Animation Event, field ini hanya jadi fallback.\n" +
        "Saran awal: 0.20 - 0.28 untuk frame release Anda."
    )]
    public float delaySebelumTembak = 0.24f;

    [Tooltip(
        "Batas maksimal menunggu event ReleaseSpreadArrow().\n\n" +
        "Kalau event lupa dipasang, skill tetap akan menembak setelah waktu ini.\n" +
        "Saran awal: 0.35 - 0.45."
    )]
    public float batasMaksimalMenungguRelease = 0.4f;

    [Tooltip(
        "Lama maksimal skill dianggap masih casting.\n\n" +
        "Kalau Anda belum memakai event akhir animasi, script akan pakai batas ini\n" +
        "untuk memastikan lock movement tidak macet.\n\n" +
        "Saran awal: 0.55 - 0.7."
    )]
    public float durasiMaksimalCast = 0.6f;

    [Header("Back Dash")]
    [Tooltip(
        "Kalau aktif, Skill 3 akan dash ke belakang.\n\n" +
        "Back dash dipanggil dari Animation Event StartBackDash()."
    )]
    public bool gunakanBackDash = true;

    [Tooltip(
        "Jarak dash ke belakang.\n\n" +
        "0.45 = ringan\n" +
        "0.70 = sedang / aman\n" +
        "1.00 = jauh\n\n" +
        "Saran awal: 0.7."
    )]
    public float jarakBackDash = 0.7f;

    [Tooltip(
        "Durasi dash ke belakang.\n\n" +
        "Semakin kecil, dash semakin cepat.\n\n" +
        "Saran awal: 0.12 - 0.18."
    )]
    public float durasiBackDash = 0.14f;

    [Tooltip(
        "Kalau aktif, selama dash velocity player akan di-nol-kan dulu agar lebih rapi."
    )]
    public bool hentikanVelocitySaatBackDash = true;

    [Header("Pola Tembak")]
    [Tooltip(
        "Pilih pola tembak Skill 3.\n\n" +
        "LurusBertingkat:\n" +
        "- panah keluar sejajar;\n" +
        "- beda tinggi atas-bawah;\n" +
        "- paling mirip referensi Little Fighter 2;\n" +
        "- paling stabil dan mudah dituning.\n\n" +
        "KipasRingan:\n" +
        "- panah tengah lurus;\n" +
        "- panah atas dan bawah sedikit diagonal;\n" +
        "- lebih lebar untuk area depan;\n" +
        "- jangan pakai sudut terlalu besar agar tidak liar."

    )]


    public PolaTembak polaTembak = PolaTembak.LurusBertingkat;

    [Tooltip(
        "Jumlah panah yang ditembakkan sekaligus.\n\n" +
        "3 = paling rapi dan aman.\n" +
        "4 = lebih padat.\n" +
        "5 = paling ramai seperti referensi.\n\n" +
        "Saran awal: 3 dulu sampai animasi + dash stabil."
    )]
    [Range(3, 5)]
    public int jumlahPanah = 3;

    [Header("Pola: Lurus Bertingkat")]
    [Tooltip(
        "Jarak tinggi antar panah pada pola LurusBertingkat.\n\n" +
        "Naikkan kalau panah terlihat terlalu menumpuk.\n" +
        "Turunkan kalau panah terlalu renggang.\n\n" +
        "Saran awal untuk 3 panah: 0.18 - 0.24."
    )]
    public float jarakVertikalLurus = 0.22f;

    [Header("Pola: Spread Tetap (sesuai sketch)")]
    [Tooltip(
    "Semua panah keluar dari titik yang sama.\n" +
    "Yang dibedakan hanya dorongan vertikal awal.\n" +
    "Cocok untuk hasil seperti sketch 'Harusnya'."

    )]

    public float bonusVyAtas = 1.15f;

    [Tooltip("Dorongan vertikal panah tengah. Biasanya 0.")]
    public float bonusVyTengah = 0f;

    [Tooltip("Dorongan vertikal panah bawah. Biasanya minus.")]
    public float bonusVyBawah = -1.15f;

    [Tooltip(
        "Kalau ingin titik keluar benar-benar sama, isi 0.\n" +
        "Kalau ingin sedikit dipisah tipis sekali, isi 0.01 - 0.03."
    )]
    public float offsetSpawnSpreadTetap = 0f;

    [Tooltip(
    "Kalau aktif, mode SpreadTetap akan bergerak lurus diagonal seperti Piercing.\n" +
    "Panah atas dan bawah tidak lagi dilengkungkan turun.\n" +
    "Landing TitikKaki dan raycast tanah juga dilewati untuk mode ini."
)]
    public bool spreadTetapLurusSepertiPiercing = true;

    [Header("Pola: Kipas Ringan")]
    [Tooltip(
        "Sudut sebar untuk pola KipasRingan.\n\n" +
        "Naikkan kalau ingin panah atas-bawah lebih menyebar.\n" +
        "Turunkan kalau ingin tetap fokus ke depan.\n\n" +
        "Saran awal: 8 - 10."
    )]
    public float sudutKipas = 9f;

    [Tooltip(
        "Jarak tinggi awal antar panah pada pola KipasRingan.\n" +
        "Ini membuat panah tidak benar-benar keluar dari titik yang sama semua.\n\n" +
        "Saran awal: 0.04 - 0.08."
    )]
    public float offsetSpawnKipas = 0.06f;

    [Header("Arrow Setup")]
    [Tooltip(
        "Kecepatan maju panah.\n\n" +
        "Saran awal: 16 - 20."
    )]
    public float kecepatanPanah = 18f;

    [Tooltip(
        "Durasi utama panah terbang sebelum masuk fase cari tanah.\n\n" +
        "Saran awal: 0.55 - 0.75."
    )]
    public float durasiTerbang = 0.65f;

    [Tooltip(
        "Kalau aktif, rotasi panah mengikuti arah gerak.\n" +
        "Saran: aktif."
    )]
    public bool rotasiIkutArah = true;

    [Tooltip(
        "Kalau aktif, TrailRenderer dibersihkan saat panah muncul.\n" +
        "Ini mencegah trail lama muncul sebagai garis aneh."
    )]
    public bool resetTrailSaatSpawn = true;

    [Header("Arrow Feel")]
    [Tooltip(
        "Panah mulai turun setelah berapa detik.\n\n" +
        "Saran awal mengikuti Quick Shot: 0.09."
    )]
    public float mulaiTurun = 0.09f;

    [Tooltip(
        "Dorongan naik kecil di awal.\n\n" +
        "Saran awal mengikuti Quick Shot: 0.4."
    )]
    public float angkatSedikit = 0.4f;

    [Tooltip(
        "Kekuatan lengkung turun.\n\n" +
        "Saran awal mengikuti Quick Shot: 7."
    )]
    public float lengkungTurun = 7f;

    [Header("Landing / Ground Check")]
    [Tooltip(
        "Layer tanah. Dipakai jika tidak memakai TitikKaki sebagai patokan jatuh."
    )]
    public LayerMask lapisanTanah;

    [Tooltip(
        "Panah harus maju minimal sejauh ini sebelum boleh dianggap jatuh.\n\n" +
        "Saran awal: 0.5."
    )]
    public float jarakMinimumSebelumBolehJatuh = 0.5f;

    [Tooltip(
        "Panah baru boleh dianggap jatuh jika kecepatan Y sudah lebih kecil dari nilai ini.\n\n" +
        "Saran awal: -0.1."
    )]
    public float batasKecepatanTurun = -0.1f;

    [Tooltip("Tinggi awal raycast tanah dari posisi panah.")]
    public float tinggiRaycastTanah = 1f;

    [Tooltip("Jarak raycast ke bawah untuk mencari tanah.")]
    public float jarakRaycastTanah = 1.5f;

    [Tooltip(
        "Jarak panah ke tanah yang dianggap sudah cukup dekat untuk mendarat.\n\n" +
        "Saran awal: 0.06."
    )]
    public float jarakDekatTanahUntukMendarat = 0.06f;

    [Tooltip(
        "Normal minimum permukaan agar dianggap tanah.\n" +
        "Saran awal: 0.6."
    )]
    [Range(0f, 1f)]
    public float minimumNormalTanah = 0.6f;

    [Tooltip(
        "Offset kecil saat panah sudah nancep / jatuh.\n\n" +
        "Saran awal: X = 0, Y = 0.04."
    )]
    public Vector2 offsetSaatJatuh = new Vector2(0f, 0.04f);

    [Tooltip(
        "Sudut panah saat sudah jatuh / nancep.\n\n" +
        "Saran awal untuk panah jatuh: 8 - 15."
    )]
    public float sudutSaatJatuh = 12f;

    [Tooltip(
        "Waktu tambahan untuk mencari titik jatuh setelah durasi terbang utama selesai.\n\n" +
        "Saran awal: 0.4 - 0.6."
    )]
    public float batasWaktuCariTanah = 0.45f;

    [Tooltip(
        "Kecepatan horizontal saat fase turun akhir.\n\n" +
        "Saran awal: 0.8."
    )]
    public float pengaliKecepatanSaatTurunAkhir = 0.8f;

    [Header("Landing Berdasarkan Titik Kaki")]
    [Tooltip(
        "Kalau aktif, panah memakai TitikKaki player sebagai garis tanah visual."
    )]
    public bool pakaiTitikKakiUntukJatuh = true;

    [Tooltip(
        "Titik kaki player.\n" +
        "Buat empty object bernama TitikKaki di bawah Player_W2, lalu letakkan di sekitar telapak kaki."
    )]
    public Transform titikKaki;

    [Tooltip(
        "Offset Y dari TitikKaki untuk menentukan garis jatuh panah.\n\n" +
        "Saran awal: 0.02 - 0.06."
    )]
    public float offsetYJatuhDariTitikKaki = 0.04f;

    [Header("Hilang Setelah Jatuh")]
    [Tooltip(
        "Berapa lama panah tetap terlihat setelah jatuh / nancep.\n\n" +
        "Saran awal: 0.6 - 0.9."
    )]
    public float waktuTerlihatSetelahJatuh = 0.8f;

    [Header("Hit Effect")]
    [Tooltip(
        "Damage tiap panah.\n\n" +
        "Karena skill ini menembakkan beberapa panah sekaligus,\n" +
        "nilai damage per panah sebaiknya tidak terlalu tinggi."
    )]
    public float damageSpread = 1f;

    [Tooltip(
        "Knockback tiap panah.\n\n" +
        "Naikkan kalau panah harus terasa mendorong musuh.\n" +
        "Turunkan kalau skill ini fokus ke area dan damage."
    )]
    public float dorongMundur = 6f;

    [Tooltip(
        "Stun / lumpuh singkat tiap panah.\n\n" +
        "0 = tidak ada stun.\n" +
        "0.05 - 0.12 = ringan.\n" +
        "0.15 ke atas = mulai terasa."
    )]
    public float lumpuhSingkat = 0.12f;

    [Tooltip(
        "Kalau aktif, panah bisa menembus musuh.\n\n" +
        "Saran awal untuk Spread Arrow: matikan dulu."
    )]
    public bool panahMenembus = false;

    [Header("Timing")]
    [Tooltip(
        "Cooldown skill setelah dipakai.\n\n" +
        "Saran awal: 0.8."
    )]
    public float jedaSkill = 0.8f;

    [Tooltip(
        "Lama gerak player dikunci setelah panah ditembakkan.\n\n" +
        "Kalau Anda nanti sudah membuat event akhir animasi,\n" +
        "field ini tetap berguna sebagai cadangan otomatis.\n\n" +
        "Saran awal: 0.15 - 0.22."
    )]
    public float lockGerakSetelahTembak = 0.18f;

    [Header("Energy")]
    [Tooltip(
        "Biaya energi Skill 3.\n\n" +
        "Naikkan kalau skill terlalu kuat dan terlalu murah.\n" +
        "Turunkan kalau skill terlalu mahal."
    )]
    [SerializeField, Min(0f)]
    private float biayaEnergi = 18f;

    public float EnergyCost => biayaEnergi;
    public bool PayEnergyInSkillBase => false;

    private CharacterBase pemilikEnergi;
    private Rigidbody2D rbPemain;

    private bool sedangCooldown;
    private bool sedangCast;
    private bool panahSudahDitembakkan;
    private bool castSudahSelesai;
    private Coroutine routineCast;
    private Coroutine routineBackDash;

    private void Awake()
    {
        if (pemain == null)
            pemain = GetComponentInParent<Player>();

        if (pemilikEnergi == null)
            pemilikEnergi = GetComponentInParent<CharacterBase>();

        if (animasiPemain == null && pemain != null)
            animasiPemain = pemain.GetComponentInChildren<PlayerAnimation>(true);

        if (animasiPemain == null)
            animasiPemain = GetComponentInParent<PlayerAnimation>();

        if (facingSprite == null && pemain != null)
            facingSprite = pemain.GetComponentInChildren<SpriteRenderer>(true);

        if (animatorVisual == null && animasiPemain != null)
            animatorVisual = animasiPemain.animator;

        if (animatorVisual == null && pemain != null)
            animatorVisual = pemain.GetComponentInChildren<Animator>(true);

        if (pemain != null)
            rbPemain = pemain.GetComponent<Rigidbody2D>();
    }

    public void TriggerSkill(int slotID)
    {
        if (sedangCooldown || sedangCast)
            return;

        if (titikTembak == null || prefabPanah == null)
        {
            Debug.LogWarning("[Bow_SpreadArrow] Titik Tembak atau Prefab Panah belum diisi.");
            return;
        }

        if (pemain != null && pemain.lockMovement)
            return;

        if (!CobaKurangiEnergi())
            return;

        routineCast = StartCoroutine(RoutineCastSpreadArrow());
    }

    private IEnumerator RoutineCastSpreadArrow()
    {
        sedangCooldown = true;
        sedangCast = true;
        panahSudahDitembakkan = false;
        castSudahSelesai = false;

        if (pemain != null)
        {
            pemain.lockMovement = true;
            HentikanGerakPemain();
        }

        if (animasiPemain != null)
            animasiPemain.PlaySpreadArrow();
        else if (animatorVisual != null && !string.IsNullOrEmpty(triggerAnimasi))
            animatorVisual.SetTrigger(triggerAnimasi);

        float waktuMulaiCast = Time.time;

        if (!pakaiAnimationEventUntukTembak)
        {
            if (delaySebelumTembak > 0f)
                yield return new WaitForSeconds(delaySebelumTembak);

            ReleaseSpreadArrow();
        }
        else
        {
            while (!panahSudahDitembakkan && Time.time - waktuMulaiCast < batasMaksimalMenungguRelease)
                yield return null;

            if (!panahSudahDitembakkan)
                ReleaseSpreadArrow();
        }

        float waktuMulaiSelesai = Time.time;

        while (!castSudahSelesai && Time.time - waktuMulaiSelesai < durasiMaksimalCast)
            yield return null;

        if (!castSudahSelesai)
            SelesaikanCastSpreadArrow();

        float totalWaktuCooldownYangSudahBerjalan = Time.time - waktuMulaiCast;
        float sisaCooldown = Mathf.Max(0f, jedaSkill - totalWaktuCooldownYangSudahBerjalan);

        if (sisaCooldown > 0f)
            yield return new WaitForSeconds(sisaCooldown);

        sedangCooldown = false;
        routineCast = null;
    }

    public void StartBackDash()
    {
        if (!sedangCast || !gunakanBackDash)
            return;

        if (routineBackDash != null)
            return;

        routineBackDash = StartCoroutine(RoutineBackDash());
    }

    private IEnumerator RoutineBackDash()
    {
        if (pemain == null)
        {
            routineBackDash = null;
            yield break;
        }

        Transform target = pemain.transform;
        float arahHadap = AmbilArahHadap();
        Vector3 posisiAwal = target.position;
        Vector3 posisiTarget = posisiAwal + Vector3.left * arahHadap * jarakBackDash;

        if (hentikanVelocitySaatBackDash)
            HentikanGerakPemain();

        float durasi = Mathf.Max(0.01f, durasiBackDash);
        float timer = 0f;

        while (timer < durasi)
        {
            if (target == null)
                yield break;

            float t = timer / durasi;
            t = Mathf.SmoothStep(0f, 1f, t);
            Vector3 posisiBaru = Vector3.Lerp(posisiAwal, posisiTarget, t);

            if (rbPemain != null && rbPemain.simulated)
                rbPemain.MovePosition(posisiBaru);
            else
                target.position = posisiBaru;

            timer += Time.deltaTime;
            yield return null;
        }

        if (target != null)
        {
            if (rbPemain != null && rbPemain.simulated)
                rbPemain.MovePosition(posisiTarget);
            else
                target.position = posisiTarget;
        }

        routineBackDash = null;
    }

    public void ReleaseSpreadArrow()
    {
        if (!sedangCast || panahSudahDitembakkan)
            return;

        panahSudahDitembakkan = true;
        TembakkanFormasiPanah();

        StartCoroutine(RoutineUnlockSetelahTembak());
    }

    public void EndSpreadArrowRecovery()
    {
        if (!sedangCast)
            return;

        SelesaikanCastSpreadArrow();
    }

    private IEnumerator RoutineUnlockSetelahTembak()
    {
        if (lockGerakSetelahTembak > 0f)
            yield return new WaitForSeconds(lockGerakSetelahTembak);

        if (!castSudahSelesai)
            SelesaikanCastSpreadArrow();
    }

    private void SelesaikanCastSpreadArrow()
    {
        if (castSudahSelesai)
            return;

        castSudahSelesai = true;
        sedangCast = false;

        if (pemain != null)
            pemain.lockMovement = false;
    }

    private void TembakkanFormasiPanah()
    {
        List<DataPanah> formasi = BangunFormasiPanah();
        float arah = AmbilArahHadap();

        for (int i = 0; i < formasi.Count; i++)
            SpawnPanah(formasi[i], arah);
    }

    private List<DataPanah> BangunFormasiPanah()
    {
        List<DataPanah> hasil = new List<DataPanah>();
        int total = Mathf.Clamp(jumlahPanah, 3, 5);

        switch (polaTembak)
        {
            case PolaTembak.SpreadTetap:
                {
                    // Mode ini dibuat sesuai sketch:
                    // semua panah keluar dari titik yang hampir sama,
                    // tetapi lintasannya lurus diagonal seperti Piercing.
                    // Tidak memakai pola kipas besar, tidak memakai elevasi bertingkat.

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
                }

            case PolaTembak.LurusBertingkat:
                {
                    for (int i = 0; i < total; i++)
                    {
                        float y = (i - (total - 1) / 2f) * jarakVertikalLurus;
                        hasil.Add(new DataPanah(y, 0f, 0f));
                    }
                    break;
                }

            case PolaTembak.KipasRingan:
                {
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
        }

        return hasil;
    }

    private void SpawnPanah(DataPanah data, float arah)
    {
        Vector3 posisiSpawn = titikTembak.position + new Vector3(0f, data.offsetY, 0f);
        GameObject panahObj = Instantiate(prefabPanah, posisiSpawn, Quaternion.identity);

        if (panahObj == null)
            return;

        Rigidbody2D rb = panahObj.GetComponent<Rigidbody2D>();
        ArrowDamage arrowDamage = panahObj.GetComponent<ArrowDamage>();

        if (rb == null)
        {
            Debug.LogWarning("[Bow_SpreadArrow] Prefab panah tidak punya Rigidbody2D.");
            Destroy(panahObj);
            return;
        }

        rb.gravityScale = 0f;
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;

        TerapkanArahAwalPanah(panahObj, arah);

        if (resetTrailSaatSpawn)
            ResetTrailPanah(panahObj);

        if (arrowDamage != null)
        {
            arrowDamage.owner = pemilikEnergi;
            arrowDamage.SetStats(damageSpread, dorongMundur, lumpuhSingkat, panahMenembus, false);
        }

        StartCoroutine(RoutinePanah(rb, panahObj, arah, data.sudut, data.bonusVy));
    }

    private IEnumerator RoutinePanah(Rigidbody2D rb, GameObject panahObj, float arah, float sudutAwal, float bonusVy)
{
    if (rb == null || panahObj == null)
        yield break;

    bool modeSpreadLurus =
        polaTembak == PolaTembak.SpreadTetap &&
        spreadTetapLurusSepertiPiercing;

    if (modeSpreadLurus)
    {
        yield return RoutinePanahSpreadLurusSepertiPiercing(rb, panahObj, arah, bonusVy);
        yield break;
    }

    float timer = 0f;
    float posisiAwalX = rb.position.x;
    bool sudahMendarat = false;

    float waktuMulaiTurun = Mathf.Clamp(mulaiTurun, 0f, Mathf.Max(0.01f, durasiTerbang - 0.01f));

    Vector2 arahAwal = HitungArahDariSudut(arah, sudutAwal);
    float vx = arahAwal.x * kecepatanPanah;
    float vy = arahAwal.y * kecepatanPanah + angkatSedikit + bonusVy;

    while (timer < durasiTerbang && !sudahMendarat)
    {
        if (rb == null || panahObj == null)
            yield break;

        if (timer >= waktuMulaiTurun)
            vy -= lengkungTurun * Time.deltaTime;

        rb.velocity = new Vector2(vx, vy);

        if (rotasiIkutArah)
            TerapkanRotasiPanah(rb);

        if (BolehCekMendarat(rb, posisiAwalX))
        {
            if (CobaMendarat(rb, panahObj, arah))
            {
                sudahMendarat = true;
                break;
            }
        }

        timer += Time.deltaTime;
        yield return null;
    }

    float timerCariTanah = 0f;

    while (!sudahMendarat && timerCariTanah < batasWaktuCariTanah)
    {
        if (rb == null || panahObj == null)
            yield break;

        vx = arah * kecepatanPanah * pengaliKecepatanSaatTurunAkhir;
        vy -= (lengkungTurun * 2f) * Time.deltaTime;

        rb.velocity = new Vector2(vx, vy);

        if (rotasiIkutArah)
            TerapkanRotasiPanah(rb);

        if (BolehCekMendarat(rb, posisiAwalX))
        {
            if (CobaMendarat(rb, panahObj, arah))
            {
                sudahMendarat = true;
                break;
            }
        }

        timerCariTanah += Time.deltaTime;
        yield return null;
    }

    if (!sudahMendarat)
    {
        if (panahObj != null)
            Destroy(panahObj);

        yield break;
    }

    yield return new WaitForSeconds(waktuTerlihatSetelahJatuh);

    if (panahObj != null)
        Destroy(panahObj);
}
    private IEnumerator RoutinePanahSpreadLurusSepertiPiercing(
    Rigidbody2D rb,
    GameObject panahObj,
    float arah,
    float bonusVy
)
    {
        float timer = 0f;

        // Untuk mode ini, angkatSedikit, mulaiTurun, lengkungTurun, TitikKaki,
        // dan raycast tanah sengaja tidak dipakai.
        // Tujuannya agar panah benar-benar lurus seperti Piercing.
        Vector2 velocityLurus = new Vector2(
            arah * kecepatanPanah,
            bonusVy
        );

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

        if (panahObj != null)
            Destroy(panahObj);
    }

    private Vector2 HitungArahDariSudut(float arahHadap, float sudut)
    {
        Vector2 dir = Quaternion.Euler(0f, 0f, sudut) * Vector2.right;

        if (arahHadap < 0f)
            dir = new Vector2(-dir.x, dir.y);

        return dir.normalized;
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

    private bool CobaMendarat(Rigidbody2D rb, GameObject panahObj, float arah)
    {
        if (pakaiTitikKakiUntukJatuh)
            return CobaMendaratDiTitikKaki(rb, panahObj, arah);

        return CobaMendaratDiTanah(rb, panahObj, arah);
    }

    private bool CobaMendaratDiTitikKaki(Rigidbody2D rb, GameObject panahObj, float arah)
    {
        if (rb == null || panahObj == null)
            return false;

        float yTanahVisual;

        if (titikKaki != null)
            yTanahVisual = titikKaki.position.y;
        else if (pemain != null)
            yTanahVisual = pemain.transform.position.y;
        else
            return false;

        float yTargetJatuh = yTanahVisual + offsetYJatuhDariTitikKaki;

        if (rb.position.y > yTargetJatuh)
            return false;

        PaksaPanahJatuh(rb, panahObj, arah, yTargetJatuh);
        return true;
    }

    private bool CobaMendaratDiTanah(Rigidbody2D rb, GameObject panahObj, float arah)
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

        PaksaPanahJatuh(rb, panahObj, arah, hit.point.y);
        return true;
    }

    private void PaksaPanahJatuh(Rigidbody2D rb, GameObject panahObj, float arah, float yTanah)
    {
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.simulated = false;

        Collider2D col = panahObj.GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        MatikanTrailPanah(panahObj);

        Vector3 posisiAkhir = panahObj.transform.position;
        posisiAkhir.x += offsetSaatJatuh.x * arah;
        posisiAkhir.y = yTanah + offsetSaatJatuh.y;

        float sudutAkhir = arah > 0f ? -sudutSaatJatuh : 180f + sudutSaatJatuh;

        panahObj.transform.position = posisiAkhir;
        panahObj.transform.rotation = Quaternion.Euler(0f, 0f, sudutAkhir);
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

    private bool CobaKurangiEnergi()
    {
        if (biayaEnergi <= 0f)
            return true;

        if (pemilikEnergi == null)
            pemilikEnergi = GetComponentInParent<CharacterBase>();

        if (pemilikEnergi == null)
        {
            Debug.LogWarning("[Bow_SpreadArrow] CharacterBase / pemilik energi tidak ditemukan.");
            return false;
        }

        if (!pemilikEnergi.TrySpendEnergy(biayaEnergi))
        {
            Debug.LogWarning("[Bow_SpreadArrow] Energi tidak cukup.");
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

    private void OnDisable()
    {
        if (pemain != null)
            pemain.lockMovement = false;

        sedangCast = false;
        sedangCooldown = false;
        panahSudahDitembakkan = false;
        castSudahSelesai = false;
        routineCast = null;
        routineBackDash = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (titikTembak == null)
            return;

        float arah = AmbilArahHadap();

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(titikTembak.position, 0.05f);

        List<DataPanah> preview = BangunFormasiPanah();

        for (int i = 0; i < preview.Count; i++)
        {
            Vector3 spawn = titikTembak.position + new Vector3(0f, preview[i].offsetY, 0f);
            Vector2 dir = HitungArahDariSudut(arah, preview[i].sudut);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(spawn, 0.035f);
            Gizmos.DrawLine(spawn, spawn + (Vector3)(dir.normalized * 0.65f));
        }

        if (titikKaki != null)
        {
            Gizmos.color = Color.green;
            Vector3 kaki = titikKaki.position + Vector3.up * offsetYJatuhDariTitikKaki;
            Gizmos.DrawLine(kaki + Vector3.left * 0.7f, kaki + Vector3.right * 0.7f);
        }

        if (gunakanBackDash && pemain != null)
        {
            Gizmos.color = Color.red;
            Vector3 start = pemain.transform.position;
            Vector3 end = start + Vector3.left * AmbilArahHadap() * jarakBackDash;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(end, 0.06f);
        }
    }
#endif
}