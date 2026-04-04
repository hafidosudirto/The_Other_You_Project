using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseDefenseNode : Node
{
    protected EnemyAI ai;

    // cooldown
    protected float cooldown = 1.2f;      // detik
    protected float lastUsedTime = -999f;

    // fail chance opsional
    protected float failChance = 0.15f;    // 15% gagal defense

    protected BaseDefenseNode(EnemyAI ai)
    {
        this.ai = ai;
    }

    protected bool CanUse()
    {
        if (Time.time - lastUsedTime < cooldown)
            return false;

        if (Random.value < failChance)
            return false;

        return true;
    }

    protected void MarkUsed()
    {
        lastUsedTime = Time.time;
    }
}

