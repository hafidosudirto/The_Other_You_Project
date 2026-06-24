using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Komponen penggerak HFSM pemain (Fase 1: observasional / read-only).
///
/// Controller ini MEMBACA flag dan kondisi yang sudah ada (lockMovement,
/// isAttacking, isStaggered, currentHP, kecepatan Rigidbody2D) lalu memilih
/// state HFSM yang sesuai. Controller TIDAK menulis flag/velocity/HP/energy
/// dan TIDAK memanggil skill/dash, sehingga behavior gameplay lama tidak berubah.
///
/// Aturan transisi dipusatkan di sini (centralized-transition HFSM) agar
/// fondasi tetap sederhana. Pasang komponen ini pada GameObject Player.
/// </summary>
[DisallowMultipleComponent]
public class PlayerStateController : MonoBehaviour
{
    [Header("Referensi (auto-isi di Awake bila kosong)")]
    [Tooltip("Player pemilik state. Biasanya komponen Player pada GameObject yang sama.")]
    [SerializeField] private Player player;

    [Tooltip("Sumber HP/stagger. Biasanya sama dengan Player (Player : CharacterBase).")]
    [SerializeField] private CharacterBase character;

    [Tooltip("Rigidbody2D pemain. Dipakai membaca kecepatan untuk membedakan Idle vs Move.")]
    [SerializeField] private Rigidbody2D body;

    [Tooltip("Komponen Dash pemain. Dipakai membaca IsDashing untuk state Locomotion/Dash.")]
    [SerializeField] private Dash dash;

    [Header("Tuning Observasi")]
    [Tooltip("Ambang kecepatan minimum agar pemain dianggap bergerak (Move).")]
    [SerializeField, Min(0f)] private float moveVelocityThreshold = 0.05f;

    [Header("Debug (read-only)")]
    [Tooltip("Jalur state aktif saat ini, mis. 'Locomotion/Move'. Hanya untuk observasi.")]
    [SerializeField] private string currentStateDebug = "(belum mulai)";

    [Tooltip("State sebelum transisi terakhir, mis. 'Locomotion/Move'.")]
    [SerializeField] private string previousStateDebug = "-";

    [Tooltip("Transisi terakhir, mis. 'Locomotion/Move -> Combat'.")]
    [SerializeField] private string lastTransitionDebug = "-";

    [Tooltip("Pemicu transisi terakhir, mis. 'skill sword aktif'.")]
    [SerializeField] private string lastTriggerDebug = "-";

    [Tooltip("Riwayat maksimum 6 transisi bermakna terakhir (Idle <-> Move difilter).")]
    [TextArea(3, 8)]
    [SerializeField] private string recentTransitionsDebug = "";

    private readonly PlayerStateMachine machine = new PlayerStateMachine();

    // Buffer riwayat transisi untuk recentTransitionsDebug (maksimum RecentCapacity baris).
    private const int RecentCapacity = 6;
    private readonly List<string> recentBuffer = new List<string>();

    // Cache state agar tidak alokasi tiap frame.
    private LocomotionState locomotion;
    private InterruptState interrupt;
    private IdleState idle;
    private MoveState move;
    private DashState dashState;
    private CombatState combat;
    private CombatSwordState combatSword;
    private CombatBowState combatBow;
    private StaggeredState staggered;
    private DeadState dead;

    /// <summary>Nama state aktif (leaf), mis. "Sword". Berguna untuk HUD.</summary>
    public string CurrentStateName =>
        machine.CurrentState != null ? machine.CurrentState.Name : string.Empty;

    /// <summary>Jalur lengkap state aktif, mis. "Combat/Sword".</summary>
    public string CurrentStatePath => currentStateDebug;

    /// <summary>State sebelum transisi terakhir, mis. "Locomotion/Move".</summary>
    public string PreviousStatePath => previousStateDebug;

    /// <summary>Transisi terakhir, mis. "Locomotion/Move -&gt; Combat/Sword".</summary>
    public string LastTransition => lastTransitionDebug;

    /// <summary>Pemicu transisi terakhir, mis. "skill sword aktif".</summary>
    public string LastTrigger => lastTriggerDebug;

    /// <summary>Riwayat (maks 6) transisi bermakna terakhir, dipisah newline.</summary>
    public string RecentTransitions => recentTransitionsDebug;

    private void Awake()
    {
        ResolveReferences();
        BuildStates();
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            player = GetComponent<Player>();
            if (player == null) player = GetComponentInParent<Player>();
            if (player == null) player = GetComponentInChildren<Player>(true);
        }

        if (character == null)
        {
            character = player != null ? player : GetComponent<CharacterBase>();
            if (character == null) character = GetComponentInParent<CharacterBase>();
            if (character == null) character = GetComponentInChildren<CharacterBase>(true);
        }

        if (body == null)
        {
            body = GetComponent<Rigidbody2D>();
            if (body == null) body = GetComponentInParent<Rigidbody2D>();
            if (body == null && character != null) body = character.GetComponent<Rigidbody2D>();
        }

