using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IsPlayerInSafeZoneNode : Node
{
    private EnemyAI enemy;

    public IsPlayerInSafeZoneNode(EnemyAI ai)
    {
        enemy = ai;
    }

    public override NodeState Evaluate()
    {
        return (enemy.EvaluateDistanceToPlayer() == PlayerDistanceState.Idle)
            ? NodeState.Success
            : NodeState.Failure;
    }
}
