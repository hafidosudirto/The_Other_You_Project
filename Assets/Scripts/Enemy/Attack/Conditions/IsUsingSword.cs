using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IsUsingSwordNode : Node
{
    public override NodeState Evaluate()
    {
        TelemetryLogger.NotifyBtNodeEvaluated(GetType().Name);
        if (DDAController.Instance == null)
            return NodeState.Failure;

        return (DDAController.Instance.currentPlayerDominantWeapon == WeaponType.Sword)
            ? NodeState.Success
            : NodeState.Failure;
    }
}