        if (dash == null)
        {
            dash = GetComponent<Dash>();
            if (dash == null) dash = GetComponentInParent<Dash>();
            if (dash == null) dash = GetComponentInChildren<Dash>(true);
        }
    }

    private void BuildStates()
    {
        locomotion = new LocomotionState(this);
        interrupt  = new InterruptState(this);

        idle      = new IdleState(this, locomotion);
        move      = new MoveState(this, locomotion);
        dashState = new DashState(this, locomotion);

        combat      = new CombatState(this);
        combatSword = new CombatSwordState(this, combat);
        combatBow   = new CombatBowState(this, combat);

        staggered = new StaggeredState(this, interrupt);
        dead      = new DeadState(this, interrupt);

        machine.ChangeState(idle);

        currentStateDebug = idle.FullPath;
        previousStateDebug = "-";
        lastTransitionDebug = "-";
        lastTriggerDebug = "-";
        recentTransitionsDebug = "";
        recentBuffer.Clear();
    }

    private void Update()
    {
        string trigger;
        PlayerBaseState target = SelectTargetState(out trigger);

        if (target != null)
        {
            string newPath = target.FullPath;

            // Catat hanya saat benar-benar terjadi perpindahan state.
            if (newPath != currentStateDebug)
                RecordTransition(currentStateDebug, newPath, trigger);

            machine.ChangeState(target);
        }

        machine.Tick();
    }

    /// <summary>
    /// Menentukan state tujuan murni dari pembacaan kondisi (read-only),
    /// sekaligus melaporkan pemicunya lewat parameter out.
    /// Prioritas: Dead > Staggered > Combat(sub) > Dash > Move > Idle.
    /// </summary>
    private PlayerBaseState SelectTargetState(out string trigger)
    {
        if (character == null)
        {
            trigger = "character null (fallback)";
            return idle;
        }

        if (character.currentHP <= 0f)
        {
            trigger = "currentHP <= 0";
            return dead;
        }

        if (character.isStaggered)
        {
            trigger = "isStaggered == true";
            return staggered;
        }

        if (player != null && (player.lockMovement || player.isAttacking))
        {
            return SelectCombatSubState(out trigger);
        }

        if (dash != null && dash.IsDashing)
        {
            trigger = "sedang dash";
            return dashState;
        }

        if (IsMoving())
        {
            trigger = "velocity > threshold";
            return move;
        }

        trigger = "diam (default)";
        return idle;
    }

    /// <summary>
    /// Memilih sub-state Combat dari senjata aktif (observasional).
    /// Bow bila weaponType == Bow; selain itu Sword (mencakup Sword dan kasus
    /// langka tak bersenjata yang tetap memicu Combat). Fase charge Full Draw
    /// dilaporkan lewat teks pemicu, bukan state terpisah. Dipanggil hanya saat
    /// kondisi Combat sudah pasti aktif, jadi player dijamin tidak null.
    /// </summary>
    private PlayerBaseState SelectCombatSubState(out string trigger)
    {
        if (player.IsBow)
        {
            bool charging = character != null && character.EnergyRegenBlocked;
            trigger = charging ? "skill bow (charge Full Draw)" : "skill bow aktif";
            return combatBow;
        }

        trigger = "skill sword aktif";
        return combatSword;
    }

    /// <summary>
    /// Mencatat transisi ke field debug Inspector. Murni penulisan teks (read-only
    /// terhadap gameplay). currentStateDebug selalu di-update ke state aktif; transisi
    /// dari placeholder dan noise Idle &lt;-&gt; Move tidak masuk riwayat.
    /// </summary>
    private void RecordTransition(string oldPath, string newPath, string trigger)
    {
        currentStateDebug = newPath;

        // Transisi awal dari placeholder (mis. "(belum mulai)"/"(stopped)") tidak dicatat.
        if (IsPlaceholder(oldPath))
            return;

        previousStateDebug = oldPath;
        lastTransitionDebug = oldPath + " -> " + newPath;
        lastTriggerDebug = trigger;

        if (!IsLocomotionNoise(oldPath, newPath))
            PushRecent(lastTransitionDebug);
    }

    private void PushRecent(string line)
    {
        recentBuffer.Add(line);

        if (recentBuffer.Count > RecentCapacity)
            recentBuffer.RemoveAt(0);

        recentTransitionsDebug = string.Join("\n", recentBuffer);
    }

    // Filter noise: abaikan perpindahan Locomotion/Idle <-> Locomotion/Move dari riwayat.
    private bool IsLocomotionNoise(string oldPath, string newPath)
    {
        return IsIdleOrMove(oldPath) && IsIdleOrMove(newPath);
    }

    private bool IsIdleOrMove(string path)
    {
        return path == "Locomotion/Idle" || path == "Locomotion/Move";
    }

    private bool IsPlaceholder(string path)
    {
        return string.IsNullOrEmpty(path)
            || path == "-"
            || path == "(belum mulai)"
            || path == "(stopped)";
    }

    private bool IsMoving()
    {
        if (body == null)
            return false;

        return body.velocity.sqrMagnitude > moveVelocityThreshold * moveVelocityThreshold;
    }

    private void OnDisable()
    {
        // Reset agar teks runtime tidak ikut tersimpan ke scene/prefab saat keluar Play Mode.
        currentStateDebug = "(stopped)";
        previousStateDebug = "-";
        lastTransitionDebug = "-";
        lastTriggerDebug = "-";
        recentTransitionsDebug = "";
        recentBuffer.Clear();
    }
}
