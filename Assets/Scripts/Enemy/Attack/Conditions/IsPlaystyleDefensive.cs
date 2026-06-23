using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IsPlaystyleDefensiveNode : Node
{
    public override NodeState Evaluate()
    {
        TelemetryLogger.NotifyBtNodeEvaluated(GetType().Name);
        if (DDAController.Instance == null)
            return NodeState.Failure;

        return (DDAController.Instance.currentPlayerPlaystyle == PlayerPlaystyle.DefensiveDominant)
            ? NodeState.Success
            : NodeState.Failure;
    }
}
