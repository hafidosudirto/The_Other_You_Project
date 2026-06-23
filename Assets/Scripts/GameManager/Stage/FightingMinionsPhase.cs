using UnityEngine;

/// <summary>
/// FASE FSM: FightingMinions.
///
/// Memantau pertarungan melawan wave minion dan memutuskan kapan transisi ke boss dilakukan.
/// Logika dipertahankan persis sama dengan StageManager versi lama:
/// <list type="bullet">
/// <item><see cref="UpdatePhase"/> menjalankan ValidateEnemyCount (cabang minion) lalu
/// CheckMinionWaveClearedFallback setiap frame.</item>
/// <item><see cref="OnEnemyDied"/> memakai DOUBLE-LOCK: <c>activeEnemiesCount</c> dan jumlah
/// objek fisik bertag Enemy di scene wajib sama-sama 0 sebelum boss dipanggil.</item>
/// <item><see cref="TryStartBossTransition"/> melepas seluruh token serangan lalu menyalakan
/// coroutine transisi boss yang dimiliki <see cref="FightingBossPhase"/>.</item>
/// </list>
/// </summary>
public class FightingMinionsPhase : StagePhaseBase
{
    public override void EnterPhase()
    {
        // GRACE PERIOD: setara baris "stateEnterTime = Time.time" saat SpawnMinionWave lama
        // beralih ke state FightingMinions, mencegah validasi/transisi palsu di frame awal.
        Manager.StateEnterTime = Time.time;
    }

    public override void UpdatePhase()
    {
        ValidateEnemyCount();
        CheckMinionWaveClearedFallback();
    }

    public override void ExitPhase() { }

    public override void OnEnemyDied()
    {
        // DOUBLE-LOCK FIX: && (DAN) memastikan objek fisik di scene juga wajib kosong.
        if (Manager.ActiveEnemiesCount <= 0 && Manager.CountAliveEnemyObjectsInScene() <= 0)
        {
            TryStartBossTransition("Semua minion terdeteksi kalah.");
        }
    }

    private void ValidateEnemyCount()
    {
        if (Manager.IsTransitioningToBoss)
            return;

        // GRACE PERIOD: Cegah validasi palsu di awal mula spawn.
        if (!Manager.IsPastGracePeriod())
            return;

        GameObject[] activeEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        int totalActive = activeEnemies.Length;

        // Pastikan kedua sisi terkonfirmasi 0 (Double-Lock).
        if (totalActive <= 0 && Manager.ActiveEnemiesCount <= 0)
        {
            TryStartBossTransition("ValidateEnemyCount mendeteksi semua minion sudah hilang.");
        }
    }

    private void CheckMinionWaveClearedFallback()
    {
        if (Manager.IsTransitioningToBoss || Manager.BossSpawnConfigurationFailed)
            return;

        if (!Manager.IsPastGracePeriod())
            return;

        int aliveEnemies = Manager.CountAliveEnemyObjectsInScene();

        // DOUBLE-LOCK FIX
        if (Manager.ActiveEnemiesCount <= 0 && aliveEnemies <= 0)
        {
            Manager.ActiveEnemiesCount = 0;
            TryStartBossTransition("Fallback scan mendeteksi semua minion telah kalah.");
        }
        else if (Manager.ActiveEnemiesCount <= 0 && aliveEnemies > 0)
        {
            // Resync hitungan jika terjadi 'phantom death' dari memori musuh sebelumnya.
            Manager.ActiveEnemiesCount = aliveEnemies;
        }
    }

    /// <summary>
    /// Memulai transisi ke boss. Dipanggil dari fase ini (ValidateEnemyCount/OnEnemyDied/
    /// CheckMinionWaveClearedFallback) maupun dari SpawningMinionsPhase ketika tidak ada
    /// minion yang berhasil dibuat.
    /// </summary>
    public void TryStartBossTransition(string reason)
    {
        if (Manager.CurrentState != StageManager.StageState.FightingMinions)
            return;

        if (Manager.IsTransitioningToBoss || Manager.BossSpawnConfigurationFailed)
            return;

        int aliveEnemies = Manager.CountAliveEnemyObjectsInScene();

        if (aliveEnemies > 0)
        {
            Manager.ActiveEnemiesCount = aliveEnemies;
            return;
        }

        Stats.ReleaseAllConcurrentAttackTokens("Transisi ke boss");
        Manager.ActiveEnemiesCount = 0;
        Manager.IsTransitioningToBoss = true;

        Debug.Log($"[STAGE MANAGER] Transisi ke boss dimulai. Alasan: {reason}");
        Manager.StartCoroutine(Manager.BossPhase.TransitionToBossRoutine());
    }
}