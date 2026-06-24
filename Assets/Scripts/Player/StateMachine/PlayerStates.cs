// =============================================================================
// Kumpulan state konkret HFSM pemain.
// Hierarki 2 level (super-state -> sub-state):
//
//   Locomotion (super) -> Idle, Move
//   Combat     (super) -> Sword, Bow
//   Interrupt  (super) -> Staggered, Dead
//
// Semua state bersifat read-only/observasional: tidak ada yang menulis flag,
// velocity, HP, atau energy. State hanya merepresentasikan kondisi pemain yang
// dibaca dari flag yang sudah ada (lockMovement, isAttacking, EnergyRegenBlocked,
// isStaggered, currentHP, kecepatan Rigidbody2D). Sengaja digabung dalam satu
// file agar jumlah file tetap secukupnya.
// =============================================================================

#region Super States

/// <summary>Super-state: pemain hidup dan dalam kendali, tidak sedang beraksi.</summary>
public class LocomotionState : PlayerBaseState
{
    public LocomotionState(PlayerStateController context) : base(context, null) { }
    public override string Name => "Locomotion";
}

/// <summary>Super-state: pemain sedang terganggu (stagger atau mati).</summary>
public class InterruptState : PlayerBaseState
{
    public InterruptState(PlayerStateController context) : base(context, null) { }
    public override string Name => "Interrupt";
}

#endregion

#region Locomotion Sub States

/// <summary>Pemain diam (tidak ada gerak signifikan).</summary>
public class IdleState : PlayerBaseState
{
    public IdleState(PlayerStateController context, PlayerBaseState parent) : base(context, parent) { }
    public override string Name => "Idle";
}

/// <summary>Pemain bergerak (kecepatan di atas ambang).</summary>
public class MoveState : PlayerBaseState
{
    public MoveState(PlayerStateController context, PlayerBaseState parent) : base(context, parent) { }
    public override string Name => "Move";
}

/// <summary>
/// Pemain sedang dash/menghindar. Terdeteksi dari Dash.IsDashing. Dash memakai
/// MovePosition (velocity ~0) sehingga butuh sinyal khusus ini agar tidak salah
/// terbaca sebagai Idle.
/// </summary>
public class DashState : PlayerBaseState
{
    public DashState(PlayerStateController context, PlayerBaseState parent) : base(context, parent) { }
    public override string Name => "Dash";
}

#endregion

#region Combat States

/// <summary>
/// Super-state aksi tempur: salah satu dari lockMovement / isAttacking sedang
/// aktif (serangan sword, casting/release skill, atau charge Bow Full Draw).
/// Combat dipecah menjadi tiga sub-state observasional agar fase aksi terlihat
/// lebih detail dan mudah dijelaskan.
/// </summary>
public class CombatState : PlayerBaseState
{
    public CombatState(PlayerStateController context) : base(context, null) { }
    public override string Name => "Combat";
}

/// <summary>
/// Combat/Sword: pemain sedang beraksi dengan senjata Sword (Slash Combo,
/// Charged Strike, Whirlwind, Riposte). Terdeteksi saat kondisi Combat aktif
/// (lockMovement / isAttacking) DAN senjata aktif = Sword.
/// </summary>
public class CombatSwordState : PlayerBaseState
{
    public CombatSwordState(PlayerStateController context, PlayerBaseState parent) : base(context, parent) { }
    public override string Name => "Sword";
}

/// <summary>
/// Combat/Bow: pemain sedang beraksi dengan senjata Bow (Quick Shot, Full Draw,
/// Spread, dll). Terdeteksi saat kondisi Combat aktif DAN senjata aktif = Bow.
/// Fase charge Full Draw dilaporkan lewat pemicu transisi (EnergyRegenBlocked),
/// bukan state terpisah.
/// </summary>
public class CombatBowState : PlayerBaseState
{
    public CombatBowState(PlayerStateController context, PlayerBaseState parent) : base(context, parent) { }
    public override string Name => "Bow";
}

#endregion

#region Interrupt Sub States

/// <summary>Pemain ter-stagger (isStaggered == true).</summary>
public class StaggeredState : PlayerBaseState
{
    public StaggeredState(PlayerStateController context, PlayerBaseState parent) : base(context, parent) { }
    public override string Name => "Staggered";
}

/// <summary>Pemain mati (currentHP &lt;= 0).</summary>
public class DeadState : PlayerBaseState
{
    public DeadState(PlayerStateController context, PlayerBaseState parent) : base(context, parent) { }
    public override string Name => "Dead";
}

#endregion
