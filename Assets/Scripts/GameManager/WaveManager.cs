using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class WaveManager : MonoBehaviour
{
    [System.Serializable]
    public class WaveData
    {
        public string waveName;
        public List<GameObject> enemyPrefabs; // Variasi musuh di wave ini
        public int count;                     // Jumlah total musuh
        public float rate;                    // Jeda antar spawn
    }

    public static WaveManager Instance;

    [Header("Settings")]
    public List<WaveData> waves; 
    public Transform[] spawnPoints;
    public float delayBetweenWaves = 3f;

    [Header("Status (Read Only)")]
    [SerializeField] private int currentWaveIndex = 0;
    [SerializeField] private int activeEnemies = 0;
    
    private bool isSpawning = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (waves.Count > 0) StartCoroutine(SpawnWave(currentWaveIndex));
    }

    IEnumerator SpawnWave(int index)
    {
        isSpawning = true;
        WaveData wave = waves[index];
        Debug.Log("Memulai Wave: " + wave.waveName);

        for (int i = 0; i < wave.count; i++)
        {
            SpawnEnemy(wave);
            yield return new WaitForSeconds(wave.rate);
        }

        isSpawning = false;
    }

    void SpawnEnemy(WaveData wave)
    {
        if (wave.enemyPrefabs.Count == 0 || spawnPoints.Length == 0) return;

        // Pilih prefab acak dan titik spawn acak
        GameObject prefab = wave.enemyPrefabs[Random.Range(0, wave.enemyPrefabs.Count)];
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        Instantiate(prefab, spawnPoint.position, Quaternion.identity);
        activeEnemies++;
    }

    // WAJIB dipanggil saat musuh mati
    public void EnemyDied()
    {
        activeEnemies--;

        // Jika musuh habis dan sudah tidak ada lagi yang akan di-spawn
        if (activeEnemies <= 0 && !isSpawning)
        {
            if (currentWaveIndex + 1 < waves.Count)
            {
                currentWaveIndex++;
                Invoke("StartNextWave", delayBetweenWaves);
            }
            else
            {
                Debug.Log("Semua Wave Selesai!");
            }
        }
    }

    void StartNextWave()
    {
        StartCoroutine(SpawnWave(currentWaveIndex));
    }
}