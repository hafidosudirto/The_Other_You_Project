using System;
using UnityEngine;

#region Skill Slot Definition

[Serializable]
public class SkillSlot
{
    [Header("Identity")]
    [Tooltip("Nama slot untuk debug, DDA, dan tampilan fallback HUD. Tidak mengubah mekanik skill.")]
    public string slotName = "Skill";

    [Tooltip("Script skill asli yang akan dipanggil oleh input. Script ini sebaiknya mengimplementasikan ISkill.")]
    public MonoBehaviour skillBehaviour;

    [Header("DDA / DataTracker")]
    [Tooltip("Jenis aksi untuk pencatatan DataTracker.")]
    public PlayerActionType actionType = PlayerActionType.Offensive;

    [Tooltip("Jenis senjata untuk pencatatan DataTracker dan sinkronisasi HUD.")]
    public WeaponType weaponType = WeaponType.Bow;

    [Tooltip("Bobot kontribusi aksi untuk DDA. Ini bukan damage, cooldown, atau energy cost.")]
    [Min(0f)]
    public float dValue = 1f;

    public bool HasSkill => skillBehaviour != null;
}

#endregion

/// <summary>
/// Registry + dispatcher slot skill milik satu CharacterBase. Membaca input
/// keyboard, menjadi gerbang pembayaran energi, mencatat aksi ke DDA, dan
/// menyimpan daftar slot skill.
/// CATATAN ARSITEKTUR (revisi PA): kelas ini memegang 4 concern sekaligus
/// (Input, Energy, DDA, Registry) — kandidat pemisahan di fase lanjut.
/// </summary>
public class SkillBase : MonoBehaviour
{
    #region Serialized Config

    [Header("Skill Slots")]
    [Tooltip("Urutan mekanik skill. Slot 0 = basic attack, Slot 1 = charged/secondary attack, Slot 2 = Q, Slot 3 = E.")]
    public SkillSlot[] slots;

    [Header("Energy Source (CharacterBase)")]
    [Tooltip("Sumber energy/stamina pemilik skill. Jika kosong, akan dicari otomatis dari parent.")]
    [SerializeField] private CharacterBase owner;

    [Header("Input Settings")]
    [Tooltip("Input untuk Slot 0. Umumnya Mouse 0 untuk basic attack.")]
    public KeyCode slot1Key = KeyCode.Alpha1;

    [Tooltip("Input untuk Slot 1. Umumnya Mouse 1 untuk charged/secondary attack.")]
    public KeyCode slot2Key = KeyCode.Alpha2;

    [Tooltip("Input untuk Slot 2. Umumnya Q.")]
    public KeyCode slot3Key = KeyCode.Alpha3;

    [Tooltip("Input untuk Slot 3. Umumnya E.")]
    public KeyCode slot4Key = KeyCode.Alpha4;

    #endregion

    #region Events & State

    public CharacterBase Owner => owner;

    public event Action<int, SkillSlot> OnSlotInputPressed;
    public event Action<int, SkillSlot> OnSlotTriggeredBySkillBase;
    public event Action<int, SkillSlot> OnSlotRejectedByEnergy;

    private float bufferedOffensive = 0f;
    private float bufferedDefensive = 0f;

    #endregion

    #region Unity Lifecycle / Input

    private void Awake()
    {
        if (owner == null)
            owner = FindOwner();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (owner == null)
            owner = FindOwner();

        EnsureSlotArrayMinimum();
    }
#endif

    private void Update()
    {
        if (slots == null || slots.Length == 0)
            return;

        if (Input.GetKeyDown(slot1Key)) TriggerSlot(0);
        if (Input.GetKeyDown(slot2Key)) TriggerSlot(1);
        if (Input.GetKeyDown(slot3Key)) TriggerSlot(2);
        if (Input.GetKeyDown(slot4Key)) TriggerSlot(3);
    }

    #endregion

    #region Owner & Energy Gateway

    public void RebindOwner(CharacterBase newOwner)
    {
        owner = newOwner != null ? newOwner : FindOwner();
    }

    /// <summary>
    /// Meneruskan permintaan pembayaran energi ke owner (CharacterBase).
    /// </summary>
    /// <returns>true bila energi cukup/dipotong; false bila owner tidak ada atau energi kurang.</returns>
    public bool TrySpendEnergy(float cost)
    {
        if (cost <= 0f)
            return true;

        if (owner == null)
            owner = FindOwner();

        if (owner == null)
        {
            DebugHub.Warning("[SkillBase] CharacterBase tidak ditemukan. Batalkan cast.");
            return false;
        }

        return owner.TrySpendEnergy(cost);
    }

    #endregion

    #region Slot Triggering

    /// <summary>
    /// Titik masuk eksekusi satu slot skill: validasi slot, bayar energi bila
    /// skill memintanya lewat IEnergySkill.PayEnergyInSkillBase, catat ke DDA,
    /// lalu panggil ISkill.TriggerSkill. Menyiarkan event untuk HUD.
    /// </summary>
    public void TriggerSlot(int slotIndex)
    {
        SkillSlot slot = GetSlot(slotIndex);

        if (slot == null)
            return;

        OnSlotInputPressed?.Invoke(slotIndex, slot);

        ISkill skill = slot.skillBehaviour as ISkill;

        if (skill == null)
        {
            DebugHub.Warning($"Skill slot {slotIndex} ({slot.slotName}) tidak mengimplementasi ISkill.");
            return;
        }

        IEnergySkill energySkill = slot.skillBehaviour as IEnergySkill;

        if (energySkill != null && energySkill.PayEnergyInSkillBase)
        {
            float cost = Mathf.Max(0f, energySkill.EnergyCost);

            if (!TrySpendEnergy(cost))
            {
                DebugHub.Warning($"ENERGY KURANG: {slot.slotName} butuh {cost}.");
                OnSlotRejectedByEnergy?.Invoke(slotIndex, slot);
                return;
            }
        }

        RegisterSkillCast(slotIndex);
        skill.TriggerSkill(slotIndex);

        OnSlotTriggeredBySkillBase?.Invoke(slotIndex, slot);
    }

