using UnityEngine;

public class BowAnimationEventRelay : MonoBehaviour
{
    [Header("Bow Skills")]
    public Bow_QuickShot quickShot;
    public Bow_FullDraw fullDraw;
    public Bow_PiercingShot piercingShot;
    public Bow_ConcussiveShot concussiveShot;

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