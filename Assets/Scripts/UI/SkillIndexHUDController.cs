using System;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class SkillIndexHUDController : MonoBehaviour
{
    public static SkillIndexHUDController Instance { get; private set; }

    [Header("HUD Slot References")]
    [Tooltip("Isi dengan Slot_Primary. Jika nama object benar, script akan mencoba mencari otomatis.")]
    [SerializeField] private SkillHUDSlotUI primarySlot;

    [Tooltip("Isi dengan Slot_Secondary. Jika nama object benar, script akan mencoba mencari otomatis.")]
    [SerializeField] private SkillHUDSlotUI secondarySlot;

    [Tooltip("Isi dengan Slot_SkillA. Jika nama object benar, script akan mencoba mencari otomatis.")]
    [SerializeField] private SkillHUDSlotUI skillASlot;

    [Tooltip("Isi dengan Slot_SkillB. Jika nama object benar, script akan mencoba mencari otomatis.")]
    [SerializeField] private SkillHUDSlotUI skillBSlot;

    [Header("Active Profile Binding")]
    [Tooltip("Aktifkan agar HUD otomatis membaca SkillRootHUDProfile dari player aktif.")]
    [SerializeField] private bool autoFindActiveProfile = true;

    [Tooltip("Interval pencarian ulang profile aktif. Berguna setelah switch prefab player.")]
    [SerializeField, Min(0.05f)] private float autoFindInterval = 0.25f;

    [Tooltip("Khusus debugging. Jika diisi, HUD akan memakai profile ini dan mengabaikan player aktif.")]
    [SerializeField] private SkillRootHUDProfile forcedProfile;

    [Header("Cooldown Presentation")]
    [Tooltip("Menampilkan lapisan cooldown pada ikon. Durasi tetap dibaca dari script skill asli.")]
    [SerializeField] private bool showCooldownFill = true;

    [Tooltip("Menampilkan angka sisa cooldown. Angka tetap berasal dari durasi script skill asli.")]
    [SerializeField] private bool showCooldownNumber = true;

    [Tooltip("Aktifkan hanya jika cooldown HUD harus tetap berjalan ketika Time.timeScale = 0.")]
    [SerializeField] private bool useUnscaledTimeForCooldown = false;

    [Header("Energy Presentation")]
    [Tooltip("Jika aktif, slot Skill A dan Skill B akan diredupkan ketika energy player tidak cukup.")]
    [SerializeField] private bool dimSkillAAndSkillBWhenEnergyIsLow = true;

    [Tooltip("Biasanya dimatikan karena Primary dan Secondary tidak perlu diredupkan oleh energy pada prototype saat ini.")]
    [SerializeField] private bool dimPrimaryAndSecondaryWhenEnergyIsLow = false;

    [Header("Optional Presentation Forwarding")]
    [Tooltip("Mengirim displayName, description, dan tag ke SkillHUDSlotUI jika script slot UI punya method penerima. Aman walaupun method tidak ada.")]
    [SerializeField] private bool forwardPresentationTextToSlot = true;

    [Header("Debug")]
    [Tooltip("Menampilkan log profile dan skill yang sedang dibaca HUD.")]
    [SerializeField] private bool debugLog = true;

    [Tooltip("Menampilkan peringatan jika cooldown atau energy cost tidak ditemukan dari script skill.")]
    [SerializeField] private bool debugMissingMechanicSource = false;

    private const int SlotCount = 4;

    private readonly float[] remainingCooldowns = new float[SlotCount];

    private SkillRootHUDProfile activeProfile;
    private CharacterBase energyTarget;
    private float nextAutoFindTime;

    private static readonly string[] CooldownMemberNames =
    {
        "jedaSkill",
        "comboCooldown",
        "cooldownTime",
        "shootCooldown",
        "cooldownDuration",
        "skillCooldown",
        "cooldown",
        "cooldownSeconds",
        "duration"
    };

    private static readonly string[] EnergyMemberNames =
    {
        "energyCost",
        "biayaEnergi",
        "manaCost",
        "costEnergy",
        "skillEnergyCost"
    };

    private const BindingFlags MemberFlags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    private void Reset()
    {
        AutoAssignSlots();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[SKILL INDEX HUD] Ada lebih dari satu SkillIndexHUDController aktif.", this);
        }

        Instance = this;

        AutoAssignSlots();
        BindProfile(true);
    }

    private void OnEnable()
    {
        Instance = this;
        BindProfile(true);
    }

    private void OnDisable()
    {
        if (Instance == this)
            Instance = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssignSlots();
    }
