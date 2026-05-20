using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SkillHUDSlotUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CanvasGroup rootGroup;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image cooldownFillImage;
    [SerializeField] private Image energyDarkOverlayImage;
    [SerializeField] private Image activeGlowImage;
    [SerializeField] private TMP_Text keyText;
    [SerializeField] private TMP_Text cooldownText;

    [Header("Visual")]
    [SerializeField] private float normalAlpha = 1f;
    [SerializeField] private float unavailableAlpha = 0.45f;
    [SerializeField] private float glowDuration = 0.15f;

    private float glowTimer;

    private void Reset()
    {
        AutoAssign();
    }

    private void Awake()
    {
        AutoAssign();
        ConfigureCooldownFill();

        if (cooldownFillImage != null)
            cooldownFillImage.fillAmount = 0f;

        if (energyDarkOverlayImage != null)
            energyDarkOverlayImage.enabled = false;

        if (activeGlowImage != null)
            activeGlowImage.enabled = false;

        if (cooldownText != null)
            cooldownText.text = string.Empty;
    }

    private void Update()
    {
        UpdateGlow();
    }

    public void SetIcon(Sprite icon)
    {
        if (iconImage == null)
            return;

        iconImage.sprite = icon;
        iconImage.enabled = icon != null;
        iconImage.preserveAspect = true;
    }

    public void SetKeyText(string value)
    {
        if (keyText != null)
            keyText.text = value;
    }

    public void SetCooldown(float remainingTime, float cooldownDuration, bool showNumber)
    {
        if (cooldownFillImage == null)
            return;

        bool isCoolingDown = remainingTime > 0.05f && cooldownDuration > 0.05f;
        float fill = isCoolingDown ? Mathf.Clamp01(remainingTime / cooldownDuration) : 0f;

        cooldownFillImage.enabled = isCoolingDown;
        cooldownFillImage.fillAmount = fill;

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
        if (rootGroup != null)
            rootGroup.alpha = available ? normalAlpha : unavailableAlpha;

        if (energyDarkOverlayImage != null)
            energyDarkOverlayImage.enabled = !available;
    }

    public void Flash()
    {
        glowTimer = glowDuration;

        if (activeGlowImage != null)
            activeGlowImage.enabled = true;
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

        Color color = activeGlowImage.color;
        color.a = Mathf.Clamp01(glowTimer / glowDuration) * 0.75f;
        activeGlowImage.color = color;
    }

    private void ConfigureCooldownFill()
    {
        if (cooldownFillImage == null)
            return;

        cooldownFillImage.type = Image.Type.Filled;
        cooldownFillImage.fillMethod = Image.FillMethod.Vertical;
        cooldownFillImage.fillOrigin = (int)Image.OriginVertical.Top;
        cooldownFillImage.raycastTarget = false;
    }

    private void AutoAssign()
    {
        if (rootGroup == null)
            rootGroup = GetComponent<CanvasGroup>();

        if (iconImage == null)
            iconImage = FindImage("Icon");

        if (cooldownFillImage == null)
            cooldownFillImage = FindImage("CooldownFill");

        if (energyDarkOverlayImage == null)
            energyDarkOverlayImage = FindImage("EnergyDarkOverlay");

        if (activeGlowImage == null)
            activeGlowImage = FindImage("ActiveGlow");

        if (keyText == null)
            keyText = FindText("KeyText");

        if (cooldownText == null)
            cooldownText = FindText("CooldownText");
    }

    private Image FindImage(string targetName)
    {
        Image[] images = GetComponentsInChildren<Image>(true);

        foreach (Image image in images)
        {
            if (image != null && image.name == targetName)
                return image;
        }

        return null;
    }

    private TMP_Text FindText(string targetName)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text != null && text.name == targetName)
                return text;
        }

        return null;
    }
}