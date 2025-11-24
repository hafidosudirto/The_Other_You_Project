using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IsPlayerTooCloseNode : Node
{
    private EnemyAI enemy;

    public IsPlayerTooCloseNode(EnemyAI ai)
    {
        enemy = ai;
    }

    public override NodeState Evaluate()
    {
        return (enemy.EvaluateDistanceToPlayer() == PlayerDistanceState.Retreat)
            ? NodeState.Success
            : NodeState.Failure;
    }
}
