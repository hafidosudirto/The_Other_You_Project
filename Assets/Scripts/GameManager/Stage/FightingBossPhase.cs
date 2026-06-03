using System.Collections;
using UnityEngine;

/// <summary>
/// FASE FSM: FightingBoss.
///
/// Menangani seluruh siklus hidup pertarungan boss, termasuk SISTEM SENJATA ADAPTIF:
/// boss yang di-spawn mengikuti senjata dominan player yang dibaca dari DDA
/// (Bow -&gt; <c>bossPrefabBow</c>, selain itu -&gt; <c>bossPrefabSword</c>).
///
/// Fase ini meng-enkapsulasi runtime boss (<see cref="currentBossObject"/>,
/// <see cref="currentBossCharacter"/>, <see cref="bossDefeatHandled"/>) sehingga tidak ada lagi
/// state boss yang bocor ke orchestrator. State direset lewat <see cref="ResetBossRuntime"/>
/// pada awal tiap stage maupun saat StageManager membangun ulang FSM (fix bug Game Over).
/// </summary>
public class FightingBossPhase : StagePhaseBase
{
    // Runtime boss yang benar adalah instance hasil Instantiate, bukan prefab asset di Project.
    private GameObject currentBossObject;
    private CharacterBase currentBossCharacter;
    private bool bossDefeatHandled = false;

    public override void EnterPhase()
    {
        // Setara baris pada TransitionToBoss lama: stateEnterTime di-set dan isTransitioningToBoss
        // dimatikan tepat ketika state berpindah menjadi FightingBoss.
        Manager.StateEnterTime = Time.time;
        Manager.IsTransitioningToBoss = false;
    }

    public override void UpdatePhase()
    {
        ValidateEnemyCount();
        CheckBossDefeatedFallback();
    }

    public override void ExitPhase() { }

    public override void OnEnemyDied()
    {
        // Beberapa prefab boss hanya memanggil OnEnemyDied(), tetapi objeknya tidak langsung Destroy.
        // Karena itu, validasi utama tetap memakai HP/runtime boss yang sedang dilawan.
        if (IsCurrentBossDefeatedForProgression())
        {
            HandleBossDefeated();
        }
        else if (Manager.ActiveEnemiesCount <= 0)
        {
            // Pengaman jika event kematian minion lama terlambat masuk ketika boss sudah aktif.
            Manager.ActiveEnemiesCount = 1;
        }
    }

    /// <summary>Reset seluruh state runtime boss. Dipanggil saat memulai stage baru / fresh run.</summary>
    public void ResetBossRuntime()
    {
        currentBossObject = null;
        currentBossCharacter = null;
        bossDefeatHandled = false;
    }

    // ---------------------------------------------------------------------
    // TRANSISI & SPAWN BOSS (SISTEM SENJATA ADAPTIF)
    // ---------------------------------------------------------------------

    /// <summary>
    /// Coroutine transisi ke boss. Dinyalakan oleh <see cref="FightingMinionsPhase.TryStartBossTransition"/>.
    /// Membaca DDA (jika diaktifkan), memilih prefab boss sesuai senjata dominan, lalu meng-spawn
    /// dan menginisialisasi boss beserta stat amplifikasi stage.
    /// </summary>
    public IEnumerator TransitionToBossRoutine()
    {
        // FASE MINION -> membaca DDA SEBELUM finalisasi ke boss.
        Manager.FinalizeDDABeforeBossIfNeeded();

        yield return new WaitForSeconds(1f);

        string dominantWeapon = Manager.GetDominantWeaponFromDDA();
        GameObject bossToSpawn = dominantWeapon == "Bow" ? Manager.bossPrefabBow : Manager.bossPrefabSword;
        float statMultiplier = Stats.GetStatMultiplier(Manager.GetStageProgressionIndex());

        Debug.Log($"Boss membaca DDA: Menirukan senjata dominan player yaitu {dominantWeapon}");

        if (bossToSpawn == null)
        {
            Manager.BossSpawnConfigurationFailed = true;
            Manager.IsTransitioningToBoss = false;

            Manager.HideBossHPBar();

            Debug.LogError(
                "[STAGE MANAGER] Boss prefab belum diisi. " +
                "Isi bossPrefabSword dan bossPrefabBow di Inspector."
            );
            yield break;
        }

        Vector3 bSpawnPos = Manager.transform.position;
        if (Manager.bossSpawnPoint != null)
        {
            bSpawnPos = Manager.bossSpawnPoint.position;
        }

        GameObject boss = Object.Instantiate(bossToSpawn, bSpawnPos, Quaternion.identity);
        boss.tag = "Enemy";
        Manager.SetLayerRecursively(boss, LayerMask.NameToLayer("Enemy"));

        CharacterBase bossCharacter = boss.GetComponent<CharacterBase>();

        if (bossCharacter == null)
        {
            bossCharacter = boss.GetComponentInChildren<CharacterBase>(true);
        }

        currentBossObject = boss;
        currentBossCharacter = bossCharacter;
        bossDefeatHandled = false;
        Manager.ActiveEnemiesCount = 1;

        // ChangeState -> EnterPhase() men-set stateEnterTime dan isTransitioningToBoss = false.
        Manager.ChangeState(StageManager.StageState.FightingBoss);

        if (bossCharacter != null)
        {
            StageStatManager.StatsSnapshot beforeStats = Stats.CaptureStats(bossCharacter);
            Stats.InitializeCharacterBaseStats(bossCharacter, statMultiplier);
            StageStatManager.StatsSnapshot afterStats = Stats.CaptureStats(bossCharacter);

            Manager.InitializeStageCombatController(boss, bossCharacter, 999, true);
            Manager.InitializeDeathHandler(boss);
            Stats.RegisterRuntimeEnemyDebug(boss, bossCharacter, 999, statMultiplier, beforeStats, afterStats, true);

            Manager.ShowBossHPBar(bossToSpawn, boss, bossCharacter);
        }
        else
        {
            Debug.LogWarning("[STAGE MANAGER] Boss tidak memiliki CharacterBase atau turunannya.");
            Manager.InitializeDeathHandler(boss);
            Manager.HideBossHPBar();
        }

        Debug.Log(
            "[STAGE MANAGER] BOSS MUNCUL. " +
            $"Prefab: {boss.name} | DominantWeapon: {dominantWeapon} | ActiveCount: {Manager.ActiveEnemiesCount}"
        );
    }

