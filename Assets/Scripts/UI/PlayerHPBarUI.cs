using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHPBarUI : MonoBehaviour
{
    public enum HPTextMode
    {
        CurrentAndMax,
        CurrentOnly,
        Percent,
        Hidden
    }

    [Header("Target")]
    [Tooltip("Target CharacterBase pemain. Boleh kosong jika Auto Find Player aktif.")]
    [SerializeField] private CharacterBase target;

    [Header("UI Images")]
    [Tooltip("Fill utama HP. Image Type sebaiknya Filled, Fill Method Horizontal.")]
    [SerializeField] private Image fill;

    [Tooltip("Fill tertinggal untuk efek damage lag. Boleh kosong.")]
    [SerializeField] private Image lagFill;

    [Header("UI Text")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private HPTextMode textMode = HPTextMode.CurrentAndMax;

    [Header("Auto Assign Player")]
    [Tooltip("Jika aktif, UI akan mencari player aktif berdasarkan tag Player atau komponen Player.")]
    [SerializeField] private bool autoFindPlayer = true;

    [Tooltip("Jeda pencarian ulang player ketika target belum ditemukan atau player aktif berganti prefab.")]
    [Min(0.05f)]
    [SerializeField] private float autoFindInterval = 0.25f;

    [Header("Display")]
    [Tooltip("Jika aktif, Image fill otomatis disetel menjadi Filled Horizontal dari kiri.")]
    [SerializeField] private bool autoConfigureFilledImages = true;

    [Tooltip("Jika aktif, objek UI disembunyikan saat target belum ditemukan.")]
    [SerializeField] private bool hideWhenTargetMissing = false;

    [Tooltip("Jika aktif, bar tetap diperbarui setiap frame. Aman karena CharacterBase belum memiliki event HP.")]
    [SerializeField] private bool refreshEveryFrame = true;

    [Header("Damage Lag")]
    [Tooltip("Aktifkan efek lag fill saat HP berkurang.")]
    [SerializeField] private bool useDamageLag = true;

    [Tooltip("Jeda sebelum lag fill mengejar fill utama saat HP berkurang.")]
    [Min(0f)]
    [SerializeField] private float lagDelay = 0.15f;

    [Tooltip("Kecepatan lag fill mengejar fill utama saat HP berkurang.")]
    [Min(0f)]
    [SerializeField] private float lagCatchUpSpeed = 1.75f;

    private Coroutine lagRoutine;
    private float latestTargetFill = 1f;
    private float lastRenderedFill = -1f;
    private float nextAutoFindTime;
    private bool delayAlreadyPassed;
    private bool hasWarnedMissingPlayerTag;

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
        ForceRefreshImmediate();
    }

    private void OnDisable()
    {
        PlayerPrefabSwitchManager.OnActiveWeaponChanged -= OnActiveWeaponChanged;
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

        if (refreshEveryFrame)
            RefreshHPBar(false);
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
            Debug.LogWarning("[PLAYER HP BAR UI] CharacterBase tidak ditemukan pada playerTransform.");
            return;
        }

        SetTarget(newTarget);
    }

    public void SetTarget(CharacterBase newTarget)
    {
        target = newTarget;
        ForceRefreshImmediate();
    }

    public void ForceRefreshImmediate()
    {
        StopLagRoutine();
        delayAlreadyPassed = false;
        RefreshHPBar(true);
    }

    private void OnActiveWeaponChanged(WeaponType weapon)
    {
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
                Debug.LogWarning("[PLAYER HP BAR UI] Tag Player belum dibuat di Project Settings > Tags and Layers.");
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

    private void RefreshHPBar(bool immediate)
    {
        if (target == null)
        {
            ApplyMissingTargetVisibility();
            return;
        }

        SetVisible(true);

        float maxHp = Mathf.Max(1f, target.maxHP);
        float currentHp = Mathf.Clamp(target.currentHP, 0f, maxHp);
        float targetFill = Mathf.Clamp01(currentHp / maxHp);

        if (!immediate && Mathf.Approximately(targetFill, lastRenderedFill))
        {
            UpdateHPText(currentHp, maxHp, targetFill);
            return;
        }

        latestTargetFill = targetFill;
        lastRenderedFill = targetFill;

        if (fill != null)
            fill.fillAmount = targetFill;

        if (lagFill != null)
        {
            if (!useDamageLag || immediate)
            {
                lagFill.fillAmount = targetFill;
            }
            else if (lagFill.fillAmount <= targetFill)
            {
                lagFill.fillAmount = targetFill;
                delayAlreadyPassed = false;
                StopLagRoutine();
            }
            else if (lagRoutine == null)
            {
                lagRoutine = StartCoroutine(LagChaseLoop());
            }
        }

        UpdateHPText(currentHp, maxHp, targetFill);
    }

    private IEnumerator LagChaseLoop()
    {
        if (!delayAlreadyPassed && lagDelay > 0f)
        {
            yield return new WaitForSeconds(lagDelay);
            delayAlreadyPassed = true;
        }

        while (lagFill != null)
        {
            if (lagFill.fillAmount <= latestTargetFill)
            {
                lagFill.fillAmount = latestTargetFill;
                break;
            }

            lagFill.fillAmount = Mathf.MoveTowards(
                lagFill.fillAmount,
                latestTargetFill,
                lagCatchUpSpeed * Time.deltaTime
            );

            yield return null;
        }

        lagRoutine = null;
        delayAlreadyPassed = false;
    }

    private void UpdateHPText(float currentHp, float maxHp, float normalized)
    {
        if (hpText == null)
            return;

        if (textMode == HPTextMode.Hidden)
        {
            hpText.text = string.Empty;
            return;
        }

        int current = Mathf.RoundToInt(currentHp);
        int max = Mathf.RoundToInt(maxHp);
        int percent = Mathf.RoundToInt(normalized * 100f);

        switch (textMode)
        {
            case HPTextMode.CurrentOnly:
                hpText.text = current.ToString();
                break;

            case HPTextMode.Percent:
                hpText.text = percent + "%";
                break;

            case HPTextMode.CurrentAndMax:
            default:
                hpText.text = current + "/" + max;
                break;
        }
    }

    private void ApplyMissingTargetVisibility()
    {
        if (hideWhenTargetMissing)
            SetVisible(false);

        if (hpText != null && textMode != HPTextMode.Hidden)
            hpText.text = string.Empty;
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

        if (fill != null)
            fill.enabled = visible;

        if (lagFill != null)
            lagFill.enabled = visible;

        if (hpText != null)
            hpText.enabled = visible;
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
        if (fill == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            foreach (Image image in images)
            {
                if (image == null)
                    continue;

                string lowerName = image.name.ToLowerInvariant();

                if (lowerName.Contains("fill"))
                {
                    fill = image;
                    break;
                }
            }
        }

        if (hpText == null)
            hpText = GetComponentInChildren<TMP_Text>(true);
    }

    private void ConfigureFillImages()
    {
        if (!autoConfigureFilledImages)
            return;

        ConfigureHorizontalFill(fill);
        ConfigureHorizontalFill(lagFill);
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
