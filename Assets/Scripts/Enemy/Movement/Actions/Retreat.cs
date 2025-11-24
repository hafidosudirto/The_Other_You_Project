using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RetreatActionNode : Node
{
    private EnemyAI enemyAI;

    public RetreatActionNode(EnemyAI ai)
    {
        enemyAI = ai;
    }

    public override NodeState Evaluate()
    {
        if (enemyAI == null)
        {
            Debug.LogWarning("[BT] EnemyAI null (RetreatAction)");
            state = NodeState.Failure;
            return state;
        }

        enemyAI.RetreatFromPlayer();

        state = NodeState.Running;
        return state;
    }
}
