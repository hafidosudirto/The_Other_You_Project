using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IdleActionNode : Node
{
    private EnemyAI enemyAI;

    public IdleActionNode(EnemyAI ai)
    {
        enemyAI = ai;
    }

    public override NodeState Evaluate()
    {
        if (enemyAI == null)
        {
            Debug.LogWarning("[BT] EnemyAI null (IdleAction)");
            state = NodeState.Failure;
            return state;
        }

        enemyAI.StopMoving();

        // Idle itu kondisi “stabil” → bisa dianggap Running supaya tree tetap stay di cabang ini
        state = NodeState.Running;
        return state;
    }
}
