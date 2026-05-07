using UnityEngine;

[DisallowMultipleComponent]
public class BowAnimationEventRelay_Minion : MonoBehaviour
{
    [Header("Minion Bow Skill")]
    [SerializeField] private Enemy_Bow_QuickShot quickShot;

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

    [ContextMenu("Auto Assign Minion QuickShot")]
    private void AutoAssign()
    {
        if (quickShot != null)
            return;

        MinionRangedCombatController controller =
            GetComponentInParent<MinionRangedCombatController>(true);

        if (controller != null)
        {
            quickShot = controller.GetComponentInChildren<Enemy_Bow_QuickShot>(true);

            if (quickShot == null)
                quickShot = controller.transform.root.GetComponentInChildren<Enemy_Bow_QuickShot>(true);
        }

        if (quickShot == null)
            quickShot = GetComponentInParent<Enemy_Bow_QuickShot>(true);

        if (quickShot == null)
            quickShot = GetComponentInChildren<Enemy_Bow_QuickShot>(true);

        if (quickShot == null)
            quickShot = transform.root.GetComponentInChildren<Enemy_Bow_QuickShot>(true);
    }

    public void AE_BowQuickShot_Release()
    {
        ReleaseQuickShot(nameof(AE_BowQuickShot_Release));
    }

    public void AE_QuickShotRelease()
    {
        ReleaseQuickShot(nameof(AE_QuickShotRelease));
    }

    public void AE_QuickShot_Release()
    {
        ReleaseQuickShot(nameof(AE_QuickShot_Release));
    }

    private void ReleaseQuickShot(string eventName)
    {
        if (quickShot == null)
            AutoAssign();

        if (quickShot == null)
        {
            Debug.LogWarning(
                $"[BowAnimationEventRelay_Minion] {eventName} gagal. Enemy_Bow_QuickShot belum ditemukan.",
                this
            );
            return;
        }

        if (showDebug)
        {
            Debug.Log(
                $"[BowAnimationEventRelay_Minion] {eventName} terpanggil. Melepas QuickShot.",
                this
            );
        }

        quickShot.ReleaseFromAnimationEvent();
    }
}