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

    private bool isOnCooldown;
    public float cooldown = 0.2f;

    public void TriggerSkill(int slotID)
    {
         if (isOnCooldown) return;
        StartCoroutine(Cooldown());

        DebugHub.Skill("CAST Quick Shot");

        if (anim != null)
            anim.PlayQuickShot();

        ShootArrow();
    }

        IEnumerator Cooldown()
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

        // Flip sprite only
        if (!player.isFacingRight)
            sr.flipX = true;

        // Color
        if (sr != null) sr.color = arrowColor;

        // Damage
        if (dmg != null)
        {
            dmg.owner = player;
            dmg.SetStats(quickDamage, knockback, stun, false, false);
        }

        StartCoroutine(ArrowRoutine(rb, arrowObj));
    }

    private IEnumerator ArrowRoutine(Rigidbody2D rb, GameObject arrowObj)
    {
        float timer = 0f;
        float dir = player.isFacingRight ? 1 : -1;

        // --------------------
        // STRAIGHT PHASE
        // --------------------
        while (timer < straightTime)
        {
            if (rb == null) yield break;

            rb.velocity = new Vector2(dir * speed, 0f);

            timer += Time.deltaTime;
            yield return null;
        }

        // --------------------
        // CURVE PHASE
        // --------------------
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

        // STOP movement before destroy to avoid errors
        if (rb != null)
            rb.velocity = Vector2.zero;

        // --------------------
        // DELAYED DESTROY
        // --------------------
        yield return new WaitForSeconds(destroyDelay);

        if (arrowObj != null)
            Destroy(arrowObj);
    }

    private void OnDrawGizmosSelected()
    {
        if (firePoint == null) return;

        Gizmos.color = arrowColor;

        float dir = (player != null && !player.isFacingRight) ? -1f : 1f;

        Vector3 start = firePoint.position;
        Vector3 straightEnd = start + Vector3.right * dir * speed * straightTime * 0.35f;
        Gizmos.DrawLine(start, straightEnd);

        Vector3 curvePeak = straightEnd + new Vector3(dir * 0.5f, 0.8f, 0);
        Gizmos.DrawLine(straightEnd, curvePeak);

        Vector3 curveEnd = curvePeak + new Vector3(dir * 0.5f, -1.2f, 0);
        Gizmos.DrawLine(curvePeak, curveEnd);
    }
}
