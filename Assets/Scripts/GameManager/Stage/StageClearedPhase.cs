using UnityEngine;

/// <summary>
/// FASE FSM: StageCleared.
///
/// Stage sudah selesai (boss kalah). Fase ini hanya memantau apakah player sudah berjalan
/// cukup jauh ke kanan (melewati <see cref="StageManager.rightTransitionX"/>) untuk memicu
/// perpindahan ke stage berikutnya.
///
/// Catatan penting (fidelity): berbeda dengan fase lain, EnterPhase di sini TIDAK mereset
/// <c>stateEnterTime</c>. Ini menyamai perilaku versi lama, di mana <c>HandleBossDefeated()</c>
/// mengubah state menjadi StageCleared tanpa menyentuh stateEnterTime.
/// </summary>
public class StageClearedPhase : StagePhaseBase
{
    public override void EnterPhase()
    {
        // Sengaja kosong: tidak mereset stateEnterTime (sesuai perilaku asli).
    }

    public override void UpdatePhase()
    {
        // Pemeriksaan transisi stage (referensi player + posisi) tetap menjadi tanggung jawab
        // orchestrator karena melibatkan logika player/coroutine perpindahan stage.
        Manager.CheckStageTransition();
    }

    public override void ExitPhase() { }
}