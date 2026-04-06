using System;
using System.Collections.Generic;
using UnityEngine;

public enum NodeStatus { Success, Failure, Running }

public abstract class BTNode
{
    public abstract NodeStatus Tick();
    public virtual void Reset() { }
}

public sealed class ConditionNode : BTNode
{
    private readonly Func<bool> _predicate;

    public ConditionNode(Func<bool> predicate)
    {
        _predicate = predicate;
    }

    public override NodeStatus Tick()
    {
        return _predicate() ? NodeStatus.Success : NodeStatus.Failure;
    }
}

public sealed class SequenceNode : BTNode
{
    private readonly List<BTNode> _children;
    private int _index;

    public SequenceNode(params BTNode[] children)
    {
        _children = new List<BTNode>(children);
    }

    public override NodeStatus Tick()
    {
        while (_index < _children.Count)
        {
            NodeStatus status = _children[_index].Tick();

            if (status == NodeStatus.Running)
                return NodeStatus.Running;

            if (status == NodeStatus.Failure)
            {
                Reset();
                return NodeStatus.Failure;
            }

            _index++;
        }

        Reset();
        return NodeStatus.Success;
    }

    public override void Reset()
    {
        _index = 0;
        foreach (BTNode child in _children)
            child.Reset();
    }
}

public sealed class DynamicWeightedRandomSelectorNode : BTNode
{
    public readonly struct Entry
    {
        public readonly BTNode Node;
        public readonly Func<int> WeightProvider;

        public Entry(BTNode node, Func<int> weightProvider)
        {
            Node = node;
            WeightProvider = weightProvider;
        }
    }

    private readonly List<Entry> _entries;
    private int _runningIndex = -1;

    public DynamicWeightedRandomSelectorNode(params Entry[] entries)
    {
        _entries = new List<Entry>(entries);
    }

    public override NodeStatus Tick()
    {
        if (_runningIndex >= 0)
        {
            NodeStatus runningStatus = _entries[_runningIndex].Node.Tick();
            if (runningStatus != NodeStatus.Running)
                _runningIndex = -1;
            return runningStatus;
        }

        List<int> availableIndices = BuildAvailableIndexList();
        while (availableIndices.Count > 0)
        {
            int chosenIndex = ChooseIndex(availableIndices);
            NodeStatus status = _entries[chosenIndex].Node.Tick();

            if (status == NodeStatus.Running)
            {
                _runningIndex = chosenIndex;
                return NodeStatus.Running;
            }

            if (status == NodeStatus.Success)
                return NodeStatus.Success;

            availableIndices.Remove(chosenIndex);
        }

        return NodeStatus.Failure;
    }

    public override void Reset()
    {
        _runningIndex = -1;
        foreach (Entry entry in _entries)
            entry.Node.Reset();
    }

    private List<int> BuildAvailableIndexList()
    {
        List<int> indices = new List<int>();

        for (int i = 0; i < _entries.Count; i++)
        {
            int weight = SafeWeight(_entries[i].WeightProvider);
            if (weight > 0)
                indices.Add(i);
        }

        return indices;
    }

    private int ChooseIndex(List<int> availableIndices)
    {
        int totalWeight = 0;
        for (int i = 0; i < availableIndices.Count; i++)
            totalWeight += SafeWeight(_entries[availableIndices[i]].WeightProvider);

        if (totalWeight <= 0)
            return availableIndices[0];

        int roll = UnityEngine.Random.Range(0, totalWeight);
        int cumulative = 0;

        for (int i = 0; i < availableIndices.Count; i++)
        {
            int index = availableIndices[i];
            cumulative += SafeWeight(_entries[index].WeightProvider);
            if (roll < cumulative)
                return index;
        }

        return availableIndices[availableIndices.Count - 1];
    }

    private int SafeWeight(Func<int> provider)
    {
        if (provider == null)
            return 0;

        return Mathf.Max(0, provider.Invoke());
    }
}
