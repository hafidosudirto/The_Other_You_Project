using UnityEngine;

public class CheckRangeNode : Node
{
    private readonly EnemyAI ai;
    private readonly IEnemySkill skill;
    private readonly float tolerance = 0.25f;

    public CheckRangeNode(EnemyAI ai, IEnemySkill skill)
    {
        this.ai = ai;
        this.skill = skill;
    }

    public override NodeState Evaluate()
    {
        if (ai == null || skill == null || ai.playerTransform == null)
        {
            state = NodeState.Failure;
            return state;
        }

        float dist = Mathf.Abs(ai.transform.position.x - ai.playerTransform.position.x);

        if (dist <= skill.SkillRange + tolerance)
            state = NodeState.Success;
        else
            state = NodeState.Running;

        return state;
    }
}
