using System;
using System.Reflection;
using UnityEngine;

public class PlayerPrefabSwitchManager : MonoBehaviour
{
    public static PlayerPrefabSwitchManager Instance { get; private set; }
    public static System.Action<WeaponType> OnActiveWeaponChanged;
    public static WeaponType CurrentWeapon { get; private set; } = WeaponType.None;

    [Header("Pickup Cleanup")]
    [SerializeField] private bool destroyAllPickupZonesAfterSwitch = true;
    [SerializeField] private float destroyPickupDelay = 0f;

    [Header("Active Player")]
    [SerializeField] private Transform activePlayerTransform;

    [Header("Player Prefabs")]
    [SerializeField] private GameObject playerSwordPrefab; // Player_W1
    [SerializeField] private GameObject playerBowPrefab;   // Player_W2

    [Header("Switch Rules")]
    [SerializeField] private bool onlyAllowOneSwitch = true;
    [SerializeField] private bool destroyOldPlayer = true;

    [Header("State Transfer")]
    [SerializeField] private bool copyHealthAndEnergy = true;
    [SerializeField] private bool copyMaxHPFromOldPlayer = true;
    [SerializeField] private bool copyFacingDirection = true;
    [SerializeField] private bool resetNewPlayerVelocity = true;

    private bool hasSwitched;

    private const BindingFlags RuntimeBindingFlags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (activePlayerTransform == null)
        {
            activePlayerTransform = FindInitialPlayer();
        }

