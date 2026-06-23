using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class EnergyBarUI : MonoBehaviour
{
    public enum EnergyTextMode
    {
        CurrentAndMax,
        CurrentOnly,
        Percent,
        Hidden
    }

    [Header("Target")]
    [Tooltip("Target CharacterBase pemain. Boleh kosong jika Auto Find Player aktif.")]
    [SerializeField] private CharacterBase target;

    [Header("Auto Assign Player")]
    [Tooltip("Jika aktif, UI akan mencari player aktif berdasarkan tag Player atau komponen Player.")]
    [SerializeField] private bool autoFindPlayer = true;

    [Tooltip("Jeda pencarian ulang player ketika target belum ditemukan atau player aktif berganti prefab.")]
    [Min(0.05f)]
    [SerializeField] private float autoFindInterval = 0.25f;

    [Header("UI Images")]
    [Tooltip("Fill utama energy/stamina. Image Type sebaiknya Filled, Fill Method Horizontal.")]
    [SerializeField] private Image mainFillImage;

    [Tooltip("Fill tertinggal untuk efek lag saat energy berkurang. Boleh kosong.")]
    [SerializeField] private Image lagFillImage;

    [Tooltip("Overlay gelap ketika energy kosong atau rendah. Boleh kosong.")]
    [SerializeField] private Image lowEnergyOverlay;

    [Header("UI Text")]
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private EnergyTextMode textMode = EnergyTextMode.CurrentAndMax;

    [Header("Display")]
    [Tooltip("Jika aktif, Image fill otomatis disetel menjadi Filled Horizontal dari kiri.")]
    [SerializeField] private bool autoConfigureFilledImages = true;

    [Tooltip("Jika aktif, UI tetap diperbarui setiap frame sebagai pengaman saat event energy tidak terpanggil.")]
    [SerializeField] private bool refreshEveryFrameAsFallback = true;

    [Tooltip("Jika aktif, objek UI disembunyikan saat target belum ditemukan.")]
    [SerializeField] private bool hideWhenTargetMissing = false;

    [Header("Low Energy Visual")]
    [Tooltip("Aktifkan untuk memberi redup/overlay saat energy hampir habis.")]
    [SerializeField] private bool useLowEnergyOverlay = false;

    [Tooltip("Batas energy rendah dalam bentuk rasio 0 sampai 1.")]
    [Range(0f, 1f)]
    [SerializeField] private float lowEnergyThreshold = 0.2f;

    [Tooltip("Alpha overlay saat energy rendah. Dipakai hanya jika Low Energy Overlay terisi.")]
    [Range(0f, 1f)]
    [SerializeField] private float lowEnergyOverlayAlpha = 0.45f;

    [Header("Lag Settings")]
    [Tooltip("Kecepatan lag fill mengejar fill utama saat energy berkurang.")]
    [Min(0f)]
    [SerializeField] private float lagCatchUpSpeed = 2f;

    [Tooltip("Jeda sebelum lag fill mengejar fill utama saat energy berkurang.")]
    [Min(0f)]
    [SerializeField] private float lagDelay = 0.2f;

    private Coroutine lagRoutine;
    private float latestTargetFill = 1f;
    private float lastRenderedFill = -1f;
    private float nextAutoFindTime;
    private bool delayAlreadyPassed;
    private bool hasWarnedMissingPlayerTag;
    private bool isSubscribedToTarget;

    private void Reset()
    {
        AutoAssignReferences();
        ConfigureFillImages();
    }

    private void Awake()
    {
        AutoAssignReferences();
        ConfigureFillImages();
        TryAutoAssignTarget();
    }

    private void OnEnable()
    {
        PlayerPrefabSwitchManager.OnActiveWeaponChanged += OnActiveWeaponChanged;

        AutoAssignReferences();
        ConfigureFillImages();
        SubscribeToTarget();
        ForceRefreshImmediate();
    }

    private void OnDisable()
    {
        PlayerPrefabSwitchManager.OnActiveWeaponChanged -= OnActiveWeaponChanged;

        UnsubscribeFromTarget();
        StopLagRoutine();
        delayAlreadyPassed = false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssignReferences();
        ConfigureFillImages();
    }
#endif

    private void Update()
    {
        AutoFindTargetTick();

        if (refreshEveryFrameAsFallback)
            RefreshFromTarget(false);
    }

    public void SetTarget(Transform playerTransform)
    {
        if (playerTransform == null)
        {
            SetTarget((CharacterBase)null);
            return;
        }

        CharacterBase newTarget = GetCharacterBaseFromTransform(playerTransform);

        if (newTarget == null)
        {
            Debug.LogWarning("[ENERGY BAR UI] CharacterBase tidak ditemukan pada playerTransform.");
            return;
        }

        SetTarget(newTarget);
    }

    public void SetTarget(CharacterBase newTarget)
    {
        if (target == newTarget)
        {
            ForceRefreshImmediate();
            return;
        }

        UnsubscribeFromTarget();
        target = newTarget;
        SubscribeToTarget();
        ForceRefreshImmediate();
    }

    public void ForceRefreshImmediate()
    {
        StopLagRoutine();
        delayAlreadyPassed = false;
        RefreshFromTarget(true);
    }

    private void OnActiveWeaponChanged(WeaponType weapon)
    {
        // Saat prefab player berganti, CharacterBase lama bisa sudah nonaktif atau dihancurkan.
        if (autoFindPlayer)
            TryAutoAssignTarget(true);

        ForceRefreshImmediate();
    }

    private void AutoFindTargetTick()
    {
        if (!autoFindPlayer)
            return;

        if (Time.unscaledTime < nextAutoFindTime)
            return;

        nextAutoFindTime = Time.unscaledTime + autoFindInterval;

        if (target == null || !target.gameObject.activeInHierarchy)
            TryAutoAssignTarget(true);
    }

    private bool TryAutoAssignTarget(bool force = false)
    {
        if (!autoFindPlayer && !force)
            return false;

        CharacterBase foundTarget = null;

        try
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                foundTarget = GetCharacterBaseFromTransform(playerObject.transform);
        }
        catch (UnityException)
        {
            if (!hasWarnedMissingPlayerTag)
            {
                Debug.LogWarning("[ENERGY BAR UI] Tag Player belum dibuat di Project Settings > Tags and Layers.");
                hasWarnedMissingPlayerTag = true;
            }
        }

        if (foundTarget == null)
        {
            Player player = FindObjectOfType<Player>();

            if (player != null)
                foundTarget = player.GetComponent<CharacterBase>();
        }

        if (foundTarget == null)
        {
            ApplyMissingTargetVisibility();
            return false;
        }

        SetTarget(foundTarget);
        return true;
    }

    private CharacterBase GetCharacterBaseFromTransform(Transform sourceTransform)
    {
        if (sourceTransform == null)
            return null;

        CharacterBase characterBase = sourceTransform.GetComponent<CharacterBase>();

        if (characterBase == null)
            characterBase = sourceTransform.GetComponentInChildren<CharacterBase>(true);

        if (characterBase == null)
            characterBase = sourceTransform.GetComponentInParent<CharacterBase>();

        return characterBase;
    }

    private void SubscribeToTarget()
    {
        if (!isActiveAndEnabled || target == null || isSubscribedToTarget)
            return;

        target.OnEnergyChanged += OnEnergyChanged;
        isSubscribedToTarget = true;
    }

    private void UnsubscribeFromTarget()
    {
        if (target == null)
        {
            isSubscribedToTarget = false;
            return;
        }

        if (!isSubscribedToTarget)
            return;

        target.OnEnergyChanged -= OnEnergyChanged;
        isSubscribedToTarget = false;
    }

    private void OnEnergyChanged()
    {
        RefreshFromTarget(false);
    }

    private void RefreshFromTarget(bool immediate)
    {
        if (target == null)
        {
            ApplyMissingTargetVisibility();
            return;
        }

        SetVisible(true);

        float targetFill = Mathf.Clamp01(target.EnergyNormalized);

        if (!immediate && Mathf.Approximately(targetFill, lastRenderedFill))
        {
            UpdateEnergyText();
            UpdateLowEnergyOverlay(targetFill);
            return;
        }

        latestTargetFill = targetFill;
        lastRenderedFill = targetFill;

        if (mainFillImage != null)
            mainFillImage.fillAmount = targetFill;

        if (lagFillImage != null)
        {
            if (immediate)
            {
                lagFillImage.fillAmount = targetFill;
            }
            else if (lagFillImage.fillAmount <= targetFill)
            {
                lagFillImage.fillAmount = targetFill;
                delayAlreadyPassed = false;
                StopLagRoutine();
            }
            else if (lagRoutine == null)
            {
                lagRoutine = StartCoroutine(LagChaseLoop());
            }
        }

        UpdateEnergyText();
        UpdateLowEnergyOverlay(targetFill);
    }

    private IEnumerator LagChaseLoop()
    {
        if (!delayAlreadyPassed && lagDelay > 0f)
        {
            yield return new WaitForSeconds(lagDelay);
            delayAlreadyPassed = true;
        }

        while (lagFillImage != null)
        {
            if (lagFillImage.fillAmount <= latestTargetFill)
            {
                lagFillImage.fillAmount = latestTargetFill;
                break;
            }

            lagFillImage.fillAmount = Mathf.MoveTowards(
                lagFillImage.fillAmount,
                latestTargetFill,
                lagCatchUpSpeed * Time.deltaTime
            );

            yield return null;
        }

        lagRoutine = null;
        delayAlreadyPassed = false;
    }

    private void UpdateEnergyText()
    {
        if (energyText == null)
            return;

        if (target == null || textMode == EnergyTextMode.Hidden)
        {
            energyText.text = string.Empty;
            return;
        }

        int current = Mathf.RoundToInt(target.CurrentEnergy);
        int max = Mathf.RoundToInt(target.MaxEnergy);
        int percent = Mathf.RoundToInt(target.EnergyNormalized * 100f);

        switch (textMode)
        {
            case EnergyTextMode.CurrentOnly:
                energyText.text = current.ToString();
                break;

            case EnergyTextMode.Percent:
                energyText.text = percent + "%";
                break;

            case EnergyTextMode.CurrentAndMax:
            default:
                energyText.text = current + "/" + max;
                break;
        }
    }

    private void UpdateLowEnergyOverlay(float fill)
    {
        if (lowEnergyOverlay == null)
            return;

        bool showOverlay = useLowEnergyOverlay && fill > 0f && fill <= lowEnergyThreshold;
        lowEnergyOverlay.enabled = showOverlay;

        Color color = lowEnergyOverlay.color;
        color.a = showOverlay ? lowEnergyOverlayAlpha : 0f;
        lowEnergyOverlay.color = color;
    }

    private void ApplyMissingTargetVisibility()
    {
        if (hideWhenTargetMissing)
            SetVisible(false);

        if (energyText != null && textMode != EnergyTextMode.Hidden)
            energyText.text = string.Empty;
    }

    private void SetVisible(bool visible)
    {
        CanvasGroup group = GetComponent<CanvasGroup>();

        if (group != null)
        {
            group.alpha = visible ? 1f : 0f;
            group.interactable = visible;
            group.blocksRaycasts = false;
            return;
        }

        if (mainFillImage != null)
            mainFillImage.enabled = visible;

        if (lagFillImage != null)
            lagFillImage.enabled = visible;

        if (energyText != null)
            energyText.enabled = visible;
    }

    private void StopLagRoutine()
    {
        if (lagRoutine == null)
            return;

        StopCoroutine(lagRoutine);
        lagRoutine = null;
    }

    private void AutoAssignReferences()
    {
        if (mainFillImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            foreach (Image image in images)
            {
                if (image == null)
                    continue;

                string lowerName = image.name.ToLowerInvariant();

                if (lowerName.Contains("main") || lowerName.Contains("fill"))
                {
                    mainFillImage = image;
                    break;
                }
            }
        }

        if (energyText == null)
            energyText = GetComponentInChildren<TMP_Text>(true);
    }

    private void ConfigureFillImages()
    {
        if (!autoConfigureFilledImages)
            return;

        ConfigureHorizontalFill(mainFillImage);
        ConfigureHorizontalFill(lagFillImage);
    }

    private void ConfigureHorizontalFill(Image image)
    {
        if (image == null)
            return;

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.raycastTarget = false;
    }
}
