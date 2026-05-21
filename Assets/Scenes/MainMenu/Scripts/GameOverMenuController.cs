using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverMenuController : MonoBehaviour
{
    [SerializeField] private string gameplaySceneName = "Sprite_SwordEnemyAI";

    public void PlayAgain()
    {
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(gameplaySceneName, LoadSceneMode.Single);
    }

    public void Quit()
    {
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
