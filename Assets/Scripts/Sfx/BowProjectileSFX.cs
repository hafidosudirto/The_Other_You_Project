using UnityEngine;

public class BowProjectileSFX : MonoBehaviour
{
    private CharacterBase owner;
    private bool playGroundMissSfx = true;
    private bool playHitSfx = true;
    private bool impactSfxPlayed = false;

    public bool HasPlayedImpactSfx => impactSfxPlayed;

    public void Setup(CharacterBase projectileOwner, bool enableGroundMissSfx, bool enableHitSfx)
    {
        owner = projectileOwner;
        playGroundMissSfx = enableGroundMissSfx;
        playHitSfx = enableHitSfx;
        impactSfxPlayed = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleImpact(other);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision == null) return;
        HandleImpact(collision.collider);
    }

    private void HandleImpact(Collider2D other)
    {
        if (impactSfxPlayed) return;
        if (other == null) return;

        CharacterBase target = other.GetComponentInParent<CharacterBase>();

        if (target != null)
        {
            if (target == owner)
                return;

            impactSfxPlayed = true;

            if (playHitSfx)
                PlaySfx(SFXManager.Instance != null ? SFXManager.Instance.swordHit : null);

            return;
        }

        if (!playGroundMissSfx) return;
        if (other.isTrigger) return;

        impactSfxPlayed = true;
        PlaySfx(SFXManager.Instance != null ? SFXManager.Instance.arrowHitGround : null);
    }

    public void PlayGroundMissIfNotPlayed()
    {
        if (impactSfxPlayed) return;
        if (!playGroundMissSfx) return;

        impactSfxPlayed = true;
        PlaySfx(SFXManager.Instance != null ? SFXManager.Instance.arrowHitGround : null);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null) return;
        if (SFXManager.Instance == null) return;
        if (SFXManager.Instance.sfxSource == null) return;

        SFXManager.Instance.PlaySFX(clip);
    }
}
