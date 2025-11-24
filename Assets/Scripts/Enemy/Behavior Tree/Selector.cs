using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public class Selector : Node
{
    public Selector() : base() { }
    public Selector(List<Node> children) : base(children) { }

    public override NodeState Evaluate()
    {
        foreach (Node child in children)
        {
            NodeState childState = child.Evaluate();
            if (childState == NodeState.Success)
            {
                state = NodeState.Success;
                return state;
            }
            if (childState == NodeState.Running)
            {
                state = NodeState.Running;
                return state;
            }
            // continue if Failure
        }
        state = NodeState.Failure;
        return state;
    }
}