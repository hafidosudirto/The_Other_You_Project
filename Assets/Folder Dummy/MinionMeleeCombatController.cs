using System;
using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(CharacterBase))]
public class MinionMeleeCombatController : MonoBehaviour
{
    [Header("Core References")]
    [SerializeField] private CharacterBase character;
    [SerializeField] private Transform player;
    [SerializeField] private Animator animator;

    [Header("Skill Root")]
    [SerializeField] private Transform skillRootSword;

    [Header("Single Melee Skill")]
    [SerializeField] private Enemy_Sword_SlashCombo slashCombo;

    [Header("Movement")]
    [SerializeField] private bool allowMovement = true;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float stopDistance = 1.25f;
    [SerializeField] private float verticalAlignSpeed = 2.5f;
    [SerializeField] private bool alignYToPlayer = true;

    [Header("Attack Settings")]
    [SerializeField] private float attackRange = 1.6f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private int slashComboSlotIndex = 0;

    [Header("Stage Token")]
    [SerializeField] private int attackTokens = 3;

    [Header("Animation Parameters")]
    [SerializeField] private string idleBool = "Idle";
    [SerializeField] private string walkBool = "Walk";
    [SerializeField] private string slash1Bool = "Slash1";
    [SerializeField] private string slash2Bool = "Slash2";
    [SerializeField] private string slashTrigger = "";
    [SerializeField] private bool useSlashBool = true;
    [SerializeField] private bool useSlashTrigger = false;

    [Header("Animation Timing")]
    [SerializeField] private float slash1AnimDuration = 0.45f;
    [SerializeField] private bool autoPlaySlash2 = false;
    [SerializeField] private float slash2Delay = 0.25f;
    [SerializeField] private float slash2AnimDuration = 0.45f;

    [Header("Busy Lock")]
    [SerializeField] private float fallbackBusyDuration = 0.7f;

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
        AutoAssignSlashCombo();
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

            AutoAssignSlashCombo();
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
                $"[MINION MELEE INIT] {name} token: {attackTokens}, " +
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
            isMoving = MoveTowardPlayer(distance);
        }

        SetMoveAnimation(isMoving);

        if (distance <= attackRange)
        {
            TryStartSlashCombo(distance);
        }
    }

    private void AutoAssignPlayer()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    private void AutoAssignSlashCombo()
    {
        if (slashCombo != null)
            return;

        Transform root = skillRootSword;

        if (root == null)
            root = FindChildRecursive(transform, "SkillRoot_Sword");

        if (root != null)
        {
            skillRootSword = root;
            slashCombo = root.GetComponentInChildren<Enemy_Sword_SlashCombo>(true);
        }

        if (slashCombo == null)
            slashCombo = GetComponentInChildren<Enemy_Sword_SlashCombo>(true);
    }

    private bool MoveTowardPlayer(float distance)
    {
        if (distance <= stopDistance)
            return false;

        float actualMoveSpeed = moveSpeed;

        if (character != null && character.moveSpeed > 0f)
            actualMoveSpeed = character.moveSpeed;

        Vector3 before = transform.position;
        Vector3 targetPosition = transform.position;

        targetPosition.x = Mathf.MoveTowards(
            transform.position.x,
            player.position.x,
            actualMoveSpeed * Time.deltaTime
        );

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

    private void TryStartSlashCombo(float distance)
    {
        if (IsBusy)
            return;

        if (Time.time < lastAttackTime + attackCooldown)
            return;

        if (attackTokens <= 0)
        {
            if (showDebug)
                Debug.Log($"[MINION MELEE] {name} tidak menyerang karena attack token habis.");

            return;
        }

        if (slashCombo == null)
        {
            Debug.LogWarning($"[MINION MELEE] {name} tidak menemukan Enemy_Sword_SlashCombo.");
            return;
        }

        if (!CanTriggerSkill(slashCombo, distance))
        {
            if (showDebug)
                Debug.Log($"[MINION MELEE] SlashCombo belum bisa dipicu. Distance: {distance}");

            return;
        }

        PlaySlashAnimation();

        bool triggered = InvokeSlashCombo();

        if (!triggered)
        {
            Debug.LogWarning(
                $"[MINION MELEE] {name} gagal memanggil SlashCombo. " +
                $"Cek nama method pada Enemy_Sword_SlashCombo."
            );

            return;
        }

        ConsumeAttackToken();

        lastAttackTime = Time.time;
        lockedUntil = Time.time + fallbackBusyDuration;

        if (showDebug)
        {
            Debug.Log(
                $"[MINION MELEE] {name} memakai SlashCombo. " +
                $"Sisa token: {attackTokens}"
            );
        }
    }

    private void PlaySlashAnimation()
    {
        SetMoveAnimation(false);

        if (animator == null)
        {
            if (showDebug)
                Debug.LogWarning($"[MINION MELEE] {name} tidak memiliki Animator.");

            return;
        }

        ResetSlashAnimation();

        if (useSlashBool && !string.IsNullOrEmpty(slash1Bool))
            animator.SetBool(slash1Bool, true);

        if (useSlashTrigger && !string.IsNullOrEmpty(slashTrigger))
            animator.SetTrigger(slashTrigger);

        CancelInvoke(nameof(ResetSlashAnimation));
        Invoke(nameof(ResetSlashAnimation), slash1AnimDuration);

        if (autoPlaySlash2)
        {
            CancelInvoke(nameof(PlaySlash2Animation));
            Invoke(nameof(PlaySlash2Animation), slash2Delay);
        }
    }

    private void PlaySlash2Animation()
    {
        if (animator == null)
            return;

        if (!string.IsNullOrEmpty(slash1Bool))
            animator.SetBool(slash1Bool, false);

        if (!string.IsNullOrEmpty(slash2Bool))
            animator.SetBool(slash2Bool, true);

        CancelInvoke(nameof(ResetSlashAnimation));
        Invoke(nameof(ResetSlashAnimation), slash2AnimDuration);
    }

    private void ResetSlashAnimation()
    {
        if (animator == null)
            return;

        if (!string.IsNullOrEmpty(slash1Bool))
            animator.SetBool(slash1Bool, false);

        if (!string.IsNullOrEmpty(slash2Bool))
            animator.SetBool(slash2Bool, false);
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

    private bool InvokeSlashCombo()
    {
        if (TryInvokeBoolOrVoid(slashCombo, "Trigger"))
            return true;

        if (TryInvokeBoolOrVoid(slashCombo, "TryStartSlashCombo"))
            return true;

        if (TryInvokeBoolOrVoid(slashCombo, "TryStartSkill"))
            return true;

        if (TryInvokeBoolOrVoid(slashCombo, "StartSkill"))
            return true;

        if (TryInvokeBoolOrVoid(slashCombo, "TriggerSkill", slashComboSlotIndex))
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