using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class PlayerHUDLayoutFixer : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private RectTransform playerHUD;

    [Header("HUD Background")]
    [SerializeField] private RectTransform backgroundImage;

    [Header("HP Bar")]
    [SerializeField] private RectTransform hpBarRoot;
    [SerializeField] private RectTransform hpBackground;
    [SerializeField] private RectTransform hpFill;
    [SerializeField] private RectTransform hpText;

    [Header("Energy Bar")]
    [SerializeField] private RectTransform energyRoot;
    [SerializeField] private RectTransform energyBorder;
    [SerializeField] private RectTransform energyFillRoot;
    [SerializeField] private RectTransform energyMainFill;
    [SerializeField] private RectTransform energyLagFill;
    [SerializeField] private RectTransform energyText;

    [Header("Weapon Icon")]
    [SerializeField] private RectTransform weaponIconRoot;
    [SerializeField] private RectTransform weaponIconImage;

    [Header("Layout Settings")]
    [Min(0.5f)]
    [SerializeField] private float hudScale = 1f;

    [SerializeField] private Vector2 hudScreenPosition = new Vector2(20f, -20f);

    [Header("Native HUD Size")]
    [SerializeField] private Vector2 nativeHudSize = new Vector2(298f, 64f);

    [Header("Native Coordinates")]
    [SerializeField] private Rect nativeHpBar = new Rect(64f, 10f, 175f, 10f);
    [SerializeField] private Rect nativeHpText = new Rect(80f, 5f, 150f, 20f);

    [SerializeField] private Rect nativeEnergyBar = new Rect(64f, 32f, 190f, 7f);
    [SerializeField] private Rect nativeEnergyText = new Rect(73f, 25f, 150f, 18f);

    [SerializeField] private Rect nativeWeaponIcon = new Rect(14f, 12f, 38f, 38f);

    [Header("Text Settings")]
    [SerializeField] private float hpFontSize = 18f;
    [SerializeField] private float energyFontSize = 8f;

    [Header("Auto Apply")]
    [SerializeField] private bool applyInEditMode = true;

    private void Reset()
    {
        playerHUD = transform as RectTransform;
        AutoFind();
        ApplyLayout();
    }

    private void Awake()
    {
        AutoFind();
        ApplyLayout();
    }

    private void OnEnable()
    {
        AutoFind();
        ApplyLayout();
    }

#if UNITY_EDITOR
    private void Update()
    {
        if (!Application.isPlaying && applyInEditMode)
        {
            AutoFind();
            ApplyLayout();
        }
    }

    private void OnValidate()
    {
        AutoFind();
        ApplyLayout();
    }
