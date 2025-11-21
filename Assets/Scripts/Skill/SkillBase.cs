using UnityEngine;

[System.Serializable]
public class SkillSlot
{
    public string slotName = "Skill";
    public MonoBehaviour skillBehaviour;

    [Header("DDA / DataTracker")]
    public PlayerActionType actionType = PlayerActionType.Offensive;
    public WeaponType weaponType = WeaponType.Sword;

    // dValue: kontribusi ke O/D (0.5, 1, dll) untuk skill ini per CAST
    public float dValue = 1f;
}

public class SkillBase : MonoBehaviour
{
    [Header("Referensi Pemilik Skill")]
    public Player player;

    [Header("Skill Slots (1–4)")]
    public SkillSlot[] slots = new SkillSlot[4];

    [Header("Input Keys")]
    public KeyCode slot1Key = KeyCode.Alpha1;
    public KeyCode slot2Key = KeyCode.Alpha2;
    public KeyCode slot3Key = KeyCode.Alpha3;
    public KeyCode slot4Key = KeyCode.Alpha4;

    [HideInInspector]
    public bool skillLocked = false;

    private float lockTimer = 0f;
    private const float MAX_LOCK_TIME = 2f;

    // ============================================
    //  BUFFER DDA (fractional → integer)
    // ============================================
    private float bufferedOffensive = 0f;
    private float bufferedDefensive = 0f;

    void Awake()
    {
        if (player == null)
            player = GetComponentInParent<Player>();
    }

    void Update()
    {
        if (!player) return;

        // ---------- Fail-safe auto unlock ----------
        if (skillLocked)
        {
            lockTimer += Time.deltaTime;

            if (lockTimer > MAX_LOCK_TIME)
            {
                DebugHub.Warning("SkillBase Auto-Unlock Triggered");
                ReleaseLock();
            }
        }
        else
        {
            lockTimer = 0f;
        }

        // ---------- INPUT (dengan cek busy per slot) ----------
        if (Input.GetKeyDown(slot1Key))
        {
            if (!IsSlotBusy(0))
                TriggerSlot(0);
        }

        if (Input.GetKeyDown(slot2Key))
        {
            if (!IsSlotBusy(1))
                TriggerSlot(1);
        }

        if (Input.GetKeyDown(slot3Key))
        {
            if (!IsSlotBusy(2))
                TriggerSlot(2);
        }

        if (Input.GetKeyDown(slot4Key))
        {
            if (!IsSlotBusy(3))
                TriggerSlot(3);
        }
    }

    // ============================================================
    //                         TRIGGER SLOT
    // ============================================================
    public void TriggerSlot(int slotIndex)
    {
        if (skillLocked) return;
        if (slotIndex < 0 || slotIndex >= slots.Length) return;

        ISkill skill = slots[slotIndex].skillBehaviour as ISkill;
        if (skill == null) return;

        // ---------- Lock skill ----------
        skillLocked = true;

        // ---------- DDA Update saat CAST ----------
        // • Riposte: dikelola di CharacterBase.Parry()
        // • SlashCombo: punya 2 CAST (0.5 + 0.5) di dalam script skill,
        //   jadi TIDAK dihitung di sini supaya tidak triple-count.
        string typeName = slots[slotIndex].skillBehaviour.GetType().Name;

        if (typeName != "Sword_Riposte" && typeName != "Sword_SlashCombo")
        {
            RegisterSkillCast(slotIndex);
        }

        // ---------- Trigger skill behaviour ----------
        skill.TriggerSkill(slotIndex);
    }

    // ============================================================
    //                    REGISTER D-VALUE (BUFFER)
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

        // Nabung dValue ke buffer lokal di SkillBase
        if (slot.actionType == PlayerActionType.Offensive)
        {
            bufferedOffensive += dValue;
        }
        else
        {
            bufferedDefensive += dValue;
        }

        // Debug untuk game designer
        DebugHub.Skill(
            $"CAST {slot.slotName} → {slot.actionType} +{dValue}"
        );

        // Coba flush ke DataTracker kalau sudah >= 1
        TryFlushToDDA(slot.weaponType);
    }

    // ============================================================
    //                  FLUSH BUFFER KE DATATRACKER
    // ============================================================
    private void TryFlushToDDA(WeaponType weaponType)
    {
        // Offensive
        while (bufferedOffensive >= 1f)
        {
            DataTracker.Instance.RecordAction(
                PlayerActionType.Offensive,
                weaponType
            );
            bufferedOffensive -= 1f;
        }

        // Defensive
        while (bufferedDefensive >= 1f)
        {
            DataTracker.Instance.RecordAction(
                PlayerActionType.Defensive,
                weaponType
            );
            bufferedDefensive -= 1f;
        }
    }

    // ============================================================
    //          CEK APAKAH SKILL SEDANG BUSY (COMBO / CAST)
    // ============================================================
    private bool IsSlotBusy(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= slots.Length)
            return false;

        var behaviour = slots[slotIndex].skillBehaviour as MonoBehaviour;
        if (behaviour == null) return false;

        // --- Slash Combo ---
        var slash = behaviour as Sword_SlashCombo;
        if (slash != null)
        {
            // Pastikan di Sword_SlashCombo: public bool isBusy;
            return slash.isBusy;
        }

        // Skill lain (kalau nanti butuh cek busy) bisa ditambah di sini

        return false;
    }

    // ============================================================
    //                      UNLOCK SKILL
    // ============================================================
    public void ReleaseLock()
    {
        skillLocked = false;
        lockTimer = 0f;
    }
}
