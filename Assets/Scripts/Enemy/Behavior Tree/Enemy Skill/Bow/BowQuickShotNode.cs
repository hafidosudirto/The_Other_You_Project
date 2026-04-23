using UnityEngine;

public class BowQuickShotNode : Node
{
    private readonly EnemyAI ai;

    public BowQuickShotNode(EnemyAI ai)
    {
        this.ai = ai;
    }

    public override NodeState Evaluate()
    {
        if (ai == null || ai.Combat == null || ai.playerTransform == null)
            return NodeState.Failure;

        Enemy_Bow_QuickShot skill = ai.Combat.quickShotBow;
        if (skill == null)
            return NodeState.Failure;

        float distance = Vector2.Distance(ai.transform.position, ai.playerTransform.position);

        if (ai.Movement != null)
            ai.Movement.SetDesiredRange(skill.skillRange);

        if (skill.IsActive)
            return NodeState.Running;

        if (distance > skill.skillRange + skill.rangeTolerance)
            return NodeState.Running;

        if (distance < skill.minRange - skill.rangeTolerance)
            return NodeState.Failure;

        if (!skill.IsInRange(distance))
            return NodeState.Failure;

        if (!skill.CanTrigger(distance))
            return NodeState.Failure;

        skill.Trigger();
        return NodeState.Running;
    }
}