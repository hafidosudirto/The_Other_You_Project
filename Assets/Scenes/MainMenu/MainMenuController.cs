using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Scene Configuration")]
    [SerializeField] private string gameplaySceneName = "Sprite_SwordEnemyAI";

    /// <summary>
    /// Dipanggil oleh tombol Start.
    /// Memuat scene gameplay secara asinkron.
    /// </summary>
    public void StartGame()
    {
        SceneManager.LoadSceneAsync(
            gameplaySceneName,
            LoadSceneMode.Single
        );
    }

    /// <summary>
    /// Dipanggil oleh tombol Quit.
    /// Menutup aplikasi pada build final.
    /// </summary>
    public void QuitGame()
    {
        Application.Quit();

#if UNITY_EDITOR
        // Agar efek Quit terlihat saat testing di Editor
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
