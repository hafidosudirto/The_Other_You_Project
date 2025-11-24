using UnityEngine;

public class Bow_ConcussiveShot : MonoBehaviour, ISkill
{
    [Header("Concussive Shot Settings")]
    public GameObject arrowPrefab;
    public Transform arrowSpawnPoint;

    public float shootCooldown = 0.8f;
    public float shootSpeed = 10f;

    [Header("Damage & Stun")]
    public float damage = 10f;
    public float knockbackForce = 6f;
    public float stunDuration = 0.3f;
    public float explosionRadius = 1.4f;

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
        FireArrow();
    }

    private void FireArrow()
    {
        if (!arrowPrefab || !arrowSpawnPoint)
            return;

        GameObject arrowObj = Instantiate(
            arrowPrefab,
            arrowSpawnPoint.position,
            arrowSpawnPoint.rotation
        );

        SpriteRenderer sr = arrowObj.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = arrowColor;

        Vector2 dir = character.isFacingRight ? Vector2.right : Vector2.left;

        Rigidbody2D rb = arrowObj.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.velocity = dir * shootSpeed;

        ArrowDamage dmg = arrowObj.GetComponent<ArrowDamage>();
        if (dmg != null)
        {
            dmg.SetOwner(character);
            dmg.SetStats(
                damage,
                knockbackForce,
                stunDuration,
                false,
                true
            );
        }

        DebugHub.Bow($"ConcussiveShot Spawn @ {arrowSpawnPoint.position}");
        DebugHub.Bow($"ConcussiveShot Velocity = {rb.velocity}");
        DebugHub.Bow($"ConcussiveShot Damage Applied");
    }
}
