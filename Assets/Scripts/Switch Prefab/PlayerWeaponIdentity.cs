using UnityEngine;

[DisallowMultipleComponent]
public class PlayerWeaponIdentity : MonoBehaviour
{
    [Header("Weapon Identity")]
    public WeaponType currentWeapon = WeaponType.None;

    [Tooltip("Hanya Player_W0 yang boleh true. Player_W1 dan Player_W2 harus false.")]
    public bool canPickupWeapon = true;

    public bool CanUseWeaponPickup
    {
        get
        {
            return canPickupWeapon && currentWeapon == WeaponType.None;
        }
    }

    private void Awake()
    {
        ApplyIdentityToPlayerComponent();
    }

    public void Initialize(WeaponType weapon, bool canPickup)
    {
        currentWeapon = weapon;
        canPickupWeapon = canPickup;

        ApplyIdentityToPlayerComponent();
    }

    public void ApplyIdentityToPlayerComponent()
    {
        Player player = GetComponent<Player>();

        if (player == null)
            player = GetComponentInChildren<Player>(true);

        if (player == null)
            player = GetComponentInParent<Player>();

        if (player != null)
        {
            player.weaponType = currentWeapon;
        }
    }
}