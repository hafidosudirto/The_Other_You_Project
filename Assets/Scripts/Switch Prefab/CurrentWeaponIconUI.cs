using UnityEngine;
using UnityEngine.UI;

public class CurrentWeaponIconUI : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private Image iconImage;

    [Header("Weapon Sprites")]
    [SerializeField] private Sprite unarmedSprite;
    [SerializeField] private Sprite swordSprite;
    [SerializeField] private Sprite bowSprite;

    [Header("Optional Object Mode")]
    [Tooltip("Aktifkan hanya jika ingin memakai 3 GameObject berbeda untuk ikon.")]
    [SerializeField] private bool useObjectMode = false;

    [SerializeField] private GameObject unarmedObject;
    [SerializeField] private GameObject swordObject;
    [SerializeField] private GameObject bowObject;

    [Header("Display")]
    [SerializeField] private bool hideImageIfSpriteIsNull = true;
    [SerializeField] private bool forceRenderOnTop = true;
    [SerializeField] private int overrideSortingOrder = 50;

    private Canvas localCanvas;

    private void Awake()
    {
        AutoAssignReferences();
        SetupRenderOrder();
        SetWeapon(PlayerPrefabSwitchManager.CurrentWeapon);
    }

    private void OnEnable()
    {
        PlayerPrefabSwitchManager.OnActiveWeaponChanged += SetWeapon;

        AutoAssignReferences();
        SetupRenderOrder();
        SetWeapon(PlayerPrefabSwitchManager.CurrentWeapon);
    }

    private void OnDisable()
    {
        PlayerPrefabSwitchManager.OnActiveWeaponChanged -= SetWeapon;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssignReferences();
    }
#endif

    public void SetWeapon(WeaponType weapon)
    {
        AutoAssignReferences();

        if (useObjectMode)
        {
            SetObjectMode(weapon);
            return;
        }

        SetImageSpriteMode(weapon);
    }

    private void SetImageSpriteMode(WeaponType weapon)
    {
        if (iconImage == null)
            return;

        Sprite selectedSprite = GetSpriteByWeapon(weapon);

        iconImage.sprite = selectedSprite;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        if (hideImageIfSpriteIsNull)
            iconImage.enabled = selectedSprite != null;
        else
            iconImage.enabled = true;

        if (forceRenderOnTop)
        {
            transform.SetAsLastSibling();
            iconImage.transform.SetAsLastSibling();
        }
    }

    private void SetObjectMode(WeaponType weapon)
    {
        if (unarmedObject != null)
            unarmedObject.SetActive(weapon == WeaponType.None || weapon == WeaponType.Gauntlet);

        if (swordObject != null)
            swordObject.SetActive(weapon == WeaponType.Sword);

        if (bowObject != null)
            bowObject.SetActive(weapon == WeaponType.Bow);

        if (forceRenderOnTop)
            transform.SetAsLastSibling();
    }

    private Sprite GetSpriteByWeapon(WeaponType weapon)
    {
        switch (weapon)
        {
            case WeaponType.Sword:
                return swordSprite;

            case WeaponType.Bow:
                return bowSprite;

            case WeaponType.None:
            case WeaponType.Gauntlet:
            default:
                return unarmedSprite;
        }
    }

    private void AutoAssignReferences()
    {
        if (iconImage == null)
            iconImage = GetComponentInChildren<Image>(true);
    }

    private void SetupRenderOrder()
    {
        if (!forceRenderOnTop)
            return;

        transform.SetAsLastSibling();

        localCanvas = GetComponent<Canvas>();

        if (localCanvas == null)
            localCanvas = gameObject.AddComponent<Canvas>();

        localCanvas.overrideSorting = true;
        localCanvas.sortingOrder = overrideSortingOrder;

        GraphicRaycaster raycaster = GetComponent<GraphicRaycaster>();

        if (raycaster != null)
            raycaster.enabled = false;
    }
}