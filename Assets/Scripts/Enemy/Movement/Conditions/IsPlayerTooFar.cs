using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IsPlayerTooFarNode : Node
{
    private EnemyAI enemy;

    public IsPlayerTooFarNode(EnemyAI ai)
    {
        enemy = ai;
    }

    public override NodeState Evaluate()
    {
        return (enemy.EvaluateDistanceToPlayer() == PlayerDistanceState.Chase)
            ? NodeState.Success
            : NodeState.Failure;
    }
}
