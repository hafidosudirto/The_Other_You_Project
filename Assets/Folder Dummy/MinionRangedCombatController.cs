using System;
using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(CharacterBase))]
public class MinionRangedCombatController : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private CharacterBase character;
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;

    [Header("Skill Root")]
    [SerializeField] private Transform skillRootBow;

    [Header("Single Ranged Skill")]
    [SerializeField] private Enemy_Bow_QuickShot quickShot;

    [Header("Movement / Range")]
    [SerializeField] private bool allowMovement = true;
    [SerializeField] private float minimumRange = 3.5f;
    [SerializeField] private float desiredRange = 6f;
    [SerializeField] private float attackRange = 7f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float verticalAlignSpeed = 2f;
    [SerializeField] private bool alignYToPlayer = false;

    [Header("Attack Settings")]
    [SerializeField] private float attackCooldown = 2.5f;

    [Header("Stage Token")]
    [SerializeField] private int attackTokens = 3;

    [Header("Animation Parameters")]
    [SerializeField] private string idleBool = "Idle";
    [SerializeField] private string walkBool = "Walk";
    [SerializeField] private string quickShotBool = "QuickShot";
    [SerializeField] private string quickShotTrigger = "";
    [SerializeField] private bool useQuickShotBool = true;
    [SerializeField] private bool useQuickShotTrigger = false;

    [Header("Animation Timing")]
    [SerializeField] private float quickShotAnimDuration = 0.45f;

    [Header("Busy Lock")]
    [SerializeField] private float fallbackBusyDuration = 0.45f;

    [Header("Debug")]
    [SerializeField] private bool showDebug = true;

    private float lastAttackTime = -999f;
    private float lockedUntil = -999f;
    private int skillBusyCounter = 0;

    public bool IsBusy => skillBusyCounter > 0 || Time.time < lockedUntil;
    public bool IsNotBusy => !IsBusy;

    private void Awake()
    {
        if (character == null)
            character = GetComponent<CharacterBase>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        AutoAssignPlayer();
        AutoAssignQuickShot();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            if (character == null)
                character = GetComponent<CharacterBase>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>(true);

            AutoAssignQuickShot();
        }
    }
