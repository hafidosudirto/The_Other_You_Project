using UnityEngine;
using System.Collections;

public class Bow_QuickShot : MonoBehaviour, ISkill
{
    [Header("References")]
    public Transform firePoint;
    public GameObject arrowPrefab;
    public Player player;
    public PlayerAnimation anim;

    [Header("Arrow Settings")]
    public float speed = 10f;
    public float lifeTime = 1.2f;
    public Color arrowColor = Color.yellow;

    [Header("Hit Effects")]
    public float quickDamage = 1f;
    public float knockback = 1.5f;
    public float stun = 0.1f;

    [Header("Flight Curve")]
    public float straightTime = 0.45f;
    public float gravityStart = 3f;
    public float gravityEnd = 12f;

    [Header("Cleanup")]
    public float destroyDelay = 0.25f;

    [Header("Timing")]
    public float cooldown = 0.35f;
    public float postShotLock = 0.05f;

    private bool isOnCooldown;
    private bool isCasting;
    private bool waitingAnimationRelease;

    public void TriggerSkill(int slotID)
    {
        if (isOnCooldown || isCasting)
            return;

        if (player == null || firePoint == null || arrowPrefab == null)
        {
            Debug.LogWarning("[QuickShot] Missing player / firePoint / arrowPrefab.");
            return;
        }

        if (player.lockMovement)
            return;

        StartCoroutine(CooldownRoutine());

        isCasting = true;
        waitingAnimationRelease = true;

        player.lockMovement = true;
        StopOwnerMovement();

        DebugHub.Skill("CAST Quick Shot");

        if (anim != null)
            anim.PlayQuickShot();
    }

    public void ReleaseFromAnimationEvent()
    {
        if (!isCasting || !waitingAnimationRelease)
            return;

        waitingAnimationRelease = false;
        ShootArrow();
        StartCoroutine(EndCastRoutine());
    }

    private IEnumerator EndCastRoutine()
    {
        yield return new WaitForSeconds(postShotLock);

        if (player != null)
            player.lockMovement = false;

        isCasting = false;
    }

    private IEnumerator CooldownRoutine()
    {
        isOnCooldown = true;
        yield return new WaitForSeconds(cooldown);
        isOnCooldown = false;
    }

    private void ShootArrow()
    {
        GameObject arrowObj = Instantiate(arrowPrefab, firePoint.position, Quaternion.identity);

        Rigidbody2D rb = arrowObj.GetComponent<Rigidbody2D>();
        SpriteRenderer sr = arrowObj.GetComponent<SpriteRenderer>();
        ArrowDamage dmg = arrowObj.GetComponent<ArrowDamage>();

        float dir = player.isFacingRight ? 1f : -1f;

        if (sr != null)
        {
            sr.flipX = dir < 0f;
            sr.color = arrowColor;
        }

        if (dmg != null)
        {
            dmg.owner = player;
            dmg.SetStats(quickDamage, knockback, stun, false, false);
        }

        StartCoroutine(ArrowRoutine(rb, arrowObj, dir));
    }

    private IEnumerator ArrowRoutine(Rigidbody2D rb, GameObject arrowObj, float dir)
    {
        float timer = 0f;

        while (timer < straightTime)
        {
            if (rb == null) yield break;

            rb.velocity = new Vector2(dir * speed, 0f);

            timer += Time.deltaTime;
            yield return null;
        }

        float t = 0f;
        while (t < 1f)
        {
            if (rb == null) yield break;

            float g = Mathf.Lerp(gravityStart, gravityEnd, t);

            rb.velocity = new Vector2(
                dir * speed,
                rb.velocity.y - g * Time.deltaTime
            );

            t += Time.deltaTime * 1.5f;
            yield return null;
        }

        if (rb != null)
            rb.velocity = Vector2.zero;

        yield return new WaitForSeconds(destroyDelay);

        if (arrowObj != null)
            Destroy(arrowObj);
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
        isOnCooldown = false;
    }
}