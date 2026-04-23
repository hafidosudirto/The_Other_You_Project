using System.Collections;
using UnityEngine;

public class Enemy_Bow_PiercingShot : MonoBehaviour, ISkill
{
    [Header("BT / Range")]
    public float minRange = 2.5f;
    public float skillRange = 7f;
    public float rangeTolerance = 0.2f;

    [Header("References")]
    public GameObject arrowPrefab;
    public Transform arrowSpawnPoint;
    public SpriteRenderer facingSprite;

    [Header("Shot Settings")]
    public float shootCooldown = 0.8f;
    public float shootSpeed = 14f;
    public float destroyAfter = 2.5f;

    [Header("Damage")]
    public float damage = 10f;

    [Header("Timing")]
    public float postShotLock = 0.08f;
    public float releaseTimeout = 0.65f;

    public bool IsActive => isCasting;

    private EnemyAI ai;
    private EnemyCombatController combat;
    private EnemyAnimation enemyAnim;
    private CharacterBase owner;
    private Rigidbody2D ownerRb;

    private bool isCasting;
    private bool waitingAnimationRelease;
    private bool skillLockClaimed;
    private float lastShootTime = -999f;

    private void Awake()
    {
        ai = GetComponentInParent<EnemyAI>();
        combat = GetComponentInParent<EnemyCombatController>();
        owner = GetComponentInParent<CharacterBase>();
        ownerRb = GetComponentInParent<Rigidbody2D>();
        if (ai != null)
        {
            enemyAnim = ai.GetComponentInChildren<EnemyAnimation>(true);
        }

            if (arrowSpawnPoint == null)
        {
            Transform found = FindChildRecursive(transform.root, "ArrowSpawnPoint");
            if (found == null)
                found = FindChildRecursive(transform.root, "FirePoint");

            arrowSpawnPoint = found;
        }

        if (facingSprite == null && ai != null)
            facingSprite = ai.GetComponentInChildren<SpriteRenderer>(true);
    }

    public bool IsInRange(float distanceToPlayer)
    {
        return distanceToPlayer >= (minRange - rangeTolerance) &&
               distanceToPlayer <= (skillRange + rangeTolerance);
    }

    public bool CanTrigger(float distanceToPlayer)
    {
        if (isCasting) return false;
        if (Time.time < lastShootTime + shootCooldown) return false;
        if (combat != null && combat.IsBusy) return false;
        if (!IsInRange(distanceToPlayer)) return false;
        if (arrowPrefab == null || arrowSpawnPoint == null) return false;
        return true;
    }

    public void Trigger()
    {
        float distance = GetDistanceToPlayer();
        if (!CanTrigger(distance))
            return;

        StartCoroutine(CastRoutine());
    }

    public void TriggerSkill(int slot)
    {
        Trigger();
    }

    private IEnumerator CastRoutine()
    {
        lastShootTime = Time.time;
        isCasting = true;
        waitingAnimationRelease = true;
        skillLockClaimed = true;

        combat?.InvokeSkillStart();
        StopOwnerMovement();

        if (enemyAnim != null)
            enemyAnim.PlayPiercingShot();

        float timer = 0f;
        while (waitingAnimationRelease && timer < releaseTimeout)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (waitingAnimationRelease)
        {
            waitingAnimationRelease = false;
            FireArrow();
        }

        yield return new WaitForSeconds(postShotLock);

        if (skillLockClaimed)
        {
            combat?.InvokeSkillEnd();
            skillLockClaimed = false;
        }

        isCasting = false;
    }

    public void ReleaseFromAnimationEvent()
    {
        if (!isCasting || !waitingAnimationRelease)
            return;

        waitingAnimationRelease = false;
        FireArrow();
    }

    private void FireArrow()
    {
        if (arrowPrefab == null || arrowSpawnPoint == null)
            return;

        GameObject arrowObj = Instantiate(
            arrowPrefab,
            arrowSpawnPoint.position,
            Quaternion.identity
        );

        if (arrowObj == null)
            return;

        float direction = GetFacingDirection();
        ApplyArrowFacing(arrowObj, direction);

        Rigidbody2D rb = arrowObj.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.velocity = new Vector2(direction * shootSpeed, 0f);

        ArrowDamage dmg = arrowObj.GetComponent<ArrowDamage>();
        if (dmg != null)
        {
            dmg.SetOwner(owner);
            dmg.SetStats(damage, 0f, 0f, true, false);
        }

        Destroy(arrowObj, destroyAfter);
    }

    private float GetDistanceToPlayer()
    {
        if (ai == null || ai.playerTransform == null)
            return Mathf.Infinity;

        return Vector2.Distance(transform.position, ai.playerTransform.position);
    }

    private float GetFacingDirection()
    {
        if (ai != null)
            return ai.IsFacingRight ? 1f : -1f;

        if (owner != null)
            return owner.isFacingRight ? 1f : -1f;

        if (facingSprite != null)
            return facingSprite.flipX ? -1f : 1f;

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
        if (ownerRb != null)
            ownerRb.velocity = Vector2.zero;
    }

    private Transform FindChildRecursive(Transform root, string targetName)
    {
        if (root == null) return null;
        if (root.name == targetName) return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildRecursive(root.GetChild(i), targetName);
            if (found != null)
                return found;
        }

        return null;
    }

    private void OnDisable()
    {
        waitingAnimationRelease = false;

        if (skillLockClaimed)
        {
            combat?.InvokeSkillEnd();
            skillLockClaimed = false;
        }

        isCasting = false;
    }
}