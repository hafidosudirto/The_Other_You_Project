using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IsUsingSwordNode : Node
{
    public override NodeState Evaluate()
    {
        if (DDAController.Instance == null)
            return NodeState.Failure;

        return (DDAController.Instance.currentPlayerDominantWeapon == WeaponType.Sword)
            ? NodeState.Success
            : NodeState.Failure;
    }
}
