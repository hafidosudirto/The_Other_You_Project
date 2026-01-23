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
    public ConditionNode(Func<bool> predicate) => _predicate = predicate;

    public override NodeStatus Tick() => _predicate() ? NodeStatus.Success : NodeStatus.Failure;
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
            var s = _children[_index].Tick();
            if (s == NodeStatus.Running) return NodeStatus.Running;
            if (s == NodeStatus.Failure) { Reset(); return NodeStatus.Failure; }
            _index++;
        }

        Reset();
        return NodeStatus.Success;
    }

    public override void Reset()
    {
        _index = 0;
        foreach (var c in _children) c.Reset();
    }
}

public sealed class WeightedRandomSelectorNode : BTNode
{
    public readonly struct Entry
    {
        public readonly BTNode Node;
        public readonly int Weight;
        public Entry(BTNode node, int weight) { Node = node; Weight = Mathf.Max(0, weight); }
    }

    private readonly List<Entry> _entries;
    private int _chosen = -1;

    public WeightedRandomSelectorNode(params Entry[] entries)
    {
        _entries = new List<Entry>(entries);
    }

    public override NodeStatus Tick()
    {
        if (_chosen < 0) _chosen = ChooseIndex();

        var status = _entries[_chosen].Node.Tick();
        if (status != NodeStatus.Running) _chosen = -1;
        return status;
    }

    public override void Reset()
    {
        _chosen = -1;
        foreach (var e in _entries) e.Node.Reset();
    }

    private int ChooseIndex()
    {
        int total = 0;
        for (int i = 0; i < _entries.Count; i++) total += _entries[i].Weight;
        if (total <= 0) return 0;

        // Random.value inklusif [0..1]. :contentReference[oaicite:5]{index=5}
        float r = UnityEngine.Random.value * total;

        float acc = 0f;
        for (int i = 0; i < _entries.Count; i++)
        {
            acc += _entries[i].Weight;
            if (r <= acc) return i;
        }
        return _entries.Count - 1;
    }
}
