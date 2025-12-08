using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IsPlaystyleDefensiveNode : Node
{
    public override NodeState Evaluate()
    {
        if (DDAController.Instance == null)
            return NodeState.Failure;

        return (DDAController.Instance.currentPlayerPlaystyle == PlayerPlaystyle.DefensiveDominant)
            ? NodeState.Success
            : NodeState.Failure;
    }
}
