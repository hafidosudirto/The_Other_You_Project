using UnityEngine;

[System.Serializable]
public class SkillSlot
{
    public string slotName = "Skill";
    public MonoBehaviour skillBehaviour;

    [Header("DDA / DataTracker")]
    public PlayerActionType actionType = PlayerActionType.Offensive;
    public WeaponType weaponType = WeaponType.Bow;

    // nilai kontribusi ke DDA
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

    // Buffer DDA lokal
    private float bufferedOffensive = 0f;
    private float bufferedDefensive = 0f;

    private void Update()
    {
        if (slots == null || slots.Length == 0)
            return;

        // --------------- INPUT ---------------
        if (Input.GetKeyDown(slot1Key)) TriggerSlot(0);
        if (Input.GetKeyDown(slot2Key)) TriggerSlot(1);
        if (Input.GetKeyDown(slot3Key)) TriggerSlot(2);
        if (Input.GetKeyDown(slot4Key)) TriggerSlot(3);
    }

    // ============================================================
    //                        TRIGGER SLOT
    // ============================================================
    public void TriggerSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        ISkill skill = slots[slotIndex].skillBehaviour as ISkill;
        if (skill == null) return;

        // DDA update saat CAST
        RegisterSkillCast(slotIndex);

        // Jalankan behaviour skill
        skill.TriggerSkill(slotIndex);
    }

    // ============================================================
    //                REGISTER D-VALUE (BUFFER)
    // ============================================================
    public void RegisterSkillCast(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length) return;
        if (DataTracker.Instance == null)
        {
            DebugHub.Warning("DataTracker.Instance NULL");
            return;
        }

        SkillSlot slot = slots[slotIndex];
        float dValue = slot.dValue;

        // Nabung nilai ke buffer
        if (slot.actionType == PlayerActionType.Offensive)
            bufferedOffensive += dValue;
        else
            bufferedDefensive += dValue;

        // Debug
        DebugHub.Skill($"CAST {slot.slotName}");

        // flush ketika sudah >= 1
        TryFlushToDDA(slot.weaponType);
    }

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
}
