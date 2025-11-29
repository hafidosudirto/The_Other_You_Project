using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IsPlaystyleOffensiveNode : Node
{
    public override NodeState Evaluate()
    {
        if (DDAController.Instance == null)
            return NodeState.Failure;

        return (DDAController.Instance.currentPlayerPlaystyle == PlayerPlaystyle.OffensiveDominant)
            ? NodeState.Success
            : NodeState.Failure;
    }
}
