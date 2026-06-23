using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NodeManager))]
public class Sword_AttackTree : MonoBehaviour
{
    private NodeManager nodeManager;
    private WeightedRandomSelector offensiveTree;

    [Header("Sword Attributes")]
    public float attackRange = 1.8f;
    public float swordSenseRange = 3.2f;

    [Header("Legacy Sword Range Offset")]
    [SerializeField] private Vector2 attackRangeOffset = new Vector2(1f, 0f);
    [SerializeField] private bool useLegacyAttackOffsetFallback = true;
    [SerializeField] private float legacyForwardAttackOffset = 1f;

    public void Initialize(NodeManager manager)
    {
        nodeManager = manager;
        BuildAttackTree();
    }

    private void BuildAttackTree()
    {
        var slashComboNode = new SwordSlashComboNode(nodeManager);
        var whirlwindNode = new SwordWhirlwindNode(nodeManager);
        var chargedStrikeNode = new SwordChargedStrikeNode(nodeManager);

        List<Node> nodes = new List<Node> { slashComboNode, whirlwindNode, chargedStrikeNode };
        List<float> weights = new List<float> { 33f, 33f, 33f };

        offensiveTree = new WeightedRandomSelector(nodes, weights);
    }

    public void EvaluateTree()
    {
        float dist = Vector2.Distance(transform.position, nodeManager.playerTransform.position);
        if (dist > swordSenseRange) return;

        // --- FIX: Sinkronisasi Bobot Dinamis ---
        // Menjamin AI selalu menggunakan data DDA terbaru sebelum mengeksekusi serangan
        if (nodeManager.adaptiveProfile != null && offensiveTree != null)
        {
            var ddaWeights = nodeManager.adaptiveProfile.GetSwordSkillWeights();
            if (ddaWeights != null && ddaWeights.Count >= 3)
            {
                offensiveTree.SetWeights(new List<float> { ddaWeights[0], ddaWeights[1], ddaWeights[2] });
            }
        }

        offensiveTree?.Evaluate();
    }

    public Vector2 GetAttackRangeCenter()
    {
        Vector2 offset = attackRangeOffset;

        /*
         * Prefab/scene lama yang sudah menyimpan Vector2.zero tidak otomatis mengambil
         * default baru dari script. Fallback ini mengembalikan perilaku EnemyAI lama:
         * pusat hit range berada 1 unit di depan arah hadap sword enemy.
         */
        if (useLegacyAttackOffsetFallback && offset == Vector2.zero)
            offset.x = legacyForwardAttackOffset;

        NodeManager manager = nodeManager != null ? nodeManager : GetComponent<NodeManager>();

        if (manager != null)
            offset.x *= manager.ForwardSign;

        return (Vector2)transform.position + offset;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, swordSenseRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetAttackRangeCenter(), attackRange);
    }
}