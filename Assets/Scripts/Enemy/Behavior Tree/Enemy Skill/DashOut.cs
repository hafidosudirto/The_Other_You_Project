using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DashOutNode : Node
{
    private EnemyAI ai;

    private float dashTime = 0.20f;
    private float speed = 12f;

    private float t;
    private bool started;
    private int dir;

    public DashOutNode(EnemyAI enemy)
    {
        ai = enemy;
    }

    public override NodeState Evaluate()
    {
        if (ai == null || ai.playerTransform == null || ai.Combat == null)
            return NodeState.Failure;

        if (!started)
        {
            started = true;
            t = dashTime;

            ai.Combat.InvokeSkillStart();

            // Menjauh dari player
            dir = (ai.playerTransform.position.x > ai.transform.position.x) ? -1 : 1;
        }

        t -= Time.deltaTime;

        ai.transform.position += new Vector3(dir * speed * Time.deltaTime, 0, 0);

        Debug.DrawRay(ai.transform.position, Vector3.right * dir, Color.magenta, 0.025f);

        if (t <= 0f)
        {
            ai.Combat.InvokeSkillEnd();
            started = false;
            return NodeState.Success;
        }

        return NodeState.Running;
    }
}






