using UnityEngine;

public sealed class Enemy_DefenseTree : MonoBehaviour
{
    [SerializeField] private EnemyCombatController combat;

    private BTNode _root;

    private void Awake()
    {
        if (combat == null) combat = GetComponent<EnemyCombatController>();
        Build();
    }

    private void Update()
    {
        if (combat == null || _root == null) return;
        _root.Tick();
    }

    private void Build()
    {
        var c1 = new ConditionNode(() => combat.IsPlayerAttacking);
        var c2 = new ConditionNode(() => combat.IsNotBusy);
        var c3 = new ConditionNode(() => combat.HasNewPlayerAttack);

        var lengah = new StartReactionNode(
            tryStart: () => combat.StartLengah(),
            isBusy: () => !combat.IsNotBusy
        );

        var dash = new StartReactionNode(
            tryStart: () => combat.StartDash(),
            isBusy: () => !combat.IsNotBusy
        );

        var riposte = new StartReactionNode(
            tryStart: () => combat.StartRiposte(),
            isBusy: () => !combat.IsNotBusy
        );

        // 50% lengah, 25% dash, 25% riposte
        var weighted = new WeightedRandomSelectorNode(
            new WeightedRandomSelectorNode.Entry(lengah, 50),
            new WeightedRandomSelectorNode.Entry(dash, 25),
            new WeightedRandomSelectorNode.Entry(riposte, 25)
        );

        _root = new SequenceNode(c1, c2, c3, weighted);
    }
}
