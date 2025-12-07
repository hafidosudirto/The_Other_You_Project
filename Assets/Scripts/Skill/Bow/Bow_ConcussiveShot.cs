using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Bow_ConcussiveShot : MonoBehaviour, ISkill
{
    [Header("Toggle Skill Mode")]
    [Tooltip("OFF = langsung hit area (kayak sword). ON = panah visual → hit area.")]
    public bool useArrowVisual = false;

    [Header("References")]
    public Transform firePoint;
    [Tooltip("Isi dengan prefab Visual_Arrow.")]
    public GameObject arrowPrefab;          // Visual_Arrow
    public GameObject hitAreaPrefab;        // HitArea_Concussive
    public Player player;
    public PlayerAnimation anim;

    [Header("Explosion Settings")]
    public float damage = 10f;
    public float knockback = 6f;
    public float stun = 0.35f;
    [Tooltip("Radius ledakan (dipakai untuk HitArea_Concussive.Setup).")]
    public float explosionRadius = 1.4f;

    [Header("Smooth Mini-Jump")]
    public float jumpHeight = 2f;
    public float jumpUpTime = 0.5f;
    public float hoverTime = 0.3f;
    public float jumpDownTime = 0.3f;

    [Header("Delays")]
    public float delayBeforeShoot = 0.15f;
    public float delayBeforeFall = 0.1f;

    [Header("Hit Area Offset (Mode A fallback)")]
    [Tooltip("Dipakai kalau raycast ke ground tidak kena apa-apa.")]
    public Vector2 hitAreaOffset = new Vector2(1.2f, -0.2f);

    [Header("Ground Mask")]
    public LayerMask groundMask;

    [Header("Camera Bump")]
    public bool enableCameraBump = true;
    public float cameraBumpStrength = 0.1f;
    public float cameraBumpDuration = 0.12f;

    private Coroutine jumpRoutine;
    private Coroutine camRoutine;
    private bool isCasting; // anti-spam

    // ============================================================
    //  TRIGGER SKILL
    // ============================================================
    public void TriggerSkill(int slot)
    {
        if (player == null || firePoint == null)
        {
            Debug.LogWarning("[Concussive] Missing player/firePoint.");
            return;
        }

        if (hitAreaPrefab == null)
        {
            Debug.LogWarning("[Concussive] HitArea prefab missing.");
            return;
        }

        if (useArrowVisual && arrowPrefab == null)
        {
            Debug.LogWarning("[Concussive] Arrow prefab missing but useArrowVisual = true.");
            return;
        }

        if (isCasting) return;
        isCasting = true;

        if (jumpRoutine != null)
            StopCoroutine(jumpRoutine);

        jumpRoutine = StartCoroutine(JumpShotRoutine(player.transform));

        if (enableCameraBump)
        {
            if (camRoutine != null)
                StopCoroutine(camRoutine);

            camRoutine = StartCoroutine(CameraBumpRoutine());
        }

        if (anim != null)
            anim.PlayQuickShot();
    }

    // ============================================================
    //  JUMP → HOVER → (MODE A / MODE B) → FALL
    // ============================================================
    private IEnumerator JumpShotRoutine(Transform target)
    {
        player.lockMovement = true;

        float peak = jumpHeight;
        float lastOffset = 0f;

        // -------- RISE --------
        float t = 0f;
        while (t < jumpUpTime)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / jumpUpTime);
            float eased = Mathf.Sin(n * Mathf.PI * 0.5f);

            float newOffset = Mathf.Lerp(0f, peak, eased);
            float delta = newOffset - lastOffset;
            target.Translate(0f, delta, 0f);
            lastOffset = newOffset;
            yield return null;
        }

        // -------- HOVER --------
        if (hoverTime > 0f)
            yield return new WaitForSeconds(hoverTime);

        // -------- DELAY BEFORE FIRE --------
        if (delayBeforeShoot > 0f)
            yield return new WaitForSeconds(delayBeforeShoot);

        // ========================================================
        // MODE A — NO ARROW → HIT AREA DROP TO GROUND
        // ========================================================
        if (!useArrowVisual)
        {
            SpawnHitAreaDirect_GroundRaycast();
        }
        // ========================================================
        // MODE B — FIRE VISUAL ARROW
        // ========================================================
        else
        {
            FireVisualArrow();
        }

        // -------- DELAY BEFORE FALL --------
        if (delayBeforeFall > 0f)
            yield return new WaitForSeconds(delayBeforeFall);

        // -------- FALL --------
        t = 0f;
        while (t < jumpDownTime)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / jumpDownTime);
            float eased = 1f - Mathf.Cos(n * Mathf.PI * 0.5f);

            float newOffset = Mathf.Lerp(peak, 0f, eased);
            float delta = newOffset - lastOffset;
            target.Translate(0f, delta, 0f);
            lastOffset = newOffset;

            yield return null;
        }
        player.lockMovement = false;

        isCasting = false;
        jumpRoutine = null;
    }

    // ============================================================
    // MODE A — AUTO GROUND HIT AREA (NO ARROW)
    // ============================================================
    private void SpawnHitAreaDirect_GroundRaycast()
    {
        float dir = player.isFacingRight ? 1f : -1f;

        Vector2 rayOrigin = firePoint.position + new Vector3(0.6f * dir, 0f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 10f, groundMask);

        Vector3 spawnPos;

        if (hit.collider != null)
            spawnPos = hit.point;
        else
            spawnPos = firePoint.position + new Vector3(hitAreaOffset.x * dir, hitAreaOffset.y);

        GameObject area = Instantiate(hitAreaPrefab, spawnPos, Quaternion.identity);

        ConcussiveHitArea ch = area.GetComponent<ConcussiveHitArea>();
        if (ch != null)
            ch.Setup(player, damage, knockback, stun, explosionRadius);

        Debug.Log("[Concussive] MODE A Spawn at: " + spawnPos);
    }

    // ============================================================
    // MODE B — FIRE ARROW VISUAL
    // ============================================================
    private void FireVisualArrow()
    {
        float dir = player.isFacingRight ? 1f : -1f;

        GameObject arrow = Instantiate(arrowPrefab, firePoint.position, Quaternion.identity);

        ConcussiveArrowVisual visual = arrow.GetComponent<ConcussiveArrowVisual>();
        if (visual != null)
        {
            visual.Init(player, dir, damage, knockback, stun, explosionRadius);
        }
    }

    // ============================================================
    // CAMERA BUMP
    // ============================================================
    private IEnumerator CameraBumpRoutine()
    {
        Camera cam = Camera.main;
        if (cam == null) yield break;

        Transform ct = cam.transform;
        Vector3 startPos = ct.position;

        float t = 0f;
        while (t < cameraBumpDuration)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / cameraBumpDuration);
            float offset = Mathf.Sin(n * Mathf.PI);

            ct.position = startPos + new Vector3(0, offset * cameraBumpStrength, 0);
            yield return null;
        }

        ct.position = startPos;
    }

    // ============================================================
    // GIZMO PREVIEW (Jump Arc + Ground Raycast + Explosion)
    // ============================================================
