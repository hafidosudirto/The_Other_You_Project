using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data serangan yang dikirim dari *hitbox* player ke enemy.
/// Dapat diperluas dengan knockback, stagger, dsb.
/// </summary>
[System.Serializable]
public struct AttackPayload
{
    public float damage;

    public AttackPayload(float damage)
    {
        this.damage = damage;
    }
}

