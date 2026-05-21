using System;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class SkillIndexHUDController : MonoBehaviour
{
    public static SkillIndexHUDController Instance { get; private set; }

    public enum SkillReadyDisplay
    {
        EnergyMode,
        CooldownMode,
        AnimationMode
    }

    public enum IconFlashTiming
    {
        Off,
        WhenUsed,
        WhenReady,
        UsedAndReady
    }

    public enum BasicAttackSlotDisplay
    {
        Separate,
        Merged
    }

    public enum MergedIconStatus
    {
        EachIcon,
        WholeSlot
    }

    public enum BasicAttackCooldownDisplay
    {
        AutoFromSkill,
        Hide,
        OnlyWhenFound
    }

    private struct SlotState
    {
        public bool hasSkill;
        public bool energyReady;
        public bool cooldownReady;
        public bool animationReady;
        public bool finalReady;
        public bool hasCooldownSource;
        public float cooldownDuration;
        public float cooldownRemaining;
    }

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
    [Tooltip("HUD otomatis membaca SkillRootHUDProfile dari player aktif. Aktifkan ini untuk scene yang memakai switch prefab.")]
    [SerializeField] private bool autoFindActiveProfile = true;

    [Tooltip("Interval pencarian ulang profile aktif setelah player berganti prefab.")]
    [SerializeField, Min(0.05f)] private float autoFindInterval = 0.25f;

    [Tooltip("Khusus debugging. Jika diisi, HUD memakai profile ini dan tidak mencari player aktif.")]
    [SerializeField] private SkillRootHUDProfile forcedProfile;

    [Header("Skill Ready Display")]
    [Tooltip("Menentukan cara ikon skill memberi tahu pemain bahwa skill siap atau belum siap digunakan.")]
    [SerializeField] private SkillReadyDisplay skillReadyDisplay = SkillReadyDisplay.EnergyMode;

    [Tooltip("Energy Mode: ikon redup jika energy tidak cukup. Cooldown Mode: ikon memakai overlay cooldown dari script skill. Animation Mode: ikon mengikuti status cast/recovery dari script skill.")]
    [SerializeField] private bool showModeHelper = true;

    [Header("Flash")]
    [Tooltip("Menentukan kapan ikon memberi flash singkat.")]
    [SerializeField] private IconFlashTiming iconFlashTiming = IconFlashTiming.WhenReady;

    [Header("Basic Attack Display")]
    [Tooltip("Menentukan apakah basic attack dan charged attack tampil terpisah atau digabung dalam satu kotak HUD.")]
    [SerializeField] private BasicAttackSlotDisplay basicAttackSlotDisplay = BasicAttackSlotDisplay.Separate;

    [Tooltip("Saat basic attack digabung, Each Icon membuat ikon kiri/kanan bisa redup sendiri-sendiri. Whole Slot membuat satu kotak redup bersama.")]
    [SerializeField] private MergedIconStatus mergedIconStatus = MergedIconStatus.EachIcon;

    [Tooltip("Mengatur tampilan cooldown untuk basic attack dan charged attack. Nilai cooldown tetap dibaca dari script skill asli.")]
    [SerializeField] private BasicAttackCooldownDisplay basicAttackCooldownDisplay = BasicAttackCooldownDisplay.OnlyWhenFound;

    [Header("Cooldown Visual")]
    [Tooltip("Menampilkan angka sisa cooldown di atas ikon.")]
    [SerializeField] private bool showCooldownNumber = true;

    [Tooltip("Arah overlay cooldown. TopToBottom berarti lapisan gelap turun/hilang dari atas ke bawah.")]
    [SerializeField] private SkillHUDSlotUI.CooldownFillDirection cooldownFillDirection = SkillHUDSlotUI.CooldownFillDirection.TopToBottom;

    [Tooltip("Aktifkan hanya jika cooldown HUD harus tetap berjalan ketika Time.timeScale = 0.")]
    [SerializeField] private bool useUnscaledTimeForCooldown = false;

    [Header("Optional Presentation Forwarding")]
    [Tooltip("Mengirim nama, deskripsi, dan tag ke slot UI jika object slot menyediakan teks tambahan.")]
    [SerializeField] private bool forwardPresentationTextToSlot = true;

    [Header("Debug")]
    [Tooltip("Menampilkan log profile dan skill yang sedang dibaca HUD.")]
    [SerializeField] private bool debugLog = true;

    [Tooltip("Menampilkan peringatan jika cooldown atau energy cost tidak ditemukan dari script skill.")]
    [SerializeField] private bool debugMissingMechanicSource = false;

    private const int SlotCount = 4;

    private readonly float[] remainingCooldowns = new float[SlotCount];
    private readonly float[] cachedCooldownDurations = new float[SlotCount];
    private readonly bool[] lastReadyStates = new bool[SlotCount];
    private readonly bool[] hasLastReadyStates = new bool[SlotCount];

    private SkillRootHUDProfile activeProfile;
    private CharacterBase energyTarget;
    private float nextAutoFindTime;

    private static readonly string[] CooldownMemberNames =
    {
        "jedaSkill",
        "comboCooldown",
        "cooldownTime",
        "cooldownDuration",
        "cooldownTimeAfterUse",
        "skillCooldown",
        "shootCooldown",
        "cooldown",
        "cooldownSeconds",
        "cooldownDelay"
    };

    private static readonly string[] EnergyMemberNames =
    {
        "EnergyCost",
        "energyCost",
        "biayaEnergi",
        "manaCost",
        "costEnergy",
        "skillEnergyCost"
    };

    private static readonly string[] BusyBoolMemberNames =
    {
        "sedangCast",
        "sedangCharge",
        "sedangCooldown",
        "isCasting",
        "isCharging",
        "isCooldown",
        "isOnCooldown",
        "isAttacking",
        "isBusy",
        "isRecovering"
    };

    private static readonly string[] ActionDurationMemberNames =
    {
        "duration",
        "stanceDuration",
        "castDuration",
        "recoveryDuration",
        "attackDuration",
        "maxChargeTime",
        "durasiChargePenuh"
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
        ConfigureAllSlotFillDirections();
        BindProfile(true);
    }

    private void OnEnable()
    {
        Instance = this;
        PlayerPrefabSwitchManager.OnActiveWeaponChanged -= HandleActiveWeaponChanged;
        PlayerPrefabSwitchManager.OnActiveWeaponChanged += HandleActiveWeaponChanged;

        ConfigureAllSlotFillDirections();
        BindProfile(true);
    }

    private void OnDisable()
    {
        PlayerPrefabSwitchManager.OnActiveWeaponChanged -= HandleActiveWeaponChanged;

        if (Instance == this)
            Instance = null;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssignSlots();
        ConfigureAllSlotFillDirections();
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

    public bool IsSlotReady(int zeroBasedSlotIndex)
    {
        SlotState state = BuildSlotState(zeroBasedSlotIndex);
        return state.hasSkill && state.finalReady;
    }

    private void NotifySkillUsed(MonoBehaviour skillBehaviour, float cooldownOverride, bool hasOverride)
    {
        if (skillBehaviour == null)
            return;

        BindProfile(false);

        int slotIndex = activeProfile != null
            ? activeProfile.FindSlotIndexBySkillBehaviour(skillBehaviour)
            : -1;

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

    private void HandleActiveWeaponChanged(WeaponType weapon)
    {
        BindProfile(true);
    }

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
        ResetReadyTracking();

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
        if (activeProfile == null)
        {
            HideAllSlots();
            return;
        }

        if (basicAttackSlotDisplay == BasicAttackSlotDisplay.Merged)
        {
            UpdateMergedBasicSlot();
            SetSlotActive(secondarySlot, false);
            UpdateSlot(2, skillASlot);
            UpdateSlot(3, skillBSlot);
            return;
        }

        UpdateSlot(0, primarySlot);
        UpdateSlot(1, secondarySlot);
        UpdateSlot(2, skillASlot);
        UpdateSlot(3, skillBSlot);
    }

    private void UpdateMergedBasicSlot()
    {
        if (primarySlot == null)
            return;

        SlotState primaryState = BuildSlotState(0);
        SlotState secondaryState = BuildSlotState(1);

        if (!primaryState.hasSkill && !secondaryState.hasSkill)
        {
            primarySlot.gameObject.SetActive(false);
            return;
        }

        primarySlot.gameObject.SetActive(true);

        primarySlot.SetMergedIcons(activeProfile.GetIcon(0), activeProfile.GetIcon(1));
        primarySlot.SetKeyText(ConvertKeyToLabel(activeProfile.GetSlotKey(0)));
        primarySlot.SetSecondaryKeyText(ConvertKeyToLabel(activeProfile.GetSlotKey(1)));

        if (mergedIconStatus == MergedIconStatus.EachIcon)
        {
            primarySlot.SetIconAvailable(!primaryState.hasSkill || primaryState.finalReady);
            primarySlot.SetSecondaryIconAvailable(!secondaryState.hasSkill || secondaryState.finalReady);
            primarySlot.SetDarkOverlayVisible(
                (primaryState.hasSkill && !primaryState.finalReady) &&
                (secondaryState.hasSkill && !secondaryState.finalReady)
            );
        }
        else
        {
            bool wholeReady =
                (!primaryState.hasSkill || primaryState.finalReady) &&
                (!secondaryState.hasSkill || secondaryState.finalReady);

            primarySlot.SetAvailable(wholeReady);
        }

        float visibleRemaining = Mathf.Max(primaryState.cooldownRemaining, secondaryState.cooldownRemaining);
        float visibleDuration = Mathf.Max(primaryState.cooldownDuration, secondaryState.cooldownDuration);

        bool showCooldown = ShouldShowCooldownForMerged(primaryState, secondaryState);

        primarySlot.SetCooldown(
            showCooldown ? visibleRemaining : 0f,
            showCooldown ? visibleDuration : 0f,
            showCooldownNumber
        );

        if (forwardPresentationTextToSlot)
            ForwardPresentationToSlot(0, primarySlot);

        TrackReadyFlash(0, primaryState.finalReady, primarySlot);
        TrackReadyFlash(1, secondaryState.finalReady, primarySlot);
    }

    private void UpdateSlot(int slotIndex, SkillHUDSlotUI slotUI)
    {
        if (slotUI == null)
            return;

        SlotState state = BuildSlotState(slotIndex);

        if (!state.hasSkill)
        {
            slotUI.gameObject.SetActive(false);
            return;
        }

        slotUI.gameObject.SetActive(true);

        slotUI.SetIcon(activeProfile.GetIcon(slotIndex));
        slotUI.SetSecondaryIcon(null);
        slotUI.SetKeyText(ConvertKeyToLabel(activeProfile.GetSlotKey(slotIndex)));
        slotUI.SetSecondaryKeyText(string.Empty);

        slotUI.SetAvailable(state.finalReady);

        bool showCooldown = ShouldShowCooldownForSlot(slotIndex, state);

        slotUI.SetCooldown(
            showCooldown ? state.cooldownRemaining : 0f,
            showCooldown ? state.cooldownDuration : 0f,
            showCooldownNumber
        );

        if (forwardPresentationTextToSlot)
            ForwardPresentationToSlot(slotIndex, slotUI);

        TrackReadyFlash(slotIndex, state.finalReady, slotUI);
    }

    private SlotState BuildSlotState(int slotIndex)
    {
        SlotState state = new SlotState();

        if (activeProfile == null)
            return state;

        MonoBehaviour skillBehaviour = activeProfile.GetSkillBehaviour(slotIndex);

        if (skillBehaviour == null)
            return state;

        state.hasSkill = true;

        state.hasCooldownSource = TryGetCooldownDuration(skillBehaviour, out state.cooldownDuration);
        state.cooldownDuration = Mathf.Max(0f, state.cooldownDuration);
        state.cooldownRemaining = Mathf.Clamp(remainingCooldowns[slotIndex], 0f, Mathf.Max(0f, state.cooldownDuration));

        state.cooldownReady = state.cooldownRemaining <= 0.05f;
        state.energyReady = IsEnergyAvailable(skillBehaviour);
        state.animationReady = IsAnimationReady(skillBehaviour, slotIndex, state);

        switch (skillReadyDisplay)
        {
            case SkillReadyDisplay.EnergyMode:
                state.finalReady = state.energyReady;
                break;

            case SkillReadyDisplay.CooldownMode:
                state.finalReady = state.cooldownReady;
                break;

            case SkillReadyDisplay.AnimationMode:
                state.finalReady = state.animationReady;
                break;

            default:
                state.finalReady = state.energyReady;
                break;
        }

        return state;
    }

    private bool ShouldShowCooldownForSlot(int slotIndex, SlotState state)
    {
        if (skillReadyDisplay != SkillReadyDisplay.CooldownMode &&
            skillReadyDisplay != SkillReadyDisplay.AnimationMode)
        {
            return false;
        }

        if (!state.hasCooldownSource || state.cooldownDuration <= 0.05f)
            return false;

        if (slotIndex <= 1)
        {
            if (basicAttackCooldownDisplay == BasicAttackCooldownDisplay.Hide)
                return false;

            if (basicAttackCooldownDisplay == BasicAttackCooldownDisplay.OnlyWhenFound && !state.hasCooldownSource)
                return false;
        }

        return state.cooldownRemaining > 0.05f;
    }

    private bool ShouldShowCooldownForMerged(SlotState primaryState, SlotState secondaryState)
    {
        if (skillReadyDisplay != SkillReadyDisplay.CooldownMode &&
            skillReadyDisplay != SkillReadyDisplay.AnimationMode)
        {
            return false;
        }

        if (basicAttackCooldownDisplay == BasicAttackCooldownDisplay.Hide)
            return false;

        bool hasAnyCooldown =
            (primaryState.hasCooldownSource && primaryState.cooldownRemaining > 0.05f) ||
            (secondaryState.hasCooldownSource && secondaryState.cooldownRemaining > 0.05f);

        return hasAnyCooldown;
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

        float cooldownDuration = 0f;
        bool hasCooldown = false;

        if (hasOverride)
        {
            cooldownDuration = Mathf.Max(0f, cooldownOverride);
            hasCooldown = cooldownDuration > 0.05f;
        }
        else
        {
            hasCooldown = TryGetCooldownDuration(skillBehaviour, out cooldownDuration);
            cooldownDuration = Mathf.Max(0f, cooldownDuration);
        }

        cachedCooldownDurations[slotIndex] = cooldownDuration;

        if (hasCooldown && cooldownDuration > 0.05f)
            remainingCooldowns[slotIndex] = cooldownDuration;
        else
            remainingCooldowns[slotIndex] = 0f;

        SkillHUDSlotUI slotUI = GetVisibleSlotUI(slotIndex);

        if (slotUI != null && ShouldFlashWhenUsed())
            slotUI.Flash();

        if (debugLog)
        {
            Debug.Log(
                "[SKILL INDEX HUD] Skill digunakan. Slot: " +
                slotIndex +
                " | Skill: " +
                skillBehaviour.GetType().Name +
                " | Cooldown terbaca: " +
                (hasCooldown ? cooldownDuration.ToString("0.00") : "tidak ada"),
                this
            );
        }
    }

    private SkillHUDSlotUI GetVisibleSlotUI(int slotIndex)
    {
        if (basicAttackSlotDisplay == BasicAttackSlotDisplay.Merged && slotIndex <= 1)
            return primarySlot;

        return GetSlotUI(slotIndex);
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
        {
            remainingCooldowns[i] = 0f;
            cachedCooldownDurations[i] = 0f;
        }
    }

    private void ResetReadyTracking()
    {
        for (int i = 0; i < hasLastReadyStates.Length; i++)
        {
            hasLastReadyStates[i] = false;
            lastReadyStates[i] = true;
        }
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

    private void TrackReadyFlash(int slotIndex, bool isReady, SkillHUDSlotUI slotUI)
    {
        if (slotIndex < 0 || slotIndex >= SlotCount || slotUI == null)
            return;

        if (!hasLastReadyStates[slotIndex])
        {
            hasLastReadyStates[slotIndex] = true;
            lastReadyStates[slotIndex] = isReady;
            return;
        }

        bool becameReady = !lastReadyStates[slotIndex] && isReady;

        if (becameReady && ShouldFlashWhenReady())
            slotUI.Flash();

        lastReadyStates[slotIndex] = isReady;
    }

    private bool ShouldFlashWhenUsed()
    {
        return iconFlashTiming == IconFlashTiming.WhenUsed ||
               iconFlashTiming == IconFlashTiming.UsedAndReady;
    }

    private bool ShouldFlashWhenReady()
    {
        return iconFlashTiming == IconFlashTiming.WhenReady ||
               iconFlashTiming == IconFlashTiming.UsedAndReady;
    }

    // =========================================================
    // PRESENTATION FORWARDING
    // =========================================================

    private void ForwardPresentationToSlot(int slotIndex, SkillHUDSlotUI slotUI)
    {
        if (slotUI == null || activeProfile == null)
            return;

        string displayName = activeProfile.GetDisplayName(slotIndex);
        string description = activeProfile.GetShortDescription(slotIndex);
        string tagLine = activeProfile.GetTagLine(slotIndex);

        slotUI.SetSkillName(displayName);
        slotUI.SetDescription(description);
        slotUI.SetTagLine(tagLine);
    }

    // =========================================================
    // MECHANIC READERS
    // Angka tetap dibaca dari script skill asli.
    // =========================================================

    private bool TryGetCooldownDuration(MonoBehaviour skillBehaviour, out float cooldown)
    {
        cooldown = 0f;

        if (skillBehaviour == null)
            return false;

        if (TryReadSpecialCooldown(skillBehaviour, out cooldown))
        {
            cooldown = Mathf.Max(0f, cooldown);
            return cooldown > 0.05f;
        }

        if (TryReadFirstFloatMember(skillBehaviour, CooldownMemberNames, out cooldown))
        {
            cooldown = Mathf.Max(0f, cooldown);
            return cooldown > 0.05f;
        }

        if (debugMissingMechanicSource)
        {
            Debug.LogWarning(
                "[SKILL INDEX HUD] Tidak menemukan field cooldown pada " +
                skillBehaviour.GetType().Name +
                ". HUD tidak membuat cooldown palsu.",
                skillBehaviour
            );
        }

        cooldown = 0f;
        return false;
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

    private bool IsEnergyAvailable(MonoBehaviour skillBehaviour)
    {
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

    private bool IsAnimationReady(MonoBehaviour skillBehaviour, int slotIndex, SlotState state)
    {
        if (skillBehaviour == null)
            return true;

        if (TryReadAnyBusyBool(skillBehaviour, out bool busy))
            return !busy;

        if (state.cooldownRemaining > 0.05f)
            return false;

        return true;
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

    private bool TryReadAnyBusyBool(MonoBehaviour target, out bool busy)
    {
        busy = false;

        if (target == null)
            return false;

        for (int i = 0; i < BusyBoolMemberNames.Length; i++)
        {
            if (TryReadBoolMember(target, BusyBoolMemberNames[i], out busy))
                return true;
        }

        return false;
    }

    private bool TryReadBoolMember(MonoBehaviour target, string memberName, out bool value)
    {
        value = false;

        if (target == null || string.IsNullOrWhiteSpace(memberName))
            return false;

        Type type = target.GetType();

        FieldInfo field = type.GetField(memberName, MemberFlags);

        if (field != null)
            return TryConvertToBool(field.GetValue(target), out value);

        PropertyInfo property = type.GetProperty(memberName, MemberFlags);

        if (property != null && property.CanRead)
            return TryConvertToBool(property.GetValue(target, null), out value);

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

    private bool TryConvertToBool(object rawValue, out bool value)
    {
        value = false;

        if (rawValue == null)
            return false;

        try
        {
            value = Convert.ToBoolean(rawValue);
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

    private void ConfigureAllSlotFillDirections()
    {
        if (primarySlot != null)
            primarySlot.SetFillDirection(cooldownFillDirection);

        if (secondarySlot != null)
            secondarySlot.SetFillDirection(cooldownFillDirection);

        if (skillASlot != null)
            skillASlot.SetFillDirection(cooldownFillDirection);

        if (skillBSlot != null)
            skillBSlot.SetFillDirection(cooldownFillDirection);
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
                return "LMB";

            case KeyCode.Mouse1:
                return "RMB";

            case KeyCode.Mouse2:
                return "MMB";

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
