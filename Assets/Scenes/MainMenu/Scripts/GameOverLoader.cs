using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverLoader : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string gameOverSceneName = "GameOver";

    // Dipanggil saat player mati
    public void LoadGameOver()
    {
        // Jika sebelumnya Anda memakai pause (Time.timeScale = 0), ini mencegah UI/animasi terkunci setelah pindah scene
        Time.timeScale = 1f;

        SceneManager.LoadSceneAsync(gameOverSceneName, LoadSceneMode.Single);
    }
}
