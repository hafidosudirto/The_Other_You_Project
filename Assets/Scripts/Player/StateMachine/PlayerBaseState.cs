/// <summary>
/// Base class abstrak untuk seluruh state pemain.
/// Menyimpan referensi ke context (PlayerStateController) dan referensi induk
/// (Parent) agar hierarki HFSM 2 level dapat dibentuk dan dilacak.
///
/// FullPath menghasilkan jalur lengkap state, mis. "Locomotion/Move" atau
/// "Interrupt/Staggered", yang berguna untuk debug Inspector dan bukti akademik.
///
/// Implementasi Enter/Tick/Exit di sini sengaja kosong (virtual) karena pada
/// Fase 1 state bersifat observasional. Behavior nyata diisi pada fase migrasi
/// kendali, bukan sekarang.
/// </summary>
public abstract class PlayerBaseState : IPlayerState
{
    protected readonly PlayerStateController context;

    /// <summary>State induk dalam hierarki. Null berarti state tingkat atas.</summary>
    public PlayerBaseState Parent { get; protected set; }

    protected PlayerBaseState(PlayerStateController context, PlayerBaseState parent = null)
    {
        this.context = context;
        Parent = parent;
    }

    public abstract string Name { get; }

    /// <summary>Jalur lengkap state termasuk induk, mis. "Locomotion/Idle".</summary>
    public string FullPath => Parent != null ? Parent.FullPath + "/" + Name : Name;

    public virtual void Enter() { }
    public virtual void Tick() { }
    public virtual void Exit() { }
}
