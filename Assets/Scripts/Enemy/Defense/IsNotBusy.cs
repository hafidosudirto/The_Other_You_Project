using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IsNotBusyNode : Node
{
    private EnemyAI ai;

    public IsNotBusyNode(EnemyAI ai)
    {
        this.ai = ai;
    }

    public override NodeState Evaluate()
    {
        // Jika enemy tidak sedang menyerang / action lock → SUCCESS
        if (!ai.isPerformingAction)
        {
            state = NodeState.Success;
            return state;
        }

        // Jika enemy sedang menyerang → FAILURE
        state = NodeState.Failure;
        return state;
    }
}

