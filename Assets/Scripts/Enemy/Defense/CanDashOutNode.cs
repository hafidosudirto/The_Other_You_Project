/*using System.Collections;
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

        Vector2 aiPos = ai.transform.position;
        Vector2 playerPos = ai.playerTransform.position;

        float distX = Mathf.Abs(aiPos.x - playerPos.x);
        float distY = Mathf.Abs(aiPos.y - playerPos.y);

        float triggerDistX = ai.attackRangeX * ratio;
        float triggerDistY = ai.attackRangeY * ratio;

        bool should = distX < triggerDistX && distY < triggerDistY;

        Debug.DrawLine(ai.transform.position, ai.playerTransform.position,
            should ? Color.yellow : Color.cyan, 0.025f);

        return should ? NodeState.Success : NodeState.Failure;
    }
}



*/