    #endregion

    #region DDA Recording (Buffer)

    /// <summary>
    /// Menambah bobot aksi (dValue) slot ke buffer offensive/defensive, lalu
    /// mem-flush ke DataTracker bila buffer sudah mencapai 1.0.
    /// </summary>
    public void RegisterSkillCast(int slotIndex)
    {
        SkillSlot slot = GetSlot(slotIndex);

        if (slot == null)
            return;

        if (DataTracker.Instance == null)
        {
            DebugHub.Warning("DataTracker.Instance NULL");
            return;
        }

        float d = Mathf.Max(0f, slot.dValue);

        if (slot.actionType == PlayerActionType.Offensive)
            bufferedOffensive += d;
        else
            bufferedDefensive += d;

        DebugHub.Skill($"CAST {slot.slotName}");
        TryFlushToDDA(slot.weaponType);
    }

    #endregion

    #region Slot Query Helpers

    public SkillSlot GetSlot(int slotIndex)
    {
        if (!IsValidSlotIndex(slotIndex))
            return null;

        return slots[slotIndex];
    }

    public MonoBehaviour GetSkillBehaviour(int slotIndex)
    {
        SkillSlot slot = GetSlot(slotIndex);
        return slot != null ? slot.skillBehaviour : null;
    }

    public KeyCode GetSlotKey(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0:
                return slot1Key;

            case 1:
                return slot2Key;

            case 2:
                return slot3Key;

            case 3:
                return slot4Key;

            default:
                return KeyCode.None;
        }
    }

    public string GetSlotName(int slotIndex)
    {
        SkillSlot slot = GetSlot(slotIndex);
        return slot != null ? slot.slotName : string.Empty;
    }

    public WeaponType GetSlotWeaponType(int slotIndex)
    {
        SkillSlot slot = GetSlot(slotIndex);
        return slot != null ? slot.weaponType : WeaponType.None;
    }

    public bool TryFindSlotIndex(MonoBehaviour skillBehaviour, out int slotIndex)
    {
        slotIndex = -1;

        if (skillBehaviour == null || slots == null)
            return false;

        for (int i = 0; i < slots.Length; i++)
        {
            SkillSlot slot = slots[i];

            if (slot == null || slot.skillBehaviour == null)
                continue;

            if (slot.skillBehaviour == skillBehaviour)
            {
                slotIndex = i;
                return true;
            }

            if (slot.skillBehaviour.GetInstanceID() == skillBehaviour.GetInstanceID())
            {
                slotIndex = i;
                return true;
            }
        }

        return false;
    }

    public float GetEnergyCostForSlot(int slotIndex)
    {
        MonoBehaviour skillBehaviour = GetSkillBehaviour(slotIndex);

        if (skillBehaviour == null)
            return 0f;

        IEnergySkill energySkill = skillBehaviour as IEnergySkill;

        if (energySkill != null)
            return Mathf.Max(0f, energySkill.EnergyCost);

        return 0f;
    }

    public bool IsEnergyAvailableForSlot(int slotIndex)
    {
        float cost = GetEnergyCostForSlot(slotIndex);

        if (cost <= 0f)
            return true;

        if (owner == null)
            owner = FindOwner();

        if (owner == null)
            return true;

        return owner.HasEnergy(cost);
    }

    public bool IsSlotReadyBySkillBaseOnly(int slotIndex)
    {
        MonoBehaviour skillBehaviour = GetSkillBehaviour(slotIndex);

        if (skillBehaviour == null)
            return false;

        if (!(skillBehaviour is ISkill))
            return false;

        return IsEnergyAvailableForSlot(slotIndex);
    }

    #endregion

    #region Private Helpers

    private void TryFlushToDDA(WeaponType weaponType)
    {
        while (bufferedOffensive >= 1f)
        {
            DataTracker.Instance.RecordAction(PlayerActionType.Offensive, weaponType);
            bufferedOffensive -= 1f;
        }

        while (bufferedDefensive >= 1f)
        {
            DataTracker.Instance.RecordAction(PlayerActionType.Defensive, weaponType);
            bufferedDefensive -= 1f;
        }
    }

    private bool IsValidSlotIndex(int slotIndex)
    {
        if (slots == null)
            return false;

        if (slotIndex < 0 || slotIndex >= slots.Length)
            return false;

        return slots[slotIndex] != null;
    }

    private CharacterBase FindOwner()
    {
        CharacterBase found = GetComponent<CharacterBase>();

        if (found != null)
            return found;

        found = GetComponentInParent<CharacterBase>();

        if (found != null)
            return found;

        return GetComponentInChildren<CharacterBase>(true);
    }

    private void EnsureSlotArrayMinimum()
    {
        if (slots == null)
            return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                slots[i] = new SkillSlot();
        }
    }

    #endregion
}