#endif

    public void InitializeStageEnemy(CharacterBase stageCharacter, int stageAttackTokens)
    {
        if (stageCharacter != null)
            character = stageCharacter;

        attackTokens = Mathf.Max(0, stageAttackTokens);

        if (showDebug)
        {
            Debug.Log(
                $"[MINION RANGED INIT] {name} token: {attackTokens}, " +
                $"attack: {(character != null ? character.attack : 0f)}"
            );
        }
    }

    private void Update()
    {
        if (player == null)
        {
            AutoAssignPlayer();
            return;
        }

        if (character == null || !character.CanAct())
        {
            SetMoveAnimation(false);
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        FacePlayer();

        bool isMoving = false;

        if (allowMovement && !IsBusy)
        {
            isMoving = MaintainBowDistance(distance);
        }

        SetMoveAnimation(isMoving);

        if (distance <= attackRange)
        {
            TryStartQuickShot(distance);
        }
    }

    private void AutoAssignPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    private void AutoAssignQuickShot()
    {
        if (quickShot != null)
            return;

        Transform root = skillRootBow;

        if (root == null)
            root = FindChildRecursive(transform, "SkillRoot_Bow");

        if (root != null)
        {
            skillRootBow = root;
            quickShot = root.GetComponentInChildren<Enemy_Bow_QuickShot>(true);
        }

        if (quickShot == null)
            quickShot = GetComponentInChildren<Enemy_Bow_QuickShot>(true);
    }

    private bool MaintainBowDistance(float distance)
    {
        float actualMoveSpeed = moveSpeed;

        if (character != null && character.moveSpeed > 0f)
            actualMoveSpeed = character.moveSpeed;

        Vector3 before = transform.position;
        Vector3 targetPosition = transform.position;

        if (distance < minimumRange)
        {
            float awayDirection = transform.position.x < player.position.x ? -1f : 1f;
            targetPosition.x += awayDirection * actualMoveSpeed * Time.deltaTime;
        }
        else if (distance > desiredRange)
        {
            targetPosition.x = Mathf.MoveTowards(
                transform.position.x,
                player.position.x,
                actualMoveSpeed * Time.deltaTime
            );
        }

        if (alignYToPlayer)
        {
            targetPosition.y = Mathf.MoveTowards(
                transform.position.y,
                player.position.y,
                verticalAlignSpeed * Time.deltaTime
            );
        }

        transform.position = targetPosition;

        return Vector3.Distance(before, transform.position) > 0.001f;
    }

    private void FacePlayer()
    {
        if (player == null || character == null)
            return;

        bool playerOnRight = player.position.x > transform.position.x;

        if (playerOnRight != character.isFacingRight)
            character.Flip();
    }

    private void TryStartQuickShot(float distance)
    {
        if (IsBusy)
            return;

        if (Time.time < lastAttackTime + attackCooldown)
            return;

        if (attackTokens <= 0)
        {
            if (showDebug)
                Debug.Log($"[MINION RANGED] {name} tidak menembak karena attack token habis.");

            return;
        }

        if (quickShot == null)
        {
            Debug.LogWarning($"[MINION RANGED] {name} tidak menemukan Enemy_Bow_QuickShot.");
            return;
        }

        if (!CanTriggerSkill(quickShot, distance))
        {
            if (showDebug)
                Debug.Log($"[MINION RANGED] QuickShot belum bisa dipicu. Distance: {distance}");

            return;
        }

        PrepareQuickShotBeforeTrigger();
        PlayQuickShotAnimation();

        bool triggered = InvokeQuickShot();

        if (!triggered)
        {
            Debug.LogWarning(
                $"[MINION RANGED] {name} gagal memanggil QuickShot. " +
                $"Cek nama method pada Enemy_Bow_QuickShot."
            );

            return;
        }

        ConsumeAttackToken();

        lastAttackTime = Time.time;
        lockedUntil = Time.time + fallbackBusyDuration;

        if (showDebug)
        {
            Debug.Log(
                $"[MINION RANGED] {name} memakai QuickShot. " +
                $"Sisa token: {attackTokens}"
            );
        }
    }

    private void PrepareQuickShotBeforeTrigger()
    {
        if (quickShot == null || character == null)
            return;

        TryInvokeBoolOrVoid(quickShot, "SetPlayer", player);
        TryInvokeBoolOrVoid(quickShot, "SetTarget", player);
        TryInvokeBoolOrVoid(quickShot, "SetOwner", character);
        TryInvokeBoolOrVoid(quickShot, "SetOwner", character.gameObject);
        TryInvokeBoolOrVoid(quickShot, "SetDamage", character.attack);
        TryInvokeBoolOrVoid(quickShot, "SetAttackPower", character.attack);
    }

    private void PlayQuickShotAnimation()
    {
        SetMoveAnimation(false);

        if (animator == null)
        {
            if (showDebug)
                Debug.LogWarning($"[MINION RANGED] {name} tidak memiliki Animator.");

            return;
        }

        ResetQuickShotAnimation();

        if (useQuickShotBool && !string.IsNullOrEmpty(quickShotBool))
            animator.SetBool(quickShotBool, true);

        if (useQuickShotTrigger && !string.IsNullOrEmpty(quickShotTrigger))
            animator.SetTrigger(quickShotTrigger);

        CancelInvoke(nameof(ResetQuickShotAnimation));
        Invoke(nameof(ResetQuickShotAnimation), quickShotAnimDuration);
    }

    private void ResetQuickShotAnimation()
    {
        if (animator == null)
            return;

        if (!string.IsNullOrEmpty(quickShotBool))
            animator.SetBool(quickShotBool, false);
    }

    private void SetMoveAnimation(bool isMoving)
    {
        if (animator == null)
            return;

        if (!string.IsNullOrEmpty(walkBool))
            animator.SetBool(walkBool, isMoving);

        if (!string.IsNullOrEmpty(idleBool))
            animator.SetBool(idleBool, !isMoving && !IsBusy);
    }

    private void ConsumeAttackToken()
    {
        attackTokens = Mathf.Max(0, attackTokens - 1);
    }

    private bool InvokeQuickShot()
    {
        if (TryInvokeBoolOrVoid(quickShot, "Trigger"))
            return true;

        if (TryInvokeBoolOrVoid(quickShot, "TryStartQuickShot"))
            return true;

        if (TryInvokeBoolOrVoid(quickShot, "TryStartSkill"))
            return true;

        if (TryInvokeBoolOrVoid(quickShot, "StartSkill"))
            return true;

        if (TryInvokeBoolOrVoid(quickShot, "Shoot"))
            return true;

        return false;
    }

    private bool CanTriggerSkill(MonoBehaviour skill, float distance)
    {
        if (skill == null)
            return false;

        MethodInfo canTriggerWithDistance = skill.GetType().GetMethod(
            "CanTrigger",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            new Type[] { typeof(float) },
            null
        );

        if (canTriggerWithDistance != null &&
            canTriggerWithDistance.ReturnType == typeof(bool))
        {
            return (bool)canTriggerWithDistance.Invoke(skill, new object[] { distance });
        }

        MethodInfo canTriggerNoParameter = skill.GetType().GetMethod(
            "CanTrigger",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null
        );

        if (canTriggerNoParameter != null &&
            canTriggerNoParameter.ReturnType == typeof(bool))
        {
            return (bool)canTriggerNoParameter.Invoke(skill, null);
        }

        return true;
    }

    private bool TryInvokeBoolOrVoid(MonoBehaviour target, string methodName, params object[] args)
    {
        if (target == null)
            return false;

        MethodInfo method = FindMethod(target.GetType(), methodName, args);

        if (method == null)
            return false;

        object result = method.Invoke(target, args);

        if (method.ReturnType == typeof(bool))
            return (bool)result;

        return true;
    }

    private MethodInfo FindMethod(Type type, string methodName, object[] args)
    {
        MethodInfo[] methods = type.GetMethods(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
        );

        foreach (MethodInfo method in methods)
        {
            if (method.Name != methodName)
                continue;

            ParameterInfo[] parameters = method.GetParameters();

            if (parameters.Length != args.Length)
                continue;

            bool match = true;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (args[i] == null)
                    continue;

                if (!parameters[i].ParameterType.IsAssignableFrom(args[i].GetType()))
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return method;
        }

        return null;
    }

    private Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null)
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), targetName);

            if (found != null)
                return found;
        }

        return null;
    }

    public void InvokeSkillStart()
    {
        skillBusyCounter++;
    }

    public void InvokeSkillEnd()
    {
        if (skillBusyCounter > 0)
            skillBusyCounter--;
    }
}