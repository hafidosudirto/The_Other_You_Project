using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnergyBarUI : MonoBehaviour
{
    [Header("Target (CharacterBase)")]
    [SerializeField] private CharacterBase target;

    [Header("UI Images")]
    [SerializeField] private Image mainFillImage; // nilai stamina aktual
    [SerializeField] private Image lagFillImage;  // efek lag ketika stamina berkurang

    [Header("UI Text")]
    [SerializeField] private TMP_Text energyText;

    [Header("Lag Settings (only when decreasing)")]
    [Min(0f)][SerializeField] private float lagCatchUpSpeed = 2f;
    [Min(0f)][SerializeField] private float lagDelay = 0.2f;

    private Coroutine lagRoutine;
    private float latestTargetFill = 1f;
    private bool delayAlreadyPassed = false;

    private void Awake()
    {
        if (target == null)
            target = FindObjectOfType<Player>();

        if (target == null)
            target = FindObjectOfType<CharacterBase>();
    }

    private void OnEnable()
    {
        if (target != null)
            target.OnEnergyChanged += OnEnergyChanged;

        ForceRefreshImmediate();
    }

    private void OnDisable()
    {
        if (target != null)
            target.OnEnergyChanged -= OnEnergyChanged;

        if (lagRoutine != null)
        {
            StopCoroutine(lagRoutine);
            lagRoutine = null;
        }

        delayAlreadyPassed = false;
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
}