        if (activePlayerTransform != null)
        {
            BindRuntimeSystems(activePlayerTransform, GetWeaponFromPlayer(activePlayerTransform));
        }
        else
        {
            Debug.LogWarning("[PLAYER SWITCH MANAGER] Active Player belum ditemukan. Isi Active Player dengan Player_W0.");
        }
    }

    public bool TrySwitchFromPickup(Collider2D enteringCollider, WeaponType targetWeapon)
    {
        if (enteringCollider == null)
            return false;

        PlayerWeaponIdentity oldIdentity = enteringCollider.GetComponentInParent<PlayerWeaponIdentity>();

        if (oldIdentity == null)
        {
            Debug.LogWarning(
                "[PLAYER SWITCH MANAGER] Objek yang masuk pickup tidak memiliki PlayerWeaponIdentity."
            );
            return false;
        }

        if (!oldIdentity.CanUseWeaponPickup)
        {
            Debug.Log(
                "[PLAYER SWITCH MANAGER] Switch ditolak. Player ini bukan Player_W0 / Unarmed, atau canPickupWeapon tidak aktif."
            );
            return false;
        }

        activePlayerTransform = oldIdentity.transform;

        return SwitchFromIdentity(oldIdentity, targetWeapon);
    }
    public void RegisterActivePlayer(Transform newActivePlayer)
    {
        if (newActivePlayer == null)
            return;

        activePlayerTransform = newActivePlayer;
        BindRuntimeSystems(activePlayerTransform, GetWeaponFromPlayer(activePlayerTransform));
    }
    public bool TrySwitchFromUI(WeaponType targetWeapon)
    {
        if (onlyAllowOneSwitch && hasSwitched)
            return false;

        if (targetWeapon != WeaponType.Sword && targetWeapon != WeaponType.Bow)
        {
            Debug.LogWarning("[PLAYER SWITCH MANAGER] Pilihan UI harus Sword atau Bow.");
            return false;
        }

        if (activePlayerTransform == null)
            activePlayerTransform = FindInitialPlayer();

        if (activePlayerTransform == null)
        {
            Debug.LogWarning("[PLAYER SWITCH MANAGER] Active Player belum ditemukan. Isi Active Player dengan Player_W0.");
            return false;
        }

        PlayerWeaponIdentity oldIdentity = activePlayerTransform.GetComponent<PlayerWeaponIdentity>();

        if (oldIdentity == null)
            oldIdentity = activePlayerTransform.GetComponentInChildren<PlayerWeaponIdentity>(true);

        if (oldIdentity == null)
            oldIdentity = activePlayerTransform.GetComponentInParent<PlayerWeaponIdentity>();

        if (oldIdentity == null)
        {
            Debug.LogWarning(
                "[PLAYER SWITCH MANAGER] Player aktif tidak memiliki PlayerWeaponIdentity. " +
                "Tambahkan PlayerWeaponIdentity pada root Player_W0."
            );
            return false;
        }

        if (!oldIdentity.CanUseWeaponPickup)
        {
            Debug.Log(
                "[PLAYER SWITCH MANAGER] Pilihan UI ditolak karena player aktif bukan Unarmed atau sudah pernah memilih senjata."
            );
            return false;
        }

        return SwitchFromIdentity(oldIdentity, targetWeapon);
    }

    public void ChooseSwordFromUI()
    {
        TrySwitchFromUI(WeaponType.Sword);
    }

    public void ChooseBowFromUI()
    {
        TrySwitchFromUI(WeaponType.Bow);
    }

    private bool SwitchFromIdentity(PlayerWeaponIdentity oldIdentity, WeaponType targetWeapon)
    {
        if (oldIdentity == null)
            return false;

        GameObject selectedPrefab = GetPrefabForWeapon(targetWeapon);

        if (selectedPrefab == null)
        {
            Debug.LogError(
                "[PLAYER SWITCH MANAGER] Prefab untuk " + targetWeapon +
                " belum diisi. Isi Player Sword Prefab dan Player Bow Prefab di Inspector."
            );
            return false;
        }

        GameObject oldPlayerObject = oldIdentity.gameObject;
        CharacterBase oldCharacter = GetCharacterBase(oldPlayerObject);
        Player oldPlayer = GetPlayer(oldPlayerObject);

        Vector3 spawnPosition = oldPlayerObject.transform.position;
        Quaternion spawnRotation = oldPlayerObject.transform.rotation;

        GameObject newPlayerObject = Instantiate(selectedPrefab, spawnPosition, spawnRotation);
        newPlayerObject.name = selectedPrefab.name;

        TrySetPlayerTag(newPlayerObject);
        newPlayerObject.layer = oldPlayerObject.layer;
        newPlayerObject.SetActive(true);
        newPlayerObject.transform.localScale = Vector3.one;

        PlayerWeaponIdentity newIdentity = newPlayerObject.GetComponent<PlayerWeaponIdentity>();

        if (newIdentity == null)
            newIdentity = newPlayerObject.AddComponent<PlayerWeaponIdentity>();

        newIdentity.Initialize(targetWeapon, false);

        RebindNewPlayerComponents(newPlayerObject);

        CharacterBase newCharacter = GetCharacterBase(newPlayerObject);
        Player newPlayer = GetPlayer(newPlayerObject);

        CopyPlayerRuntimeState(oldCharacter, newCharacter, oldPlayer, newPlayer, targetWeapon);

        activePlayerTransform = newPlayerObject.transform;
        hasSwitched = true;

        oldPlayerObject.SetActive(false);

        BindRuntimeSystems(activePlayerTransform, targetWeapon);

        if (destroyAllPickupZonesAfterSwitch)
        {
            DestroyAllPickupZones();
        }

        if (destroyOldPlayer)
        {
            Destroy(oldPlayerObject);
        }

        Debug.Log(
            "[PLAYER SWITCH MANAGER] Player berhasil switch dari UI ke " +
            targetWeapon +
            ". Prefab baru: " +
            newPlayerObject.name
        );

        return true;
    }

    public void BindRuntimeSystems(Transform playerTransform, WeaponType currentWeapon)
    {
        if (playerTransform == null)
            return;

        CharacterBase character = GetCharacterBase(playerTransform.gameObject);

        StageManager[] stageManagers = FindObjectsOfType<StageManager>();

        foreach (StageManager stageManager in stageManagers)
        {
            if (stageManager != null)
                stageManager.playerTransform = playerTransform;
        }

        CameraFollow[] cameraFollows = FindObjectsOfType<CameraFollow>();

        foreach (CameraFollow cameraFollow in cameraFollows)
        {
            if (cameraFollow != null)
                cameraFollow.SetTarget(playerTransform);
        }

        if (DataTracker.Instance != null)
        {
            AssignDataTrackerTarget(DataTracker.Instance, playerTransform);
        }

        PlayerHPBarUI[] hpBars = FindObjectsOfType<PlayerHPBarUI>();

        foreach (PlayerHPBarUI hpBar in hpBars)
        {
            AssignCharacterTargetToComponent(hpBar, playerTransform, character);
        }

        EnergyBarUI[] energyBars = FindObjectsOfType<EnergyBarUI>();

        foreach (EnergyBarUI energyBar in energyBars)
        {
            AssignCharacterTargetToComponent(energyBar, playerTransform, character);
        }

        GameOverOnPlayerDeath[] gameOverHandlers = FindObjectsOfType<GameOverOnPlayerDeath>();

        foreach (GameOverOnPlayerDeath gameOverHandler in gameOverHandlers)
        {
            AssignGameOverTarget(gameOverHandler, playerTransform, character);
        }

        CurrentWeaponIconUI[] weaponIcons = FindObjectsOfType<CurrentWeaponIconUI>(true);

        foreach (CurrentWeaponIconUI weaponIcon in weaponIcons)
        {
            if (weaponIcon != null)
                weaponIcon.SetWeapon(currentWeapon);
        }

        NotifyActiveWeaponChanged(currentWeapon);
    }

    private Transform FindInitialPlayer()
    {
        PlayerWeaponIdentity[] identities = FindObjectsOfType<PlayerWeaponIdentity>();

        foreach (PlayerWeaponIdentity identity in identities)
        {
            if (identity != null &&
                identity.gameObject.activeInHierarchy &&
                identity.currentWeapon == WeaponType.None)
            {
                return identity.transform;
            }
        }

        GameObject taggedPlayer = null;

        try
        {
            taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        }
        catch (UnityException)
        {
            Debug.LogWarning("[PLAYER SWITCH MANAGER] Tag Player belum dibuat.");
        }

        if (taggedPlayer != null)
            return taggedPlayer.transform;

        Player fallbackPlayer = FindObjectOfType<Player>();

        if (fallbackPlayer != null)
            return fallbackPlayer.transform;

        return null;
    }

    private GameObject GetPrefabForWeapon(WeaponType weapon)
    {
        switch (weapon)
        {
            case WeaponType.Sword:
                return playerSwordPrefab;

            case WeaponType.Bow:
                return playerBowPrefab;

            default:
                return null;
        }
    }

    private void CopyPlayerRuntimeState(
        CharacterBase oldCharacter,
        CharacterBase newCharacter,
        Player oldPlayer,
        Player newPlayer,
        WeaponType targetWeapon
    )
    {
        if (newPlayer != null)
        {
            newPlayer.weaponType = targetWeapon;
            newPlayer.lockMovement = false;
            newPlayer.isAttacking = false;
        }

        if (copyHealthAndEnergy && oldCharacter != null && newCharacter != null)
        {
            if (copyMaxHPFromOldPlayer)
                newCharacter.maxHP = oldCharacter.maxHP;

            newCharacter.currentHP = Mathf.Clamp(
                oldCharacter.currentHP,
                0f,
                Mathf.Max(1f, newCharacter.maxHP)
            );

            newCharacter.SetEnergy(oldCharacter.CurrentEnergy);
        }

        if (copyFacingDirection && oldPlayer != null && newPlayer != null)
        {
            newPlayer.isFacingRight = oldPlayer.isFacingRight;

            PlayerAnimation newAnimation = newPlayer.GetComponentInChildren<PlayerAnimation>(true);

            if (newAnimation != null)
                newAnimation.SetFlip(!newPlayer.isFacingRight);
        }

        if (resetNewPlayerVelocity && newCharacter != null)
        {
            Rigidbody2D rb = newCharacter.GetComponent<Rigidbody2D>();

            if (rb == null)
                rb = newCharacter.GetComponentInParent<Rigidbody2D>();

            if (rb == null)
                rb = newCharacter.GetComponentInChildren<Rigidbody2D>();

            if (rb != null)
                rb.velocity = Vector2.zero;
        }
    }

    private WeaponType GetWeaponFromPlayer(Transform playerTransform)
    {
        if (playerTransform == null)
            return WeaponType.None;

        PlayerWeaponIdentity identity = playerTransform.GetComponent<PlayerWeaponIdentity>();

        if (identity == null)
            identity = playerTransform.GetComponentInChildren<PlayerWeaponIdentity>(true);

        if (identity != null)
            return identity.currentWeapon;

        Player player = playerTransform.GetComponent<Player>();

        if (player == null)
            player = playerTransform.GetComponentInChildren<Player>(true);

        if (player != null)
            return player.weaponType;

        return WeaponType.None;
    }

    private CharacterBase GetCharacterBase(GameObject source)
    {
        if (source == null)
            return null;

        CharacterBase character = source.GetComponent<CharacterBase>();

        if (character == null)
            character = source.GetComponentInChildren<CharacterBase>(true);

        if (character == null)
            character = source.GetComponentInParent<CharacterBase>();

        return character;
    }

    private Player GetPlayer(GameObject source)
    {
        if (source == null)
            return null;

        Player player = source.GetComponent<Player>();

        if (player == null)
            player = source.GetComponentInChildren<Player>(true);

        if (player == null)
            player = source.GetComponentInParent<Player>();

        return player;
    }

    private void TrySetPlayerTag(GameObject playerObject)
    {
        if (playerObject == null)
            return;

        try
        {
            playerObject.tag = "Player";
        }
        catch (UnityException)
        {
            Debug.LogWarning("[PLAYER SWITCH MANAGER] Tag Player belum dibuat. Buat tag Player di Project Settings > Tags and Layers.");
        }
    }

    private void SetLayerRecursively(GameObject root, int layer)
    {
        if (root == null)
            return;

        root.layer = layer;

        foreach (Transform child in root.transform)
        {
            if (child != null)
                SetLayerRecursively(child.gameObject, layer);
        }
    }

    private void AssignDataTrackerTarget(DataTracker tracker, Transform playerTransform)
    {
        if (tracker == null || playerTransform == null)
            return;

        if (TryInvokeMethod(tracker, "SetPlayerTransform", typeof(Transform), playerTransform))
            return;

        TrySetField(tracker, "playerTransform", playerTransform);
        TrySetField(tracker, "lastPlayerPos", playerTransform.position);
    }

    private void AssignCharacterTargetToComponent(
        MonoBehaviour component,
        Transform playerTransform,
        CharacterBase character
    )
    {
        if (component == null || playerTransform == null || character == null)
            return;

        if (TryInvokeMethod(component, "SetTarget", typeof(Transform), playerTransform))
            return;

        if (TryInvokeMethod(component, "SetTarget", typeof(CharacterBase), character))
            return;

        bool isEnergyBar = component is EnergyBarUI;

        if (isEnergyBar)
            TryInvokeNoParameterMethod(component, "OnDisable");

        TrySetField(component, "target", character);

        if (isEnergyBar)
        {
            TryInvokeNoParameterMethod(component, "OnEnable");
        }

        TryInvokeNoParameterMethod(component, "ForceRefreshImmediate");
    }

    private void AssignGameOverTarget(
        GameOverOnPlayerDeath gameOverHandler,
        Transform playerTransform,
        CharacterBase character
    )
    {
        if (gameOverHandler == null || playerTransform == null || character == null)
            return;

        if (TryInvokeMethod(gameOverHandler, "SetPlayerTransform", typeof(Transform), playerTransform))
            return;

        if (TryInvokeMethod(gameOverHandler, "SetPlayerCharacter", typeof(CharacterBase), character))
            return;

        TrySetField(gameOverHandler, "playerCharacter", character);
        TrySetField(gameOverHandler, "wasAssignedAtLeastOnce", true);
    }

    private bool TryInvokeMethod(object target, string methodName, Type parameterType, object argument)
    {
        if (target == null || parameterType == null)
            return false;

        MethodInfo method = target.GetType().GetMethod(
            methodName,
            RuntimeBindingFlags,
            null,
            new Type[] { parameterType },
            null
        );

        if (method == null)
            return false;

        try
        {
            method.Invoke(target, new object[] { argument });
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[PLAYER SWITCH MANAGER] Gagal memanggil method " +
                methodName +
                " pada " +
                target.GetType().Name +
                ". Error: " +
                exception.Message
            );

            return false;
        }
    }

    private bool TryInvokeNoParameterMethod(object target, string methodName)
    {
        if (target == null)
            return false;

        MethodInfo method = target.GetType().GetMethod(methodName, RuntimeBindingFlags);

        if (method == null)
            return false;

        if (method.GetParameters().Length > 0)
            return false;

        try
        {
            method.Invoke(target, null);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TrySetField(object target, string fieldName, object value)
    {
        if (target == null)
            return false;

        FieldInfo field = target.GetType().GetField(fieldName, RuntimeBindingFlags);

        if (field == null)
            return false;

        try
        {
            field.SetValue(target, value);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "[PLAYER SWITCH MANAGER] Gagal mengisi field " +
                fieldName +
                " pada " +
                target.GetType().Name +
                ". Error: " +
                exception.Message
            );

            return false;
        }
    }
    private void RebindNewPlayerComponents(GameObject newPlayerObject)
    {
        if (newPlayerObject == null)
            return;

        Player player = newPlayerObject.GetComponent<Player>();
        if (player == null)
            player = newPlayerObject.GetComponentInChildren<Player>(true);

        CharacterBase character = newPlayerObject.GetComponent<CharacterBase>();
        if (character == null)
            character = newPlayerObject.GetComponentInChildren<CharacterBase>(true);

        PlayerAnimation anim = newPlayerObject.GetComponentInChildren<PlayerAnimation>(true);

        MoveKeyboard mover = newPlayerObject.GetComponent<MoveKeyboard>();
        if (mover == null)
            mover = newPlayerObject.GetComponentInChildren<MoveKeyboard>(true);

        if (mover != null)
        {
            mover.player = player;
            mover.anim = anim;
        }

        Dash dash = newPlayerObject.GetComponent<Dash>();
        if (dash == null)
            dash = newPlayerObject.GetComponentInChildren<Dash>(true);

        if (dash != null)
        {
            dash.player = player;
            dash.anim = anim;
        }

        SkillBase[] skillBases = newPlayerObject.GetComponentsInChildren<SkillBase>(true);

        foreach (SkillBase skillBase in skillBases)
        {
            if (skillBase != null)
                skillBase.enabled = true;
        }

        MonoBehaviour[] behaviours = newPlayerObject.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour != null)
                behaviour.enabled = true;
        }

        if (player != null)
        {
            player.lockMovement = false;
            player.isAttacking = false;
        }

        if (character != null)
        {
            Rigidbody2D rb = character.GetComponent<Rigidbody2D>();

            if (rb != null)
                rb.velocity = Vector2.zero;
        }
    }
    private void DestroyAllPickupZones()
    {
        WeaponPickupZone[] pickupZones = FindObjectsOfType<WeaponPickupZone>(true);

        foreach (WeaponPickupZone pickupZone in pickupZones)
        {
            if (pickupZone == null)
                continue;

            Destroy(pickupZone.gameObject, destroyPickupDelay);
        }
    }

    private void NotifyActiveWeaponChanged(WeaponType weapon)
    {
        CurrentWeapon = weapon;

        if (OnActiveWeaponChanged != null)
            OnActiveWeaponChanged.Invoke(weapon);
    }
}