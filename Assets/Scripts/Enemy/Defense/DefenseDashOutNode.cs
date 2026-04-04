using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class DefenseDashOutNode : BaseDefenseNode
{
    private float dashDistance = 3.0f;

    public DefenseDashOutNode(EnemyAI ai) : base(ai)
    {
        cooldown = 2.0f;     // dash out lebih lama cooldown
        failChance = 0.10f;  // dash cukup dapat diandalkan
    }

    public override NodeState Evaluate()
    {
        if (!CanUse())
        {
            state = NodeState.Failure;
            return state;
        }

        // Tentukan arah menjauh dari player
        Vector3 dir = (ai.transform.position.x < ai.playerTransform.position.x)
            ? Vector3.left : Vector3.right;

        ai.Movement.StopImmediately();
        ai.transform.position += dir * dashDistance;

        MarkUsed(); // VERY IMPORTANT
        state = NodeState.Success;
        return state;
    }
}








