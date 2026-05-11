using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHPBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterBase target;
    [SerializeField] private Image fill;
    [SerializeField] private TMP_Text hpText;

    [Header("Auto Assign Player")]
    [SerializeField] private bool autoFindPlayer = true;
    [Min(0.05f)]
    [SerializeField] private float autoFindInterval = 0.25f;

    private float nextAutoFindTime;
    private bool hasWarnedMissingPlayerTag;

    private void Awake()
    {
        TryAutoAssignTarget();
    }

    private void Update()
    {
        if (target == null && autoFindPlayer && Time.unscaledTime >= nextAutoFindTime)
        {
            nextAutoFindTime = Time.unscaledTime + autoFindInterval;
            TryAutoAssignTarget();
        }

        RefreshHPBar();
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
        RefreshHPBar();
    }

    private bool TryAutoAssignTarget()
    {
        if (!autoFindPlayer)
            return false;

        CharacterBase foundTarget = null;

        try
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                foundTarget = GetCharacterBaseFromTransform(playerObject.transform);
            }
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
            {
                foundTarget = player;
            }
        }

        if (foundTarget == null)
            return false;

        SetTarget(foundTarget);
        return true;
    }

    private CharacterBase GetCharacterBaseFromTransform(Transform sourceTransform)
    {
        if (sourceTransform == null)
            return null;

        CharacterBase characterBase = sourceTransform.GetComponent<CharacterBase>();

        if (characterBase == null)
            characterBase = sourceTransform.GetComponentInChildren<CharacterBase>();

        if (characterBase == null)
            characterBase = sourceTransform.GetComponentInParent<CharacterBase>();

        return characterBase;
    }

    private void RefreshHPBar()
    {
        if (target == null)
            return;

        float maxHp = Mathf.Max(1f, target.maxHP);
        float currentHp = Mathf.Clamp(target.currentHP, 0f, maxHp);

        if (fill != null)
            fill.fillAmount = currentHp / maxHp;

        if (hpText != null)
            hpText.text = $"{Mathf.RoundToInt(currentHp)}/{Mathf.RoundToInt(maxHp)}";
    }
}