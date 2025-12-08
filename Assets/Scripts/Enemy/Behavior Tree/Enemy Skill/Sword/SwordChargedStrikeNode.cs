using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwordChargedStrikeNode : Node
{
    private EnemyAI ai;
    private float timer;

    private float cdMin = 2.5f;
    private float cdMax = 3.5f;

    public SwordChargedStrikeNode(EnemyAI enemy)
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
        if (!ai || ai.Combat == null || ai.Combat.chargedStrike == null)
            return NodeState.Failure;

        if (timer > 0f)
        {
            timer -= Time.deltaTime;
            return NodeState.Failure;
        }

        ai.Combat.chargedStrike.Trigger();

        if (ai.playerTransform)
            Debug.DrawRay(ai.transform.position, Vector3.up, new Color(1f, 0.5f, 0f), 0.1f);

        ResetCD();
        return NodeState.Success;
    }
}




