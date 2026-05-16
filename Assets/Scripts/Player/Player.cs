using UnityEngine;

public class Player : CharacterBase
{
    public WeaponType weaponType = WeaponType.None;

    public bool lockMovement = false;

    public bool isAttacking = false;

    protected override void Awake()
    {
        base.Awake();
    }
}