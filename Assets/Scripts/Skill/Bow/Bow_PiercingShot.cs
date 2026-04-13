using UnityEngine;
using System.Collections;

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

    [Header("Animation")]
    public PlayerAnimation anim;

    [Header("Timing")]
    public float postShotLock = 0.08f;

    private CharacterBase character;
    private Player player;
    private float lastShootTime = -999f;

    private bool isCasting;
    private bool waitingAnimationRelease;

    void Awake()
    {
        character = GetComponentInParent<CharacterBase>();
        player = GetComponentInParent<Player>();
    }

    public void TriggerSkill(int slotIndex)
    {
        if (isCasting)
            return;

        if (Time.time < lastShootTime + shootCooldown)
            return;

        if (!character || !character.CanAct())
            return;

        if (player != null && player.lockMovement)
            return;

        lastShootTime = Time.time;
        isCasting = true;
        waitingAnimationRelease = true;

        if (player != null)
        {
            player.lockMovement = true;
            StopOwnerMovement();
        }

        if (anim != null)
            anim.PlayPiercingShot();
    }

    public void ReleaseFromAnimationEvent()
    {
        if (!isCasting || !waitingAnimationRelease)
            return;

        waitingAnimationRelease = false;
        FireArrow();
        StartCoroutine(EndCastRoutine());
    }

    private IEnumerator EndCastRoutine()
    {
        yield return new WaitForSeconds(postShotLock);

        if (player != null)
            player.lockMovement = false;

        isCasting = false;
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
        if (rb != null)
            DebugHub.Bow($"PiercingShot Velocity = {rb.velocity}");
        DebugHub.Bow("PiercingShot Damage Applied");
    }

    private void StopOwnerMovement()
    {
        if (player == null) return;

        Rigidbody2D ownerRb = player.GetComponent<Rigidbody2D>();
        if (ownerRb != null)
            ownerRb.velocity = Vector2.zero;
    }

    private void OnDisable()
    {
        if (player != null)
            player.lockMovement = false;

        isCasting = false;
        waitingAnimationRelease = false;
    }
}