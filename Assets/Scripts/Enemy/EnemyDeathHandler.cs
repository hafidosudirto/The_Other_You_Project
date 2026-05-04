using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyDeathHandler : MonoBehaviour
{
    private bool processed = false;
    private StageManager stageManager;

    public void Init(StageManager manager)
    {
        stageManager = manager;
    }

    // dipanggil dari script HP / CharacterBase saat HP <= 0
    public void HandleDeath()
    {
        if (processed) return;
        processed = true;

        // Perbaikan CS0117: Cari komponen aktif di scene jika belum di-set
        if (stageManager == null)
            stageManager = FindObjectOfType<StageManager>();

        // Perbaikan CS1061: Gunakan method OnEnemyDied() sesuai dengan StageManager
        if (stageManager != null)
            stageManager.OnEnemyDied();

        // destroy enemy setelah sedikit delay (biar animasi death bisa jalan kalau ada)
        Destroy(gameObject, 1.0f);
    }
}