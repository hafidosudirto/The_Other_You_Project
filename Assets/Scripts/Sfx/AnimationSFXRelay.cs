using UnityEngine;

public class AnimationSFXRelay : MonoBehaviour
{
    // Dipanggil lewat Animation Event di frame saat pedang mengayun
    public void PlaySlash1()
    {
        if (SFXManager.Instance == null) return;
        SFXManager.Instance.PlaySFX(SFXManager.Instance.swordSlash1);
    }

    public void PlaySlash2()
    {
        if (SFXManager.Instance == null) return;
        SFXManager.Instance.PlaySFX(SFXManager.Instance.swordSlash2);
    }

    public void PlayWhirlwind()
    {
        if (SFXManager.Instance == null) return;
        SFXManager.Instance.PlaySFX(SFXManager.Instance.swordWhirlwind);
    }

    // Dipanggil di frame awal saat tangan mulai menarik panah.
    // Memakai gate agar tidak menumpuk jika Animation Event terpanggil berulang selama charge.
    public void PlayBowDraw()
    {
        if (SFXManager.Instance == null) return;
        SFXManager.Instance.PlayBowDrawGuarded();
    }

    // Khusus concussive saat melompat
    public void PlayConcussiveJump()
    {
        if (SFXManager.Instance == null) return;
        SFXManager.Instance.PlaySFX(SFXManager.Instance.arrowLaunchCharged);
    }
}
