using System;
using UnityEngine;

public enum SkillHUDSlotStyle
{
    Primary,
    Secondary,
    SkillA,
    SkillB
}

[DisallowMultipleComponent]
public class SkillRootHUDProfile : MonoBehaviour
{
    [Serializable]
    public class SkillHUDPresentation
    {
        [Header("Display Identity")]
        [Tooltip("Nama skill yang tampil di HUD. Ini hanya nama tampilan, bukan data mekanik.")]
        public string displayName;

        [Tooltip("Deskripsi pendek untuk membantu pemain memahami fungsi skill. Tidak mengubah damage, cooldown, atau energy.")]
        [TextArea(2, 4)]
        public string shortDescription;

        [Header("Visual")]
        [Tooltip("Ikon skill yang tampil di HUD.")]
        public Sprite icon;

        [Tooltip("Jenis slot untuk keterbacaan Inspector. Urutan mekanik tetap mengikuti SkillBase.slots.")]
        public SkillHUDSlotStyle slotStyle;

        [Header("UI Tags")]
        [Tooltip("Label pendek seperti Melee, Ranged, Counter, AoE, atau Burst. Ini hanya informasi tampilan.")]
        public string[] tags;
    }

    [Header("Auto Source")]
    [Tooltip("SkillBase adalah sumber urutan skill dan tombol input. Jika kosong, script mencari otomatis.")]
    [SerializeField] private SkillBase skillBase;

    [Header("HUD Presentation Only")]
    [Tooltip("Hanya data tampilan. Cooldown dan energy cost tetap dibaca langsung dari script skill asli.")]
    [SerializeField] private SkillHUDPresentation[] slots = new SkillHUDPresentation[4];

    private const int RequiredSlotCount = 4;

    public SkillBase SkillBase
    {
        get
        {
            if (skillBase == null)
                skillBase = FindSkillBase();

            return skillBase;
        }
    }

    public int SlotCount => slots != null ? slots.Length : 0;

    private void Reset()
    {
        skillBase = FindSkillBase();
        EnsureSlotArray();
        ApplyDefaultSlotStyles();
    }

    private void Awake()
    {
        if (skillBase == null)
            skillBase = FindSkillBase();

        EnsureSlotArray();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (skillBase == null)
            skillBase = FindSkillBase();

        EnsureSlotArray();
        ApplyDefaultSlotStyles();
    }
#endif

    public SkillHUDPresentation GetPresentation(int slotIndex)
    {
        EnsureSlotArray();

        if (slotIndex < 0 || slotIndex >= slots.Length)
            return null;

        return slots[slotIndex];
    }

    public Sprite GetIcon(int slotIndex)
    {
        SkillHUDPresentation presentation = GetPresentation(slotIndex);
        return presentation != null ? presentation.icon : null;
    }

    public Sprite GetSlotIcon(int slotIndex)
    {
        return GetIcon(slotIndex);
    }

    public string GetDisplayName(int slotIndex)
    {
        SkillHUDPresentation presentation = GetPresentation(slotIndex);

        if (presentation != null && !string.IsNullOrWhiteSpace(presentation.displayName))
            return presentation.displayName;

        string slotName = GetSlotNameFromSkillBase(slotIndex);

        if (!string.IsNullOrWhiteSpace(slotName))
            return slotName;

        MonoBehaviour skillBehaviour = GetSkillBehaviour(slotIndex);

        if (skillBehaviour != null)
            return skillBehaviour.GetType().Name;

        return string.Empty;
    }

    public string GetShortDescription(int slotIndex)
    {
        SkillHUDPresentation presentation = GetPresentation(slotIndex);
        return presentation != null ? presentation.shortDescription : string.Empty;
    }

    public string GetTagLine(int slotIndex)
    {
        SkillHUDPresentation presentation = GetPresentation(slotIndex);

        if (presentation == null || presentation.tags == null || presentation.tags.Length == 0)
            return string.Empty;

        return string.Join(" • ", presentation.tags);
    }

    public MonoBehaviour GetSkillBehaviour(int slotIndex)
    {
        SkillBase source = SkillBase;

        if (source == null || source.slots == null)
            return null;

        if (slotIndex < 0 || slotIndex >= source.slots.Length)
            return null;

        SkillSlot slot = source.slots[slotIndex];

        if (slot == null)
            return null;

        return slot.skillBehaviour;
    }

    public KeyCode GetSlotKey(int slotIndex)
    {
        SkillBase source = SkillBase;

        if (source == null)
            return KeyCode.None;

        switch (slotIndex)
        {
            case 0:
                return source.slot1Key;

            case 1:
                return source.slot2Key;

            case 2:
                return source.slot3Key;

            case 3:
                return source.slot4Key;

            default:
                return KeyCode.None;
        }
    }

    public string GetSlotNameFromSkillBase(int slotIndex)
    {
        SkillBase source = SkillBase;

        if (source == null || source.slots == null)
            return string.Empty;

        if (slotIndex < 0 || slotIndex >= source.slots.Length)
            return string.Empty;

        SkillSlot slot = source.slots[slotIndex];

        if (slot == null)
            return string.Empty;

        return slot.slotName;
    }

    public WeaponType GetWeaponTypeFromSkillBase(int slotIndex)
    {
        SkillBase source = SkillBase;

        if (source == null || source.slots == null)
            return WeaponType.None;

        if (slotIndex < 0 || slotIndex >= source.slots.Length)
            return WeaponType.None;

        SkillSlot slot = source.slots[slotIndex];

        if (slot == null)
            return WeaponType.None;

        return slot.weaponType;
    }

    public int FindSlotIndexBySkillBehaviour(MonoBehaviour skillBehaviour)
    {
        if (skillBehaviour == null)
            return -1;

        for (int i = 0; i < RequiredSlotCount; i++)
        {
            MonoBehaviour candidate = GetSkillBehaviour(i);

            if (candidate == null)
                continue;

            if (candidate == skillBehaviour)
                return i;

            if (candidate.GetInstanceID() == skillBehaviour.GetInstanceID())
                return i;
        }

        return -1;
    }

    private SkillBase FindSkillBase()
    {
        SkillBase found = GetComponent<SkillBase>();

        if (found != null)
            return found;

        found = GetComponentInChildren<SkillBase>(true);

        if (found != null)
            return found;

        found = GetComponentInParent<SkillBase>();

        return found;
    }

    private void EnsureSlotArray()
    {
        if (slots == null || slots.Length != RequiredSlotCount)
        {
            SkillHUDPresentation[] oldSlots = slots;
            slots = new SkillHUDPresentation[RequiredSlotCount];

            for (int i = 0; i < RequiredSlotCount; i++)
            {
                if (oldSlots != null && i < oldSlots.Length && oldSlots[i] != null)
                    slots[i] = oldSlots[i];
                else
                    slots[i] = new SkillHUDPresentation();
            }
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null)
                slots[i] = new SkillHUDPresentation();
        }
    }

    private void ApplyDefaultSlotStyles()
    {
        EnsureSlotArray();

        slots[0].slotStyle = SkillHUDSlotStyle.Primary;
        slots[1].slotStyle = SkillHUDSlotStyle.Secondary;
        slots[2].slotStyle = SkillHUDSlotStyle.SkillA;
        slots[3].slotStyle = SkillHUDSlotStyle.SkillB;
    }
}
