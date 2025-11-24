using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChaseActionNode : Node
{
    private EnemyAI enemyAI;

    public ChaseActionNode(EnemyAI ai)
    {
        enemyAI = ai;
    }

    public override NodeState Evaluate()
    {
        if (enemyAI == null)
        {
            Debug.LogWarning("[BT] EnemyAI null (ChaseAction)");
            state = NodeState.Failure;
            return state;
        }

        enemyAI.ChasePlayer();

        // Selama masih ngejar, anggap Running
        state = NodeState.Running;
        return state;
    }
}
