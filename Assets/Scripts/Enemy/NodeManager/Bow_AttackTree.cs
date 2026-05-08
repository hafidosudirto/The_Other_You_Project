using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NodeManager))]
public class Bow_AttackTree : MonoBehaviour
{
    private NodeManager nodeManager;
    private WeightedRandomSelector offensiveTree;

    [Header("Bow Attributes")]
    public float attackRange = 8f;
    public float bowSenseRange = 10f;
    [SerializeField] private Vector2 attackRangeOffset = Vector2.zero;

    private BowQuickShotNode quickShot;
    private BowFullDrawNode fullDraw;
    private BowPiercingShotNode piercing;
    private BowConcussiveShotNode concussive;

    public void Initialize(NodeManager manager)
    {
        nodeManager = manager;

        quickShot = new BowQuickShotNode(nodeManager);
        fullDraw = new BowFullDrawNode(nodeManager);
        piercing = new BowPiercingShotNode(nodeManager);
        concussive = new BowConcussiveShotNode(nodeManager);

        BuildAttackTree();
    }

    private void BuildAttackTree()
    {
        // --- FIX: Urutan Node Disesuaikan dengan DataTracker ---
        // 0 = Quick, 1 = Piercing, 2 = FullDraw, 3 = Concussive
        List<Node> nodes = new List<Node> { quickShot, piercing, fullDraw, concussive };
        List<float> weights = new List<float> { 25f, 25f, 25f, 25f };

        offensiveTree = new WeightedRandomSelector(nodes, weights);
    }

    public void EvaluateTree()
    {
        float dist = Vector2.Distance(transform.position, nodeManager.playerTransform.position);
        if (dist > bowSenseRange) return;

        // --- FIX: Sinkronisasi Bobot Dinamis ---
        if (nodeManager.adaptiveProfile != null && offensiveTree != null)
        {
            var ddaWeights = nodeManager.adaptiveProfile.GetBowSkillWeights();
            if (ddaWeights != null && ddaWeights.Count >= 4)
            {
                offensiveTree.SetWeights(new List<float> { ddaWeights[0], ddaWeights[1], ddaWeights[2], ddaWeights[3] });
            }
        }

        offensiveTree?.Evaluate();
    }

    public Vector2 GetAttackRangeCenter() => (Vector2)transform.position + attackRangeOffset;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, bowSenseRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetAttackRangeCenter(), attackRange);
    }
}