    // ---------------------------------------------------------------------
    // VALIDASI & FALLBACK
    // ---------------------------------------------------------------------

    private void ValidateEnemyCount()
    {
        if (Manager.IsTransitioningToBoss)
            return;

        if (!Manager.IsPastGracePeriod())
            return;

        GameObject[] activeEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        int totalActive = activeEnemies.Length;

        if (totalActive <= 0 && !bossDefeatHandled)
        {
            HandleBossDefeated();
        }
    }

    private void CheckBossDefeatedFallback()
    {
        if (bossDefeatHandled)
            return;

        if (!Manager.IsPastGracePeriod())
            return;

        if (IsCurrentBossDefeatedForProgression())
        {
            Debug.LogWarning(
                "[STAGE MANAGER] Fallback mendeteksi boss sudah kalah. " +
                "Stage akan dipaksa menjadi StageCleared."
            );

            Manager.ActiveEnemiesCount = 0;
            HandleBossDefeated();
        }
    }

    private bool IsCurrentBossDefeatedForProgression()
    {
        if (currentBossObject == null)
            return true;

        if (currentBossCharacter == null)
        {
            currentBossCharacter = currentBossObject.GetComponent<CharacterBase>();

            if (currentBossCharacter == null)
                currentBossCharacter = currentBossObject.GetComponentInChildren<CharacterBase>(true);
        }

        if (currentBossCharacter == null)
            return false;

        return currentBossCharacter.currentHP <= 0f;
    }

    // ---------------------------------------------------------------------
    // PENANGANAN BOSS KALAH
    // ---------------------------------------------------------------------

    private void HandleBossDefeated()
    {
        if (bossDefeatHandled)
            return;

        bossDefeatHandled = true;
        Manager.ActiveEnemiesCount = 0;
        Stats.ReleaseAllConcurrentAttackTokens("Boss kalah");

        // PENTING: boss sering masih menyisakan collider ketika animasi mati berjalan.
        // Jika collider tetap aktif, player dapat tertahan dan tidak pernah mencapai rightTransitionX.
        PrepareDefeatedBossForStageTransition();

        // Finalisasi DDA setelah boss (jika diaktifkan).
        Manager.FinalizeDDAAfterBossIfNeeded();

        Manager.HideBossHPBar();

        currentBossCharacter = null;
        currentBossObject = null;

        Manager.ChangeState(StageManager.StageState.StageCleared);

        // Reset DDA/DataTracker sesuai pengaturan stage cleared.
        Manager.ApplyStageClearedDDAReset();
    }

    private void PrepareDefeatedBossForStageTransition()
    {
        if (currentBossObject == null)
            return;

        if (Manager.UntagBossOnDefeat)
        {
            try
            {
                currentBossObject.tag = "Untagged";
            }
            catch (UnityException)
            {
                // Diabaikan; tag Untagged selalu ada, tetapi try-catch tetap aman untuk prefab lama.
            }
        }

        if (!Manager.DisableBossCollidersOnDefeat)
            return;

        Collider2D[] colliders2D = currentBossObject.GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D collider2D in colliders2D)
        {
            if (collider2D != null)
                collider2D.enabled = false;
        }

        Collider[] colliders3D = currentBossObject.GetComponentsInChildren<Collider>(true);

        foreach (Collider collider3D in colliders3D)
        {
            if (collider3D != null)
                collider3D.enabled = false;
        }
    }
}