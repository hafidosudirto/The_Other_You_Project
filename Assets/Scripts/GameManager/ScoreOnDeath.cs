using UnityEngine;

public class ScoreOnDeath : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private int scoreValue = 10;

    private bool isQuitting = false;

    // Menandai jika aplikasi sedang ditutup agar tidak trigger logic saat stop play
    private void OnApplicationQuit()
    {
        isQuitting = true;
    }

    private void OnDestroy()
    {
        // 1. Validasi: Jangan eksekusi jika aplikasi tutup atau editor distop
        if (isQuitting) return;

        // 2. Tambah Skor
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.AddScore(scoreValue);
        }

        // 3. Lapor ke WaveManager agar jumlah musuh di layar berkurang
        // Kita cari WaveManager yang ada di scene
        WaveManager wm = Object.FindFirstObjectByType<WaveManager>();
        if (wm != null) 
        {
            wm.EnemyDied();
        }
    }
}