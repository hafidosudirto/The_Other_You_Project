using UnityEngine;

public class ExecuteSkillNode : Node
{
    private EnemyAI ai;
    private Enemy_SkillBase skill;

    public ExecuteSkillNode(EnemyAI ai, Enemy_SkillBase skill)
    {
        this.ai = ai;
        this.skill = skill;
    }

    public override NodeState Evaluate()
    {
        if (skill == null)
        {
            state = NodeState.Failure;
            return state;
        }

        // Jika skill belum aktif, mulai dulu
        if (!skill.IsActive)
        {
            skill.Trigger();
        }

        // Selama skill masih berjalan, BT anggap Running
        state = skill.IsActive ? NodeState.Running : NodeState.Success;
        return state;
    }
}
