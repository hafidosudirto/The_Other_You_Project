using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NodeManager))]
public class Bow_AttackTree : MonoBehaviour
{
    private enum BowSkillIndex
    {
        QuickShot = 0,
        SpreadArrow = 1, // Diubah dari PiercingShot menjadi SpreadArrow
        FullDraw = 2,
        ConcussiveShot = 3
    }

    private NodeManager nodeManager;
    private WeightedRandomSelector offensiveTree;

    [Header("Bow Attributes")]
    public float attackRange = 8f;
    public float bowSenseRange = 10f;
    [SerializeField] private Vector2 attackRangeOffset = Vector2.zero;

    [Header("DDA Selection")]
    [Tooltip("Jika aktif, skill dengan bobot DDA tertinggi akan diprioritaskan.")]
    [SerializeField] private bool forceHighestDDAWeight = true;

    [Tooltip("Jika bobot tertinggi mencapai nilai ini, AI akan memilih skill tersebut secara deterministik.")]
    [SerializeField] private float deterministicThreshold = 50f;

    [Tooltip("Jika aktif, log bobot DDA dan skill yang dipilih akan muncul di Console.")]
    [SerializeField] private bool showDDADebugLog = true;

    [Header("Fallback Weights")]
    [SerializeField] private float fallbackQuickWeight = 25f;
    [SerializeField] private float fallbackSpreadWeight = 25f; // Update nama variabel
    [SerializeField] private float fallbackFullDrawWeight = 25f;
    [SerializeField] private float fallbackConcussiveWeight = 25f;

    // Pastikan kamu memiliki Node kelas BowSpreadArrowNode di sistem kamu
    private BowQuickShotNode quickShot;
    private BowFullDrawNode fullDraw;
    private BowSpreadArrowNode spreadArrow; // Update node
    private BowConcussiveShotNode concussive;

    private readonly List<float> currentWeights = new List<float> { 25f, 25f, 25f, 25f };

    public void Initialize(NodeManager manager)
    {
        nodeManager = manager;

        quickShot = new BowQuickShotNode(nodeManager);
        fullDraw = new BowFullDrawNode(nodeManager);
        spreadArrow = new BowSpreadArrowNode(nodeManager); // Update inisialisasi
        concussive = new BowConcussiveShotNode(nodeManager);

        BuildAttackTree();
    }

    private void BuildAttackTree()
    {
        /*
         * Urutan WAJIB sama dengan DDA/DataTracker:
         * 0 = QuickShot
         * 1 = SpreadArrow
         * 2 = FullDraw
         * 3 = ConcussiveShot
         */
        List<Node> nodes = new List<Node>
        {
            quickShot,
            spreadArrow, // Menggantikan piercing
            fullDraw,
            concussive
        };

        ApplyFallbackWeightsToCache();
        offensiveTree = new WeightedRandomSelector(nodes, new List<float>(currentWeights));
    }

    public void EvaluateTree()
    {
        if (nodeManager == null || nodeManager.playerTransform == null)
            return;

        float dist = Vector2.Distance(transform.position, nodeManager.playerTransform.position);
        if (dist > bowSenseRange)
            return;

        SyncWeightsFromDDA();

        if (ShouldUseDeterministicDDA(out int selectedIndex))
        {
            EvaluateSelectedSkill(selectedIndex);
            return;
        }

        offensiveTree?.Evaluate();
    }

