using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerAnimation : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Animator yang berada pada object Visual. Jika kosong, script akan mencari Animator pada GameObject ini atau child-nya.")]
    public Animator animator;

    [Tooltip("SpriteRenderer visual player. Dipakai untuk flip arah karakter.")]
    public SpriteRenderer spriteRenderer;

    [Header("Debug")]
    [Tooltip("Aktifkan hanya saat debugging parameter Animator. Jika aktif, parameter yang tidak ditemukan akan ditulis ke Console.")]
    [SerializeField] private bool logMissingParameters = false;

    [Tooltip("Jika aktif, semua trigger bow dibersihkan ketika object aktif. Ini membantu mencegah trigger lama tersisa saat Play.")]
    [SerializeField] private bool resetBowTriggersOnEnable = true;

    [Tooltip("Jika aktif, status charging bow dimatikan ketika object aktif. Ini mencegah Animator mulai dari kondisi charge yang tersisa.")]
    [SerializeField] private bool clearChargeFlagsOnEnable = true;

    // =========================
    // HASH PARAMETERS - BOW
    // =========================
    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");
    private static readonly int HashQuickShot = Animator.StringToHash("QuickShot");
    private static readonly int HashSpreadArrow = Animator.StringToHash("SpreadArrow");
    private static readonly int HashConcussive = Animator.StringToHash("Concussive");

    // Masih dipertahankan untuk sistem Full Draw.
    private static readonly int HashIsCharging = Animator.StringToHash("IsCharging");
    private static readonly int HashChargeRelease = Animator.StringToHash("ChargeRelease");
    private static readonly int HashIsFullCharge = Animator.StringToHash("isFullCharge");

    // Opsional. Dipakai hanya jika Bow_PiercingShot standalone benar-benar dipakai lagi.
    private static readonly int HashPiercing = Animator.StringToHash("Piercing");

    // =========================
    // HASH PARAMETERS - SWORD / LEGACY
    // =========================
    private static readonly int HashSlash1 = Animator.StringToHash("Slash1");
    private static readonly int HashSlash2 = Animator.StringToHash("Slash2");
    private static readonly int HashDash = Animator.StringToHash("Dash");
    private static readonly int HashWhirlwind = Animator.StringToHash("Whirlwind");
    private static readonly int HashRiposteReady = Animator.StringToHash("RiposteReady");
    private static readonly int HashRiposteCounter = Animator.StringToHash("RiposteCounter");

    private readonly HashSet<int> availableParameters = new HashSet<int>();
    private readonly HashSet<int> warnedMissingParameters = new HashSet<int>();

    private void Awake()
    {
        AutoAssignReferences();
        RefreshAnimatorParameterCache();
    }

    private void OnEnable()
    {
        AutoAssignReferences();
        RefreshAnimatorParameterCache();

        if (resetBowTriggersOnEnable)
            ResetBowActionTriggers();

        if (clearChargeFlagsOnEnable)
            ClearBowChargeFlags();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssignReferences();
    }
