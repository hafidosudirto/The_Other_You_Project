using UnityEngine;

public class Player : CharacterBase
{
    public WeaponType weaponType = WeaponType.Sword;

    protected override void Awake()
    {
        base.Awake();
    }
}