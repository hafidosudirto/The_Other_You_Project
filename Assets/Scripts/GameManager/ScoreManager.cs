using UnityEngine;
using TMPro; // Wajib ada untuk kontrol TextMeshPro

public class ScoreManager : MonoBehaviour
{
    // Singleton: Agar bisa diakses dari script mana pun
    public static ScoreManager Instance;

    [Header("UI Reference")]
    [SerializeField] private TextMeshProUGUI scoreText;

    private int currentScore = 0;

    void Awake()
    {
        // Logika Singleton: Pastikan hanya ada satu ScoreManager
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        UpdateScoreUI();
    }

    // Fungsi yang akan dipanggil oleh musuh/objek lain
    public void AddScore(int amount)
    {
        currentScore += amount;
        UpdateScoreUI();
    }

    // Fungsi internal untuk memperbarui tampilan layar
    private void UpdateScoreUI()
    {
        if (scoreText != null)
        {
            scoreText.text = "Score: " + currentScore.ToString();
        }
    }

    // Opsional: Fungsi untuk ambil nilai skor (buat sistem Wave nanti)
    public int GetCurrentScore()
    {
        return currentScore;
    }
}