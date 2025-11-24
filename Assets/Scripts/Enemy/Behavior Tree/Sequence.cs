using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Sequence : Node
{
    public Sequence() : base() { }
    public Sequence(List<Node> children) : base(children) { }

    public override NodeState Evaluate()
    {
        foreach (Node child in children)
        {
            NodeState childState = child.Evaluate();
            if (childState == NodeState.Failure)
            {
                state = NodeState.Failure;
                return state;
            }
            if (childState == NodeState.Running)
            {
                state = NodeState.Running;
                return state;
            }
        }
        state = NodeState.Success;
        return state;
    }
}