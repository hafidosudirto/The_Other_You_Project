using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomSelector : Node
{
    private List<Node> nodes;

    public RandomSelector(List<Node> children)
    {
        nodes = children;
    }

    public override NodeState Evaluate()
    {
        if (nodes.Count == 0)
            return NodeState.Failure;

        int index = Random.Range(0, nodes.Count);
        Node chosen = nodes[index];

        NodeState s = chosen.Evaluate();
        state = s;
        return state;
    }
}

