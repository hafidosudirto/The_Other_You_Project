using TMPro;
using UnityEngine;

/// <summary>
/// Menampilkan nomor stage saat ini di layar selama bermain.
///
/// Script ini mencari <see cref="StageManager"/> di scene secara otomatis
/// dan menampilkan teks "Stage X" menggunakan <see cref="TextMeshProUGUI"/>.
/// Teks otomatis di-update saat stage berubah, dengan animasi pulse singkat
/// agar pemain tahu bahwa stage baru telah dimulai.
///
/// Cara pakai:
/// 1. Buat UI Text (TextMeshPro) pada Canvas gameplay.
/// 2. Tambahkan script ini pada GameObject yang sama, atau pasang referensi
///    TextMeshProUGUI melalui Inspector.
/// 3. (Opsional) Atur prefix, ukuran font, dan warna melalui Inspector.
///
/// Script ini TIDAK menggunakan DontDestroyOnLoad dan aman untuk Play Again.
/// </summary>
public class CurrentStageUI : MonoBehaviour
{
    [Header("Referensi UI")]
    [Tooltip("TextMeshProUGUI untuk menampilkan nomor stage. Jika kosong, otomatis dicari dari komponen pada GameObject ini.")]
    [SerializeField] private TextMeshProUGUI stageText;

    [Header("Format")]
    [Tooltip("Prefix sebelum nomor stage. Contoh: 'Stage ' → 'Stage 1'.")]
    [SerializeField] private string stagePrefix = "Stage ";

    [Header("Animasi Pulse saat Stage Berubah")]
    [Tooltip("Jika true, teks akan membesar sesaat saat stage berubah.")]
    [SerializeField] private bool enablePulseAnimation = true;

    [Tooltip("Skala maksimal saat pulse (relatif terhadap skala normal).")]
    [SerializeField] private float pulseScale = 1.3f;

    [Tooltip("Durasi animasi pulse (detik).")]
    [SerializeField] private float pulseDuration = 0.4f;

    [Header("Auto Find")]
    [Tooltip("Jika true, StageManager akan dicari otomatis di scene saat Start.")]
    [SerializeField] private bool autoFindStageManager = true;

    private StageManager stageManager;
    private int lastDisplayedStageNumber = -1;

    // Pulse animation state
    private float pulseTimer;
    private bool isPulsing;
    private Vector3 originalScale;

    private void Awake()
    {
        if (stageText == null)
        {
            stageText = GetComponent<TextMeshProUGUI>();
        }

        if (stageText == null)
        {
            stageText = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (stageText == null)
        {
            Debug.LogWarning(
                "[CURRENT STAGE UI] TextMeshProUGUI belum di-assign dan tidak ditemukan " +
                "pada GameObject ini. Isi referensi Stage Text di Inspector."
            );
        }
    }

    private void Start()
    {
        if (autoFindStageManager && stageManager == null)
        {
            stageManager = FindObjectOfType<StageManager>();
        }

        if (stageManager == null)
        {
            Debug.LogWarning(
                "[CURRENT STAGE UI] StageManager tidak ditemukan di scene. " +
                "Pastikan StageManager ada di scene gameplay."
            );
        }

        if (stageText != null)
        {
            originalScale = stageText.transform.localScale;
        }

        // Tampilkan stage awal langsung
        RefreshStageDisplay();
    }

    private void Update()
    {
        RefreshStageDisplay();
        UpdatePulseAnimation();
    }

    /// <summary>
    /// Perbarui teks stage jika nomor stage berubah.
    /// </summary>
    private void RefreshStageDisplay()
    {
        if (stageText == null)
            return;

        if (stageManager == null)
        {
            if (autoFindStageManager)
            {
                stageManager = FindObjectOfType<StageManager>();
            }

            if (stageManager == null)
                return;
        }

        int currentStageNumber = stageManager.GetDisplayedStageNumber();

        if (currentStageNumber != lastDisplayedStageNumber)
        {
            lastDisplayedStageNumber = currentStageNumber;
            stageText.text = stagePrefix + currentStageNumber;

            // Trigger pulse animation saat stage berubah (kecuali inisialisasi pertama)
            if (enablePulseAnimation && lastDisplayedStageNumber > 0)
            {
                StartPulse();
            }

            Debug.Log("[CURRENT STAGE UI] Stage ditampilkan: " + stageText.text);
        }
    }

    /// <summary>
    /// Mulai animasi pulse (membesar sesaat lalu kembali ke ukuran normal).
    /// </summary>
    private void StartPulse()
    {
        if (stageText == null)
            return;

        isPulsing = true;
        pulseTimer = 0f;
    }

    /// <summary>
    /// Update animasi pulse setiap frame.
    /// </summary>
    private void UpdatePulseAnimation()
    {
        if (!isPulsing || stageText == null)
            return;

        pulseTimer += Time.unscaledDeltaTime;

        float progress = Mathf.Clamp01(pulseTimer / Mathf.Max(0.01f, pulseDuration));

        // Kurva: naik ke pulseScale di tengah, kembali ke 1 di akhir
        float scaleMultiplier;

        if (progress < 0.5f)
        {
            // Naik dari 1 ke pulseScale
            float halfProgress = progress / 0.5f;
            scaleMultiplier = Mathf.Lerp(1f, pulseScale, halfProgress);
        }
        else
        {
            // Turun dari pulseScale ke 1
            float halfProgress = (progress - 0.5f) / 0.5f;
            scaleMultiplier = Mathf.Lerp(pulseScale, 1f, halfProgress);
        }

        stageText.transform.localScale = originalScale * scaleMultiplier;

        if (progress >= 1f)
        {
            isPulsing = false;
            stageText.transform.localScale = originalScale;
        }
    }

    // =========================================================
    // API PUBLIK
    // =========================================================

    /// <summary>
    /// Set referensi StageManager secara manual (jika autoFindStageManager = false).
    /// </summary>
    public void SetStageManager(StageManager manager)
    {
        stageManager = manager;
        lastDisplayedStageNumber = -1; // Force refresh
        RefreshStageDisplay();
    }

    /// <summary>
    /// Paksa refresh tampilan stage (misalnya setelah scene reload).
    /// </summary>
    public void ForceRefresh()
    {
        lastDisplayedStageNumber = -1;
        RefreshStageDisplay();
    }
}
