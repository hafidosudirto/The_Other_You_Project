using UnityEngine;

public sealed class Enemy_DefenseTree : MonoBehaviour
{
    [SerializeField] private EnemyCombatController combat;

    private BTNode _root;

    private void Awake()
    {
        if (combat == null)
            combat = GetComponent<EnemyCombatController>();

        Build();
    }

    private void Update()
    {
        if (combat == null || _root == null)
            return;

        _root.Tick();
    }

    private void Build()
    {
        _root = new SequenceNode(
            new ConditionNode(IsCombatValid),
            new ConditionNode(() => combat.IsPlayerAttacking),
            new ConditionNode(() => combat.IsNotBusy),
            new ConditionNode(() => combat.HasNewPlayerAttack),
            new DynamicDefenseReactionNode(combat)
        );
    }

    private bool IsCombatValid()
    {
        return combat != null;
    }

    private sealed class DynamicDefenseReactionNode : BTNode
    {
        private enum ReactionChoice
        {
            None,
            Lengah,
            Dash,
            Riposte,
            Concussive
        }

        private readonly EnemyCombatController combat;
        private readonly StartReactionNode lengahNode;
        private readonly StartReactionNode dashNode;
        private readonly StartReactionNode riposteNode;
        private readonly StartReactionNode concussiveNode;

        private ReactionChoice chosen = ReactionChoice.None;

        public DynamicDefenseReactionNode(EnemyCombatController combat)
        {
            this.combat = combat;

            lengahNode = new StartReactionNode(
                tryStart: () => combat.StartLengah(),
                isBusy: () => !combat.IsNotBusy
            );

            dashNode = new StartReactionNode(
                tryStart: () => combat.StartDash(),
                isBusy: () => !combat.IsNotBusy
            );

            riposteNode = new StartReactionNode(
                tryStart: () => combat.StartRiposte(),
                isBusy: () => !combat.IsNotBusy
            );

            concussiveNode = new StartReactionNode(
                tryStart: () => combat.StartConcussive(),
                isBusy: () => !combat.IsNotBusy
            );
        }

        public override NodeStatus Tick()
        {
            if (combat == null)
                return NodeStatus.Failure;

            // Jika kondisi dasar defense berubah di tengah proses, reset.
            if (!combat.IsPlayerAttacking || !combat.HasNewPlayerAttack)
            {
                Reset();
                return NodeStatus.Failure;
            }

            if (chosen == ReactionChoice.None)
                chosen = ChooseReaction();

            NodeStatus result;

            switch (chosen)
            {
                case ReactionChoice.Lengah:
                    result = lengahNode.Tick();
                    break;

                case ReactionChoice.Dash:
                    result = dashNode.Tick();
                    break;

                case ReactionChoice.Riposte:
                    result = riposteNode.Tick();
                    break;

                case ReactionChoice.Concussive:
                    result = concussiveNode.Tick();
                    break;

                default:
                    result = NodeStatus.Failure;
                    break;
            }

            if (result != NodeStatus.Running)
                Reset();

            return result;
        }

        public override void Reset()
        {
            chosen = ReactionChoice.None;
            lengahNode.Reset();
            dashNode.Reset();
            riposteNode.Reset();
            concussiveNode.Reset();
        }

        private ReactionChoice ChooseReaction()
        {
            // Jika tidak ada histori defense valid dari DDA,
            // enemy hanya memberi celah (lengah).
            if (!combat.HasDefenseProfile)
                return ReactionChoice.Lengah;

            int lengahWeight = Mathf.Max(0, combat.GetLengahWeight());
            int dashWeight = Mathf.Max(0, combat.GetDashWeight());

            int riposteWeight = combat.CanRiposteCurrentPlayerAction
                ? Mathf.Max(0, combat.GetRiposteWeight())
                : 0;

            int concussiveWeight = combat.CanConcussiveCurrentSituation
                ? Mathf.Max(0, combat.GetConcussiveWeight())
                : 0;

            int total = lengahWeight + dashWeight + riposteWeight + concussiveWeight;

            if (total <= 0)
                return ReactionChoice.Lengah;

            float roll = Random.value * total;

            if (roll < lengahWeight)
                return ReactionChoice.Lengah;

            roll -= lengahWeight;
            if (roll < dashWeight)
                return ReactionChoice.Dash;

            roll -= dashWeight;
            if (roll < riposteWeight)
                return ReactionChoice.Riposte;

            // Sisa roll akan masuk ke concussive
            return ReactionChoice.Concussive;
        }
    }
}