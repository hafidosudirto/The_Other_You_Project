using System;
using UnityEngine;

public sealed class StartReactionNode : BTNode
{
    private readonly Func<bool> _tryStart;
    private readonly Func<bool> _isBusy;

    private bool _started;

    public StartReactionNode(Func<bool> tryStart, Func<bool> isBusy)
    {
        _tryStart = tryStart;
        _isBusy = isBusy;
    }

    public override NodeStatus Tick()
    {
        if (!_started)
        {
            if (!_tryStart()) return NodeStatus.Failure;
            _started = true;
            return NodeStatus.Running;
        }

        return _isBusy() ? NodeStatus.Running : NodeStatus.Success;
    }

    public override void Reset() => _started = false;
}
