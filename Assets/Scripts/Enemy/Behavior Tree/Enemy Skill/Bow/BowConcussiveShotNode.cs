using UnityEngine;

public class BowConcussiveShotNode : Node
{
    private readonly NodeManager ai;

    public BowConcussiveShotNode(NodeManager ai)
    {
        this.ai = ai;
    }

    public override NodeState Evaluate()
    {
        if (ai == null || ai.Combat == null || ai.playerTransform == null)
            return NodeState.Failure;

        Enemy_Bow_ConcussiveShot skill = ai.Combat.concussiveBow;
        if (skill == null)
            return NodeState.Failure;

        if (skill.IsActive)
            return NodeState.Running;

        float distance = Vector2.Distance(ai.transform.position, ai.playerTransform.position);

        /*
         * Penting:
         * Jika target terlalu jauh, node harus Failure, bukan Running.
         * Running akan membuat WeightedRandomSelector mengunci node ini,
         * sehingga Bow dapat terlihat diam atau seperti menunggu skill yang belum valid.
         */
        if (!skill.IsInRange(distance))
            return NodeState.Failure;

        if (!skill.CanTrigger(distance))
            return NodeState.Failure;

        skill.Trigger();
        return NodeState.Running;
    }
}