#endif

    private void Update()
    {
        AutoRebindProfileTick();
        UpdateCooldownTimers();
        UpdateAllSlots();
    }

    // =========================================================
    // GLOBAL NOTIFY API
    // Dipanggil dari script skill setelah skill benar-benar valid digunakan.
    // =========================================================

    public static void NotifyGlobalSkillUsed(MonoBehaviour skillBehaviour)
    {
        if (Instance == null)
            return;

        Instance.NotifySkillUsed(skillBehaviour, -1f, false);
    }

    public static void NotifyGlobalSkillUsed(MonoBehaviour skillBehaviour, float cooldownOverride)
    {
        if (Instance == null)
            return;

        Instance.NotifySkillUsed(skillBehaviour, cooldownOverride, true);
    }

    public static void NotifyGlobalSkillUsedBySlot(int zeroBasedSlotIndex)
    {
        if (Instance == null)
            return;

        Instance.TriggerSlotVisual(zeroBasedSlotIndex, -1f, false);
    }

    public static void NotifyGlobalSkillUsedBySlot(int zeroBasedSlotIndex, float cooldownOverride)
    {
        if (Instance == null)
            return;

        Instance.TriggerSlotVisual(zeroBasedSlotIndex, cooldownOverride, true);
    }

    public static void NotifyGlobalSkillUsed(string skillTypeNameOrDisplayName)
    {
        if (Instance == null)
            return;

        Instance.NotifySkillUsedByName(skillTypeNameOrDisplayName, -1f, false);
    }

    public static void NotifyGlobalSkillUsed(string skillTypeNameOrDisplayName, float cooldownOverride)
    {
        if (Instance == null)
            return;

        Instance.NotifySkillUsedByName(skillTypeNameOrDisplayName, cooldownOverride, true);
    }

    private void NotifySkillUsed(MonoBehaviour skillBehaviour, float cooldownOverride, bool hasOverride)
    {
        if (skillBehaviour == null)
            return;

        BindProfile(false);

        int slotIndex = FindSlotIndexBySkillBehaviour(skillBehaviour);

        if (slotIndex < 0)
        {
            if (debugLog)
            {
                Debug.LogWarning(
                    "[SKILL INDEX HUD] Skill tidak ditemukan pada SkillRootHUDProfile aktif: " +
                    skillBehaviour.GetType().Name,
                    this
                );
            }

            return;
        }

        TriggerSlotVisual(slotIndex, cooldownOverride, hasOverride);
    }

    private void NotifySkillUsedByName(string skillTypeNameOrDisplayName, float cooldownOverride, bool hasOverride)
    {
        if (string.IsNullOrWhiteSpace(skillTypeNameOrDisplayName))
            return;

        BindProfile(false);

        int slotIndex = FindSlotIndexByName(skillTypeNameOrDisplayName);

        if (slotIndex < 0)
        {
            if (debugLog)
            {
                Debug.LogWarning(
                    "[SKILL INDEX HUD] Skill tidak ditemukan berdasarkan nama: " +
                    skillTypeNameOrDisplayName,
                    this
                );
            }

            return;
        }

        TriggerSlotVisual(slotIndex, cooldownOverride, hasOverride);
    }

    // =========================================================
    // PROFILE BINDING
    // =========================================================

    private void AutoRebindProfileTick()
    {
        if (!autoFindActiveProfile)
            return;

        if (Time.unscaledTime < nextAutoFindTime)
            return;

        nextAutoFindTime = Time.unscaledTime + autoFindInterval;
        BindProfile(false);
    }

    private void BindProfile(bool force)
    {
        SkillRootHUDProfile foundProfile = FindTargetProfile();

        if (!force && foundProfile == activeProfile)
            return;

        activeProfile = foundProfile;

        if (activeProfile == null)
        {
            energyTarget = null;
            ClearCooldowns();
            HideAllSlots();

            if (debugLog)
                Debug.Log("[SKILL INDEX HUD] SkillRootHUDProfile aktif belum ditemukan.", this);

            return;
        }

        energyTarget = activeProfile.GetComponentInParent<CharacterBase>();
        ClearCooldowns();

        if (debugLog)
        {
            Debug.Log(
                "[SKILL INDEX HUD] Profile aktif: " +
                activeProfile.name,
                this
            );
        }
    }

    private SkillRootHUDProfile FindTargetProfile()
    {
        if (forcedProfile != null)
            return forcedProfile;

        SkillRootHUDProfile profileFromPlayer = FindProfileFromTaggedPlayer();

        if (profileFromPlayer != null)
            return profileFromPlayer;

        return FindFirstActiveProfileInScene();
    }

    private SkillRootHUDProfile FindProfileFromTaggedPlayer()
    {
        GameObject playerObject = null;

        try
        {
            playerObject = GameObject.FindGameObjectWithTag("Player");
        }
        catch
        {
            playerObject = null;
        }

        if (playerObject == null || !playerObject.activeInHierarchy)
            return null;

        SkillRootHUDProfile profile = playerObject.GetComponentInChildren<SkillRootHUDProfile>(true);

        if (profile != null && profile.gameObject.activeInHierarchy)
            return profile;

        return null;
    }

    private SkillRootHUDProfile FindFirstActiveProfileInScene()
    {
        SkillRootHUDProfile[] profiles = FindObjectsOfType<SkillRootHUDProfile>(true);

        foreach (SkillRootHUDProfile profile in profiles)
        {
            if (profile == null)
                continue;

            if (!profile.gameObject.activeInHierarchy)
                continue;

            return profile;
        }

        return null;
    }

    // =========================================================
    // SLOT UPDATE
    // =========================================================

    private void UpdateAllSlots()
    {
        UpdateSlot(0, primarySlot);
        UpdateSlot(1, secondarySlot);
        UpdateSlot(2, skillASlot);
        UpdateSlot(3, skillBSlot);
    }

    private void UpdateSlot(int slotIndex, SkillHUDSlotUI slotUI)
    {
        if (slotUI == null)
            return;

        if (activeProfile == null)
        {
            slotUI.gameObject.SetActive(false);
            return;
        }

        MonoBehaviour skillBehaviour = activeProfile.GetSkillBehaviour(slotIndex);

        if (skillBehaviour == null)
        {
            slotUI.gameObject.SetActive(false);
            return;
        }

        slotUI.gameObject.SetActive(true);

        slotUI.SetIcon(activeProfile.GetIcon(slotIndex));
        slotUI.SetKeyText(ConvertKeyToLabel(activeProfile.GetSlotKey(slotIndex)));

        float cooldownDuration = GetCooldownDuration(skillBehaviour);
        float visibleRemainingCooldown = showCooldownFill ? remainingCooldowns[slotIndex] : 0f;
        float visibleCooldownDuration = showCooldownFill ? cooldownDuration : 0f;

        slotUI.SetCooldown(
            visibleRemainingCooldown,
            visibleCooldownDuration,
            showCooldownNumber
        );

        bool energyAvailable = IsEnergyAvailableForSlot(slotIndex, skillBehaviour);
        slotUI.SetEnergyAvailable(energyAvailable);

        if (forwardPresentationTextToSlot)
            ForwardPresentationToSlot(slotIndex, slotUI);
    }

    private void TriggerSlotVisual(int slotIndex, float cooldownOverride, bool hasOverride)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount)
            return;

        BindProfile(false);

        if (activeProfile == null)
            return;

        MonoBehaviour skillBehaviour = activeProfile.GetSkillBehaviour(slotIndex);

        if (skillBehaviour == null)
            return;

        float cooldownDuration = hasOverride
            ? Mathf.Max(0f, cooldownOverride)
            : GetCooldownDuration(skillBehaviour);

        remainingCooldowns[slotIndex] = Mathf.Max(0f, cooldownDuration);

        SkillHUDSlotUI slotUI = GetSlotUI(slotIndex);

        if (slotUI != null)
            slotUI.Flash();

        if (debugLog)
        {
            Debug.Log(
                "[SKILL INDEX HUD] Skill digunakan. Slot: " +
                slotIndex +
                " | Skill: " +
                skillBehaviour.GetType().Name +
                " | Cooldown UI: " +
                remainingCooldowns[slotIndex].ToString("0.00"),
                this
            );
        }
    }

    private void UpdateCooldownTimers()
    {
        float deltaTime = useUnscaledTimeForCooldown
            ? Time.unscaledDeltaTime
            : Time.deltaTime;

        if (deltaTime <= 0f)
            return;

        for (int i = 0; i < remainingCooldowns.Length; i++)
        {
            if (remainingCooldowns[i] <= 0f)
                continue;

            remainingCooldowns[i] -= deltaTime;

            if (remainingCooldowns[i] < 0f)
                remainingCooldowns[i] = 0f;
        }
    }

    private void ClearCooldowns()
    {
        for (int i = 0; i < remainingCooldowns.Length; i++)
            remainingCooldowns[i] = 0f;
    }

    private void HideAllSlots()
    {
        SetSlotActive(primarySlot, false);
        SetSlotActive(secondarySlot, false);
        SetSlotActive(skillASlot, false);
        SetSlotActive(skillBSlot, false);
    }

    private void SetSlotActive(SkillHUDSlotUI slotUI, bool active)
    {
        if (slotUI != null)
            slotUI.gameObject.SetActive(active);
    }

    // =========================================================
    // PRESENTATION FORWARDING
    // Aman walau SkillHUDSlotUI belum punya method ini.
    // =========================================================

    private void ForwardPresentationToSlot(int slotIndex, SkillHUDSlotUI slotUI)
    {
        if (slotUI == null || activeProfile == null)
            return;

        string displayName = activeProfile.GetDisplayName(slotIndex);
        string description = activeProfile.GetShortDescription(slotIndex);
        string tagLine = activeProfile.GetTagLine(slotIndex);

        slotUI.SendMessage("SetSkillName", displayName, SendMessageOptions.DontRequireReceiver);
        slotUI.SendMessage("SetDisplayName", displayName, SendMessageOptions.DontRequireReceiver);

        slotUI.SendMessage("SetSkillDescription", description, SendMessageOptions.DontRequireReceiver);
        slotUI.SendMessage("SetDescription", description, SendMessageOptions.DontRequireReceiver);

        slotUI.SendMessage("SetTagLine", tagLine, SendMessageOptions.DontRequireReceiver);
        slotUI.SendMessage("SetTagText", tagLine, SendMessageOptions.DontRequireReceiver);
    }

    // =========================================================
    // COOLDOWN READER
    // Sumber angka tetap script skill asli, bukan Inspector HUD.
    // =========================================================

    private float GetCooldownDuration(MonoBehaviour skillBehaviour)
    {
        if (skillBehaviour == null)
            return 0f;

        if (TryReadSpecialCooldown(skillBehaviour, out float specialCooldown))
            return Mathf.Max(0f, specialCooldown);

        if (TryReadFirstFloatMember(skillBehaviour, CooldownMemberNames, out float cooldown))
            return Mathf.Max(0f, cooldown);

        if (debugMissingMechanicSource)
        {
            Debug.LogWarning(
                "[SKILL INDEX HUD] Tidak menemukan field cooldown pada " +
                skillBehaviour.GetType().Name +
                ". Cooldown HUD dianggap 0.",
                skillBehaviour
            );
        }

        return 0f;
    }

    private bool TryReadSpecialCooldown(MonoBehaviour skillBehaviour, out float cooldown)
    {
        cooldown = 0f;

        if (skillBehaviour == null)
            return false;

        string typeName = skillBehaviour.GetType().Name;

        if (typeName == "Sword_Riposte")
        {
            bool hasCooldownTime = TryReadFloatMember(skillBehaviour, "cooldownTime", out float cooldownTime);
            bool hasStanceDuration = TryReadFloatMember(skillBehaviour, "stanceDuration", out float stanceDuration);

            if (hasCooldownTime && hasStanceDuration)
            {
                cooldown = cooldownTime + stanceDuration;
                return true;
            }

            if (hasCooldownTime)
            {
                cooldown = cooldownTime;
                return true;
            }
        }

        return false;
    }

    // =========================================================
    // ENERGY READER
    // Sumber angka tetap script skill asli, bukan Inspector HUD.
    // =========================================================

    private bool IsEnergyAvailableForSlot(int slotIndex, MonoBehaviour skillBehaviour)
    {
        bool shouldCheckEnergy =
            (slotIndex <= 1 && dimPrimaryAndSecondaryWhenEnergyIsLow) ||
            (slotIndex >= 2 && dimSkillAAndSkillBWhenEnergyIsLow);

        if (!shouldCheckEnergy)
            return true;

        float energyCost = GetEnergyCost(skillBehaviour);

        if (energyCost <= 0f)
            return true;

        if (energyTarget == null)
            energyTarget = FindEnergyTarget();

        if (energyTarget == null)
            return true;

        return energyTarget.HasEnergy(energyCost);
    }

    private float GetEnergyCost(MonoBehaviour skillBehaviour)
    {
        if (skillBehaviour == null)
            return 0f;

        IEnergySkill energySkill = skillBehaviour as IEnergySkill;

        if (energySkill != null)
            return Mathf.Max(0f, energySkill.EnergyCost);

        if (TryReadFirstFloatMember(skillBehaviour, EnergyMemberNames, out float energyCost))
            return Mathf.Max(0f, energyCost);

        if (debugMissingMechanicSource)
        {
            Debug.LogWarning(
                "[SKILL INDEX HUD] Tidak menemukan field energy cost pada " +
                skillBehaviour.GetType().Name +
                ". Skill dianggap tidak membutuhkan energy untuk tampilan HUD.",
                skillBehaviour
            );
        }

        return 0f;
    }

    private CharacterBase FindEnergyTarget()
    {
        if (activeProfile != null)
        {
            CharacterBase characterFromProfile = activeProfile.GetComponentInParent<CharacterBase>();

            if (characterFromProfile != null && characterFromProfile.gameObject.activeInHierarchy)
                return characterFromProfile;
        }

        GameObject playerObject = null;

        try
        {
            playerObject = GameObject.FindGameObjectWithTag("Player");
        }
        catch
        {
            playerObject = null;
        }

        if (playerObject != null && playerObject.activeInHierarchy)
        {
            CharacterBase character = playerObject.GetComponent<CharacterBase>();

            if (character == null)
                character = playerObject.GetComponentInChildren<CharacterBase>(true);

            if (character == null)
                character = playerObject.GetComponentInParent<CharacterBase>();

            if (character != null)
                return character;
        }

        CharacterBase[] allCharacters = FindObjectsOfType<CharacterBase>(true);

        foreach (CharacterBase character in allCharacters)
        {
            if (character == null)
                continue;

            if (!character.gameObject.activeInHierarchy)
                continue;

            if (character.CompareTag("Player"))
                return character;
        }

        return null;
    }

    // =========================================================
    // REFLECTION UTILITY
    // =========================================================

    private bool TryReadFirstFloatMember(
        MonoBehaviour target,
        string[] memberNames,
        out float value
    )
    {
        value = 0f;

        if (target == null || memberNames == null)
            return false;

        for (int i = 0; i < memberNames.Length; i++)
        {
            string memberName = memberNames[i];

            if (string.IsNullOrWhiteSpace(memberName))
                continue;

            if (TryReadFloatMember(target, memberName, out value))
                return true;
        }

        return false;
    }

    private bool TryReadFloatMember(
        MonoBehaviour target,
        string memberName,
        out float value
    )
    {
        value = 0f;

        if (target == null)
            return false;

        if (string.IsNullOrWhiteSpace(memberName))
            return false;

        Type type = target.GetType();

        FieldInfo field = type.GetField(memberName, MemberFlags);

        if (field != null)
            return TryConvertToFloat(field.GetValue(target), out value);

        PropertyInfo property = type.GetProperty(memberName, MemberFlags);

        if (property != null && property.CanRead)
            return TryConvertToFloat(property.GetValue(target, null), out value);

        return false;
    }

    private bool TryConvertToFloat(object rawValue, out float value)
    {
        value = 0f;

        if (rawValue == null)
            return false;

        try
        {
            value = Convert.ToSingle(rawValue);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // =========================================================
    // SLOT FINDER
    // =========================================================

    private int FindSlotIndexBySkillBehaviour(MonoBehaviour skillBehaviour)
    {
        if (activeProfile == null || skillBehaviour == null)
            return -1;

        for (int i = 0; i < SlotCount; i++)
        {
            MonoBehaviour candidate = activeProfile.GetSkillBehaviour(i);

            if (candidate == null)
                continue;

            if (candidate == skillBehaviour)
                return i;

            if (candidate.GetInstanceID() == skillBehaviour.GetInstanceID())
                return i;
        }

        return -1;
    }

    private int FindSlotIndexByName(string skillTypeNameOrDisplayName)
    {
        if (activeProfile == null)
            return -1;

        for (int i = 0; i < SlotCount; i++)
        {
            MonoBehaviour skillBehaviour = activeProfile.GetSkillBehaviour(i);

            if (skillBehaviour != null)
            {
                string typeName = skillBehaviour.GetType().Name;

                if (string.Equals(typeName, skillTypeNameOrDisplayName, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            string displayName = activeProfile.GetDisplayName(i);

            if (!string.IsNullOrWhiteSpace(displayName) &&
                string.Equals(displayName, skillTypeNameOrDisplayName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }

            string slotName = activeProfile.GetSlotNameFromSkillBase(i);

            if (!string.IsNullOrWhiteSpace(slotName) &&
                string.Equals(slotName, skillTypeNameOrDisplayName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private SkillHUDSlotUI GetSlotUI(int slotIndex)
    {
        switch (slotIndex)
        {
            case 0:
                return primarySlot;

            case 1:
                return secondarySlot;

            case 2:
                return skillASlot;

            case 3:
                return skillBSlot;

            default:
                return null;
        }
    }

    // =========================================================
    // AUTO ASSIGN
    // =========================================================

    private void AutoAssignSlots()
    {
        if (primarySlot == null)
            primarySlot = FindSlotUI("Slot_Primary");

        if (secondarySlot == null)
            secondarySlot = FindSlotUI("Slot_Secondary");

        if (skillASlot == null)
            skillASlot = FindSlotUI("Slot_SkillA");

        if (skillBSlot == null)
            skillBSlot = FindSlotUI("Slot_SkillB");
    }

    private SkillHUDSlotUI FindSlotUI(string objectName)
    {
        SkillHUDSlotUI[] slots = GetComponentsInChildren<SkillHUDSlotUI>(true);

        foreach (SkillHUDSlotUI slot in slots)
        {
            if (slot == null)
                continue;

            if (slot.name == objectName)
                return slot;
        }

        return null;
    }

    // =========================================================
    // KEY LABEL
    // =========================================================

    private string ConvertKeyToLabel(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.None:
                return string.Empty;

            case KeyCode.Mouse0:
                return "M1";

            case KeyCode.Mouse1:
                return "M2";

            case KeyCode.Mouse2:
                return "M3";

            case KeyCode.LeftShift:
            case KeyCode.RightShift:
                return "SHIFT";

            case KeyCode.LeftControl:
            case KeyCode.RightControl:
                return "CTRL";

            case KeyCode.LeftAlt:
            case KeyCode.RightAlt:
                return "ALT";

            case KeyCode.Space:
                return "SPACE";

            case KeyCode.Alpha0:
                return "0";

            case KeyCode.Alpha1:
                return "1";

            case KeyCode.Alpha2:
                return "2";

            case KeyCode.Alpha3:
                return "3";

            case KeyCode.Alpha4:
                return "4";

            case KeyCode.Alpha5:
                return "5";

            case KeyCode.Alpha6:
                return "6";

            case KeyCode.Alpha7:
                return "7";

            case KeyCode.Alpha8:
                return "8";

            case KeyCode.Alpha9:
                return "9";

            default:
                return key.ToString().ToUpperInvariant();
        }
    }
}