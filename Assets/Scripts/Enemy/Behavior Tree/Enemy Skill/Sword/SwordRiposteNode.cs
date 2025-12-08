using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwordRiposteNode : Node
{
    private EnemyAI ai;
    private float timer;

    private float cdMin = 2f;
    private float cdMax = 4f;

    public SwordRiposteNode(EnemyAI enemy)
    {
        ai = enemy;
        ResetCD();
    }

    private void ResetCD()
    {
        timer = Random.Range(cdMin, cdMax);
    }

    public override NodeState Evaluate()
    {
        if (!ai || ai.Combat == null || ai.Combat.riposte == null)
            return NodeState.Failure;

        if (timer > 0)
        {
            timer -= Time.deltaTime;
            return NodeState.Failure;
        }

        ai.Combat.riposte.Trigger();

        Debug.DrawRay(ai.transform.position,
            ai.IsFacingRight ? Vector3.right : Vector3.left,
            Color.green, 0.12f);

        ResetCD();
        return NodeState.Success;
    }
}




