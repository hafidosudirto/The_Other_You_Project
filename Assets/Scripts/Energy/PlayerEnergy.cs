using UnityEngine;

/// <summary>
/// Komponen runtime untuk menjalankan regenerasi energi berbasis waktu.
/// Energi disimpan di EnergyResource (ScriptableObject), sehingga UI/skill memakai sumber yang sama.
/// </summary>
public class PlayerEnergy : MonoBehaviour
{
    [Header("Energy Source (ScriptableObject)")]
    [SerializeField] private EnergyResource energy;

    [Header("Time-Based Regeneration")]
    [Min(0f)] public float regenPerSecond = 8f;

    [Tooltip("Jika true, regen memakai unscaled time (mis. tetap jalan saat pause/timeScale=0).")]
    public bool useUnscaledTime = false;

    [Header("Initialization")]
    [Tooltip("Jika true, setiap OnEnable akan reset energi ke startingEnergy.")]
    public bool resetOnEnable = true;

    [Tooltip("Nilai energi awal ketika resetOnEnable = true.")]
    [Min(0f)] public float startingEnergy = 100f;

    public EnergyResource Energy => energy;

    private void OnEnable()
    {
        if (energy == null)
        {
            Debug.LogWarning("[PlayerEnergy] EnergyResource belum di-assign.");
            return;
        }

        if (resetOnEnable)
        {
            energy.SetEnergy(Mathf.Min(startingEnergy, energy.Max));
        }
    }

    private void Update()
    {
        if (energy == null) return;
        if (regenPerSecond <= 0f) return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        if (dt <= 0f) return;

        energy.Add(regenPerSecond * dt);
    }
}