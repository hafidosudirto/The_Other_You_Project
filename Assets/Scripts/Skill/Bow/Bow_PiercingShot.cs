using UnityEngine;
using System.Collections;

public class Bow_PiercingShot : MonoBehaviour, ISkill, IEnergySkill
{
    [Header("Piercing Shot Settings")]
    public GameObject arrowPrefab;
    public Transform arrowSpawnPoint;

    public float shootCooldown = 0.8f;
    public float shootSpeed = 14f;

    [Header("Damage Settings")]
    public float damage = 10f;

    [Header("Animation")]
    public PlayerAnimation anim;

    [Header("Facing Source")]
    public SpriteRenderer facingSprite;

    [Header("Timing")]
    public float postShotLock = 0.08f;

    [Header("Energy")]
    [SerializeField, Min(0f)] private float energyCost = 12f;

    public float EnergyCost => energyCost;
    public bool PayEnergyInSkillBase => false;

    private CharacterBase character;
    private Player player;
    private float lastShootTime = -999f;

    private bool isCasting;
    private bool waitingAnimationRelease;

    private void Awake()
    {
        character = GetComponentInParent<CharacterBase>();
        player = GetComponentInParent<Player>();

        if (anim == null)
            anim = GetComponentInParent<PlayerAnimation>();
    }

    public void TriggerSkill(int slotIndex)
    {
        if (isCasting)
            return;

        if (Time.time < lastShootTime + shootCooldown)
            return;

        if (character == null || !character.CanAct())
            return;

        if (player != null && player.lockMovement)
            return;

        if (!TryPayEnergy())
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
            Quaternion.identity
        );

        if (arrowObj == null)
        {
            DebugHub.Warning("PiercingShot: Instantiate failed!");
            return;
        }

        float direction = GetFacingDirection();

        ApplyArrowFacing(arrowObj, direction);

        Rigidbody2D rb = arrowObj.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.velocity = new Vector2(direction * shootSpeed, 0f);

        ArrowDamage dmg = arrowObj.GetComponent<ArrowDamage>();
        if (dmg != null)
        {
            dmg.SetOwner(character);
            dmg.SetStats(damage, 0f, 0f, true, false);
        }

        NotifyDataTrackerPiercingShot();

        DebugHub.Bow($"PiercingShot dir={direction} spawn={arrowSpawnPoint.position}");
        if (rb != null)
            DebugHub.Bow($"PiercingShot velocity={rb.velocity}");
    }

    private void NotifyDataTrackerPiercingShot()
    {
        DataTracker tracker = DataTracker.Instance;
        if (tracker == null)
            return;

        var method = tracker.GetType().GetMethod("RecordBowPiercingShot");
        if (method != null)
        {
            method.Invoke(tracker, null);
            return;
        }

        tracker.RecordAction(PlayerActionType.Offensive, WeaponType.Bow);
    }

    private float GetFacingDirection()
    {
        if (facingSprite != null)
            return facingSprite.flipX ? -1f : 1f;

        if (character != null)
            return character.isFacingRight ? 1f : -1f;

        if (player != null)
            return player.isFacingRight ? 1f : -1f;

        return 1f;
    }

    private void ApplyArrowFacing(GameObject arrowObj, float direction)
    {
        if (arrowObj == null) return;

        Vector3 scale = arrowObj.transform.localScale;
        arrowObj.transform.localScale = new Vector3(
            Mathf.Abs(scale.x),
            Mathf.Abs(scale.y),
            Mathf.Abs(scale.z)
        );

        float z = direction > 0f ? 0f : 180f;
        arrowObj.transform.rotation = Quaternion.Euler(0f, 0f, z);

        SpriteRenderer[] renderers = arrowObj.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in renderers)
            sr.flipX = false;
    }

    private void StopOwnerMovement()
    {
        if (player == null) return;

        Rigidbody2D ownerRb = player.GetComponent<Rigidbody2D>();
        if (ownerRb != null)
            ownerRb.velocity = Vector2.zero;
    }

    private bool TryPayEnergy()
    {
        if (energyCost <= 0f)
            return true;

        if (character == null)
            character = player != null ? player : GetComponentInParent<CharacterBase>();

        if (character == null)
        {
            Debug.LogWarning("[PiercingShot] Energy owner tidak ditemukan. Cast dibatalkan.");
            return false;
        }

        if (!character.TrySpendEnergy(energyCost))
        {
            Debug.LogWarning($"[PiercingShot] Energy kurang. Butuh {energyCost} energy.");
            return false;
        }

        return true;
    }

    private void OnDisable()
    {
        if (player != null)
            player.lockMovement = false;

        isCasting = false;
        waitingAnimationRelease = false;
    }
}