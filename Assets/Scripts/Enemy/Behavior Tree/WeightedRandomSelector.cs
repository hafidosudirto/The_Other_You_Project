using System.Collections.Generic;
using UnityEngine;

public class WeightedRandomSelector : Node
{
    private readonly List<Node> nodes;
    private readonly List<float> weights;

    // -1 = tidak ada yang sedang running
    private int runningIndex = -1;

    public WeightedRandomSelector(List<Node> nodes, List<float> weights)
    {
        this.nodes = nodes ?? new List<Node>();
        this.weights = weights ?? new List<float>();
    }

    public override NodeState Evaluate()
    {
        if (nodes.Count == 0) return NodeState.Failure;
        if (weights.Count != nodes.Count) return NodeState.Failure;

        // Jika ada yang sedang Running, lanjutkan node itu sampai selesai.
        if (runningIndex >= 0 && runningIndex < nodes.Count)
        {
            var r = nodes[runningIndex].Evaluate();
            if (r == NodeState.Running) return NodeState.Running;

            // selesai -> reset agar bisa pilih lagi
            runningIndex = -1;
            return r;
        }

        // Pilih node baru secara weighted (di sini bobot sama -> 25% per skill)
        int picked = PickWeightedIndex();
        var result = nodes[picked].Evaluate();

        if (result == NodeState.Running)
            runningIndex = picked;

        return result;
    }

    private int PickWeightedIndex()
    {
        float total = 0f;
        for (int i = 0; i < weights.Count; i++)
            total += Mathf.Max(0f, weights[i]);

        float roll = Random.Range(0f, total);
        float acc = 0f;

        for (int i = 0; i < weights.Count; i++)
        {
            acc += Mathf.Max(0f, weights[i]);
            if (roll <= acc) return i;
        }

        return weights.Count - 1;
    }
}
