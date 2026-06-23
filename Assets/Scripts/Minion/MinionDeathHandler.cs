using UnityEngine;

public class MinionDeathHandler : MonoBehaviour
{
    private StageManager stageManager;
    private bool isDead = false;

    private void Start()
    {
        // MENGATASI ERROR: Mencari StageManager secara langsung di dalam Scene
        stageManager = FindObjectOfType<StageManager>();
    }

    public void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        // Jika StageManager punya fungsi untuk mendeteksi musuh mati, panggil di sini
        // if (stageManager != null) {
        //     stageManager.EnemyKilled(); 
        // }

        Destroy(gameObject, 2.5f);
    }
}