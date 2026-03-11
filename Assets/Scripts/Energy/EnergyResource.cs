using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Energy Resource", fileName = "EnergyResource")]
public class EnergyResource : ScriptableObject
{
    [Header("Capacity")]
    [Min(0f)] public float maxEnergy = 100f;

    [Header("Runtime (read-only)")]
    [SerializeField] private float currentEnergy = 100f;

    /// <summary>Dipanggil setiap energi berubah: (current, max).</summary>
    public event Action<float, float> OnEnergyChanged;

    public float Current => currentEnergy;
    public float Max => maxEnergy;

    public float Normalized
    {
        get
        {
            if (maxEnergy <= 0f) return 0f;
            return Mathf.Clamp01(currentEnergy / maxEnergy);
        }
    }

    private void OnEnable()
    {
        // Saat asset di-load, pastikan nilai valid.
        maxEnergy = Mathf.Max(0f, maxEnergy);
        currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);

        // Kirim snapshot awal agar UI langsung sinkron.
        OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }

    /// <summary>Set energi secara paksa (mis. untuk debug atau spawn).</summary>
    public void SetEnergy(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, maxEnergy);
        if (Mathf.Approximately(clamped, currentEnergy)) return;

        currentEnergy = clamped;
        OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }

    /// <summary>Tambah energi (regen/pickup). Mengembalikan nilai aktual yang bertambah.</summary>
    public float Add(float amount)
    {
        if (amount <= 0f) return 0f;

        float before = currentEnergy;
        SetEnergy(currentEnergy + amount);
        return currentEnergy - before;
    }

    /// <summary>
    /// Coba bayar biaya energi. Jika cukup, dipotong dan return true. Jika tidak cukup, return false (tidak berubah).
    /// </summary>
    public bool TrySpend(float cost)
    {
        if (cost <= 0f) return true;
        if (currentEnergy < cost) return false;

        SetEnergy(currentEnergy - cost);
        return true;
    }

    /// <summary>Isi penuh.</summary>
    public void FillToMax()
    {
        SetEnergy(maxEnergy);
    }

    /// <summary>Untuk memastikan max berubah tanpa merusak current.</summary>
    public void SetMax(float newMax, bool keepRatio = true)
    {
        newMax = Mathf.Max(0f, newMax);
        if (Mathf.Approximately(newMax, maxEnergy)) return;

        float ratio = (maxEnergy <= 0f) ? 0f : (currentEnergy / maxEnergy);
        maxEnergy = newMax;

        currentEnergy = keepRatio ? Mathf.Clamp(ratio * maxEnergy, 0f, maxEnergy) : Mathf.Clamp(currentEnergy, 0f, maxEnergy);
        OnEnergyChanged?.Invoke(currentEnergy, maxEnergy);
    }
}