#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (firePoint == null) return;

        Handles.color = Color.green;
        Handles.Label(firePoint.position + Vector3.up * 0.5f, "FirePoint");

        float dir = (player != null && player.isFacingRight) ? 1f : -1f;

        // Jump Arc Preview
        Gizmos.color = Color.yellow;
        Vector3 start = firePoint.position;
        Vector3 top = firePoint.position + new Vector3(0, jumpHeight, 0);
        Vector3 end = firePoint.position;

        Gizmos.DrawLine(start, top);
        Gizmos.DrawLine(top, end);
        Handles.Label(top, "Jump Peak");

        // Ground Raycast Preview (Mode A)
        Vector3 rayOrigin = firePoint.position + new Vector3(0.6f * dir, 0f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(rayOrigin, 0.08f);
        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * 10f);
        Handles.Label(rayOrigin + Vector3.right * 0.2f, "Raycast Down");

        // Explosion Preview (fallback)
        Vector3 predicted = firePoint.position + new Vector3(hitAreaOffset.x * dir, hitAreaOffset.y, 0);

        Gizmos.color = new Color(1f, 0.4f, 0f, 0.6f);
        Gizmos.DrawWireSphere(predicted, explosionRadius);
        Handles.Label(predicted + Vector3.up * 0.2f, "Predicted Explosion (Fallback)");
    }
#endif
}
