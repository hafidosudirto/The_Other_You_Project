using UnityEngine;

public class Player : CharacterBase
{

    public WeaponType weaponType = WeaponType.Sword;

    public bool lockMovement = false;   // ← tambah ini

    public bool isAttacking = false;
    protected override void Awake()
    {
        base.Awake();
    }
}
