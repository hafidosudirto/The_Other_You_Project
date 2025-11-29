using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IsInAttackRangeNode : Node
{
    private EnemyAI ai;

    public IsInAttackRangeNode(EnemyAI enemy)
    {
        ai = enemy;
    }

    public override NodeState Evaluate()
    {
        if (ai == null || ai.playerTransform == null)
            return NodeState.Failure;

        bool inRange = ai.IsInAttackRange();

        Debug.DrawLine(ai.transform.position, ai.playerTransform.position,
            inRange ? Color.green : Color.red, 0.025f);

        return inRange ? NodeState.Success : NodeState.Failure;
    }
}






