using UnityEngine;

public class SFXManager : MonoBehaviour
{
    public static SFXManager Instance;

    [Header("Audio Source")]
    public AudioSource sfxSource;

    [Header("Sword SFX")]
    public AudioClip swordSlash1; // pedang sing.mp3
    public AudioClip swordSlash2; // pedang sing 2.mp3
    public AudioClip swordCharge; // charged nya sword.mp3
    public AudioClip swordWhirlwind; // angin putar.mp3
    public AudioClip swordHit; // pedang kena hit.mp3

    [Header("Bow SFX")]
    public AudioClip bowDraw; // bow ditarik.mp3
    public AudioClip arrowLaunchNormal; // panah meluncur.mp3
    public AudioClip arrowLaunchCharged; // panah meluncur opsi 2.mp3
    public AudioClip concussiveLaunch; // concussive meluncur.mp3
    public AudioClip concussiveExplode; // concussive meledak.mp3
    public AudioClip arrowHitGround; // panah menancap.mp3

    [Header("Bow Draw Anti-Stack")]
    [Tooltip("Jeda kunci untuk mencegah suara bowDraw menumpuk ketika Animation Event terpanggil berulang pada satu proses charge.")]
    public float bowDrawAntiStackWindow = 1.25f;

    private float nextAllowedBowDrawTime = -999f;

    private void Awake()
    {
        if (Instance == null) { Instance = this; }
        else { Destroy(gameObject); }
    }

    // Fungsi untuk dipanggil dari script lain
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        if (sfxSource == null) return;

        sfxSource.PlayOneShot(clip);
    }

    public void ResetBowDrawGate()
    {
        nextAllowedBowDrawTime = -999f;
    }

    public void PlayBowDrawGuarded()
    {
        if (bowDraw == null) return;
        if (sfxSource == null) return;

        float lockWindow = Mathf.Max(0.05f, bowDrawAntiStackWindow);

        if (Time.time < nextAllowedBowDrawTime)
            return;

        nextAllowedBowDrawTime = Time.time + lockWindow;
        PlaySFX(bowDraw);
    }
}