#endif

    [ContextMenu("Auto Assign References")]
    public void AutoAssignReferences()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
    }

    [ContextMenu("Refresh Animator Parameter Cache")]
    public void RefreshAnimatorParameterCache()
    {
        availableParameters.Clear();
        warnedMissingParameters.Clear();

        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
            availableParameters.Add(parameter.nameHash);
    }

    private bool HasParameter(int hash, string parameterName)
    {
        if (animator == null)
            return false;

        if (availableParameters.Count == 0)
            RefreshAnimatorParameterCache();

        bool exists = availableParameters.Contains(hash);

        if (!exists && logMissingParameters && !warnedMissingParameters.Contains(hash))
        {
            warnedMissingParameters.Add(hash);
            Debug.LogWarning($"[PlayerAnimation] Parameter Animator tidak ditemukan: {parameterName}", this);
        }

        return exists;
    }

    private void SafeSetTrigger(int hash, string parameterName)
    {
        if (animator != null && HasParameter(hash, parameterName))
            animator.SetTrigger(hash);
    }

    private void SafeResetTrigger(int hash, string parameterName)
    {
        if (animator != null && HasParameter(hash, parameterName))
            animator.ResetTrigger(hash);
    }

    private void SafeSetBool(int hash, string parameterName, bool value)
    {
        if (animator != null && HasParameter(hash, parameterName))
            animator.SetBool(hash, value);
    }

    private void SafeSetFloat(int hash, string parameterName, float value)
    {
        if (animator != null && HasParameter(hash, parameterName))
            animator.SetFloat(hash, value);
    }

    // =========================
    // MOVEMENT
    // =========================
    public void SetMoveSpeed(float speed)
    {
        SafeSetFloat(HashMoveSpeed, "MoveSpeed", speed);
    }

    public void SetFlip(bool flip)
    {
        if (spriteRenderer != null)
            spriteRenderer.flipX = flip;
    }

    // =========================
    // BOW - ONE SHOT ACTIONS
    // =========================
    public void PlayQuickShot()
    {
        PrepareBowOneShotAction();
        SafeSetTrigger(HashQuickShot, "QuickShot");
    }

    public void PlaySpreadArrow()
    {
        PrepareBowOneShotAction();
        SafeSetTrigger(HashSpreadArrow, "SpreadArrow");
    }

    public void PlayConcussiveShot()
    {
        PrepareBowOneShotAction();
        SafeSetTrigger(HashConcussive, "Concussive");
    }

    // Dipertahankan sebagai kompatibilitas jika Bow_PiercingShot standalone masih dipakai.
    // Jika parameter Piercing sudah dihapus dari Animator, fungsi ini tidak akan menimbulkan error.
    public void PlayPiercingShot()
    {
        PrepareBowOneShotAction();
        SafeSetTrigger(HashPiercing, "Piercing");
    }

    private void PrepareBowOneShotAction()
    {
        ResetBowActionTriggers();
        ClearBowChargeFlags();
    }

    // =========================
    // BOW - FULL DRAW CHARGE FLOW
    // =========================
    public void TriggerBowChargeStart()
    {
        ResetBowActionTriggers();
        SafeSetBool(HashIsCharging, "IsCharging", true);
        SafeSetBool(HashIsFullCharge, "isFullCharge", false);
    }

    public void SetBowFullCharge(bool value)
    {
        SafeSetBool(HashIsFullCharge, "isFullCharge", value);
    }

    public void TriggerBowChargeRelease()
    {
        SafeSetBool(HashIsCharging, "IsCharging", false);
        SafeResetTrigger(HashChargeRelease, "ChargeRelease");
        SafeSetTrigger(HashChargeRelease, "ChargeRelease");
    }

    public void TriggerBowChargeRelease(bool fullCharge)
    {
        SafeSetBool(HashIsFullCharge, "isFullCharge", fullCharge);
        TriggerBowChargeRelease();
    }

    public void ClearBowChargeFlags()
    {
        SafeSetBool(HashIsCharging, "IsCharging", false);
        SafeSetBool(HashIsFullCharge, "isFullCharge", false);
    }

    public void ResetBowActionTriggers()
    {
        SafeResetTrigger(HashQuickShot, "QuickShot");
        SafeResetTrigger(HashSpreadArrow, "SpreadArrow");
        SafeResetTrigger(HashConcussive, "Concussive");
        SafeResetTrigger(HashPiercing, "Piercing");
        SafeResetTrigger(HashChargeRelease, "ChargeRelease");
    }

    public void ResetBowToNeutral()
    {
        ResetBowActionTriggers();
        ClearBowChargeFlags();
        SetMoveSpeed(0f);
    }

    // =========================
    // KOMPATIBILITAS LAMA / SWORD
    // =========================
    public void SetCharging(bool value)
    {
        SafeSetBool(HashIsCharging, "IsCharging", value);
    }

    public void PlayDash()
    {
        SafeSetTrigger(HashDash, "Dash");
    }

    public void SetSlash1(bool value)
    {
        if (value)
        {
            SafeResetTrigger(HashSlash1, "Slash1");
            SafeSetTrigger(HashSlash1, "Slash1");
        }
        else
        {
            SafeResetTrigger(HashSlash1, "Slash1");
        }
    }

    public void SetSlash2(bool value)
    {
        if (value)
        {
            SafeResetTrigger(HashSlash2, "Slash2");
            SafeSetTrigger(HashSlash2, "Slash2");
        }
        else
        {
            SafeResetTrigger(HashSlash2, "Slash2");
        }
    }

    public void PlaySlash1()
    {
        SetSlash1(true);
    }

    public void PlaySlash2()
    {
        SetSlash2(true);
    }

    public void ResetSlashFlags()
    {
        SafeResetTrigger(HashSlash1, "Slash1");
        SafeResetTrigger(HashSlash2, "Slash2");
    }

    public void PlayWhirlwind()
    {
        SafeSetTrigger(HashWhirlwind, "Whirlwind");
    }

    public void SetRiposteReady(bool isReady)
    {
        SafeSetBool(HashRiposteReady, "RiposteReady", isReady);
    }

    public void TriggerRiposteCounter()
    {
        SafeResetTrigger(HashRiposteCounter, "RiposteCounter");
        SafeSetTrigger(HashRiposteCounter, "RiposteCounter");
    }
}