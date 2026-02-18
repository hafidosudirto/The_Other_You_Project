using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverOnPlayerDeath : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterBase playerCharacter;

    [Header("Scene Names")]
    [SerializeField] private string gameOverSceneName = "GameOver";

    [Header("Safety")]
    [Tooltip("Jika true, GameOver juga dipicu ketika reference player menjadi null (karena Destroy).")]
    [SerializeField] private bool triggerOnDestroyedReference = true;

    private bool triggered;
    private bool wasAssignedAtLeastOnce;

    private void Awake()
    {
        // Menandai bahwa kita pernah memiliki reference ke player
        wasAssignedAtLeastOnce = (playerCharacter != null);
    }

    private void Update()
    {
        if (triggered) return;

        // Jika player tidak di-assign di Inspector, skrip tidak dapat bekerja.
        // (Lebih baik fail-fast agar bug cepat terlihat.)
        if (!wasAssignedAtLeastOnce && playerCharacter == null)
            return;

        // 1) Kondisi eksplisit: HP habis
        if (playerCharacter != null && playerCharacter.currentHP <= 0f)
        {
            TriggerGameOver();
            return;
        }

        // 2) Kondisi implisit: object player sudah dihancurkan oleh Destroy(gameObject)
        if (triggerOnDestroyedReference && wasAssignedAtLeastOnce && playerCharacter == null)
        {
            TriggerGameOver();
            return;
        }
    }

    private void TriggerGameOver()
    {
        triggered = true;

        // Jika sebelumnya Anda pernah melakukan pause (Time.timeScale = 0),
        // ini mencegah UI pada scene GameOver ikut "macet".
        Time.timeScale = 1f;

        SceneManager.LoadSceneAsync(gameOverSceneName, LoadSceneMode.Single);
    }
}
