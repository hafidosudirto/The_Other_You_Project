using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwordSlashComboNode : Node
{
    private EnemyAI ai;
    private float timer;

    private float cdMin = 0.8f;
    private float cdMax = 1.25f;

    public SwordSlashComboNode(EnemyAI enemy)
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
        if (ai == null) return NodeState.Failure;
        if (ai.Combat == null) return NodeState.Failure;
        if (ai.Combat.slashCombo == null) return NodeState.Failure;

        if (timer > 0f)
        {
            timer -= Time.deltaTime;
            return NodeState.Failure;
        }

        ai.Combat.slashCombo.Trigger();

        if (ai.playerTransform)
            Debug.DrawLine(ai.transform.position, ai.playerTransform.position, Color.red, 0.1f);

        ResetCD();
        return NodeState.Success;
    }
}

