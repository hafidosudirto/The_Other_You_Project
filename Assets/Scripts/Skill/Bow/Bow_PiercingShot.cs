using UnityEngine;

public class Bow_PiercingShot : MonoBehaviour, ISkill
{
    [Header("Piercing Shot Settings")]
    public GameObject arrowPrefab;
    public Transform arrowSpawnPoint;

    public float shootCooldown = 0.8f;
    public float shootSpeed = 14f;

    [Header("Damage Settings")]
    public float damage = 10f;

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
        {
            DebugHub.Warning("PiercingShot: Missing prefab / spawnpoint!");
            return;
        }

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
            dmg.SetStats(damage, 0f, 0f, true, false);
        }

        DebugHub.Bow($"PiercingShot Spawn @ {arrowSpawnPoint.position}");
        DebugHub.Bow($"PiercingShot Velocity = {rb.velocity}");
        DebugHub.Bow("PiercingShot Damage Applied");
    }
}
