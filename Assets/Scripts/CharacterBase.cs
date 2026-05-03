using UnityEngine;
using System;
using System.Collections;

public class CharacterBase : MonoBehaviour
{
    [Header("Character Stats")]
    public float maxHP = 1000f;
    public float currentHP = 1000f;

    public float attack = 10f;
    public float defense = 5f;
    public float moveSpeed = 5f;

    [Header("Energy (Character-Level)")]
    [SerializeField, Min(0f)] private float maxEnergy = 100f;
    [SerializeField, Min(0f)] private float currentEnergy = 100f;

    [Header("Energy Regen (Time-Based)")]
    [SerializeField, Min(0f)] private float regenPerSecond = 8f;

    [Tooltip("Jika true, regen memakai unscaled time.")]
    [SerializeField] private bool useUnscaledTime = false;

    [Tooltip("Jika aktif, energi tidak akan regen. Dipakai saat skill seperti Bow Full Draw sedang charge/release.")]
    [SerializeField] private bool energyRegenBlocked = false;

    public event Action OnEnergyChanged;

    public float MaxEnergy => maxEnergy;
    public float CurrentEnergy => currentEnergy;
    public bool EnergyRegenBlocked => energyRegenBlocked;

    public bool HasEnergy(float cost)
    {
        return cost <= 0f || CurrentEnergy + 1e-6f >= cost;
    }

    public float EnergyNormalized
    {
        get
        {
            if (maxEnergy <= 0f)
                return 0f;

            return Mathf.Clamp01(currentEnergy / maxEnergy);
        }
    }

    [Header("Status Flags")]
    public bool isFacingRight = true;
    public bool isStaggered = false;

    [Header("Riposte")]
    public bool isRiposteStance = false;
    public bool canRiposte = true;
    public float riposteWindow = 0.6f;

    [Header("Stagger Settings")]
    public float staggerResistance = 1f;

    protected Rigidbody2D rb;

