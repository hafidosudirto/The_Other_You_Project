using UnityEngine;

public class ChooseSkillNode : Node
{
    private readonly EnemyAI ai;
    private readonly IEnemySkill skill;

    public ChooseSkillNode(EnemyAI ai, IEnemySkill skill)
    {
        this.ai = ai;
        this.skill = skill;
    }

    public override NodeState Evaluate()
    {
        if (ai == null || skill == null)
        {
            state = NodeState.Failure;
            return state;
        }

        if (ai.Movement != null)
            ai.Movement.SetDesiredRange(skill.SkillRange);

        state = NodeState.Success;
        return state;
    }
}
