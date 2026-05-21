using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CurrentWeaponIconUI : MonoBehaviour
{
    [Header("UI Reference")]
    [Tooltip("Image utama untuk menampilkan ikon senjata aktif.")]
    [SerializeField] private Image iconImage;

    [Header("Weapon Sprites")]
    [SerializeField] private Sprite unarmedSprite;
    [SerializeField] private Sprite swordSprite;
    [SerializeField] private Sprite bowSprite;

    [Header("Optional Object Mode")]
    [Tooltip("Aktifkan hanya jika ikon memakai tiga GameObject berbeda, bukan satu Image yang berganti sprite.")]
    [SerializeField] private bool useObjectMode = false;

    [SerializeField] private GameObject unarmedObject;
    [SerializeField] private GameObject swordObject;
    [SerializeField] private GameObject bowObject;

    [Header("Display")]
    [Tooltip("Jika aktif, Image disembunyikan ketika sprite senjata belum diisi.")]
    [SerializeField] private bool hideImageIfSpriteIsNull = true;

    [Tooltip("Jika aktif, objek ikon dipindahkan ke urutan paling atas dalam Canvas.")]
    [SerializeField] private bool forceRenderOnTop = true;

    [Tooltip("Jika aktif, parent sampai Canvas juga ikut dipindahkan ke urutan paling atas. Ini membantu jika ikon berada di dalam PlayerHUD.")]
    [SerializeField] private bool promoteParentChainToFront = true;

    [Tooltip("Gunakan hanya jika ikon tetap tertutup UI lain. Menambahkan Canvas lokal dengan override sorting.")]
    [SerializeField] private bool useLocalCanvasOverride = false;

    [SerializeField] private int overrideSortingOrder = 50;

    [Header("Feedback")]
    [Tooltip("Flash singkat saat senjata aktif berubah.")]
    [SerializeField] private bool flashOnWeaponChanged = true;

    [Min(0.01f)]
    [SerializeField] private float flashDuration = 0.12f;

    [Tooltip("Skala puncak saat ikon flash.")]
    [Min(1f)]
    [SerializeField] private float flashScale = 1.12f;

    [Header("Runtime")]
    [SerializeField] private WeaponType currentWeapon = WeaponType.None;

    private Canvas localCanvas;
    private GraphicRaycaster localRaycaster;
    private Coroutine flashRoutine;
    private Vector3 defaultScale = Vector3.one;
    private bool hasDefaultScale;

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        CaptureDefaultScale();
        AutoAssignReferences();
        SetupRenderOrder();
        SetWeapon(PlayerPrefabSwitchManager.CurrentWeapon, false);
    }

    private void OnEnable()
    {
        PlayerPrefabSwitchManager.OnActiveWeaponChanged += HandleWeaponChanged;

        CaptureDefaultScale();
        AutoAssignReferences();
        SetupRenderOrder();
        SetWeapon(PlayerPrefabSwitchManager.CurrentWeapon, false);
    }

    private void OnDisable()
    {
        PlayerPrefabSwitchManager.OnActiveWeaponChanged -= HandleWeaponChanged;

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
            flashRoutine = null;
        }

        if (hasDefaultScale)
            transform.localScale = defaultScale;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssignReferences();
    }
#endif

    public void SetWeapon(WeaponType weapon)
    {
        SetWeapon(weapon, flashOnWeaponChanged);
    }

    public void SetWeapon(int weaponValue)
    {
        SetWeapon((WeaponType)weaponValue, flashOnWeaponChanged);
    }

    private void HandleWeaponChanged(WeaponType weapon)
    {
        SetWeapon(weapon, flashOnWeaponChanged);
    }

    private void SetWeapon(WeaponType weapon, bool allowFlash)
    {
        AutoAssignReferences();
        SetupRenderOrder();

        bool changed = currentWeapon != weapon;
        currentWeapon = weapon;

        if (useObjectMode)
            SetObjectMode(weapon);
        else
            SetImageSpriteMode(weapon);

        if (allowFlash && changed && flashOnWeaponChanged)
            PlayFlash();
    }

    private void SetImageSpriteMode(WeaponType weapon)
    {
        if (iconImage == null)
            return;

        Sprite selectedSprite = GetSpriteByWeapon(weapon);

        iconImage.sprite = selectedSprite;
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
        iconImage.enabled = !hideImageIfSpriteIsNull || selectedSprite != null;

        if (forceRenderOnTop)
            BringToFront();
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
            BringToFront();
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

    private void PlayFlash()
    {
        if (!gameObject.activeInHierarchy)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        CaptureDefaultScale();

        float timer = 0f;
        float half = Mathf.Max(0.01f, flashDuration * 0.5f);
        Vector3 peakScale = defaultScale * flashScale;

        while (timer < flashDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t;

            if (timer <= half)
                t = timer / half;
            else
                t = 1f - ((timer - half) / half);

            transform.localScale = Vector3.Lerp(defaultScale, peakScale, Mathf.Clamp01(t));
            yield return null;
        }

        transform.localScale = defaultScale;
        flashRoutine = null;
    }

    private void CaptureDefaultScale()
    {
        if (hasDefaultScale)
            return;

        defaultScale = transform.localScale;
        hasDefaultScale = true;
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

        BringToFront();

        if (!useLocalCanvasOverride)
            return;

        localCanvas = GetComponent<Canvas>();

        if (localCanvas == null)
            localCanvas = gameObject.AddComponent<Canvas>();

        localCanvas.overrideSorting = true;
        localCanvas.sortingOrder = overrideSortingOrder;

        localRaycaster = GetComponent<GraphicRaycaster>();

        if (localRaycaster != null)
            localRaycaster.enabled = false;
    }

    private void BringToFront()
    {
        transform.SetAsLastSibling();

        if (!promoteParentChainToFront)
            return;

        Transform current = transform.parent;

        while (current != null)
        {
            if (current.GetComponent<Canvas>() != null)
                break;

            current.SetAsLastSibling();
            current = current.parent;
        }
    }
}
