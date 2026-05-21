using UnityEngine;

public class Player : CharacterBase
{
    [Header("Player Identity")]
    [Tooltip("Jenis senjata player aktif. Dipakai untuk sinkronisasi HUD, DDA, dan prefab switch.")]
    public WeaponType weaponType = WeaponType.None;

    [Header("Input / Action State")]
    [Tooltip("Jika aktif, movement player dikunci. Biasanya dipakai saat casting, stagger, atau transisi.")]
    public bool lockMovement = false;

    [Tooltip("Status serangan umum. Dipakai sebagai sinyal sederhana untuk Animation Mode HUD jika skill belum punya state khusus.")]
    public bool isAttacking = false;

    public bool IsUnarmed => weaponType == WeaponType.None;
    public bool IsSword => weaponType == WeaponType.Sword;
    public bool IsBow => weaponType == WeaponType.Bow;

    public bool CanReceiveInput
    {
        get
        {
            return currentHP > 0f &&
                   !lockMovement &&
                   !isStaggered;
        }
    }

    protected override void Awake()
    {
        base.Awake();
    }

    public void SetWeaponType(WeaponType newWeaponType)
    {
        weaponType = newWeaponType;
    }

    public void SetMovementLocked(bool locked)
    {
        lockMovement = locked;
    }

    public void SetAttacking(bool attacking)
    {
        isAttacking = attacking;
    }
}
