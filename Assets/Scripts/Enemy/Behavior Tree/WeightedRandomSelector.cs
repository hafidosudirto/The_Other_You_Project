using System.Collections.Generic;
using UnityEngine;

public class WeightedRandomSelector : Node
{
    private readonly List<Node> nodes;
    private readonly List<float> weights;
    private int runningIndex = -1;

    public WeightedRandomSelector(List<Node> nodes, List<float> weights)
    {
        this.nodes = nodes ?? new List<Node>();
        this.weights = weights ?? new List<float>();
    }

    public void SetWeights(IReadOnlyList<float> newWeights)
    {
        if (newWeights == null || newWeights.Count != nodes.Count)
            return;

        weights.Clear();

        for (int i = 0; i < newWeights.Count; i++)
            weights.Add(Mathf.Max(0f, newWeights[i]));
    }

    public IReadOnlyList<float> GetWeights() => weights;

    public override NodeState Evaluate()
    {
        if (nodes.Count == 0)
            return NodeState.Failure;

        if (weights.Count != nodes.Count)
            return NodeState.Failure;

        // Jika ada node yang sedang Running, pertahankan node tersebut
        // sampai node selesai. Ini mencegah selector mengganti skill
        // di tengah eksekusi animasi atau action lock.
        if (runningIndex >= 0 && runningIndex < nodes.Count)
        {
            NodeState runningResult = nodes[runningIndex].Evaluate();

            if (runningResult == NodeState.Running)
                return NodeState.Running;

            runningIndex = -1;
            return runningResult;
        }

        bool[] tried = new bool[nodes.Count];

        // Fallback dilakukan maksimal sebanyak jumlah node.
        // Pemilihan pertama tetap berbasis bobot DDA.
        // Jika node pertama gagal, node berikutnya dipilih lagi
        // berdasarkan bobot yang tersisa, bukan urutan tetap.
        for (int attempt = 0; attempt < nodes.Count; attempt++)
        {
            int pickedIndex = PickWeightedIndexExcluding(tried);

            if (pickedIndex < 0)
                break;

            tried[pickedIndex] = true;

            NodeState result = nodes[pickedIndex].Evaluate();

            if (result == NodeState.Running)
            {
                runningIndex = pickedIndex;
                return NodeState.Running;
            }

            if (result == NodeState.Success)
            {
                return NodeState.Success;
            }

            // Jika Failure, selector tidak langsung berhenti.
            // Selector mencoba node lain yang belum dicoba.
        }

        return NodeState.Failure;
    }

    private int PickWeightedIndexExcluding(bool[] excluded)
    {
        if (excluded == null || excluded.Length != weights.Count)
            return -1;

        float total = 0f;

        for (int i = 0; i < weights.Count; i++)
        {
            if (excluded[i])
                continue;

            total += Mathf.Max(0f, weights[i]);
        }

        if (total <= 0f)
            return -1;

        float roll = Random.Range(0f, total);
        float accumulator = 0f;

        for (int i = 0; i < weights.Count; i++)
        {
            if (excluded[i])
                continue;

            accumulator += Mathf.Max(0f, weights[i]);

            if (roll <= accumulator)
                return i;
        }

        for (int i = weights.Count - 1; i >= 0; i--)
        {
            if (!excluded[i] && weights[i] > 0f)
                return i;
        }

        return -1;
    }
}