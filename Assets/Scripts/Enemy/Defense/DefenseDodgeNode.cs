using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class DefenseDodgeNode : BaseDefenseNode
{
    private float dodgeDistance = 1.3f;

    public DefenseDodgeNode(EnemyAI ai) : base(ai)
    {
        cooldown = 0.9f;
        failChance = 0.20f;
    }

    public override NodeState Evaluate()
    {
        if (!CanUse())
        {
            state = NodeState.Failure;
            return state;
        }

        Vector3 dir = (ai.transform.position.x < ai.playerTransform.position.x)
            ? Vector3.left : Vector3.right;

        // Instead of StopImmediately, set velocity to zero if Rigidbody2D is available
        var rb = ai.Movement.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
        }

        ai.transform.position += dir * dodgeDistance;

        MarkUsed();
        state = NodeState.Success;
        return state;
    }
}


