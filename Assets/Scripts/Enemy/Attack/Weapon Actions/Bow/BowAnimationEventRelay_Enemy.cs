using UnityEngine;

public class BowAnimationEventRelay_Enemy : MonoBehaviour
{
    [Header("Bow Skills Enemy")]
    public Enemy_Bow_QuickShot quickShot;
    public Enemy_Bow_FullDraw fullDraw;
    public Enemy_Bow_PiercingShot piercingShot;
    public Enemy_Bow_ConcussiveShot concussiveShot;

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

    private void AutoAssign()
    {
        EnemyCombatController combat = GetComponentInParent<EnemyCombatController>();

        if (combat != null)
        {
            if (quickShot == null)
                quickShot = combat.quickShotBow;

            if (fullDraw == null)
                fullDraw = combat.fullDrawBow;

            if (piercingShot == null)
                piercingShot = combat.piercingBow;

            if (concussiveShot == null)
                concussiveShot = combat.concussiveBow;
        }

        if (quickShot == null)
            quickShot = GetComponentInParent<Enemy_Bow_QuickShot>(true);

        if (fullDraw == null)
            fullDraw = GetComponentInParent<Enemy_Bow_FullDraw>(true);

        if (piercingShot == null)
            piercingShot = GetComponentInParent<Enemy_Bow_PiercingShot>(true);

        if (concussiveShot == null)
            concussiveShot = GetComponentInParent<Enemy_Bow_ConcussiveShot>(true);
    }

    public void AE_QuickShotRelease()
    {
        if (quickShot != null)
            quickShot.ReleaseFromAnimationEvent();
    }

    public void AE_FullDrawRelease()
    {
        if (fullDraw != null)
            fullDraw.ReleaseFromAnimationEvent();
    }

    public void AE_PiercingRelease()
    {
        if (piercingShot != null)
            piercingShot.ReleaseFromAnimationEvent();
    }

    public void AE_ConcussiveRelease()
    {
        if (concussiveShot != null)
            concussiveShot.ReleaseFromAnimationEvent();
    }
}