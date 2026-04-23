using UnityEngine;

public class BowPiercingShotNode : Node
{
    private readonly EnemyAI ai;

    public BowPiercingShotNode(EnemyAI ai)
    {
        this.ai = ai;
    }

    public override NodeState Evaluate()
    {
        if (ai == null || ai.Combat == null || ai.playerTransform == null)
            return NodeState.Failure;

        Enemy_Bow_PiercingShot skill = ai.Combat.piercingBow;
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