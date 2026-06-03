using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnemyAnimation : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Animator yang berada pada object Visual. Jika kosong, script akan mencari Animator pada GameObject ini atau child-nya.")]
    public Animator animator;

    [Tooltip("SpriteRenderer visual enemy. Dipakai untuk flip arah karakter.")]
    public SpriteRenderer spriteRenderer;

    [Header("Debug")]
    [Tooltip("Aktifkan hanya saat debugging parameter Animator. Jika aktif, parameter yang tidak ditemukan akan ditulis ke Console.")]
    [SerializeField] private bool logMissingParameters = false;

    [Tooltip("Jika aktif, semua trigger bow dibersihkan ketika object aktif. Ini membantu mencegah trigger lama tersisa saat prefab enemy aktif kembali.")]
    [SerializeField] private bool resetBowTriggersOnEnable = true;

    [Tooltip("Jika aktif, status charging bow dimatikan ketika object aktif. Ini mencegah Animator mulai dari kondisi charge yang tersisa.")]
    [SerializeField] private bool clearChargeFlagsOnEnable = true;

    [Header("Sword Combo")]
    [Tooltip("Jika aktif, pemanggilan PlaySlash1() pada enemy dianggap sebagai awal combo: Slash1 lalu Slash2.")]
    [SerializeField] private bool playSlash1AsFullCombo = true;

    [Tooltip("Jika aktif, EndCurrentAction() pada akhir Slash1 akan melanjutkan combo ke Slash2, bukan langsung mengakhiri aksi enemy.")]
    [SerializeField] private bool useEndCurrentActionAsComboBridge = true;

    [Tooltip("Cadangan jika Animation Event EndCurrentAction tidak dipasang pada akhir Slash1. Nilai 0 berarti tidak memakai fallback waktu.")]
    [SerializeField, Min(0f)] private float slash2TimedFallbackDelay = 0f;

    [Header("Sword Animator State Force")]
    [Tooltip("Jika aktif, script akan memaksa Animator memainkan state Slash1/Slash2 setelah trigger dikirim. Ini memperbaiki kasus suara keluar tetapi animasi tidak berpindah.")]
    [SerializeField] private bool forceSwordAnimationState = true;

    [Tooltip("Layer Animator untuk animasi sword. Umumnya 0 jika Animator enemy hanya memakai Base Layer.")]
    [SerializeField, Min(0)] private int swordLayerIndex = 0;

    [Tooltip("Nama state animasi Slash1 pada Animator enemy. Samakan dengan state milik player jika memungkinkan.")]
    [SerializeField] private string slash1StateName = "Slash1";

    [Tooltip("Nama state animasi Slash2 pada Animator enemy. Samakan dengan state milik player jika memungkinkan.")]
    [SerializeField] private string slash2StateName = "Slash2";

    [Tooltip("Durasi perpindahan paksa ke state sword. Nilai kecil membuat perpindahan cepat tanpa terlalu patah.")]
    [SerializeField, Min(0f)] private float swordCrossFadeDuration = 0.02f;

    // =========================
    // HASH PARAMETERS - BOW
    // Disamakan dengan PlayerAnimation.cs
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
    // Disamakan dengan PlayerAnimation.cs
    // =========================
    private static readonly int HashSlash1 = Animator.StringToHash("Slash1");
    private static readonly int HashSlash2 = Animator.StringToHash("Slash2");
    private static readonly int HashDash = Animator.StringToHash("Dash");
    private static readonly int HashWhirlwind = Animator.StringToHash("Whirlwind");
    private static readonly int HashRiposteReady = Animator.StringToHash("RiposteReady");
    private static readonly int HashRiposteCounter = Animator.StringToHash("RiposteCounter");

    private enum SlashComboState
    {
        None,
        WaitingForSlash2,
        PlayingSlash2
    }

    private SlashComboState slashComboState = SlashComboState.None;

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

        ResetSlashComboState();
    }

    private void OnDisable()
    {
        ResetSlashComboState();
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
            Debug.LogWarning($"[EnemyAnimation] Parameter Animator tidak ditemukan: {parameterName}", this);
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
    // Bagian parameter disamakan dengan PlayerAnimation.cs:
    // SetSlash1(true) -> ResetTrigger("Slash1") lalu SetTrigger("Slash1")
    // SetSlash2(true) -> ResetTrigger("Slash2") lalu SetTrigger("Slash2")
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
            PlaySlash1Internal(playSlash1AsFullCombo);
        }
        else
        {
            SafeResetTrigger(HashSlash1, "Slash1");

            if (slashComboState == SlashComboState.WaitingForSlash2)
                ResetSlashComboState();
        }
    }

    public void SetSlash2(bool value)
    {
        if (value)
        {
            PlaySlash2Internal(true);
        }
        else
        {
            SafeResetTrigger(HashSlash2, "Slash2");

            if (slashComboState == SlashComboState.PlayingSlash2)
                ResetSlashComboState();
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

    public void PlaySlashCombo()
    {
        PlaySlash1Internal(true);
    }

    public void ContinueSlashComboToSlash2()
    {
        if (slashComboState != SlashComboState.WaitingForSlash2)
            return;

        PlaySlash2Internal(true);
    }

    public void ResetSlashFlags()
    {
        ResetSlashComboState();
        SafeResetTrigger(HashSlash1, "Slash1");
        SafeResetTrigger(HashSlash2, "Slash2");
    }

    private void PlaySlash1Internal(bool asComboStart)
    {
        CancelInvoke(nameof(ContinueSlashComboToSlash2));

        slashComboState = asComboStart ? SlashComboState.WaitingForSlash2 : SlashComboState.None;

        // Disamakan dengan PlayerAnimation.cs:
        // reset trigger yang sama, lalu set trigger yang sama.
        SafeResetTrigger(HashSlash1, "Slash1");
        SafeSetTrigger(HashSlash1, "Slash1");

        // Tambahan khusus enemy:
        // Jika trigger tidak berhasil memindahkan Animator, state tetap dipaksa dimainkan.
        ForcePlaySwordState(slash1StateName);

        if (asComboStart && slash2TimedFallbackDelay > 0f)
            Invoke(nameof(ContinueSlashComboToSlash2), slash2TimedFallbackDelay);
    }

    private void PlaySlash2Internal(bool markAsComboSecondHit)
    {
        CancelInvoke(nameof(ContinueSlashComboToSlash2));

        slashComboState = markAsComboSecondHit ? SlashComboState.PlayingSlash2 : SlashComboState.None;

        // Disamakan dengan PlayerAnimation.cs:
        // reset trigger yang sama, lalu set trigger yang sama.
        SafeResetTrigger(HashSlash2, "Slash2");
        SafeSetTrigger(HashSlash2, "Slash2");

        // Perbaikan utama:
        // Suara Slash2 sudah keluar berarti fungsi ini terpanggil.
        // Jika animasi tetap tidak terlihat, Animator dipaksa masuk ke state Slash2.
        ForcePlaySwordState(slash2StateName);
    }

    private void ResetSlashComboState()
    {
        CancelInvoke(nameof(ContinueSlashComboToSlash2));
        slashComboState = SlashComboState.None;
    }

    private void ForcePlaySwordState(string stateName)
    {
        if (!forceSwordAnimationState)
            return;

        if (animator == null)
            return;

        if (animator.runtimeAnimatorController == null)
            return;

        if (string.IsNullOrWhiteSpace(stateName))
            return;

        if (swordLayerIndex < 0 || swordLayerIndex >= animator.layerCount)
        {
            if (logMissingParameters)
            {
                Debug.LogWarning(
                    $"[EnemyAnimation] Sword Layer Index {swordLayerIndex} tidak valid. Jumlah layer Animator: {animator.layerCount}.",
                    this
                );
            }

            return;
        }

        int stateHash;

        if (!TryGetAnimatorStateHash(stateName, out stateHash))
        {
            if (logMissingParameters)
            {
                Debug.LogWarning(
                    $"[EnemyAnimation] State Animator tidak ditemukan: {stateName}. " +
                    $"Pastikan nama state pada Animator enemy sama dengan nama state player, misalnya Slash1 dan Slash2.",
                    this
                );
            }

            return;
        }

        if (swordCrossFadeDuration > 0f)
            animator.CrossFadeInFixedTime(stateHash, swordCrossFadeDuration, swordLayerIndex, 0f);
        else
            animator.Play(stateHash, swordLayerIndex, 0f);
    }

    private bool TryGetAnimatorStateHash(string stateName, out int stateHash)
    {
        stateHash = 0;

        if (animator == null)
            return false;

        if (swordLayerIndex < 0 || swordLayerIndex >= animator.layerCount)
            return false;

        int shortNameHash = Animator.StringToHash(stateName);

        if (animator.HasState(swordLayerIndex, shortNameHash))
        {
            stateHash = shortNameHash;
            return true;
        }

        string layerName = animator.GetLayerName(swordLayerIndex);
        int fullPathHash = Animator.StringToHash(layerName + "." + stateName);

        if (animator.HasState(swordLayerIndex, fullPathHash))
        {
            stateHash = fullPathHash;
            return true;
        }

        return false;
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

    // =========================
    // ANIMATION EVENT FALLBACK - ENEMY ONLY
    // =========================
    public void EndCurrentAction()
    {
        if (useEndCurrentActionAsComboBridge && slashComboState == SlashComboState.WaitingForSlash2)
        {
            ContinueSlashComboToSlash2();
            return;
        }

        ResetSlashComboState();
        SendMessageUpwards("OnActionEnd", SendMessageOptions.DontRequireReceiver);
    }
}