#endif

    [ContextMenu("Auto Find References")]
    public void AutoFind()
    {
        if (playerHUD == null)
            playerHUD = transform as RectTransform;

        if (backgroundImage == null)
            backgroundImage = FindChildRect("Image");

        if (hpBarRoot == null)
            hpBarRoot = FindChildRect("HPBar_Player");

        if (hpBarRoot != null)
        {
            if (hpBackground == null)
                hpBackground = FindChildRect(hpBarRoot, "Background");

            if (hpFill == null)
                hpFill = FindChildRect(hpBarRoot, "Fill");

            if (hpText == null)
                hpText = FindChildRect(hpBarRoot, "HealthText");
        }

        if (energyRoot == null)
            energyRoot = FindChildRect("EnergyHUD");

        if (energyRoot != null)
        {
            if (energyBorder == null)
                energyBorder = FindChildRect(energyRoot, "Border");

            if (energyFillRoot == null)
                energyFillRoot = FindChildRect(energyRoot, "FillRoot");

            if (energyMainFill == null)
                energyMainFill = FindChildRect(energyRoot, "MainFill");

            if (energyLagFill == null)
                energyLagFill = FindChildRect(energyRoot, "LagFill");

            if (energyText == null)
                energyText = FindChildRect(energyRoot, "EnergyText");
        }

        if (weaponIconRoot == null)
            weaponIconRoot = FindChildRect("CurrentWeaponIconUI");

        if (weaponIconRoot != null && weaponIconImage == null)
            weaponIconImage = FindChildRect(weaponIconRoot, "IconImage");
    }

    [ContextMenu("Apply Layout")]
    public void ApplyLayout()
    {
        if (playerHUD == null)
            return;

        ConfigureTopLeftRoot(playerHUD, hudScreenPosition, nativeHudSize * hudScale);

        if (backgroundImage != null)
        {
            StretchFull(backgroundImage);
            backgroundImage.SetAsFirstSibling();
        }

        if (hpBarRoot != null)
        {
            SetTopLeft(hpBarRoot, 0f, 0f, nativeHudSize.x, nativeHudSize.y, hudScale);
        }

        if (hpBackground != null)
        {
            SetTopLeft(hpBackground, nativeHpBar.x, nativeHpBar.y, nativeHpBar.width, nativeHpBar.height, hudScale);
        }

        if (hpFill != null)
        {
            SetTopLeft(hpFill, nativeHpBar.x, nativeHpBar.y, nativeHpBar.width, nativeHpBar.height, hudScale);
            ConfigureFillImage(hpFill);
        }

        if (hpText != null)
        {
            SetTopLeft(hpText, nativeHpText.x, nativeHpText.y, nativeHpText.width, nativeHpText.height, hudScale);
            ConfigureText(hpText, hpFontSize * hudScale, TextAlignmentOptions.Center);
        }

        if (energyRoot != null)
        {
            SetTopLeft(energyRoot, 0f, 0f, nativeHudSize.x, nativeHudSize.y, hudScale);
        }

        if (energyBorder != null)
        {
            SetTopLeft(energyBorder, nativeEnergyBar.x - 2f, nativeEnergyBar.y - 2f, nativeEnergyBar.width + 4f, nativeEnergyBar.height + 4f, hudScale);
        }

        if (energyFillRoot != null)
        {
            SetTopLeft(energyFillRoot, nativeEnergyBar.x, nativeEnergyBar.y, nativeEnergyBar.width, nativeEnergyBar.height, hudScale);
        }

        if (energyMainFill != null)
        {
            StretchFull(energyMainFill);
            ConfigureFillImage(energyMainFill);
        }

        if (energyLagFill != null)
        {
            StretchFull(energyLagFill);
            ConfigureFillImage(energyLagFill);
        }

        if (energyText != null)
        {
            SetTopLeft(energyText, nativeEnergyText.x, nativeEnergyText.y, nativeEnergyText.width, nativeEnergyText.height, hudScale);
            ConfigureText(energyText, energyFontSize * hudScale, TextAlignmentOptions.Center);
        }

        if (weaponIconRoot != null)
        {
            SetTopLeft(
                weaponIconRoot,
                nativeWeaponIcon.x,
                nativeWeaponIcon.y,
                nativeWeaponIcon.width,
                nativeWeaponIcon.height,
                hudScale
            );

            weaponIconRoot.SetAsLastSibling();
        }

        if (weaponIconImage != null)
        {
            StretchFull(weaponIconImage);

            Image iconImage = weaponIconImage.GetComponent<Image>();
            if (iconImage != null)
            {
                iconImage.preserveAspect = true;
                iconImage.raycastTarget = false;
            }
        }
    }

    private void ConfigureTopLeftRoot(RectTransform rect, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private void SetTopLeft(RectTransform rect, float x, float y, float width, float height, float scale)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x * scale, -y * scale);
        rect.sizeDelta = new Vector2(width * scale, height * scale);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    private void ConfigureFillImage(RectTransform rect)
    {
        Image image = rect.GetComponent<Image>();

        if (image == null)
            return;

        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.raycastTarget = false;
    }

    private void ConfigureText(RectTransform rect, float fontSize, TextAlignmentOptions alignment)
    {
        TMP_Text tmp = rect.GetComponent<TMP_Text>();

        if (tmp == null)
            return;

        tmp.fontSize = fontSize;
        tmp.enableAutoSizing = false;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
    }

    private RectTransform FindChildRect(string childName)
    {
        return FindChildRect(transform, childName);
    }

    private RectTransform FindChildRect(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
            return null;

        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);

        foreach (RectTransform rect in rects)
        {
            if (rect != null && rect.name == childName)
                return rect;
        }

        return null;
    }
}