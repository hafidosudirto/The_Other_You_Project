using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CanDashOutNode : Node
{
    private EnemyAI ai;
    private float ratio = 0.75f;

    public CanDashOutNode(EnemyAI enemy)
    {
        ai = enemy;
    }

    public override NodeState Evaluate()
    {
        if (!ai || !ai.playerTransform) return NodeState.Failure;

        float dist = Vector2.Distance(ai.transform.position, ai.playerTransform.position);
        float triggerDist = ai.attackRange * ratio;

        bool should = dist < triggerDist;

        Debug.DrawLine(ai.transform.position, ai.playerTransform.position,
            should ? Color.yellow : Color.cyan, 0.025f);

        return should ? NodeState.Success : NodeState.Failure;
    }
}



