using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IsPlaystyleOffensiveNode : Node
{
    public override NodeState Evaluate()
    {
        TelemetryLogger.NotifyBtNodeEvaluated(GetType().Name);
        if (DDAController.Instance == null)
            return NodeState.Failure;

        return (DDAController.Instance.currentPlayerPlaystyle == PlayerPlaystyle.OffensiveDominant)
            ? NodeState.Success
            : NodeState.Failure;
    }
}