    private Coroutine stunRoutine;
    private Coroutine staggerRoutine;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
        }

        currentHP = maxHP;

        maxEnergy = Mathf.Max(0f, maxEnergy);
        currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);

        OnEnergyChanged?.Invoke();
    }

    protected virtual void Update()
    {
        TickEnergyRegen();
    }

    private void TickEnergyRegen()
    {
        if (energyRegenBlocked)
            return;

        if (regenPerSecond <= 0f)
            return;

        if (currentEnergy >= maxEnergy)
            return;

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        if (dt <= 0f)
            return;

        AddEnergy(regenPerSecond * dt);
    }

    public void SetEnergyRegenBlocked(bool blocked)
    {
        energyRegenBlocked = blocked;
    }

    public void StopEnergyRegen()
    {
        SetEnergyRegenBlocked(true);
    }

    public void StartEnergyRegen()
    {
        SetEnergyRegenBlocked(false);
    }

    public void StopRegenerasiEnergi()
    {
        SetEnergyRegenBlocked(true);
    }

    public void StartRegenerasiEnergi()
    {
        SetEnergyRegenBlocked(false);
    }

    public void SetEnergy(float value)
    {
        float prev = currentEnergy;
        currentEnergy = Mathf.Clamp(value, 0f, maxEnergy);

        if (!Mathf.Approximately(prev, currentEnergy))
            OnEnergyChanged?.Invoke();
    }

    public void AddEnergy(float amount)
    {
        if (amount <= 0f)
            return;

        float prev = currentEnergy;
        currentEnergy = Mathf.Clamp(currentEnergy + amount, 0f, maxEnergy);

        if (!Mathf.Approximately(prev, currentEnergy))
            OnEnergyChanged?.Invoke();
    }

    public bool TrySpendEnergy(float cost)
    {
        if (cost <= 0f)
            return true;

        if (currentEnergy + 1e-6f < cost)
            return false;

        currentEnergy = Mathf.Clamp(currentEnergy - cost, 0f, maxEnergy);
        OnEnergyChanged?.Invoke();

        return true;
    }

    public void Flip()
    {
        isFacingRight = !isFacingRight;

        Vector3 scale = transform.localScale;
        scale.x *= -1f;
        transform.localScale = scale;
    }

    public bool CanAct()
    {
        return !isStaggered && currentHP > 0f;
    }

    public virtual void TakeDamage(float dmg, GameObject attacker = null)
    {
        if (currentHP <= 0f)
            return;

        Enemy_Sword_Riposte enemyRiposte = GetComponentInChildren<Enemy_Sword_Riposte>(true);

        if (enemyRiposte != null && enemyRiposte.IsStanceActive)
        {
            enemyRiposte.NotifyPlayerAttackAttempt(attacker);
            return;
        }

        if (isRiposteStance)
        {
            Parry(attacker);
            return;
        }

        float finalDamage = Mathf.Max(1f, dmg - defense);
        currentHP -= finalDamage;

        if (currentHP <= 0f)
            Die();
    }

    private void Parry(GameObject attacker)
    {
        if (DataTracker.Instance != null)
            DataTracker.Instance.RecordAction(PlayerActionType.Defensive, WeaponType.Sword);

        Sword_Riposte riposte = GetComponentInChildren<Sword_Riposte>(true);

        if (riposte != null)
            riposte.TriggerFollowUpDash();

        isRiposteStance = false;

        if (attacker != null)
        {
            CharacterBase enemy = attacker.GetComponent<CharacterBase>();

            if (enemy != null)
            {
                Vector2 dir = (enemy.transform.position - transform.position).normalized;
                enemy.ApplyStagger(dir, 4f, 0.2f);
            }
        }
    }

    public void ActivateRiposte()
    {
        if (!canRiposte)
            return;

        isRiposteStance = true;
        canRiposte = false;
    }

    public void EndRiposteStance()
    {
        isRiposteStance = false;
        StartCoroutine(RiposteCooldown());
    }

    private IEnumerator RiposteCooldown()
    {
        yield return new WaitForSeconds(1f);
        canRiposte = true;
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        if (force <= 0f || currentHP <= 0f)
            return;

        float knockDuration = 0.15f;
        ApplyStagger(direction, force, knockDuration);
    }

    public void ApplyStun(float duration)
    {
        if (duration <= 0f || currentHP <= 0f)
            return;

        if (stunRoutine != null)
            StopCoroutine(stunRoutine);

        stunRoutine = StartCoroutine(ApplyStunRoutine(duration));
    }

    private IEnumerator ApplyStunRoutine(float duration)
    {
        isStaggered = true;

        yield return new WaitForSeconds(duration);

        isStaggered = false;
        stunRoutine = null;
    }

    public void ApplyStagger(Vector2 direction, float force, float duration)
    {
        if (currentHP <= 0f)
            return;

        if (staggerRoutine != null)
            StopCoroutine(staggerRoutine);

        staggerRoutine = StartCoroutine(StaggerRoutine(direction, force, duration));
    }

    private IEnumerator StaggerRoutine(Vector2 dir, float force, float duration)
    {
        isStaggered = true;

        if (rb != null && rb.bodyType == RigidbodyType2D.Dynamic)
        {
            rb.AddForce(dir * force, ForceMode2D.Impulse);
        }
        else
        {
            yield return StartCoroutine(KinematicKnockback(dir, force, duration));
        }

        yield return new WaitForSeconds(duration);

        isStaggered = false;
        staggerRoutine = null;
    }

    private IEnumerator KinematicKnockback(Vector2 dir, float force, float time)
    {
        float timer = time;
        float speed = force;

        while (timer > 0f)
        {
            timer -= Time.deltaTime;
            transform.position += (Vector3)(dir * speed * Time.deltaTime);
            yield return null;
        }
    }

    public virtual void Die()
    {
        Debug.Log($"{name} MATI");

        EnemyDeathHandler death = GetComponent<EnemyDeathHandler>();

        if (death != null)
            death.HandleDeath();

        Destroy(gameObject);
    }
}