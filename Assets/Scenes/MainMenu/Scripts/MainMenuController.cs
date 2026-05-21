using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public enum MenuCommand
    {
        None,
        StartGame,
        OpenPanel,
        QuitGame,
        BackToMainMenu // Tambahan opsi untuk kembali ke Main Menu
    }

    [System.Serializable]
    public class MainMenuItem
    {
        [Header("Identity")]
        public string itemName;

        [Header("UI Reference")]
        public TextMeshProUGUI text;
        public RectTransform highlightTarget;

        [Header("Behaviour")]
        public bool interactable = true;
        public MenuCommand command = MenuCommand.None;

        [Tooltip("Kosongkan jika ingin memakai Gameplay/MainMenu Scene Name utama.")]
        public string sceneNameOverride;

        [Tooltip("Dipakai jika Command = OpenPanel.")]
        public GameObject panelToOpen;
    }

    [Header("Scene Configuration")]
    [SerializeField] private string gameplaySceneName = "Sprite_SwordEnemyAI";
    [SerializeField] private string mainMenuSceneName = "MainMenu"; // Tambahan variabel untuk nama scene Main Menu

    [Header("Menu Items")]
    [SerializeField] private MainMenuItem[] menuItems;
    [SerializeField] private int startIndex = 0;

    [Header("Text Colors")]
    [SerializeField] private Color normalTextColor = new Color32(90, 112, 208, 190);
    [SerializeField] private Color selectedTextColor = new Color32(233, 236, 242, 255);
    [SerializeField] private Color disabledTextColor = new Color32(59, 78, 142, 150);

    [Header("Selection Highlight")]
    [SerializeField] private Image selectionHighlightImage;
    [SerializeField] private Color highlightColor = new Color32(59, 78, 142, 165);
    [SerializeField] private Vector2 highlightPadding = new Vector2(46f, 10f);
    [SerializeField] private bool usePreferredTextWidth = true;
    [SerializeField] private bool animateHighlight = true;
    [SerializeField] private float highlightLerpSpeed = 18f;

    [Header("Audio - Music")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip mainMenuMusic;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.55f;

    [Header("Audio - SFX")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip navigateSfx;
    [SerializeField] private AudioClip confirmSfx;

    private int currentIndex;
    private RectTransform highlightRect;
    private Vector2 targetHighlightPosition;
    private Vector2 targetHighlightSize;

    private void Awake()
    {
        highlightRect = selectionHighlightImage != null
            ? selectionHighlightImage.rectTransform
            : null;

        if (selectionHighlightImage != null)
        {
            selectionHighlightImage.color = highlightColor;
            selectionHighlightImage.raycastTarget = false;
            selectionHighlightImage.transform.SetAsFirstSibling();
        }

        currentIndex = Mathf.Clamp(startIndex, 0, Mathf.Max(0, menuItems.Length - 1));

        if (!IsSelectable(currentIndex))
        {
            currentIndex = FindFirstSelectableIndex();
        }

        SetupMainMenuMusic();
    }

    private void Start()
    {
        Canvas.ForceUpdateCanvases();
        RefreshMenuVisual(true);
    }

    private void Update()
    {
        ReadKeyboardInput();
        UpdateHighlightAnimation();
    }

    private void ReadKeyboardInput()
    {
        if (menuItems == null || menuItems.Length == 0)
            return;

        bool upPressed =
            Input.GetKeyDown(KeyCode.W) ||
            Input.GetKeyDown(KeyCode.UpArrow);

        bool downPressed =
            Input.GetKeyDown(KeyCode.S) ||
            Input.GetKeyDown(KeyCode.DownArrow);

        bool confirmPressed =
            Input.GetKeyDown(KeyCode.Return) ||
            Input.GetKeyDown(KeyCode.KeypadEnter);

        if (upPressed)
        {
            MoveSelection(-1);
        }
        else if (downPressed)
        {
            MoveSelection(1);
        }

        if (confirmPressed)
        {
            ConfirmSelection();
        }
    }

    private void MoveSelection(int direction)
    {
        if (menuItems == null || menuItems.Length == 0)
            return;

        int nextIndex = currentIndex;

        for (int attempt = 0; attempt < menuItems.Length; attempt++)
        {
            nextIndex += direction;

            if (nextIndex < 0)
                nextIndex = menuItems.Length - 1;
            else if (nextIndex >= menuItems.Length)
                nextIndex = 0;

            if (IsSelectable(nextIndex))
            {
                currentIndex = nextIndex;
                PlaySfx(navigateSfx);
                RefreshMenuVisual(false);
                return;
            }
        }
    }

    private void ConfirmSelection()
    {
        if (!IsSelectable(currentIndex))
            return;

        MainMenuItem selectedItem = menuItems[currentIndex];
        PlaySfx(confirmSfx);

        switch (selectedItem.command)
        {
            case MenuCommand.StartGame:
                string targetSceneName = string.IsNullOrEmpty(selectedItem.sceneNameOverride)
                    ? gameplaySceneName
                    : selectedItem.sceneNameOverride;

                LoadScene(targetSceneName);
                break;

            // Logika untuk kembali ke Main Menu
            case MenuCommand.BackToMainMenu:
                // Paksa waktu berjalan normal sebelum pindah scene (penting jika ini dipakai di Pause Menu)
                Time.timeScale = 1f;
                AudioListener.pause = false;

                string targetMainMenu = string.IsNullOrEmpty(selectedItem.sceneNameOverride)
                    ? mainMenuSceneName
                    : selectedItem.sceneNameOverride;

                LoadScene(targetMainMenu);
                break;

            case MenuCommand.OpenPanel:
                if (selectedItem.panelToOpen != null)
                {
                    selectedItem.panelToOpen.SetActive(true);
                }
                else
                {
                    Debug.LogWarning("[MAIN MENU] Panel belum di-assign pada item: " + selectedItem.itemName);
                }
                break;

            case MenuCommand.QuitGame:
                QuitGame();
                break;

            case MenuCommand.None:
                Debug.Log("[MAIN MENU] Item tidak memiliki aksi: " + selectedItem.itemName);
                break;
        }
    }

    private void RefreshMenuVisual(bool immediateHighlight)
    {
        if (menuItems == null)
            return;

        for (int i = 0; i < menuItems.Length; i++)
        {
            if (menuItems[i] == null || menuItems[i].text == null)
                continue;

            if (!menuItems[i].interactable)
            {
                menuItems[i].text.color = disabledTextColor;
            }
            else if (i == currentIndex)
            {
                menuItems[i].text.color = selectedTextColor;
            }
            else
            {
                menuItems[i].text.color = normalTextColor;
            }
        }

        RefreshHighlightTarget(immediateHighlight);
    }

    private void RefreshHighlightTarget(bool immediate)
    {
        if (selectionHighlightImage == null || highlightRect == null)
            return;

        if (!IsSelectable(currentIndex))
        {
            selectionHighlightImage.enabled = false;
            return;
        }

        RectTransform targetRect = GetHighlightTarget(currentIndex);

        if (targetRect == null)
        {
            selectionHighlightImage.enabled = false;
            return;
        }

        selectionHighlightImage.enabled = true;
        selectionHighlightImage.color = highlightColor;

        targetHighlightPosition = targetRect.anchoredPosition;

        float targetWidth = targetRect.rect.width;
        float targetHeight = targetRect.rect.height;

        TextMeshProUGUI targetText = menuItems[currentIndex].text;

        if (usePreferredTextWidth && targetText != null)
        {
            targetWidth = targetText.preferredWidth;
            targetHeight = Mathf.Max(targetHeight, targetText.preferredHeight);
        }

        targetHighlightSize = new Vector2(
            targetWidth + highlightPadding.x,
            targetHeight + highlightPadding.y
        );

        if (immediate || !animateHighlight)
        {
            highlightRect.anchoredPosition = targetHighlightPosition;
            highlightRect.sizeDelta = targetHighlightSize;
        }
    }

    private void UpdateHighlightAnimation()
    {
        if (!animateHighlight || highlightRect == null || selectionHighlightImage == null)
            return;

        if (!selectionHighlightImage.enabled)
            return;

        float lerpValue = Time.unscaledDeltaTime * highlightLerpSpeed;

        highlightRect.anchoredPosition = Vector2.Lerp(
            highlightRect.anchoredPosition,
            targetHighlightPosition,
            lerpValue
        );

        highlightRect.sizeDelta = Vector2.Lerp(
            highlightRect.sizeDelta,
            targetHighlightSize,
            lerpValue
        );
    }

    private RectTransform GetHighlightTarget(int index)
    {
        if (index < 0 || index >= menuItems.Length)
            return null;

        if (menuItems[index].highlightTarget != null)
            return menuItems[index].highlightTarget;

        if (menuItems[index].text != null)
            return menuItems[index].text.rectTransform;

        return null;
    }

    private bool IsSelectable(int index)
    {
        if (menuItems == null)
            return false;

        if (index < 0 || index >= menuItems.Length)
            return false;

        if (menuItems[index] == null)
            return false;

        if (!menuItems[index].interactable)
            return false;

        if (menuItems[index].text == null)
            return false;

        return true;
    }

    private int FindFirstSelectableIndex()
    {
        if (menuItems == null)
            return 0;

        for (int i = 0; i < menuItems.Length; i++)
        {
            if (IsSelectable(i))
                return i;
        }

        return 0;
    }

    private void SetupMainMenuMusic()
    {
        bool hasClipFromField = mainMenuMusic != null;
        bool hasClipFromSource = musicSource != null && musicSource.clip != null;

        if (!hasClipFromField && !hasClipFromSource)
            return;

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
        }

        if (mainMenuMusic != null)
        {
            musicSource.clip = mainMenuMusic;
        }

        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.spatialBlend = 0f;
        musicSource.volume = musicVolume;

        if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (sfxSource == null || clip == null)
            return;

        sfxSource.PlayOneShot(clip);
    }

    public void StartGame()
    {
        LoadScene(gameplaySceneName);
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[MAIN MENU] Nama scene kosong. Isi Scene Name di Inspector.");
            return;
        }

        SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
    }

    public void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}