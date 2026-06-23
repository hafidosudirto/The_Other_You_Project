using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverController : MonoBehaviour
{
    [Header("Scene Configuration")]
    [Tooltip("Nama scene Main Menu kamu. Pastikan sudah ada di Build Settings.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    /// <summary>
    /// Panggil fungsi ini dari tombol "Quit Game"
    /// </summary>
    public void QuitGame()
    {
        Debug.Log("Quit Game dipanggil.");

        Application.Quit();

#if UNITY_EDITOR
        // Fungsi ini hanya untuk testing di dalam Unity Editor
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}