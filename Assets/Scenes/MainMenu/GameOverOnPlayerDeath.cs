using UnityEngine;
using UnityEngine.SceneManagement;

public class GameOverOnPlayerDeath : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterBase playerCharacter;

    [Header("Auto Assign Player")]
    [SerializeField] private bool autoFindPlayer = true;
    [SerializeField] private string playerTag = "Player";

    [Min(0.05f)]
    [SerializeField] private float autoFindInterval = 0.25f;

    [Header("Scene Names")]
    [SerializeField] private string gameOverSceneName = "GameOver";

    [Header("Safety")]
    [Tooltip("Jika true, GameOver juga dipicu ketika reference player menjadi null karena Destroy.")]
    [SerializeField] private bool triggerOnDestroyedReference = true;

    private bool triggered;
    private bool wasAssignedAtLeastOnce;
    private float nextAutoFindTime;
    private bool hasWarnedMissingPlayerTag;

    private void Awake()
    {
        if (playerCharacter != null)
        {
            wasAssignedAtLeastOnce = true;
        }
        else
        {
            TryAutoAssignPlayerCharacter();
        }
    }

    private void Start()
    {
        if (playerCharacter == null)
        {
            TryAutoAssignPlayerCharacter();
        }
    }

    private void Update()
    {
        if (triggered)
            return;

        if (playerCharacter == null && autoFindPlayer && Time.unscaledTime >= nextAutoFindTime)
        {
            nextAutoFindTime = Time.unscaledTime + autoFindInterval;
            TryAutoAssignPlayerCharacter();
        }

        if (playerCharacter != null)
        {
            wasAssignedAtLeastOnce = true;

            if (playerCharacter.currentHP <= 0f)
            {
                TriggerGameOver();
            }

            return;
        }

        if (triggerOnDestroyedReference && wasAssignedAtLeastOnce && playerCharacter == null)
        {
            TriggerGameOver();
        }
    }

    public void SetPlayerTransform(Transform playerTransform)
    {
        if (playerTransform == null)
        {
            playerCharacter = null;
            return;
        }

        CharacterBase foundCharacter = GetCharacterBaseFromTransform(playerTransform);

        if (foundCharacter == null)
        {
            Debug.LogWarning(
                "[GAME OVER] CharacterBase tidak ditemukan pada playerTransform. " +
                "Pastikan prefab player Bow atau Sword memiliki CharacterBase atau class turunan CharacterBase."
            );

            return;
        }

        SetPlayerCharacter(foundCharacter);
    }

    public void SetPlayerCharacter(CharacterBase newPlayerCharacter)
    {
        playerCharacter = newPlayerCharacter;

        if (playerCharacter != null)
        {
            wasAssignedAtLeastOnce = true;

            Debug.Log(
                "[GAME OVER] Player berhasil di-assign: " +
                playerCharacter.gameObject.name
            );
        }
    }

    private bool TryAutoAssignPlayerCharacter()
    {
        if (!autoFindPlayer)
            return false;

        if (string.IsNullOrEmpty(playerTag))
        {
            Debug.LogWarning("[GAME OVER] Player Tag kosong. Isi playerTag dengan tag Player.");
            return false;
        }

        CharacterBase foundCharacter = FindPlayerCharacterByTag();

        if (foundCharacter == null)
            return false;

        SetPlayerCharacter(foundCharacter);
        return true;
    }

    private CharacterBase FindPlayerCharacterByTag()
    {
        GameObject[] playerObjects;

        try
        {
            playerObjects = GameObject.FindGameObjectsWithTag(playerTag);
        }
        catch (UnityException)
        {
            if (!hasWarnedMissingPlayerTag)
            {
                Debug.LogWarning(
                    "[GAME OVER] Tag " + playerTag + " belum dibuat. " +
                    "Buat tag tersebut di Project Settings > Tags and Layers, " +
                    "lalu pasang tag itu pada prefab player Bow dan Sword."
                );

                hasWarnedMissingPlayerTag = true;
            }

            return null;
        }

        if (playerObjects == null || playerObjects.Length == 0)
            return null;

        CharacterBase firstValidCharacter = null;

        foreach (GameObject playerObject in playerObjects)
        {
            if (playerObject == null || !playerObject.activeInHierarchy)
                continue;

            CharacterBase character = GetCharacterBaseFromTransform(playerObject.transform);

            if (character == null)
                continue;

            if (firstValidCharacter == null)
                firstValidCharacter = character;

            if (character.currentHP > 0f)
                return character;
        }

        return firstValidCharacter;
    }

    private CharacterBase GetCharacterBaseFromTransform(Transform sourceTransform)
    {
        if (sourceTransform == null)
            return null;

        CharacterBase character = sourceTransform.GetComponent<CharacterBase>();

        if (character == null)
            character = sourceTransform.GetComponentInChildren<CharacterBase>();

        if (character == null)
            character = sourceTransform.GetComponentInParent<CharacterBase>();

        return character;
    }

    private void TriggerGameOver()
    {
        if (triggered)
            return;

        triggered = true;

        Time.timeScale = 1f;

        Debug.Log("[GAME OVER] Player mati. Memuat scene: " + gameOverSceneName);

        SceneManager.LoadSceneAsync(gameOverSceneName, LoadSceneMode.Single);
    }
}