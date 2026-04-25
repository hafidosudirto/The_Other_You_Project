using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class Bow_ConcussiveShot : MonoBehaviour, ISkill
{
    [Header("Toggle Skill Mode")]
    [Tooltip("OFF = langsung hit area. ON = panah visual -> hit area.")]
    public bool useArrowVisual = true;

    [Header("References")]
    public Transform firePoint;
    public GameObject arrowPrefab;
    public GameObject hitAreaPrefab;
    public Player player;
    public PlayerAnimation anim;

    [Header("Explosion Settings")]
    public float damage = 10f;
    public float knockback = 6f;
    public float stun = 0.35f;
    public float explosionRadius = 1.4f;

    [Header("Smooth Mini-Jump")]
    public float jumpHeight = 2f;
    public float jumpUpTime = 0.5f;
    public float hoverTime = 0.2f;
    public float jumpDownTime = 0.3f;

    [Header("Delays")]
    [Tooltip("Jeda minimum sebelum release boleh dieksekusi.")]
    public float delayBeforeShoot = 0.05f;

    [Tooltip("Berapa lama sesudah panah keluar karakter masih tertahan sebelum turun.")]
    public float delayBeforeFall = 0.05f;

    [Header("Safety")]
    [Tooltip("Kalau event animasi tidak terpanggil, skill tidak akan freeze selamanya.")]
    public float releaseEventTimeout = 1.0f;

    [Header("Hit Area Offset (Mode A fallback)")]
    public Vector2 hitAreaOffset = new Vector2(1.2f, -0.2f);

    [Header("Ground Mask")]
    public LayerMask groundMask;

    [Header("Camera Bump")]
    public bool enableCameraBump = true;
    public float cameraBumpStrength = 0.1f;
    public float cameraBumpDuration = 0.12f;

    private Coroutine jumpRoutine;
    private Coroutine camRoutine;

    private bool isCasting;
    private bool releaseExecuted;
    private Transform jumpTarget;

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

        if (isCasting || player.lockMovement)
            return;

        isCasting = true;
        releaseExecuted = false;
        jumpTarget = player.transform;

        player.lockMovement = true;
        StopOwnerMovement();

        if (jumpRoutine != null)
            StopCoroutine(jumpRoutine);

        jumpRoutine = StartCoroutine(JumpShotRoutine(jumpTarget));

        if (enableCameraBump)
        {
            if (camRoutine != null)
                StopCoroutine(camRoutine);

            camRoutine = StartCoroutine(CameraBumpRoutine());
        }

        if (anim != null)
            anim.PlayConcussiveShot();
    }

    private IEnumerator JumpShotRoutine(Transform target)
    {
        float peak = jumpHeight;
        float lastOffset = 0f;

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

        if (hoverTime > 0f)
            yield return new WaitForSeconds(hoverTime);

        ExecuteRelease();

        if (delayBeforeFall > 0f)
            yield return new WaitForSeconds(delayBeforeFall);

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

        if (player != null)
            player.lockMovement = false;

        isCasting = false;
        jumpRoutine = null;
    }

    public void ReleaseFromAnimationEvent()
    {
        if (!isCasting)
            return;
    }

    private void ExecuteRelease()
    {
        if (releaseExecuted)
            return;

        releaseExecuted = true;

        if (!useArrowVisual)
            SpawnHitAreaDirect_GroundRaycast();
        else
            FireVisualArrow();
    }

    private void SpawnHitAreaDirect_GroundRaycast()
    {
        float dir = player.isFacingRight ? 1f : -1f;

        Vector2 rayOrigin = firePoint.position + new Vector3(0.6f * dir, 0f);
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, 10f, groundMask);

        Vector3 spawnPos;

        if (hit.collider != null)
            spawnPos = hit.point;
        else
            spawnPos = firePoint.position + new Vector3(hitAreaOffset.x * dir, hitAreaOffset.y, 0f);

        GameObject area = Instantiate(hitAreaPrefab, spawnPos, Quaternion.identity);
        if (area == null)
        {
            Debug.LogWarning("[Concussive] Failed to instantiate hit area.");
            return;
        }

        ConcussiveHitArea ch = area.GetComponent<ConcussiveHitArea>();
        if (ch != null)
            ch.Setup(player, damage, knockback, stun, explosionRadius);

        NotifyDataTrackerConcussiveShot();
    }

    private void FireVisualArrow()
    {
        float dir = player.isFacingRight ? 1f : -1f;

        GameObject arrow = Instantiate(arrowPrefab, firePoint.position, Quaternion.identity);
        if (arrow == null)
        {
            Debug.LogWarning("[Concussive] Failed to instantiate arrow visual.");
            return;
        }

        ConcussiveArrowVisual visual = arrow.GetComponent<ConcussiveArrowVisual>();
        if (visual != null)
        {
            visual.Init(player, dir, damage, knockback, stun, explosionRadius);
            NotifyDataTrackerConcussiveShot();
        }
        else
        {
            Debug.LogWarning("[Concussive] Spawned arrow has no ConcussiveArrowVisual component.");
        }
    }

    private void NotifyDataTrackerConcussiveShot()
    {
        DataTracker tracker = DataTracker.Instance;
        if (tracker == null)
            return;

        var method = tracker.GetType().GetMethod("RecordBowConcussiveShot");
        if (method != null)
        {
            method.Invoke(tracker, null);
            return;
        }

        tracker.RecordAction(PlayerActionType.Defensive, WeaponType.Bow);
    }

    private void StopOwnerMovement()
    {
        if (player == null) return;

        Rigidbody2D ownerRb = player.GetComponent<Rigidbody2D>();
        if (ownerRb != null)
            ownerRb.velocity = Vector2.zero;
    }

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

            ct.position = startPos + new Vector3(0f, offset * cameraBumpStrength, 0f);
            yield return null;
        }

        ct.position = startPos;
    }

    private void OnDisable()
    {
        if (player != null)
            player.lockMovement = false;

        isCasting = false;
        releaseExecuted = false;
        jumpRoutine = null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (firePoint == null) return;

        float dir = (player != null && player.isFacingRight) ? 1f : -1f;

        Gizmos.color = Color.yellow;
        Vector3 start = firePoint.position;
        Vector3 top = firePoint.position + new Vector3(0f, jumpHeight, 0f);
        Vector3 end = firePoint.position;

        Gizmos.DrawLine(start, top);
        Gizmos.DrawLine(top, end);

        Vector3 rayOrigin = firePoint.position + new Vector3(0.6f * dir, 0f, 0f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(rayOrigin, 0.06f);
        Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * 10f);

        Vector3 predicted = firePoint.position + new Vector3(hitAreaOffset.x * dir, hitAreaOffset.y, 0f);
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.75f);
        Gizmos.DrawWireSphere(predicted, explosionRadius);
    }
#endif
}