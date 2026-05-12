using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossHPBarUI : MonoBehaviour
{
    public enum BossTargetType
    {
        None,
        Sword,
        Bow
    }

    [Header("Boss Prefab References")]
    [Tooltip("Isi dengan prefab boss sword dari Project, bukan boss hasil spawn di Hierarchy.")]
    [SerializeField] private GameObject bossSwordPrefab;

    [Tooltip("Isi dengan prefab boss bow dari Project, bukan boss hasil spawn di Hierarchy.")]
    [SerializeField] private GameObject bossBowPrefab;

    [Header("UI References")]
    [SerializeField] private Image hpFill;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Fill Animation")]
    [SerializeField] private bool useSmoothFill = true;
    [SerializeField] private float smoothSpeed = 12f;

    [Header("Visibility")]
    [SerializeField] private bool hideOnAwake = true;
    [SerializeField] private bool hideWhenBossDead = true;

    private BossTargetType activeBossType = BossTargetType.None;
    private GameObject activeBossObject;
    private CharacterBase activeBossStats;
    private float targetFillAmount = 0f;

    public BossTargetType ActiveBossType => activeBossType;
    public GameObject ActiveBossObject => activeBossObject;
    public CharacterBase ActiveBossStats => activeBossStats;

    private void Awake()
    {
        EnsureCanvasGroup();

        if (hideOnAwake)
            Hide();
        else
            RefreshInstant();
    }

    private void LateUpdate()
    {
        Refresh();
    }

    public void SetBossPrefabs(GameObject swordPrefab, GameObject bowPrefab)
    {
        bossSwordPrefab = swordPrefab;
        bossBowPrefab = bowPrefab;
    }

    public void ShowSwordBoss(GameObject spawnedBoss, CharacterBase spawnedBossStats)
    {
        ShowBoss(BossTargetType.Sword, spawnedBoss, spawnedBossStats);
    }

    public void ShowBowBoss(GameObject spawnedBoss, CharacterBase spawnedBossStats)
    {
        ShowBoss(BossTargetType.Bow, spawnedBoss, spawnedBossStats);
    }

    public void ShowBossByPrefab(GameObject bossPrefab, GameObject spawnedBoss, CharacterBase spawnedBossStats)
    {
        ShowBoss(ResolveBossTypeFromPrefab(bossPrefab), spawnedBoss, spawnedBossStats);
    }

    public void ShowBoss(BossTargetType bossType, GameObject spawnedBoss, CharacterBase spawnedBossStats)
    {
        activeBossType = bossType;
        activeBossObject = spawnedBoss;
        activeBossStats = spawnedBossStats != null ? spawnedBossStats : FindCharacterBase(spawnedBoss);

        if (activeBossStats == null)
        {
            Debug.LogWarning(
                "[BossHPBarUI] CharacterBase pada boss hasil spawn tidak ditemukan. " +
                "UI boss disembunyikan. Pastikan prefab boss memiliki CharacterBase pada root atau child."
            );

            Hide();
            return;
        }

        RefreshInstant();
        SetVisible(true);
    }

    public void SetTarget(CharacterBase bossStats)
    {
        activeBossType = BossTargetType.None;
        activeBossObject = bossStats != null ? bossStats.gameObject : null;
        activeBossStats = bossStats;

        if (activeBossStats == null)
        {
            Hide();
            return;
        }

        RefreshInstant();
        SetVisible(true);
    }

    public void Hide()
    {
        activeBossType = BossTargetType.None;
        activeBossObject = null;
        activeBossStats = null;
        targetFillAmount = 0f;

        if (hpFill != null)
            hpFill.fillAmount = 0f;

        if (hpText != null)
            hpText.text = string.Empty;

        SetVisible(false);
    }

    private void Refresh()
    {
        if (activeBossStats == null)
        {
            SetVisible(false);
            return;
        }

        float maxHP = Mathf.Max(1f, activeBossStats.maxHP);
        float currentHP = Mathf.Clamp(activeBossStats.currentHP, 0f, maxHP);
        targetFillAmount = currentHP / maxHP;

        if (hpFill != null)
        {
            if (useSmoothFill)
            {
                hpFill.fillAmount = Mathf.MoveTowards(
                    hpFill.fillAmount,
                    targetFillAmount,
                    Time.deltaTime * smoothSpeed
                );
            }
            else
            {
                hpFill.fillAmount = targetFillAmount;
            }
        }

        if (hpText != null)
        {
            hpText.text = $"{Mathf.CeilToInt(currentHP)} / {Mathf.CeilToInt(maxHP)}";
        }

        if (hideWhenBossDead && currentHP <= 0f)
            SetVisible(false);
        else
            SetVisible(true);
    }

    private void RefreshInstant()
    {
        if (activeBossStats == null)
        {
            if (hpFill != null)
                hpFill.fillAmount = 0f;

            if (hpText != null)
                hpText.text = string.Empty;

            return;
        }

        float maxHP = Mathf.Max(1f, activeBossStats.maxHP);
        float currentHP = Mathf.Clamp(activeBossStats.currentHP, 0f, maxHP);
        targetFillAmount = currentHP / maxHP;

        if (hpFill != null)
            hpFill.fillAmount = targetFillAmount;

        if (hpText != null)
            hpText.text = $"{Mathf.CeilToInt(currentHP)} / {Mathf.CeilToInt(maxHP)}";
    }

    private CharacterBase FindCharacterBase(GameObject bossObject)
    {
        if (bossObject == null)
            return null;

        CharacterBase character = bossObject.GetComponent<CharacterBase>();

        if (character == null)
            character = bossObject.GetComponentInChildren<CharacterBase>(true);

        if (character == null)
            character = bossObject.GetComponentInParent<CharacterBase>();

        return character;
    }

    private BossTargetType ResolveBossTypeFromPrefab(GameObject bossPrefab)
    {
        if (bossPrefab == null)
            return BossTargetType.None;

        if (bossPrefab == bossSwordPrefab)
            return BossTargetType.Sword;

        if (bossPrefab == bossBowPrefab)
            return BossTargetType.Bow;

        string prefabName = bossPrefab.name.ToLowerInvariant();

        if (bossSwordPrefab != null && prefabName.Contains(bossSwordPrefab.name.ToLowerInvariant()))
            return BossTargetType.Sword;

        if (bossBowPrefab != null && prefabName.Contains(bossBowPrefab.name.ToLowerInvariant()))
            return BossTargetType.Bow;

        if (prefabName.Contains("sword"))
            return BossTargetType.Sword;

        if (prefabName.Contains("bow"))
            return BossTargetType.Bow;

        return BossTargetType.None;
    }

    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void SetVisible(bool visible)
    {
        EnsureCanvasGroup();

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }
}
