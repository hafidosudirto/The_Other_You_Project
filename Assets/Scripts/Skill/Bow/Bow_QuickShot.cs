using UnityEngine;

public class Bow_QuickShot : MonoBehaviour, ISkill
{
    [Header("Quick Shot Settings")]
    public GameObject arrowPrefab;
    public Transform arrowSpawnPoint;

    public float shootCooldown = 0.4f;
    public float shootSpeed = 12f;

    [Header("Knockback & Stun Settings")]
    public float knockbackForce = 0f;
    public float stunDuration = 0f;

    [Header("Visual")]
    public Color arrowColor = Color.white;

    private CharacterBase character;
    private float lastShootTime = -999f;

    void Awake()
    {
        character = GetComponentInParent<CharacterBase>();
    }

    public void TriggerSkill(int slotIndex)
    {
        if (Time.time < lastShootTime + shootCooldown)
            return;

        if (!character || !character.CanAct())
            return;

        lastShootTime = Time.time;
        ShootArrow();
    }

    private void ShootArrow()
    {
        if (!arrowPrefab || !arrowSpawnPoint)
        {
            Debug.LogWarning("[QuickShot] Missing arrow prefab or spawn point!");
            return;
        }

        GameObject arrowObj = Instantiate(
            arrowPrefab,
            arrowSpawnPoint.position,
            arrowSpawnPoint.rotation
        );

        // VISUAL
        SpriteRenderer sr = arrowObj.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = arrowColor;

        // ARAH
        Vector2 dir = character != null && character.isFacingRight ?
            Vector2.right : Vector2.left;

        Rigidbody2D rb = arrowObj.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.velocity = dir * shootSpeed;

        // DAMAGE
        ArrowDamage dmg = arrowObj.GetComponent<ArrowDamage>();
        if (dmg != null)
        {
            dmg.SetOwner(character);
            dmg.SetStats(
                dmg.baseDamage,
                knockbackForce,
                stunDuration,
                false,
                false
            );
        }

        DebugHub.Bow("[QuickShot] Arrow Spawn");
    }
}
