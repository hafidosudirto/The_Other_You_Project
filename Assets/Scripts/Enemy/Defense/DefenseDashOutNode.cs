using UnityEngine;

public class DefenseDashOutNode : BaseDefenseNode
{
    private Enemy_Dash enemyDash;

    public DefenseDashOutNode(EnemyAI ai) : base(ai)
    {
        cooldown = 2.0f;
        failChance = 0.10f;
    }

    public override NodeState Evaluate()
    {
        if (!CanUse())
        {
            state = NodeState.Failure;
            return state;
        }

        if (ai == null || ai.playerTransform == null)
        {
            state = NodeState.Failure;
            return state;
        }

        if (enemyDash == null)
            enemyDash = ai.GetComponent<Enemy_Dash>();

        if (enemyDash == null)
            enemyDash = ai.gameObject.AddComponent<Enemy_Dash>();

        enemyDash.SetPlayer(ai.playerTransform);

        bool dashStarted = enemyDash.TryDashAwayFromPlayer();

        if (!dashStarted)
        {
            state = NodeState.Failure;
            return state;
        }

        MarkUsed();

        state = NodeState.Success;
        return state;
    }
}