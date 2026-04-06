using UnityEngine;

public sealed class PlayerAttackSensor : MonoBehaviour
{
    public enum ActionType
    {
        None = 0,
        SlashCombo = 1,
        ChargedStrike = 2,
        Whirlwind = 3,
        Dash = 4,
        Riposte = 5
    }

    [Header("Referensi Animator")]
    [SerializeField] private Animator playerAnimator;

    [Header("Konfigurasi Layer")]
    [SerializeField] private int layerIndex = 0;
    [SerializeField] private string layerNameForIsName = "Base Layer";

    [Header("Parameter Animator Opsional")]
    [SerializeField] private string chargingBoolParameter = "isCharging";

    [Header("State Serangan Berdasarkan Animator")]
    [SerializeField] private string[] slashComboStateNames = new[] { "Slash_Combo1", "Slash_Combo2" };
    [SerializeField] private string[] chargedStartStateNames = new[] { "Charged_Start" };
    [SerializeField] private string[] chargedStrikeStateNames = new[] { "Charged_Strike" };
    [SerializeField] private string[] whirlwindStateNames = new[] { "Whirlwind" };

    [Header("State Defense Berdasarkan Animator")]
    [SerializeField] private string[] dashStateNames = new[] { "Dash" };
    [SerializeField] private string[] riposteStateNames = new[] { "Riposte_Ready", "Riposte_Parry", "Riposte_Counter" };

    public ActionType CurrentActionType { get; private set; } = ActionType.None;
    public ActionType LastStartedActionType { get; private set; } = ActionType.None;

    public bool IsCharging { get; private set; }
    public bool IsAttacking => IsOffensiveAction(CurrentActionType);
    public bool IsDashing => CurrentActionType == ActionType.Dash;
    public bool IsRiposting => CurrentActionType == ActionType.Riposte;
    public bool IsCurrentActionRipostable => IsOffensiveAction(CurrentActionType);

    public int ActionTriggerId { get; private set; }
    public int OffensiveTriggerId { get; private set; }

    public int SlashComboCount { get; private set; }
    public int ChargedStrikeCount { get; private set; }
    public int WhirlwindCount { get; private set; }
    public int DashCount { get; private set; }
    public int RiposteCount { get; private set; }

    private ActionType _previousActionType = ActionType.None;
    private int _lastOffensiveStateHash;

    private void Awake()
    {
        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (playerAnimator == null)
            return;

        AnimatorStateInfo currentState = playerAnimator.GetCurrentAnimatorStateInfo(layerIndex);

        bool chargingFromBool = HasBoolParameter(playerAnimator, chargingBoolParameter)
            && playerAnimator.GetBool(chargingBoolParameter);
        bool chargingFromState = IsInAnyState(currentState, chargedStartStateNames);
        IsCharging = chargingFromBool || chargingFromState;

        CurrentActionType = DetectCurrentAction(currentState);

        bool actionStarted = CurrentActionType != ActionType.None && CurrentActionType != _previousActionType;
        if (actionStarted)
        {
            ActionTriggerId++;
            LastStartedActionType = CurrentActionType;
            RegisterActionCount(CurrentActionType);
        }

        UpdateOffensiveTrigger(currentState);
        _previousActionType = CurrentActionType;
    }

    private ActionType DetectCurrentAction(AnimatorStateInfo stateInfo)
    {
        if (IsInAnyState(stateInfo, slashComboStateNames))
            return ActionType.SlashCombo;

        if (IsInAnyState(stateInfo, chargedStrikeStateNames))
            return ActionType.ChargedStrike;

        if (IsInAnyState(stateInfo, whirlwindStateNames))
            return ActionType.Whirlwind;

        if (IsInAnyState(stateInfo, dashStateNames))
            return ActionType.Dash;

        if (IsInAnyState(stateInfo, riposteStateNames))
            return ActionType.Riposte;

        return ActionType.None;
    }

    private void UpdateOffensiveTrigger(AnimatorStateInfo stateInfo)
    {
        if (!IsOffensiveAction(CurrentActionType))
        {
            _lastOffensiveStateHash = 0;
            return;
        }

        int currentHash = stateInfo.fullPathHash;
        if (_lastOffensiveStateHash == 0 || _lastOffensiveStateHash != currentHash)
            OffensiveTriggerId++;

        _lastOffensiveStateHash = currentHash;
    }

    private void RegisterActionCount(ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.SlashCombo:
                SlashComboCount++;
                break;
            case ActionType.ChargedStrike:
                ChargedStrikeCount++;
                break;
            case ActionType.Whirlwind:
                WhirlwindCount++;
                break;
            case ActionType.Dash:
                DashCount++;
                break;
            case ActionType.Riposte:
                RiposteCount++;
                break;
        }
    }

    private bool IsInAnyState(AnimatorStateInfo stateInfo, string[] stateNames)
    {
        if (stateNames == null)
            return false;

        for (int i = 0; i < stateNames.Length; i++)
        {
            string stateName = stateNames[i];
            if (string.IsNullOrWhiteSpace(stateName))
                continue;

            if (stateInfo.IsName(stateName))
                return true;

            string fullName = $"{layerNameForIsName}.{stateName}";
            if (stateInfo.IsName(fullName))
                return true;
        }

        return false;
    }

    private bool HasBoolParameter(Animator animator, string parameterName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(parameterName))
            return false;

        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
                return true;
        }

        return false;
    }

    private bool IsOffensiveAction(ActionType actionType)
    {
        return actionType == ActionType.SlashCombo
            || actionType == ActionType.ChargedStrike
            || actionType == ActionType.Whirlwind;
    }
}
