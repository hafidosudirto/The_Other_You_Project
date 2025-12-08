using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.PlayerSettings;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [Header("Enemy Spawn")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("HP Bar Enemy (yang sudah ada di scene)")]
    [SerializeField] private HPBar_Follow_Enemy enemyHpBar;  
    [SerializeField] private Vector3 hpBarOffset = new Vector3(0f, 1.5f, 0f);

    private int stageNumber = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        SpawnNextEnemy();
    }

    public void OnEnemyDefeated()
    {
        // 1. Kirim data stage ke DDA
        if (DataTracker.Instance != null)
            DataTracker.Instance.FinalizeStageData();

        stageNumber++;
        Debug.Log($"[StageManager] Stage {stageNumber} selesai → spawn enemy berikutnya");

        // 2. Spawn enemy baru (hasil analisis DDAController)
        SpawnNextEnemy();
    }

    private void SpawnNextEnemy()
    {
        if (enemyPrefab == null || spawnPoint == null)
        {
            Debug.LogError("[StageManager] enemyPrefab atau spawnPoint belum di-assign!");
            return;
        }

        GameObject enemy = Instantiate(enemyPrefab, spawnPoint.position, Quaternion.identity);
        enemy.name = $"Enemy_Stage_{stageNumber}";

        // Hubungkan EnemyDeathHandler dengan StageManager
        EnemyDeathHandler death = enemy.GetComponent<EnemyDeathHandler>();
        if (death != null)
            death.Init(this);

        // Atur HP bar untuk follow enemy baru
        if (enemyHpBar != null)
        {
            enemyHpBar.SetTarget(enemy.transform, hpBarOffset);
        }

        Debug.Log($"[StageManager] Enemy Spawned for Stage {stageNumber}");
    }
}


