using UnityEngine;
using UnityEngine.SceneManagement;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }

    [Header("Scene")]
    [SerializeField] private string gameplaySceneName = "Sprite_SwordEnemyAI";

    [Header("Enemy Spawn")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform spawnPoint;

    [Header("HP Bar Enemy (yang sudah ada di scene)")]
    [SerializeField] private HPBar_Follow_Enemy enemyHpBar;
    [SerializeField] private Vector3 hpBarOffset = new Vector3(0f, 1.5f, 0f);

    private int stageNumber;
    private GameObject currentEnemy;
    private bool sessionInitialized;

    private void Awake()
    {
        // Singleton aman
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResetStageSession();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        EnsureInitialEnemySpawned();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != gameplaySceneName)
            return;

        // PERBAIKAN 1: Cari ulang HP Bar di Canvas jika referensi terputus akibat Play Again
        if (enemyHpBar == null)
        {
            enemyHpBar = FindObjectOfType<HPBar_Follow_Enemy>(true);
        }

        ResetStageSession();
        EnsureInitialEnemySpawned();
    }

    private void ResetStageSession()
    {
        stageNumber = 0;
        currentEnemy = null;
        sessionInitialized = false;

        // Sembunyikan HP Bar saat stage di-reset (Play Again)
        if (enemyHpBar != null)
        {
            enemyHpBar.gameObject.SetActive(false);
        }
    }

    private void EnsureInitialEnemySpawned()
    {
        if (sessionInitialized)
            return;

        sessionInitialized = true;
        SpawnEnemyForCurrentStage();
    }

    public void OnEnemyDefeated()
    {
        // Kosongkan referensi enemy lama
        currentEnemy = null;

        // PERBAIKAN 2: Matikan HP Bar musuh segera setelah dia mati agar tidak nyangkut di layar
        if (enemyHpBar != null)
        {
            enemyHpBar.gameObject.SetActive(false);
        }

        if (DataTracker.Instance != null)
            DataTracker.Instance.FinalizeStageData();

        stageNumber++;
        Debug.Log($"[StageManager] Stage {stageNumber} selesai -> spawn enemy berikutnya");

        SpawnEnemyForCurrentStage();
    }

    private void SpawnEnemyForCurrentStage()
    {
        if (enemyPrefab == null || spawnPoint == null)
        {
            Debug.LogError("[StageManager] enemyPrefab atau spawnPoint belum di-assign!");
            return;
        }

        if (currentEnemy != null)
        {
            Debug.LogWarning("[StageManager] Spawn dibatalkan karena currentEnemy masih ada.");
            return;
        }

        currentEnemy = Instantiate(enemyPrefab, spawnPoint.position, Quaternion.identity);
        currentEnemy.name = $"Enemy_Stage_{stageNumber}";

        EnemyDeathHandler death = currentEnemy.GetComponent<EnemyDeathHandler>();
        if (death != null)
            death.Init(this);
        else
            Debug.LogWarning("[StageManager] EnemyDeathHandler tidak ditemukan pada enemyPrefab.");

        // PERBAIKAN 3: Pastikan HP bar ditarik kembali dan dihubungkan ke musuh yang baru
        if (enemyHpBar == null)
        {
            // Fallback jaga-jaga jika masih null
            enemyHpBar = FindObjectOfType<HPBar_Follow_Enemy>(true);
        }

        if (enemyHpBar != null)
        {
            enemyHpBar.gameObject.SetActive(true); // Nyalakan ulang UI-nya
            enemyHpBar.SetTarget(currentEnemy.transform, hpBarOffset);
        }
        else
        {
            Debug.LogWarning("[StageManager] HP Bar musuh tidak ditemukan di scene canvas!");
        }

        Debug.Log($"[StageManager] Enemy Spawned for Stage {stageNumber}");
    }
}