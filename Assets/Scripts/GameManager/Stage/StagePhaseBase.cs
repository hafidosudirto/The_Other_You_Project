using UnityEngine;

/// <summary>
/// Abstract base untuk seluruh fase (state) permainan dalam Finite State Machine StageManager.
///
/// Pola yang dipakai: State Pattern. Setiap turunan (SpawningMinionsPhase, FightingMinionsPhase,
/// FightingBossPhase, StageClearedPhase) mengenkapsulasi logika satu state. <see cref="StageManager"/>
/// bertindak sebagai "context" yang menyimpan referensi global dan menyalakan/mematikan fase.
///
/// Fase adalah plain C# object (BUKAN MonoBehaviour). Untuk menjalankan coroutine,
/// fase memakai <c>Manager.StartCoroutine(...)</c> karena StageManager adalah MonoBehaviour.
/// </summary>
public abstract class StagePhaseBase
{
    /// <summary>Context/orchestrator pemilik fase ini.</summary>
    protected StageManager Manager { get; private set; }

    /// <summary>Modul komposisi untuk stat & attack token.</summary>
    protected StageStatManager Stats => Manager != null ? Manager.Stats : null;

    /// <summary>Modul komposisi untuk visual debug (OnGUI/label dunia).</summary>
    protected StageVisualDebug Visual => Manager != null ? Manager.Visual : null;

    /// <summary>
    /// Dipanggil sekali ketika FSM membangun (atau membangun ulang) daftar fase.
    /// Menyuntikkan referensi context. Override boleh memanggil base lalu menyiapkan field.
    /// </summary>
    public virtual void Initialize(StageManager manager)
    {
        Manager = manager;
    }

    /// <summary>Dipanggil tepat saat fase ini menjadi aktif (transisi masuk).</summary>
    public abstract void EnterPhase();

    /// <summary>Dipanggil tiap frame dari <c>StageManager.Update()</c> selama fase ini aktif.</summary>
    public abstract void UpdatePhase();

    /// <summary>Dipanggil tepat sebelum fase ini ditinggalkan (transisi keluar).</summary>
    public abstract void ExitPhase();

    /// <summary>
    /// Dipanggil oleh <c>StageManager.OnEnemyDied()</c> SETELAH proses umum
    /// (decrement counter, log, dan grace-period check) lolos. Default no-op.
    /// Hanya fase pertarungan (minion/boss) yang meng-override.
    /// </summary>
    public virtual void OnEnemyDied() { }
}
