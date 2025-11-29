using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwordWhirlwindNode : Node
{
    private EnemyAI ai;
    private float timer;

    private float cdMin = 4f;
    private float cdMax = 5.5f;

    public SwordWhirlwindNode(EnemyAI enemy)
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
        if (!ai || ai.Combat == null || ai.Combat.whirlwind == null)
            return NodeState.Failure;

        if (timer > 0f)
        {
            timer -= Time.deltaTime;
            return NodeState.Failure;
        }

        ai.Combat.whirlwind.Trigger();

        Debug.DrawRay(ai.transform.position, Vector3.up * 1.2f, Color.cyan, 0.1f);

        ResetCD();
        return NodeState.Success;
    }
}
