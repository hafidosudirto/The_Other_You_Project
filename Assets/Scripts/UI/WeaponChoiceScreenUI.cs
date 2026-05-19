using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponChoiceScreenUI : MonoBehaviour
{
    private enum WeaponSelection
    {
        Sword,
        Bow
    }

    [Header("Core References")]
    [SerializeField] private PlayerPrefabSwitchManager switchManager;
    [SerializeField] private CanvasGroup panelGroup;
    [SerializeField] private RectTransform windowRoot;

    [Header("Buttons")]
    [SerializeField] private Button swordButton;
    [SerializeField] private Button bowButton;

    [Header("Card Roots")]
    [SerializeField] private RectTransform swordCardRoot;
    [SerializeField] private RectTransform bowCardRoot;

    [Header("Card Canvas Groups")]
    [SerializeField] private CanvasGroup swordCardGroup;
    [SerializeField] private CanvasGroup bowCardGroup;

    [Header("Card Visuals")]
    [SerializeField] private Image swordCardBackground;
    [SerializeField] private Image bowCardBackground;

    [Tooltip("Opsional. Isi kalau Anda membuat border khusus di dalam SwordCard.")]
    [SerializeField] private Image swordSelectionBorder;

    [Tooltip("Opsional. Isi kalau Anda membuat border khusus di dalam BowCard.")]
    [SerializeField] private Image bowSelectionBorder;

    [Header("Text References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private TMP_Text hintText;

    [Header("Opening")]
    [SerializeField] private bool showOnStart = true;

    [Tooltip("Aktifkan jika ingin gameplay berhenti saat memilih senjata.")]
    [SerializeField] private bool pauseGameWhileChoosing = true;

    [Tooltip("Mengunci Player_W0 selama panel pilihan terbuka.")]
    [SerializeField] private bool lockPlayerWhileChoosing = true;

    [Header("After Choice")]
    [SerializeField] private bool hidePanelAfterChoice = true;
    [SerializeField] private GameObject[] objectsToHideAfterChoice;
    [SerializeField] private GameObject[] objectsToShowAfterChoice;

    [Header("Default Selection")]
    [SerializeField] private WeaponSelection defaultSelection = WeaponSelection.Sword;

    [Header("Animation")]
    [SerializeField] private float openFadeDuration = 0.18f;
    [SerializeField] private float closeFadeDuration = 0.12f;
    [SerializeField] private float windowStartScale = 0.92f;
    [SerializeField] private float selectedCardScale = 1.06f;
    [SerializeField] private float unselectedCardScale = 0.96f;
    [SerializeField] private float cardVisualLerpSpeed = 14f;

    [Header("Card Alpha")]
    [SerializeField] private float selectedCardAlpha = 1f;
    [SerializeField] private float unselectedCardAlpha = 0.58f;

    [Header("Card Colors")]
    [SerializeField] private Color selectedCardColor = new Color(0.23f, 0.16f, 0.40f, 0.95f);
    [SerializeField] private Color unselectedCardColor = new Color(0.12f, 0.08f, 0.22f, 0.80f);
    [SerializeField] private Color selectedBorderColor = new Color(1f, 0.82f, 0.24f, 1f);
    [SerializeField] private Color unselectedBorderColor = new Color(0.25f, 0.35f, 0.60f, 0.35f);

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = true;

    private WeaponSelection currentSelection;
    private bool isOpen;
    private bool choiceMade;
    private Coroutine panelRoutine;

    private Vector3 swordTargetScale = Vector3.one;
    private Vector3 bowTargetScale = Vector3.one;

    private void Reset()
    {
        AutoAssignReferences();
    }

    private void Awake()
    {
        AutoAssignReferences();
        RegisterButtonEvents();
        SetDefaultText();
        SetPanelInstant(false);
    }

    private void Start()
    {
        currentSelection = defaultSelection;
        ApplySelectionInstant();

        if (showOnStart)
            OpenPanel();
    }

    private void Update()
    {
        if (!isOpen || choiceMade)
            return;

        HandleKeyboardInput();
        UpdateCardVisuals();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssignReferences();
    }
#endif

    // =========================================================
    // PUBLIC METHODS
    // =========================================================

    public void OpenPanel()
    {
        if (choiceMade)
            return;

        AutoAssignReferences();
        RegisterButtonEvents();

        gameObject.SetActive(true);

        isOpen = true;
        currentSelection = defaultSelection;

        SetPanelInteractable(true);
        ApplySelectionInstant();

        if (lockPlayerWhileChoosing)
            SetActivePlayersLocked(true);

        if (pauseGameWhileChoosing)
            Time.timeScale = 0f;

        if (panelRoutine != null)
            StopCoroutine(panelRoutine);

        panelRoutine = StartCoroutine(OpenRoutine());

        if (showDebugLog)
            Debug.Log("[WEAPON CHOICE SCREEN] Panel pilihan senjata dibuka.");
    }

    public void ClosePanel()
    {
        isOpen = false;
        SetPanelInteractable(false);

        if (panelRoutine != null)
            StopCoroutine(panelRoutine);

        panelRoutine = StartCoroutine(CloseRoutine());

        if (showDebugLog)
            Debug.Log("[WEAPON CHOICE SCREEN] Panel pilihan senjata ditutup.");
    }

    public void SelectSword()
    {
        currentSelection = WeaponSelection.Sword;
        ApplySelectionInstant();
    }

    public void SelectBow()
    {
        currentSelection = WeaponSelection.Bow;
        ApplySelectionInstant();
    }

    public void ConfirmSword()
    {
        currentSelection = WeaponSelection.Sword;
        ConfirmCurrentSelection();
    }

    public void ConfirmBow()
    {
        currentSelection = WeaponSelection.Bow;
        ConfirmCurrentSelection();
    }

    public void ConfirmCurrentSelection()
    {
        if (choiceMade)
            return;

        choiceMade = true;
        SetPanelInteractable(false);

        if (pauseGameWhileChoosing)
            Time.timeScale = 1f;

        WeaponType selectedWeapon = GetSelectedWeaponType();

        if (switchManager == null)
            switchManager = FindObjectOfType<PlayerPrefabSwitchManager>();

        if (switchManager == null)
        {
            Debug.LogWarning("[WEAPON CHOICE SCREEN] PlayerPrefabSwitchManager tidak ditemukan.");
            choiceMade = false;
            SetPanelInteractable(true);
            return;
        }

        bool success = switchManager.TrySwitchFromUI(selectedWeapon);

        if (!success)
        {
            Debug.LogWarning("[WEAPON CHOICE SCREEN] Gagal memilih senjata: " + selectedWeapon);
            choiceMade = false;
            SetPanelInteractable(true);

            if (pauseGameWhileChoosing)
                Time.timeScale = 0f;

            return;
        }

        SetObjectsActive(objectsToHideAfterChoice, false);
        SetObjectsActive(objectsToShowAfterChoice, true);

        if (lockPlayerWhileChoosing)
            SetActivePlayersLocked(false);

        if (hidePanelAfterChoice)
            ClosePanel();

        if (showDebugLog)
            Debug.Log("[WEAPON CHOICE SCREEN] Senjata dipilih: " + selectedWeapon);
    }

    // =========================================================
    // INPUT
    // =========================================================

    private void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
            MoveSelection(-1);

        if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
            MoveSelection(1);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            ConfirmCurrentSelection();
    }

    private void MoveSelection(int direction)
    {
        if (direction == 0)
            return;

        if (currentSelection == WeaponSelection.Sword)
            currentSelection = WeaponSelection.Bow;
        else
            currentSelection = WeaponSelection.Sword;

        ApplySelectionInstant();

        if (showDebugLog)
            Debug.Log("[WEAPON CHOICE SCREEN] Selection berpindah ke: " + currentSelection);
    }

    private WeaponType GetSelectedWeaponType()
    {
        switch (currentSelection)
        {
            case WeaponSelection.Sword:
                return WeaponType.Sword;

            case WeaponSelection.Bow:
                return WeaponType.Bow;

            default:
                return WeaponType.Sword;
        }
    }

    // =========================================================
    // VISUAL UPDATE
    // =========================================================

    private void ApplySelectionInstant()
    {
        bool swordSelected = currentSelection == WeaponSelection.Sword;
        bool bowSelected = currentSelection == WeaponSelection.Bow;

        swordTargetScale = Vector3.one * (swordSelected ? selectedCardScale : unselectedCardScale);
        bowTargetScale = Vector3.one * (bowSelected ? selectedCardScale : unselectedCardScale);

        if (swordCardRoot != null)
            swordCardRoot.localScale = swordTargetScale;

        if (bowCardRoot != null)
            bowCardRoot.localScale = bowTargetScale;

        if (swordCardGroup != null)
            swordCardGroup.alpha = swordSelected ? selectedCardAlpha : unselectedCardAlpha;

        if (bowCardGroup != null)
            bowCardGroup.alpha = bowSelected ? selectedCardAlpha : unselectedCardAlpha;

        if (swordCardBackground != null)
            swordCardBackground.color = swordSelected ? selectedCardColor : unselectedCardColor;

        if (bowCardBackground != null)
            bowCardBackground.color = bowSelected ? selectedCardColor : unselectedCardColor;

        if (swordSelectionBorder != null)
            swordSelectionBorder.color = swordSelected ? selectedBorderColor : unselectedBorderColor;

        if (bowSelectionBorder != null)
            bowSelectionBorder.color = bowSelected ? selectedBorderColor : unselectedBorderColor;
    }

    private void UpdateCardVisuals()
    {
        float t = Time.unscaledDeltaTime * cardVisualLerpSpeed;

        bool swordSelected = currentSelection == WeaponSelection.Sword;
        bool bowSelected = currentSelection == WeaponSelection.Bow;

        swordTargetScale = Vector3.one * (swordSelected ? selectedCardScale : unselectedCardScale);
        bowTargetScale = Vector3.one * (bowSelected ? selectedCardScale : unselectedCardScale);

        if (swordCardRoot != null)
            swordCardRoot.localScale = Vector3.Lerp(swordCardRoot.localScale, swordTargetScale, t);

        if (bowCardRoot != null)
            bowCardRoot.localScale = Vector3.Lerp(bowCardRoot.localScale, bowTargetScale, t);

        if (swordCardGroup != null)
            swordCardGroup.alpha = Mathf.Lerp(
                swordCardGroup.alpha,
                swordSelected ? selectedCardAlpha : unselectedCardAlpha,
                t
            );

        if (bowCardGroup != null)
            bowCardGroup.alpha = Mathf.Lerp(
                bowCardGroup.alpha,
                bowSelected ? selectedCardAlpha : unselectedCardAlpha,
                t
            );

        if (swordCardBackground != null)
            swordCardBackground.color = Color.Lerp(
                swordCardBackground.color,
                swordSelected ? selectedCardColor : unselectedCardColor,
                t
            );

        if (bowCardBackground != null)
            bowCardBackground.color = Color.Lerp(
                bowCardBackground.color,
                bowSelected ? selectedCardColor : unselectedCardColor,
                t
            );

        if (swordSelectionBorder != null)
            swordSelectionBorder.color = Color.Lerp(
                swordSelectionBorder.color,
                swordSelected ? selectedBorderColor : unselectedBorderColor,
                t
            );

        if (bowSelectionBorder != null)
            bowSelectionBorder.color = Color.Lerp(
                bowSelectionBorder.color,
                bowSelected ? selectedBorderColor : unselectedBorderColor,
                t
            );
    }

    // =========================================================
    // PANEL ANIMATION
    // =========================================================

    private IEnumerator OpenRoutine()
    {
        if (panelGroup == null)
            yield break;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, openFadeDuration);

        panelGroup.alpha = 0f;

        if (windowRoot != null)
            windowRoot.localScale = Vector3.one * windowStartScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutCubic(progress);

            panelGroup.alpha = eased;

            if (windowRoot != null)
                windowRoot.localScale = Vector3.Lerp(Vector3.one * windowStartScale, Vector3.one, eased);

            yield return null;
        }

        panelGroup.alpha = 1f;

        if (windowRoot != null)
            windowRoot.localScale = Vector3.one;
    }

    private IEnumerator CloseRoutine()
    {
        if (panelGroup == null)
            yield break;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, closeFadeDuration);
        float startAlpha = panelGroup.alpha;

        Vector3 startScale = Vector3.one;

        if (windowRoot != null)
            startScale = windowRoot.localScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutCubic(progress);

            panelGroup.alpha = Mathf.Lerp(startAlpha, 0f, eased);

            if (windowRoot != null)
                windowRoot.localScale = Vector3.Lerp(startScale, Vector3.one * windowStartScale, eased);

            yield return null;
        }

        SetPanelInstant(false);
        gameObject.SetActive(false);
    }

    private float EaseOutCubic(float value)
    {
        value = Mathf.Clamp01(value);
        return 1f - Mathf.Pow(1f - value, 3f);
    }

    // =========================================================
    // PLAYER LOCK
    // =========================================================

    private void SetActivePlayersLocked(bool locked)
    {
        Player[] players = FindObjectsOfType<Player>(true);

        foreach (Player player in players)
        {
            if (player == null)
                continue;

            if (!player.gameObject.activeInHierarchy)
                continue;

            player.lockMovement = locked;

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();

            if (rb == null)
                rb = player.GetComponentInParent<Rigidbody2D>();

            if (rb == null)
                rb = player.GetComponentInChildren<Rigidbody2D>();

            if (rb != null && locked)
                rb.velocity = Vector2.zero;
        }
    }

    // =========================================================
    // SETUP HELPERS
    // =========================================================

    private void AutoAssignReferences()
    {
        if (switchManager == null)
            switchManager = FindObjectOfType<PlayerPrefabSwitchManager>();

        if (panelGroup == null)
        {
            panelGroup = GetComponent<CanvasGroup>();

            if (panelGroup == null)
                panelGroup = gameObject.AddComponent<CanvasGroup>();
        }

        if (windowRoot == null)
            windowRoot = FindRectByName(transform, "EquipmentWindow");

        if (swordButton == null)
            swordButton = FindButtonByName(transform, "SwordCard");

        if (bowButton == null)
            bowButton = FindButtonByName(transform, "BowCard");

        if (swordCardRoot == null && swordButton != null)
            swordCardRoot = swordButton.transform as RectTransform;

        if (bowCardRoot == null && bowButton != null)
            bowCardRoot = bowButton.transform as RectTransform;

        if (swordCardRoot != null && swordCardGroup == null)
            swordCardGroup = GetOrAddCanvasGroup(swordCardRoot.gameObject);

        if (bowCardRoot != null && bowCardGroup == null)
            bowCardGroup = GetOrAddCanvasGroup(bowCardRoot.gameObject);

        if (swordCardBackground == null && swordCardRoot != null)
            swordCardBackground = swordCardRoot.GetComponent<Image>();

        if (bowCardBackground == null && bowCardRoot != null)
            bowCardBackground = bowCardRoot.GetComponent<Image>();

        if (titleText == null)
            titleText = FindTMPByName(transform, "TitleText");

        if (subtitleText == null)
            subtitleText = FindTMPByName(transform, "SubtitleText");

        if (hintText == null)
            hintText = FindTMPByName(transform, "HintText");

        ConfigureButton(swordButton);
        ConfigureButton(bowButton);
    }

    private void RegisterButtonEvents()
    {
        if (swordButton != null)
        {
            swordButton.onClick.RemoveListener(ConfirmSword);
            swordButton.onClick.RemoveListener(SelectSword);
            swordButton.onClick.AddListener(ConfirmSword);
        }

        if (bowButton != null)
        {
            bowButton.onClick.RemoveListener(ConfirmBow);
            bowButton.onClick.RemoveListener(SelectBow);
            bowButton.onClick.AddListener(ConfirmBow);
        }
    }

    private void ConfigureButton(Button button)
    {
        if (button == null)
            return;

        button.interactable = true;

        if (button.targetGraphic == null)
        {
            Graphic graphic = button.GetComponent<Graphic>();

            if (graphic != null)
                button.targetGraphic = graphic;
        }
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject target)
    {
        if (target == null)
            return null;

        CanvasGroup group = target.GetComponent<CanvasGroup>();

        if (group == null)
            group = target.AddComponent<CanvasGroup>();

        return group;
    }

    private RectTransform FindRectByName(Transform root, string objectName)
    {
        if (root == null)
            return null;

        RectTransform[] rects = root.GetComponentsInChildren<RectTransform>(true);

        foreach (RectTransform rect in rects)
        {
            if (rect != null && rect.name == objectName)
                return rect;
        }

        return null;
    }

    private Button FindButtonByName(Transform root, string objectName)
    {
        if (root == null)
            return null;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);

        foreach (Button button in buttons)
        {
            if (button != null && button.name == objectName)
                return button;
        }

        return null;
    }

    private TMP_Text FindTMPByName(Transform root, string objectName)
    {
        if (root == null)
            return null;

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text != null && text.name == objectName)
                return text;
        }

        return null;
    }

    private void SetPanelInstant(bool visible)
    {
        if (panelGroup == null)
            return;

        panelGroup.alpha = visible ? 1f : 0f;
        panelGroup.interactable = visible;
        panelGroup.blocksRaycasts = visible;

        if (windowRoot != null)
            windowRoot.localScale = visible ? Vector3.one : Vector3.one * windowStartScale;

        isOpen = visible;
    }

    private void SetPanelInteractable(bool interactable)
    {
        if (panelGroup == null)
            return;

        panelGroup.interactable = interactable;
        panelGroup.blocksRaycasts = interactable;

        if (swordButton != null)
            swordButton.interactable = interactable;

        if (bowButton != null)
            bowButton.interactable = interactable;
    }

    private void SetObjectsActive(GameObject[] targets, bool active)
    {
        if (targets == null)
            return;

        foreach (GameObject target in targets)
        {
            if (target != null)
                target.SetActive(active);
        }
    }

    private void SetDefaultText()
    {
        if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
            titleText.text = "CHOOSE YOUR WEAPON";

        if (subtitleText != null && string.IsNullOrWhiteSpace(subtitleText.text))
            subtitleText.text = "Select one weapon style to begin the stage.";

        if (hintText != null && string.IsNullOrWhiteSpace(hintText.text))
            hintText.text = "A / D : Switch     Enter : Confirm     Mouse : Select";
    }
}