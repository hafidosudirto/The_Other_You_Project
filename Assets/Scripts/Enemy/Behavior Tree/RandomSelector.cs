using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomSelector : Node
{
    public RandomSelector(List<Node> children) : base(children) { }

    public override NodeState Evaluate()
    {
        if (children.Count > 0)
        {
            int randomIndex = Random.Range(0, children.Count);
            Node randomChild = children[randomIndex];
            return randomChild.Evaluate();
        }
        return NodeState.Failure;
    }
}
