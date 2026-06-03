using System.Collections;
using UnityEngine;

/// <summary>
/// FASE FSM: SpawningMinions.
///
/// Bertanggung jawab memunculkan satu wave minion di awal stage. Logikanya identik dengan
/// <c>SpawnMinionWave()</c> pada StageManager versi lama:
/// <list type="number">
/// <item>Menunggu <see cref="StageManager.MinionSpawnDelayOnStageStart"/> detik (jika &gt; 0).</item>
/// <item>Menghitung total minion, batas attack token konkuren, dan stat multiplier stage ini.</item>
/// <item>Membaca playstyle DDA untuk menentukan rasio varian melee vs range.</item>
/// <item>Berpindah ke state FightingMinions, lalu men-spawn seluruh minion.</item>
/// <item>Jika tidak ada minion yang berhasil dibuat, langsung memicu transisi ke boss.</item>
/// </list>
/// </summary>
public class SpawningMinionsPhase : StagePhaseBase
{
    public override void EnterPhase()
    {
        // GRACE PERIOD: tandai waktu masuk state agar validasi awal tidak salah trigger.
        Manager.StateEnterTime = Time.time;

        if (Visual != null)
            Visual.AddFloatingDebugLine($"[STAGE] Mulai Stage {Manager.GetDisplayedStageNumber()}.");

        Manager.StartCoroutine(SpawnMinionWaveRoutine());
    }

    public override void UpdatePhase()
    {
        // Tidak ada validasi enemy/transisi selama masih dalam state SpawningMinions.
        // (Sama seperti versi lama: ValidateEnemyCount hanya jalan di FightingMinions/FightingBoss.)
    }

    public override void ExitPhase() { }

    private IEnumerator SpawnMinionWaveRoutine()
    {
        float spawnDelay = Manager.MinionSpawnDelayOnStageStart;

        if (spawnDelay > 0f)
        {
            Debug.Log(
                $"[STAGE MANAGER] Stage {Manager.GetDisplayedStageNumber()} dimulai. " +
                $"Menunggu {spawnDelay} detik sebelum spawn minion."
            );

            yield return new WaitForSeconds(spawnDelay);
        }

        int stageProgressionIndex = Manager.GetStageProgressionIndex();
        int totalMinions = Manager.CalculateTotalMinions(stageProgressionIndex);
        int minionAttackTokens = Stats.CalculateMinionAttackTokens(stageProgressionIndex);
        float statMultiplier = Stats.GetStatMultiplier(stageProgressionIndex);

        int meleeCount = 0;
        int rangeCount = 0;

        string playerPlaystyle = Manager.GetPlayerPlaystyleFromDDA();

        // Catat ringkasan stage untuk panel visual debug + atur kapasitas token konkuren.
        Stats.lastStageTotalMinions = totalMinions;
        Stats.lastStageMeleeCount = 0;
        Stats.lastStageRangeCount = 0;
        Stats.lastStageMinionAttackTokens = minionAttackTokens;
        Stats.ConfigureStageTokenLimit(minionAttackTokens);
        Stats.lastStageStatMultiplier = statMultiplier;
        Stats.lastStagePlaystyle = playerPlaystyle;

        if (playerPlaystyle == "OffensiveDominant")
        {
            meleeCount = Mathf.RoundToInt(totalMinions * Manager.dominantVariantRatio);
            rangeCount = totalMinions - meleeCount;
        }
        else if (playerPlaystyle == "DefensiveDominant")
        {
            rangeCount = Mathf.RoundToInt(totalMinions * Manager.dominantVariantRatio);
            meleeCount = totalMinions - rangeCount;
        }
        else
        {
            meleeCount = Mathf.RoundToInt(totalMinions * 0.5f);
            rangeCount = totalMinions - meleeCount;
        }

        Stats.lastStageMeleeCount = meleeCount;
        Stats.lastStageRangeCount = rangeCount;

        if (Visual != null)
        {
            Visual.AddFloatingDebugLine(
                $"[STAT] Stage {Manager.GetDisplayedStageNumber()} | Stat minion x{statMultiplier:0.00} " +
                $"(+{Stats.GetStatIncreasePercent(statMultiplier):0.#}%)."
            );
            Visual.AddFloatingDebugLine(
                $"[TOKEN] Batas minion menyerang bersamaan: {minionAttackTokens}."
            );
        }

        Debug.Log(
            $"Spawn: {totalMinions} Minions " +
            $"({meleeCount} Melee, {rangeCount} Range). " +
            $"Concurrent Attack Token Limit: {minionAttackTokens}. Stat Mult: {statMultiplier}x"
        );

        // Pindah ke state FightingMinions sebelum men-spawn (sama seperti versi lama).
        // ChangeState -> FightingMinionsPhase.EnterPhase() yang mereset stateEnterTime.
        Manager.ChangeState(StageManager.StageState.FightingMinions);

        int spawnedCount = 0;

        for (int i = 0; i < meleeCount; i++)
        {
            if (Manager.SpawnEnemy(Manager.meleeMinionPrefab, statMultiplier, minionAttackTokens))
                spawnedCount++;
        }

        for (int i = 0; i < rangeCount; i++)
        {
            if (Manager.SpawnEnemy(Manager.rangeMinionPrefab, statMultiplier, minionAttackTokens))
                spawnedCount++;
        }

        if (spawnedCount <= 0)
        {
            Debug.LogWarning(
                "[STAGE MANAGER] Tidak ada minion yang berhasil dibuat. " +
                "Boss akan langsung dicoba untuk dimunculkan."
            );

            Manager.MinionsPhase.TryStartBossTransition("Tidak ada minion yang berhasil dibuat.");
        }
    }
}