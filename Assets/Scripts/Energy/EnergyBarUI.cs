using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EnergyBarUI : MonoBehaviour
{
    [Header("Target (CharacterBase)")]
    [SerializeField] private CharacterBase target;

    [Header("UI Images")]
    [SerializeField] private Image mainFillImage; // berubah cepat (nilai aktual)
    [SerializeField] private Image lagFillImage;  // mengejar (lag effect)

    [Header("Lag Settings (only when decreasing)")]
    [Min(0f)][SerializeField] private float lagCatchUpSpeed = 2f;
    [Min(0f)][SerializeField] private float lagDelay = 0.2f;

    private Coroutine lagRoutine;
    private float latestTargetFill = 1f;
    private bool delayAlreadyPassed = false;

    private void Awake()
    {
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
        if (target == null) return;

        latestTargetFill = Mathf.Clamp01(target.EnergyNormalized);

        // Main selalu mengikuti nilai aktual
        if (mainFillImage != null)
            mainFillImage.fillAmount = latestTargetFill;

        if (lagFillImage == null) return;

        // Jika energi naik, lag langsung ikut (agar tidak “lag kebalikan”)
        if (lagFillImage.fillAmount <= latestTargetFill)
        {
            lagFillImage.fillAmount = latestTargetFill;
            delayAlreadyPassed = false; // reset logika lag untuk penurunan berikutnya
            return;
        }

        // Jika energi turun, jalankan 1 coroutine yang selalu mengejar target terbaru
        if (lagRoutine == null)
        {
            lagRoutine = StartCoroutine(LagChaseLoop());
        }
    }

    private IEnumerator LagChaseLoop()
    {
        // Delay hanya sekali untuk satu rangkaian penurunan
        if (!delayAlreadyPassed && lagDelay > 0f)
        {
            yield return new WaitForSeconds(lagDelay);
            delayAlreadyPassed = true;
        }

        while (lagFillImage != null)
        {
            // Jika sudah menyamai target, hentikan dan tunggu event berikutnya
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
        if (target == null) return;

        float fill = Mathf.Clamp01(target.EnergyNormalized);

        if (mainFillImage != null)
            mainFillImage.fillAmount = fill;

        if (lagFillImage != null)
            lagFillImage.fillAmount = fill;

        latestTargetFill = fill;
        delayAlreadyPassed = false;
    }
}