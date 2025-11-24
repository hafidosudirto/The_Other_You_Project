using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum NodeState
{
    Running,
    Success,
    Failure
}

public abstract class Node
{
    protected NodeState state;
    public Node parent;
    protected List<Node> children = new List<Node>();

    public Node()
    {
        parent = null;
    }

    public Node(List<Node> children)
    {
        foreach (Node child in children)
        {
            Attach(child);
        }
    }

    private void Attach(Node node)
    {
        node.parent = this;
        children.Add(node);
    }

    public abstract NodeState Evaluate();
}
