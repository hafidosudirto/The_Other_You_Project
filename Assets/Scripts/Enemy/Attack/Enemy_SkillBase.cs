using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Enemy_SkillBase : MonoBehaviour
{
    [Header("General Skill Settings")]
    public float windup = 0.15f;
    public float activeTime = 0.20f;
    public float recovery = 0.25f;

    protected EnemySwordSkillController controller;
    protected EnemyMovementFSM movementFSM;

    protected bool isActive = false;
    protected float timer = 0f;

    protected virtual void Awake()
    {
        controller = GetComponent<EnemySwordSkillController>();
        movementFSM = GetComponent<EnemyMovementFSM>();
    }

    public void Ttigger()
    {
        if (!isActive)
        {
            StartCoroutine(ExecuteSkill());
        }
    }

    protected virtual IEnumerator ExecuteSkill()
    {
        isActive = true;

        // Action Lock ON
        controller.InvokeSkillStart();

        // Windup
        yield return new WaitForSeconds(windup);

        // Active
        OnSkillActive();
        yield return new WaitForSeconds(activeTime);

        // Recovery
        OnSkillEnd();
        yield return new WaitForSeconds(recovery);

        // Action Lock OFF
        controller.InvokeSkillEnd();

        isActive = false;
    }

    protected virtual void OnSkillActive() { }
    protected virtual void OnSkillEnd() { }

    // Gizmos handled per-skill
    protected abstract void OnDrawGizmosSelected();
}
