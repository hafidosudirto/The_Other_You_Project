using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WeaponPickupZone : MonoBehaviour
{
    [Header("Pickup Data")]
    [SerializeField] private WeaponType targetWeapon = WeaponType.Sword;

    [Header("References")]
    [SerializeField] private PlayerPrefabSwitchManager switchManager;

    [Header("After Pickup")]
    [SerializeField] private bool disableColliderAfterSuccess = true;
    [SerializeField] private bool hideVisualAfterSuccess = true;
    [SerializeField] private GameObject visualRoot;

    [Header("Trigger Safety")]
    [SerializeField] private bool alsoCheckOnTriggerStay = true;

    private Collider2D pickupCollider;
    private bool alreadyUsed;

    private void Reset()
    {
        pickupCollider = GetComponent<Collider2D>();

        if (pickupCollider != null)
            pickupCollider.isTrigger = true;
    }

    private void Awake()
    {
        pickupCollider = GetComponent<Collider2D>();

        if (pickupCollider != null)
            pickupCollider.isTrigger = true;

        if (switchManager == null)
            switchManager = FindObjectOfType<PlayerPrefabSwitchManager>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryProcessPickup(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!alsoCheckOnTriggerStay)
            return;

        TryProcessPickup(other);
    }

    private void TryProcessPickup(Collider2D other)
    {
        if (alreadyUsed)
            return;

        if (other == null)
            return;

        if (switchManager == null)
            switchManager = FindObjectOfType<PlayerPrefabSwitchManager>();

        if (switchManager == null)
        {
            Debug.LogWarning("[WEAPON PICKUP ZONE] PlayerPrefabSwitchManager tidak ditemukan di scene.");
            return;
        }

        bool success = switchManager.TrySwitchFromPickup(other, targetWeapon);

        if (!success)
            return;

        alreadyUsed = true;

        if (disableColliderAfterSuccess && pickupCollider != null)
            pickupCollider.enabled = false;

        if (hideVisualAfterSuccess)
        {
            if (visualRoot != null)
                visualRoot.SetActive(false);
        }
    }
}