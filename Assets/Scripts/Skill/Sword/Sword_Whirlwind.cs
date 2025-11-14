using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Sword_Whirlwind : MonoBehaviour, ISkill
{
    [Header("Whirlwind Settings")]
    public float radius = 2f;
    public float duration = 1.5f;
    public float damageMultiplier = 1.2f;
    public float hitInterval = 0.3f;
    public float speedMultiplierWhileActive = 0.5f;

    [Header("Stagger")]
    public float staggerForce = 2f;
    public float staggerDuration = 0.4f;
    public float staggerInterval = 0.8f;

    private Player player;
    private SkillBase skillBase;

    private bool isActive = false;
    private float tDuration;
    private float tHit;

    private float originalSpeed;

    private Dictionary<CharacterBase, float> staggerTimers = new Dictionary<CharacterBase, float>();

    void Awake()
    {
        skillBase = GetComponentInParent<SkillBase>();
        player = GetComponentInParent<Player>();
    }

    public void TriggerSkill()
    {
        if (!isActive && player != null)
        {
            StartCoroutine(WhirlwindRoutine());
        }
    }

    private IEnumerator WhirlwindRoutine()
    {
        isActive = true;

        tDuration = duration;
        tHit = 0f;

        originalSpeed = player.moveSpeed;
        player.moveSpeed *= speedMultiplierWhileActive;

        Debug.Log("Whirlwind aktif");

        while (tDuration > 0f)
        {
            tDuration -= Time.deltaTime;
            tHit -= Time.deltaTime;

            if (tHit <= 0f)
            {
                ApplyWhirlwindDamage();
                tHit = hitInterval;
            }

            UpdateStaggerTimers();

            yield return null;
        }

        // Reset movement dan status akhir
        player.moveSpeed = originalSpeed;
        isActive = false;

        Debug.Log("Whirlwind selesai");

        // LEPAS LOCK SKILL DI PALING AKHIR
        if (skillBase != null)
            skillBase.ReleaseLock();
    }

    private void ApplyWhirlwindDamage()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(player.transform.position, radius);

        foreach (Collider2D h in hits)
        {
            CharacterBase target = h.GetComponent<CharacterBase>();
            if (target == null || target == player)
                continue;

            // Damage
            float dmg = player.attack * damageMultiplier;
            target.TakeDamage(dmg);

            // Stagger
            if (CanStagger(target))
            {
                Vector2 dir = (target.transform.position - player.transform.position).normalized;
                target.ApplyStagger(dir, staggerForce, staggerDuration);
                staggerTimers[target] = staggerInterval;
            }
        }
    }

    private void UpdateStaggerTimers()
    {
        var keys = new List<CharacterBase>(staggerTimers.Keys);

        foreach (CharacterBase cb in keys)
        {
            staggerTimers[cb] -= Time.deltaTime;
            if (staggerTimers[cb] <= 0f)
                staggerTimers.Remove(cb);
        }
    }

    private bool CanStagger(CharacterBase target)
    {
        return staggerTimers.ContainsKey(target) == false;
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !isActive)
            return;

        if (player == null)
            player = GetComponentInParent<Player>();

        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.4f);
        Gizmos.DrawWireSphere(player.transform.position, radius);
    }
#endif
}
