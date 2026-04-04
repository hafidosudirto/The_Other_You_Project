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
        if (nodes.Count == 0) return NodeState.Failure;
        if (weights.Count != nodes.Count) return NodeState.Failure;

        if (runningIndex >= 0 && runningIndex < nodes.Count)
        {
            var result = nodes[runningIndex].Evaluate();
            if (result == NodeState.Running)
                return NodeState.Running;

            runningIndex = -1;
            return result;
        }

        int picked = PickWeightedIndex();
        var pickedResult = nodes[picked].Evaluate();

        if (pickedResult == NodeState.Running)
            runningIndex = picked;

        return pickedResult;
    }

    private int PickWeightedIndex()
    {
        float total = 0f;
        for (int i = 0; i < weights.Count; i++)
            total += Mathf.Max(0f, weights[i]);

        if (total <= 0f)
            return 0;

        float roll = Random.Range(0f, total);
        float acc = 0f;

        for (int i = 0; i < weights.Count; i++)
        {
            acc += Mathf.Max(0f, weights[i]);
            if (roll <= acc)
                return i;
        }

        return weights.Count - 1;
    }
}
