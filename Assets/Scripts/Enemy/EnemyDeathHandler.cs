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

        // 1. Matikan otak AI (NodeManager) agar musuh berhenti bergerak/menyerang
        NodeManager nodeManager = GetComponent<NodeManager>();
        if (nodeManager != null)
            nodeManager.enabled = false;

        // 2. Matikan pergerakan physics
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.velocity = Vector2.zero;

        // 3. Matikan collider agar mayat tidak menghalangi jalan / dipukul lagi
        Collider2D coll = GetComponent<Collider2D>();
        if (coll != null)
            coll.enabled = false;

        // Cari stage manager jika terlewat di-set
        if (stageManager == null)
            stageManager = FindObjectOfType<StageManager>();

        // Laporkan kematian ke StageManager untuk memicu kemunculan Boss
        if (stageManager != null)
            stageManager.OnEnemyDied();

        // Destroy enemy setelah sedikit delay (agar animasi death selesai)
        Destroy(gameObject, 1.0f);
    }
}