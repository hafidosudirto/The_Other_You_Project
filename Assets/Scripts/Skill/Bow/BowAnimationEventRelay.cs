using UnityEngine;

[DisallowMultipleComponent]
public class BowAnimationEventRelay : MonoBehaviour
{
    [Header("Skill aktif dari SkillRoot_Bow")]
    public Bow_QuickShot quickShot;
    public Bow_FullDraw fullDraw;
    public Bow_ConcussiveShot concussiveShot;
    public Bow_SpreadArrow spreadArrow;

    [Header("Opsional: hanya kalau Piercing dipakai sebagai skill mandiri")]
    public Bow_PiercingShot piercingStandalone;

    [Header("Debug")]
    [SerializeField] private bool debugEvent = false;

    private void Awake()
    {
        AutoAssignFromPlayerRoot();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            AutoAssignFromPlayerRoot();
    }
#endif

    [ContextMenu("Auto Assign Bow Skills From Player Root")]
    public void AutoAssignFromPlayerRoot()
    {
        Transform root = transform.root;

        Player player = GetComponentInParent<Player>(true);
        if (player != null)
            root = player.transform;

        if (quickShot == null)
            quickShot = root.GetComponentInChildren<Bow_QuickShot>(true);

        if (fullDraw == null)
            fullDraw = root.GetComponentInChildren<Bow_FullDraw>(true);

        if (concussiveShot == null)
            concussiveShot = root.GetComponentInChildren<Bow_ConcussiveShot>(true);

        if (spreadArrow == null)
            spreadArrow = root.GetComponentInChildren<Bow_SpreadArrow>(true);

        if (piercingStandalone == null)
            piercingStandalone = root.GetComponentInChildren<Bow_PiercingShot>(true);
    }

    public void AE_BowQuickShot_Release()
    {
        LogEvent(nameof(AE_BowQuickShot_Release));

        if (quickShot != null)
            quickShot.ReleaseFromAnimationEvent();
        else
            Debug.LogWarning("[BowAnimationEventRelay] QuickShot belum diisi.", this);
    }

    public void AE_BowFullDraw_Release()
    {
        LogEvent(nameof(AE_BowFullDraw_Release));

        if (fullDraw != null)
            fullDraw.ReleaseFromAnimationEvent();
        else
            Debug.LogWarning("[BowAnimationEventRelay] FullDraw belum diisi.", this);
    }


    public void AE_BowConcussive_Release()
    {
        AutoAssignFromPlayerRoot();

        if (concussiveShot != null)
            concussiveShot.ReleaseFromAnimationEvent();
        else
            Debug.LogWarning("[BowAnimationEventRelay] Bow_ConcussiveShot belum terhubung.", this);
    }
    public void AE_BowConcussive_EndRecovery()
    {
        AutoAssignFromPlayerRoot();

        if (concussiveShot != null)
            concussiveShot.EndRecoveryFromAnimationEvent();
    }
    public void AE_BowConcussive_StartPop()
    {
        AutoAssignFromPlayerRoot();

        if (concussiveShot != null)
        {
            concussiveShot.StartPopFromAnimationEvent();
        }
        else
        {
            Debug.LogWarning("[BowAnimationEventRelay] Bow_ConcussiveShot belum terhubung untuk AE_BowConcussive_StartPop.", this);
        }
    }
    public void AE_BowSpreadArrow_StartBackDash()
    {
        LogEvent(nameof(AE_BowSpreadArrow_StartBackDash));

        if (spreadArrow != null)
            spreadArrow.StartBackDash();
        else
            Debug.LogWarning("[BowAnimationEventRelay] SpreadArrow belum diisi untuk StartBackDash.", this);
    }

    public void AE_BowSpreadArrow_Release()
    {
        LogEvent(nameof(AE_BowSpreadArrow_Release));

        if (spreadArrow != null)
            spreadArrow.ReleaseSpreadArrow();
        else
            Debug.LogWarning("[BowAnimationEventRelay] SpreadArrow belum diisi untuk Release.", this);
    }

    public void AE_BowSpreadArrow_EndRecovery()
    {
        LogEvent(nameof(AE_BowSpreadArrow_EndRecovery));

        if (spreadArrow != null)
            spreadArrow.EndSpreadArrowRecovery();
        else
            Debug.LogWarning("[BowAnimationEventRelay] SpreadArrow belum diisi untuk EndRecovery.", this);
    }

    public void AE_BowPiercingStandalone_Release()
    {
        LogEvent(nameof(AE_BowPiercingStandalone_Release));

        if (piercingStandalone != null)
            piercingStandalone.ReleaseFromAnimationEvent();
        else
            Debug.LogWarning("[BowAnimationEventRelay] Piercing standalone belum diisi.", this);
    }

    public void AE_BowPiercing_Release()
    {
        Debug.LogError(
            "[BowAnimationEventRelay] Event AE_BowPiercing_Release tidak boleh dipakai pada sistem baru. " +
            "Untuk Bow_Release_Piercing hasil charge, pakai AE_BowFullDraw_Release. " +
            "Kalau Piercing adalah skill mandiri, pakai AE_BowPiercingStandalone_Release.",
            this
        );
    }

    private void LogEvent(string eventName)
    {
        if (!debugEvent)
            return;

        Debug.Log($"[BowAnimationEventRelay] {eventName}", this);
    }
}