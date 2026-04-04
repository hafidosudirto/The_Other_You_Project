using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DefenseParryNode : BaseDefenseNode
{
    public DefenseParryNode(EnemyAI ai) : base(ai)
    {
        cooldown = 1.0f;
        failChance = 0.35f;   // parry lebih sulit, sering gagal
    }

    public override NodeState Evaluate()
    {
        if (!CanUse())
        {
            state = NodeState.Failure;
            return state;
        }

        Debug.Log("Enemy Parry!");

        // TODO: animasi parry + parry window
        // ai.Combat.StartParryWindow();

        MarkUsed();
        state = NodeState.Success;
        return state;
    }
}