    public void SyncWeightsFromDDA()
    {
        ApplyFallbackWeightsToCache();
        bool hasValidDDAWeights = false;

        if (nodeManager != null && nodeManager.adaptiveProfile != null)
        {
            IReadOnlyList<float> ddaWeights = nodeManager.adaptiveProfile.GetBowSkillWeights();
            if (ddaWeights != null && ddaWeights.Count > 0)
            {
                int copyCount = Mathf.Min(4, ddaWeights.Count);
                for (int i = 0; i < copyCount; i++)
                    currentWeights[i] = Mathf.Max(0f, ddaWeights[i]);
                hasValidDDAWeights = true;
            }
        }

        if (!hasValidDDAWeights && DDAController.Instance != null)
        {
            float[] ddaWeights = DDAController.Instance.GetCurrentBowSkillWeightsCopy();
            if (ddaWeights != null && ddaWeights.Length > 0)
            {
                int copyCount = Mathf.Min(4, ddaWeights.Length);
                for (int i = 0; i < copyCount; i++)
                    currentWeights[i] = Mathf.Max(0f, ddaWeights[i]);
                hasValidDDAWeights = true;
            }
        }

        float totalWeight = 0f;
        for (int i = 0; i < currentWeights.Count; i++)
            totalWeight += currentWeights[i];

        if (totalWeight <= 0.001f)
            ApplyFallbackWeightsToCache();

        if (offensiveTree != null)
            offensiveTree.SetWeights(new List<float>(currentWeights));

        if (showDDADebugLog)
        {
            Debug.Log(
                $"[Bow_AttackTree] Bobot bow diterapkan | " +
                $"Quick={currentWeights[0]:F5}, " +
                $"Spread={currentWeights[1]:F5}, " +
                $"FullDraw={currentWeights[2]:F5}, " +
                $"Concussive={currentWeights[3]:F5}"
            );
        }
    }

    private void ApplyFallbackWeightsToCache()
    {
        currentWeights[0] = Mathf.Max(0f, fallbackQuickWeight);
        currentWeights[1] = Mathf.Max(0f, fallbackSpreadWeight);
        currentWeights[2] = Mathf.Max(0f, fallbackFullDrawWeight);
        currentWeights[3] = Mathf.Max(0f, fallbackConcussiveWeight);
    }

    private bool ShouldUseDeterministicDDA(out int selectedIndex)
    {
        selectedIndex = 0;
        if (!forceHighestDDAWeight) return false;

        float highestWeight = currentWeights[0];
        for (int i = 1; i < currentWeights.Count; i++)
        {
            if (currentWeights[i] > highestWeight)
            {
                highestWeight = currentWeights[i];
                selectedIndex = i;
            }
        }

        bool allWeightsEqual =
            Mathf.Approximately(currentWeights[0], currentWeights[1]) &&
            Mathf.Approximately(currentWeights[1], currentWeights[2]) &&
            Mathf.Approximately(currentWeights[2], currentWeights[3]);

        if (allWeightsEqual) return false;
        return highestWeight >= deterministicThreshold;
    }

    private void EvaluateSelectedSkill(int selectedIndex)
    {
        Node selectedNode = GetNodeByIndex(selectedIndex);

        if (selectedNode == null)
        {
            Debug.LogWarning($"[Bow_AttackTree] Node index {selectedIndex} null. Fallback ke WeightedRandomSelector.");
            offensiveTree?.Evaluate();
            return;
        }

        if (showDDADebugLog)
        {
            Debug.Log($"[Bow_AttackTree] Skill dipilih DDA -> {GetSkillName(selectedIndex)} | Bobot={currentWeights[selectedIndex]:F5}");
        }

        selectedNode.Evaluate();
    }

    private Node GetNodeByIndex(int index)
    {
        switch ((BowSkillIndex)index)
        {
            case BowSkillIndex.QuickShot: return quickShot;
            case BowSkillIndex.SpreadArrow: return spreadArrow; // Update
            case BowSkillIndex.FullDraw: return fullDraw;
            case BowSkillIndex.ConcussiveShot: return concussive;
            default: return null;
        }
    }

    private string GetSkillName(int index)
    {
        switch ((BowSkillIndex)index)
        {
            case BowSkillIndex.QuickShot: return "QuickShot";
            case BowSkillIndex.SpreadArrow: return "SpreadArrow"; // Update
            case BowSkillIndex.FullDraw: return "FullDraw";
            case BowSkillIndex.ConcussiveShot: return "ConcussiveShot";
            default: return "Unknown";
        }
    }

    public Vector2 GetAttackRangeCenter()
    {
        return (Vector2)transform.position + attackRangeOffset;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, bowSenseRange);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetAttackRangeCenter(), attackRange);
    }
}