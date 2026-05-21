using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverLoader : MonoBehaviour
{
    [Header("Scene Names")]
    [SerializeField] private string gameOverSceneName = "GameOver";

    private bool isLoading;

    public void LoadGameOver()
    {
        if (isLoading)
            return;

        isLoading = true;

        Time.timeScale = 1f;
        AudioListener.pause = false;

        StageManager.RequestFreshRunOnNextGameplayLoad();

        Debug.Log("[GAME OVER LOADER] Player mati. Fresh run sudah ditandai. Memuat scene: " + gameOverSceneName);
        SceneManager.LoadSceneAsync(gameOverSceneName, LoadSceneMode.Single);
    }
}
