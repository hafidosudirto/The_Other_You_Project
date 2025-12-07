using UnityEngine;

[System.Serializable]
public class SkillSlot
{
    public string slotName = "Skill";
    public MonoBehaviour skillBehaviour;

    [Header("DDA / DataTracker")]
    public PlayerActionType actionType = PlayerActionType.Offensive;
    public WeaponType weaponType = WeaponType.Bow;

    // Nilai kontribusi skill terhadap DDA
    [Min(0f)]
    public float dValue = 1f;
}

public class SkillBase : MonoBehaviour
{
    [Header("Skill Slots")]
    public SkillSlot[] slots;

    [Header("Input Settings")]
    public KeyCode slot1Key = KeyCode.Alpha1;
    public KeyCode slot2Key = KeyCode.Alpha2;
    public KeyCode slot3Key = KeyCode.Alpha3;
    public KeyCode slot4Key = KeyCode.Alpha4;

    // Buffer lokal untuk DDA
    private float bufferedOffensive = 0f;
    private float bufferedDefensive = 0f;

    private void Update()
    {
        if (slots == null || slots.Length == 0)
            return;

        // Input per slot
        if (Input.GetKeyDown(slot1Key)) TriggerSlot(0);
        if (Input.GetKeyDown(slot2Key)) TriggerSlot(1);
        if (Input.GetKeyDown(slot3Key)) TriggerSlot(2);
        if (Input.GetKeyDown(slot4Key)) TriggerSlot(3);
    }

    // ------------------------------------------------------------
    //                    TRIGGER SLOT
    // ------------------------------------------------------------
    public void TriggerSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length)
            return;

        SkillSlot slot = slots[slotIndex];

        ISkill skill = slot.skillBehaviour as ISkill;
        if (skill == null)
        {
            DebugHub.Warning(
                $"Skill slot {slotIndex} ({slot.slotName}) tidak memiliki *behaviour* yang mengimplementasi ISkill."
            );
            return;
        }

        // Catat ke DDA
        RegisterSkillCast(slotIndex);

        // Jalankan perilaku skill
        skill.TriggerSkill(slotIndex);
    }

    // ------------------------------------------------------------
    //                  REGISTER D-VALUE (BUFFER)
    // ------------------------------------------------------------
    public void RegisterSkillCast(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length)
            return;

        if (DataTracker.Instance == null)
        {
            DebugHub.Warning("DataTracker.Instance NULL");
            return;
        }

        SkillSlot slot = slots[slotIndex];

        // Pastikan tidak negatif
        float d = Mathf.Max(0f, slot.dValue);

        // Nabung ke buffer sesuai tipe aksi
        if (slot.actionType == PlayerActionType.Offensive)
            bufferedOffensive += d;
        else
            bufferedDefensive += d;

        // Log debug
        DebugHub.Skill($"CAST {slot.slotName}");

        // Coba flush ke sistem DDA
        TryFlushToDDA(slot.weaponType);
    }

    private void TryFlushToDDA(WeaponType weaponType)
    {
        // Setiap kali buffer >= 1, kirim satu event ke DataTracker
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
}
