using UnityEngine;
using UnityEngine.UI;

public class WeaponChoiceUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerPrefabSwitchManager switchManager;
    [SerializeField] private GameObject choicePanel;
    [SerializeField] private Button swordButton;
    [SerializeField] private Button bowButton;

    [Header("Opening")]
    [SerializeField] private bool showPanelOnStart = true;
    [SerializeField] private bool pauseGameWhileChoosing = false;

    [Header("After Choice")]
    [SerializeField] private bool hidePanelAfterChoice = true;
    [SerializeField] private GameObject[] objectsToHideAfterChoice;

    private CanvasGroup canvasGroup;
    private bool choiceMade;

    private void Awake()
    {
        if (switchManager == null)
            switchManager = FindObjectOfType<PlayerPrefabSwitchManager>();

        if (choicePanel == null)
            choicePanel = gameObject;

        canvasGroup = choicePanel.GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = choicePanel.AddComponent<CanvasGroup>();

        if (swordButton != null)
        {
            swordButton.onClick.RemoveListener(ChooseSword);
            swordButton.onClick.AddListener(ChooseSword);
        }

        if (bowButton != null)
        {
            bowButton.onClick.RemoveListener(ChooseBow);
            bowButton.onClick.AddListener(ChooseBow);
        }
    }

    private void Start()
    {
        if (showPanelOnStart)
            OpenChoicePanel();
        else
            CloseChoicePanel(false);
    }

    public void OpenChoicePanel()
    {
        choiceMade = false;

        if (choicePanel != null)
            choicePanel.SetActive(true);

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        if (pauseGameWhileChoosing)
            Time.timeScale = 0f;

        Debug.Log("[WEAPON CHOICE UI] Panel pilihan senjata dibuka.");
    }

    public void CloseChoicePanel(bool restoreTimeScale = true)
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (choicePanel != null)
            choicePanel.SetActive(false);

        if (restoreTimeScale && pauseGameWhileChoosing)
            Time.timeScale = 1f;

        Debug.Log("[WEAPON CHOICE UI] Panel pilihan senjata ditutup.");
    }

    public void ChooseSword()
    {
        ChooseWeapon(WeaponType.Sword);
    }

    public void ChooseBow()
    {
        ChooseWeapon(WeaponType.Bow);
    }

    private void ChooseWeapon(WeaponType selectedWeapon)
    {
        if (choiceMade)
            return;

        if (switchManager == null)
        {
            switchManager = FindObjectOfType<PlayerPrefabSwitchManager>();

            if (switchManager == null)
            {
                Debug.LogWarning("[WEAPON CHOICE UI] PlayerPrefabSwitchManager tidak ditemukan.");
                return;
            }
        }

        if (pauseGameWhileChoosing)
            Time.timeScale = 1f;

        bool success = switchManager.TrySwitchFromUI(selectedWeapon);

        if (!success)
        {
            Debug.LogWarning("[WEAPON CHOICE UI] Gagal memilih senjata: " + selectedWeapon);
            return;
        }

        choiceMade = true;

        HideObjectsAfterChoice();

        if (hidePanelAfterChoice)
            CloseChoicePanel(false);

        Debug.Log("[WEAPON CHOICE UI] Senjata dipilih: " + selectedWeapon);
    }

    private void HideObjectsAfterChoice()
    {
        if (objectsToHideAfterChoice == null)
            return;

        foreach (GameObject targetObject in objectsToHideAfterChoice)
        {
            if (targetObject != null)
                targetObject.SetActive(false);
        }
    }
}