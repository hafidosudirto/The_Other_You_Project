using UnityEngine;

/// <summary>
/// Animation Event Relay khusus Minion Range.
///
/// Fungsi ini menerima Animation Event pada clip MinionRange_Attack
/// lalu meneruskannya ke MinionRange_Projectile.ReleaseFromAnimationEvent().
///
/// Pemakaian di prefab:
///   1. Letakkan komponen ini di child yang sama dengan SpriteRenderer
///      (atau GameObject tempat Animation Event dipanggil).
///   2. Pastikan MinionRange_Projectile ada di child dari MinionRangeController
///      (akan di-auto-assign saat Awake / OnValidate).
///   3. Pada Animation clip, tambahkan Events di frame release dengan
///      function name "AE_BowQuickShot_Release" (atau alias lain).
/// </summary>
[DisallowMultipleComponent]
public class BowAnimationEventRelay_MinionRange : MonoBehaviour
{
    [Header("Minion Range Projectile (auto-assigned)")]
    [SerializeField] private MinionRange_Projectile projectileSkill;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private void Awake()
    {
        AutoAssign();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            AutoAssign();
    }
#endif

    [ContextMenu("Auto Assign Minion Projectile")]
    private void AutoAssign()
    {
        if (projectileSkill != null)
            return;

        MinionRangeController controller =
            GetComponentInParent<MinionRangeController>(true);

        if (controller != null)
        {
            projectileSkill = controller.GetComponentInChildren<MinionRange_Projectile>(true);

            if (projectileSkill == null)
                projectileSkill = controller.transform.root.GetComponentInChildren<MinionRange_Projectile>(true);
        }

        if (projectileSkill == null)
            projectileSkill = GetComponentInParent<MinionRange_Projectile>(true);

        if (projectileSkill == null)
            projectileSkill = GetComponentInChildren<MinionRange_Projectile>(true);

        if (projectileSkill == null)
            projectileSkill = transform.root.GetComponentInChildren<MinionRange_Projectile>(true);
    }

    // Nama function ini dipanggil dari Animation Event.
    // Tambahkan beberapa alias agar fleksibel saat men-setup clip.
    public void AE_BowQuickShot_Release()
    {
        Release(nameof(AE_BowQuickShot_Release));
    }

    public void AE_QuickShotRelease()
    {
        Release(nameof(AE_QuickShotRelease));
    }

    public void AE_QuickShot_Release()
    {
        Release(nameof(AE_QuickShot_Release));
    }

    private void Release(string eventName)
    {
        if (projectileSkill == null)
            AutoAssign();

        if (projectileSkill == null)
        {
            Debug.LogWarning(
                $"[BowAnimationEventRelay_MinionRange] {eventName} gagal. " +
                "MinionRange_Projectile belum ditemukan.",
                this
            );
            return;
        }

        if (showDebug)
        {
            Debug.Log(
                $"[BowAnimationEventRelay_MinionRange] {eventName} terpanggil. Melepas Projectile.",
                this
            );
        }

        projectileSkill.ReleaseFromAnimationEvent();
    }
}