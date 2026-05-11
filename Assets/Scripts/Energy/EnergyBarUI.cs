using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnergyBarUI : MonoBehaviour
{
    [Header("Target (CharacterBase)")]
    [SerializeField] private CharacterBase target;

    [Header("Auto Assign Player")]
    [SerializeField] private bool autoFindPlayer = true;
    [Min(0.05f)]
    [SerializeField] private float autoFindInterval = 0.25f;

    [Header("UI Images")]
    [SerializeField] private Image mainFillImage; // nilai stamina aktual
    [SerializeField] private Image lagFillImage;  // efek lag ketika stamina berkurang

    [Header("UI Text")]
    [SerializeField] private TMP_Text energyText;

    [Header("Lag Settings (only when decreasing)")]
    [Min(0f)]
    [SerializeField] private float lagCatchUpSpeed = 2f;

    [Min(0f)]
    [SerializeField] private float lagDelay = 0.2f;

    private Coroutine lagRoutine;
    private float latestTargetFill = 1f;
    private bool delayAlreadyPassed = false;

    private float nextAutoFindTime;
    private bool hasWarnedMissingPlayerTag;
    private bool isSubscribedToTarget;

    private void Awake()
    {
        TryAutoAssignTarget();
    }

    private void OnEnable()
    {
        SubscribeToTarget();
        ForceRefreshImmediate();
    }

    private void OnDisable()
    {
        UnsubscribeFromTarget();
        StopLagRoutine();

        delayAlreadyPassed = false;
    }

    private void Update()
    {
        if (target == null && autoFindPlayer && Time.unscaledTime >= nextAutoFindTime)
        {
            nextAutoFindTime = Time.unscaledTime + autoFindInterval;
            TryAutoAssignTarget();
        }
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
            Debug.LogWarning("[ENERGY BAR UI] CharacterBase tidak ditemukan pada playerTransform.");
            return;
        }

        SetTarget(newTarget);
    }

    public void SetTarget(CharacterBase newTarget)
    {
        if (target == newTarget)
        {
            ForceRefreshImmediate();
            return;
        }

        UnsubscribeFromTarget();

        target = newTarget;

        SubscribeToTarget();
        ForceRefreshImmediate();
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
                Debug.LogWarning("[ENERGY BAR UI] Tag Player belum dibuat di Project Settings > Tags and Layers.");
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

    private void SubscribeToTarget()
    {
        if (!isActiveAndEnabled)
            return;

        if (target == null)
            return;

        if (isSubscribedToTarget)
            return;

        target.OnEnergyChanged += OnEnergyChanged;
        isSubscribedToTarget = true;
    }

    private void UnsubscribeFromTarget()
    {
        if (target == null)
        {
            isSubscribedToTarget = false;
            return;
        }

        if (!isSubscribedToTarget)
            return;

        target.OnEnergyChanged -= OnEnergyChanged;
        isSubscribedToTarget = false;
    }

    private void OnEnergyChanged()
    {
        if (target == null)
            return;

        latestTargetFill = Mathf.Clamp01(target.EnergyNormalized);

        if (mainFillImage != null)
            mainFillImage.fillAmount = latestTargetFill;

        UpdateEnergyText();

        if (lagFillImage == null)
            return;

        if (lagFillImage.fillAmount <= latestTargetFill)
        {
            lagFillImage.fillAmount = latestTargetFill;
            delayAlreadyPassed = false;
            return;
        }

        if (lagRoutine == null)
        {
            lagRoutine = StartCoroutine(LagChaseLoop());
        }
    }

    private IEnumerator LagChaseLoop()
    {
        if (!delayAlreadyPassed && lagDelay > 0f)
        {
            yield return new WaitForSeconds(lagDelay);
            delayAlreadyPassed = true;
        }

        while (lagFillImage != null)
        {
            if (lagFillImage.fillAmount <= latestTargetFill)
            {
                lagFillImage.fillAmount = latestTargetFill;
                break;
            }

            lagFillImage.fillAmount = Mathf.MoveTowards(
                lagFillImage.fillAmount,
                latestTargetFill,
                lagCatchUpSpeed * Time.deltaTime
            );

            yield return null;
        }

        lagRoutine = null;
        delayAlreadyPassed = false;
    }

    private void ForceRefreshImmediate()
    {
        if (target == null)
            return;

        float fill = Mathf.Clamp01(target.EnergyNormalized);

        if (mainFillImage != null)
            mainFillImage.fillAmount = fill;

        if (lagFillImage != null)
            lagFillImage.fillAmount = fill;

        latestTargetFill = fill;
        delayAlreadyPassed = false;

        UpdateEnergyText();
    }

    private void UpdateEnergyText()
    {
        if (target == null || energyText == null)
            return;

        int current = Mathf.RoundToInt(target.CurrentEnergy);
        int max = Mathf.RoundToInt(target.MaxEnergy);

        energyText.text = $"{current}/{max}";
    }

    private void StopLagRoutine()
    {
        if (lagRoutine == null)
            return;

        StopCoroutine(lagRoutine);
        lagRoutine = null;
    }
}