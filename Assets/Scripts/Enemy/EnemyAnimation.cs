using System.Collections.Generic;
using UnityEngine;

public class EnemyAnimation : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    private SpriteRenderer sr;
    private NodeManager nodeManager;

    [Header("Debug")]
    [SerializeField] private bool warnMissingAnimatorParameter = true;

    // =========================
    // HASH PARAMETERS
    // =========================
    private static readonly int HashMoveSpeed = Animator.StringToHash("MoveSpeed");

    // Bow
    private static readonly int HashQuickShot = Animator.StringToHash("QuickShot");
    private static readonly int HashIsCharging = Animator.StringToHash("IsCharging");
    private static readonly int HashChargeRelease = Animator.StringToHash("ChargeRelease");
    private static readonly int HashSpreadArrow = Animator.StringToHash("SpreadArrow");
    private static readonly int HashPiercing = Animator.StringToHash("Piercing");
    private static readonly int HashConcussive = Animator.StringToHash("Concussive");

    // Sword
    private static readonly int HashSlash1 = Animator.StringToHash("Slash1");
    private static readonly int HashSlash2 = Animator.StringToHash("Slash2");
    private static readonly int HashDash = Animator.StringToHash("Dash");
    private static readonly int HashWhirlwind = Animator.StringToHash("Whirlwind");
    private static readonly int HashRiposteReady = Animator.StringToHash("RiposteReady");
    private static readonly int HashRiposteCounter = Animator.StringToHash("RiposteCounter");

    private readonly HashSet<int> missingParameterWarnings = new HashSet<int>();

    private void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>(true);

        sr = GetComponentInChildren<SpriteRenderer>(true);
        nodeManager = GetComponentInParent<NodeManager>();

        if (sr == null)
            Debug.LogError($"[EnemyAnimation] SpriteRenderer tidak ditemukan di {gameObject.name}", this);

        if (animator == null)
            Debug.LogError($"[EnemyAnimation] Animator tidak ditemukan di {gameObject.name}", this);
    }

    public void SetMoveSpeed(float speed)
    {
        SetFloatIfExists(HashMoveSpeed, "MoveSpeed", speed);
    }

    public void SetFlip(bool flip)
    {
        if (sr != null)
            sr.flipX = flip;
    }

    // =========================
    // BOW
    // =========================

    public void PlayQuickShot()
    {
        if (animator == null)
            return;

        ResetAllTriggers();
        SetTriggerIfExists(HashQuickShot, "QuickShot");
    }

    public void TriggerBowChargeStart()
    {
        if (animator == null)
            return;

        ResetTriggerIfExists(HashChargeRelease, "ChargeRelease");
        SetBoolIfExists(HashIsCharging, "IsCharging", true);
    }

    public void TriggerBowChargeRelease()
    {
        if (animator == null)
            return;

        SetBoolIfExists(HashIsCharging, "IsCharging", false);
        SetTriggerIfExists(HashChargeRelease, "ChargeRelease");
    }

    public void PlaySpreadArrow()
    {
        if (animator == null)
            return;

        ResetAllTriggers();
        SetTriggerIfExists(HashSpreadArrow, "SpreadArrow");
    }

    public void PlayPiercingShot()
    {
        if (animator == null)
            return;

        ResetAllTriggers();
        SetTriggerIfExists(HashPiercing, "Piercing");
    }

    public void PlayConcussiveShot()
    {
        if (animator == null)
            return;

        ResetAllTriggers();
        SetTriggerIfExists(HashConcussive, "Concussive");
    }

    // =========================
    // SWORD
    // =========================

    public void SetCharging(bool value)
    {
        SetBoolIfExists(HashIsCharging, "IsCharging", value);
    }

    public void PlayDash()
    {
        if (animator == null)
            return;

        ResetAllTriggers();
        SetTriggerIfExists(HashDash, "Dash");
    }

    public void PlaySlash1()
    {
        if (animator == null)
            return;

        ResetAllTriggers();
        SetTriggerIfExists(HashSlash1, "Slash1");
    }

    public void PlaySlash2()
    {
        if (animator == null)
            return;

        ResetAllTriggers();
        SetTriggerIfExists(HashSlash2, "Slash2");
    }

    public void PlayWhirlwind()
    {
        if (animator == null)
            return;

        ResetAllTriggers();
        SetTriggerIfExists(HashWhirlwind, "Whirlwind");
    }

    public void SetRiposteReady(bool isReady)
    {
        SetBoolIfExists(HashRiposteReady, "RiposteReady", isReady);
    }

    public void TriggerRiposteCounter()
    {
        if (animator == null)
            return;

        ResetAllTriggers();
        SetTriggerIfExists(HashRiposteCounter, "RiposteCounter");
    }

    // =========================
    // UTILITY & ANIMATION EVENTS
    // =========================

    private void ResetAllTriggers()
    {
        if (animator == null)
            return;

        ResetTriggerIfExists(HashQuickShot, "QuickShot");
        ResetTriggerIfExists(HashChargeRelease, "ChargeRelease");
        ResetTriggerIfExists(HashSpreadArrow, "SpreadArrow");
        ResetTriggerIfExists(HashPiercing, "Piercing");
        ResetTriggerIfExists(HashConcussive, "Concussive");

        ResetTriggerIfExists(HashSlash1, "Slash1");
        ResetTriggerIfExists(HashSlash2, "Slash2");
        ResetTriggerIfExists(HashDash, "Dash");
        ResetTriggerIfExists(HashWhirlwind, "Whirlwind");
        ResetTriggerIfExists(HashRiposteCounter, "RiposteCounter");
    }

    public void EndCurrentAction()
    {
        if (nodeManager != null)
            nodeManager.OnActionEnd();
    }

    // =========================
    // SAFE ANIMATOR PARAMETER API
    // =========================

    private void SetFloatIfExists(int hash, string parameterName, float value)
    {
        if (animator == null)
            return;

        if (!HasParameter(hash, AnimatorControllerParameterType.Float))
        {
            WarnMissingParameter(hash, parameterName, "Float");
            return;
        }

        animator.SetFloat(hash, value);
    }

    private void SetBoolIfExists(int hash, string parameterName, bool value)
    {
        if (animator == null)
            return;

        if (!HasParameter(hash, AnimatorControllerParameterType.Bool))
        {
            WarnMissingParameter(hash, parameterName, "Bool");
            return;
        }

        animator.SetBool(hash, value);
    }

    private void SetTriggerIfExists(int hash, string parameterName)
    {
        if (animator == null)
            return;

        if (!HasParameter(hash, AnimatorControllerParameterType.Trigger))
        {
            WarnMissingParameter(hash, parameterName, "Trigger");
            return;
        }

        animator.SetTrigger(hash);
    }

    private void ResetTriggerIfExists(int hash, string parameterName)
    {
        if (animator == null)
            return;

        if (!HasParameter(hash, AnimatorControllerParameterType.Trigger))
            return;

        animator.ResetTrigger(hash);
    }

    private bool HasParameter(int hash, AnimatorControllerParameterType expectedType)
    {
        if (animator == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;

        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];

            if (parameter.nameHash == hash && parameter.type == expectedType)
                return true;
        }

        return false;
    }

    private void WarnMissingParameter(int hash, string parameterName, string expectedType)
    {
        if (!warnMissingAnimatorParameter)
            return;

        if (missingParameterWarnings.Contains(hash))
            return;

        missingParameterWarnings.Add(hash);

        Debug.LogWarning(
            $"[EnemyAnimation] Animator '{animator.runtimeAnimatorController?.name}' tidak memiliki parameter {expectedType} bernama '{parameterName}'. " +
            $"Tambahkan parameter ini di Animator Controller atau sesuaikan nama parameternya.",
            this
        );
    }
}