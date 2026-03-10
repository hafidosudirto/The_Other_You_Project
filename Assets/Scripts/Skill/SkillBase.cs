using UnityEngine;

[System.Serializable]
public class SkillSlot
{
    public string slotName = "Skill";
    public MonoBehaviour skillBehaviour;

    [Header("DDA / DataTracker")]
    public PlayerActionType actionType = PlayerActionType.Offensive;
    public WeaponType weaponType = WeaponType.Bow;

    [Min(0f)]
    public float dValue = 1f;
}

public class SkillBase : MonoBehaviour
{
    [Header("Skill Slots")]
    public SkillSlot[] slots;

    [Header("Energy Source (CharacterBase)")]
    [SerializeField] private CharacterBase owner;

    [Header("Input Settings")]
    public KeyCode slot1Key = KeyCode.Alpha1;
    public KeyCode slot2Key = KeyCode.Alpha2;
    public KeyCode slot3Key = KeyCode.Alpha3;
    public KeyCode slot4Key = KeyCode.Alpha4;

    private float bufferedOffensive = 0f;
    private float bufferedDefensive = 0f;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponentInParent<CharacterBase>();
    }

    public bool TrySpendEnergy(float cost)
    {
        if (cost <= 0f) return true;

        if (owner == null)
        {
            DebugHub.Warning("[SkillBase] CharacterBase tidak ditemukan. Batalkan cast.");
            return false;
        }

        return owner.TrySpendEnergy(cost);
    }

    private void Update()
    {
        if (slots == null || slots.Length == 0)
            return;

        if (Input.GetKeyDown(slot1Key)) TriggerSlot(0);
        if (Input.GetKeyDown(slot2Key)) TriggerSlot(1);
        if (Input.GetKeyDown(slot3Key)) TriggerSlot(2);
        if (Input.GetKeyDown(slot4Key)) TriggerSlot(3);
    }

    public void TriggerSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length)
            return;

        SkillSlot slot = slots[slotIndex];

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
                return;
            }
        }

        RegisterSkillCast(slotIndex);
        skill.TriggerSkill(slotIndex);
    }

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
        float d = Mathf.Max(0f, slot.dValue);

        if (slot.actionType == PlayerActionType.Offensive)
            bufferedOffensive += d;
        else
            bufferedDefensive += d;

        DebugHub.Skill($"CAST {slot.slotName}");
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