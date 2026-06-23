using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SkillHUDSlotUI : MonoBehaviour
{
    public enum CooldownFillDirection
    {
        TopToBottom,
        BottomToTop
    }

    [Header("References")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image secondaryIconImage;
    [SerializeField] private CanvasGroup iconGroup;
    [SerializeField] private CanvasGroup secondaryIconGroup;
    [SerializeField] private Image cooldownFillImage;
    [SerializeField] private Image energyDarkOverlayImage;
    [SerializeField] private Image activeGlowImage;
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private TMP_Text secondaryKeyText;
    [SerializeField] private TMP_Text cooldownText;

    [Header("Optional Text")]
    [SerializeField] private TMP_Text skillNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text tagText;

    [Header("Visual")]
    [Tooltip("Alpha ikon ketika skill siap dipakai.")]
    [SerializeField, Range(0f, 1f)] private float normalAlpha = 1f;

    [Tooltip("Alpha ikon ketika skill belum siap dipakai.")]
    [SerializeField, Range(0f, 1f)] private float unavailableAlpha = 0.42f;

    [Tooltip("Durasi flash singkat pada ikon.")]
    [SerializeField, Min(0.01f)] private float glowDuration = 0.15f;

    [Header("Cooldown Fill")]
    [SerializeField] private CooldownFillDirection fillDirection = CooldownFillDirection.TopToBottom;

    private float glowTimer;
    private Color initialGlowColor = Color.white;

    private void Reset()
    {
        AutoAssign();
        ConfigureCooldownFill();
    }

    private void Awake()
    {
        AutoAssign();
        ConfigureCooldownFill();

        if (activeGlowImage != null)
        {
            initialGlowColor = activeGlowImage.color;
            activeGlowImage.enabled = false;
        }

        SetCooldown(0f, 0f, false);
        SetEnergyAvailable(true);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssign();
        ConfigureCooldownFill();
    }
#endif

    private void Update()
    {
        UpdateGlow();
    }

    public void SetIcon(Sprite icon)
    {
        AutoAssign();

        if (iconImage == null)
            return;

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
    }

    public void SetSecondaryIcon(Sprite icon)
    {
        AutoAssign();

        if (secondaryIconImage == null)
            return;

        secondaryIconImage.sprite = icon;
        secondaryIconImage.enabled = icon != null;
        secondaryIconImage.preserveAspect = true;
        secondaryIconImage.raycastTarget = false;
    }

    public void SetMergedIcons(Sprite primaryIcon, Sprite secondaryIcon)
    {
        SetIcon(primaryIcon);
        SetSecondaryIcon(secondaryIcon);
    }

    public void SetKeyText(string value)
    {
        AutoAssign();

        if (keyText != null)
            keyText.text = value;
    }

    public void SetSecondaryKeyText(string value)
    {
        AutoAssign();

        if (secondaryKeyText != null)
            secondaryKeyText.text = value;
    }

    public void SetCooldown(float remainingTime, float cooldownDuration, bool showNumber)
    {
        AutoAssign();

        bool isCoolingDown = remainingTime > 0.05f && cooldownDuration > 0.05f;
        float fill = isCoolingDown ? Mathf.Clamp01(remainingTime / cooldownDuration) : 0f;

        if (cooldownFillImage != null)
        {
            cooldownFillImage.enabled = isCoolingDown;
            cooldownFillImage.fillAmount = fill;
        }

        if (cooldownText != null)
        {
            cooldownText.enabled = isCoolingDown && showNumber;
            cooldownText.text = isCoolingDown && showNumber
                ? Mathf.CeilToInt(remainingTime).ToString()
                : string.Empty;
        }
    }

    public void SetEnergyAvailable(bool available)
    {
        SetAvailable(available);
    }

    public void SetAvailable(bool available)
    {
        AutoAssign();

        SetIconAvailable(available);
        SetSecondaryIconAvailable(available);

        if (energyDarkOverlayImage != null)
            energyDarkOverlayImage.enabled = !available;
    }


    public void SetDarkOverlayVisible(bool visible)
    {
        AutoAssign();

        if (energyDarkOverlayImage != null)
            energyDarkOverlayImage.enabled = visible;
    }

    public void SetIconAvailable(bool available)
    {
        AutoAssign();

        if (iconGroup != null)
        {
            iconGroup.alpha = available ? normalAlpha : unavailableAlpha;
        }
        else if (rootGroup != null)
        {
            rootGroup.alpha = available ? normalAlpha : unavailableAlpha;
        }
    }

    public void SetSecondaryIconAvailable(bool available)
    {
        AutoAssign();

        if (secondaryIconGroup != null)
            secondaryIconGroup.alpha = available ? normalAlpha : unavailableAlpha;
    }

    public void SetFillDirection(CooldownFillDirection direction)
    {
        fillDirection = direction;
        ConfigureCooldownFill();
    }

    public void Flash()
    {
        AutoAssign();

        glowTimer = glowDuration;

        if (activeGlowImage != null)
        {
            Color color = initialGlowColor;
            color.a = 0.75f;
            activeGlowImage.color = color;
            activeGlowImage.enabled = true;
        }
    }

    public void SetSkillName(string value)
    {
        if (skillNameText != null)
            skillNameText.text = value;
    }

    public void SetDisplayName(string value)
    {
        SetSkillName(value);
    }

    public void SetSkillDescription(string value)
    {
        if (descriptionText != null)
            descriptionText.text = value;
    }

    public void SetDescription(string value)
    {
        SetSkillDescription(value);
    }

    public void SetTagLine(string value)
    {
        if (tagText != null)
            tagText.text = value;
    }

    public void SetTagText(string value)
    {
        SetTagLine(value);
    }

    private void UpdateGlow()
    {
        if (activeGlowImage == null)
            return;

        if (glowTimer <= 0f)
        {
            activeGlowImage.enabled = false;
            return;
        }

        glowTimer -= Time.unscaledDeltaTime;

        float normalized = Mathf.Clamp01(glowTimer / Mathf.Max(0.01f, glowDuration));
        Color color = initialGlowColor;
        color.a = normalized * 0.75f;
        activeGlowImage.color = color;
    }

    private void ConfigureCooldownFill()
    {
        if (cooldownFillImage == null)
            return;

        cooldownFillImage.type = Image.Type.Filled;
        cooldownFillImage.fillMethod = Image.FillMethod.Vertical;
        cooldownFillImage.fillOrigin = fillDirection == CooldownFillDirection.TopToBottom
            ? (int)Image.OriginVertical.Top
            : (int)Image.OriginVertical.Bottom;

        cooldownFillImage.raycastTarget = false;
    }

    private void AutoAssign()
    {
        if (rootGroup == null)
        {
            rootGroup = GetComponent<CanvasGroup>();

            if (rootGroup == null)
                rootGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (iconImage == null)
            iconImage = FindImage("Icon", "IconLeft", "PrimaryIcon");

        if (secondaryIconImage == null)
            secondaryIconImage = FindImage("SecondaryIcon", "IconRight", "SecondIcon");

        if (iconGroup == null && iconImage != null)
        {
            iconGroup = iconImage.GetComponent<CanvasGroup>();

            if (iconGroup == null)
                iconGroup = iconImage.gameObject.AddComponent<CanvasGroup>();
        }

        if (secondaryIconGroup == null && secondaryIconImage != null)
        {
            secondaryIconGroup = secondaryIconImage.GetComponent<CanvasGroup>();

            if (secondaryIconGroup == null)
                secondaryIconGroup = secondaryIconImage.gameObject.AddComponent<CanvasGroup>();
        }

        if (cooldownFillImage == null)
            cooldownFillImage = FindImage("CooldownFill", "CooldownOverlay");

        if (energyDarkOverlayImage == null)
            energyDarkOverlayImage = FindImage("EnergyDarkOverlay", "DarkOverlay", "UnavailableOverlay");

        if (activeGlowImage == null)
            activeGlowImage = FindImage("ActiveGlow", "Glow", "Flash");

        if (keyText == null)
            keyText = FindText("KeyText", "KeyTextLeft", "PrimaryKeyText");

        if (secondaryKeyText == null)
            secondaryKeyText = FindText("SecondaryKeyText", "KeyTextRight", "SecondKeyText");

        if (cooldownText == null)
            cooldownText = FindText("CooldownText");

        if (skillNameText == null)
            skillNameText = FindText("SkillNameText", "NameText");

        if (descriptionText == null)
            descriptionText = FindText("DescriptionText", "SkillDescriptionText");

        if (tagText == null)
            tagText = FindText("TagText", "TagLineText");
    }

    private Image FindImage(params string[] targetNames)
    {
        Image[] images = GetComponentsInChildren<Image>(true);

        foreach (Image image in images)
        {
            if (image == null)
                continue;

            foreach (string targetName in targetNames)
            {
                if (image.name == targetName)
                    return image;
            }
        }

        return null;
    }

    private TMP_Text FindText(params string[] targetNames)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text == null)
                continue;

            foreach (string targetName in targetNames)
            {
                if (text.name == targetName)
                    return text;
            }
        }

        return null;
    }
}
