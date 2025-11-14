using UnityEngine;
using System.Collections;

public class CharacterBase : MonoBehaviour
{
    [Header("Character Stats")]
    public float maxHP = 100f;
    public float currentHP = 100f;

    public float attack = 10f;
    public float defense = 5f;
    public float moveSpeed = 5f;

    [Header("Status Flags")]
    public bool isFacingRight = true;
    public bool isStaggered = false;

    // Riposte state
    public bool isRiposteStance = false;
    public bool canRiposte = true;
    public float riposteWindow = 0.6f;

    [Header("Stagger Settings")]
    public float staggerResistance = 1f;

    protected Rigidbody2D rb;

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.gravityScale = 0;
            rb.freezeRotation = true;
        }

        currentHP = maxHP;
    }

    // -------------------------------------------------------------
    // Flip mengatur arah hadap karakter (Player & Enemy)
    // -------------------------------------------------------------
    public void Flip()
    {
        isFacingRight = !isFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1f;
        transform.localScale = scale;
    }

    // -------------------------------------------------------------
    // Mengecek apakah karakter bisa bertindak (tidak stagger, tidak mati)
    // -------------------------------------------------------------
    public bool CanAct()
    {
        return !isStaggered && currentHP > 0;
    }

    // -------------------------------------------------------------
    // Fungsi utama untuk menerima damage
    // attacker = musuh (enemy atau player)
    // -------------------------------------------------------------
    public virtual void TakeDamage(float dmg, GameObject attacker = null)
    {
        if (currentHP <= 0)
            return;

        // Jika sedang stance riposte → parry sukses
        if (isRiposteStance)
        {
            Parry(attacker);
            return;
        }

        float finalDamage = Mathf.Max(1f, dmg - defense);
        currentHP -= finalDamage;

        Debug.Log($"{name} CurrentHP: {currentHP}");

        if (currentHP <= 0)
        {
            Die();
        }
    }

    // -------------------------------------------------------------
    // Parry logic → memanggil follow-up dash dari Sword_Riposte
    // -------------------------------------------------------------
    private void Parry(GameObject attacker)
    {
        Debug.Log($"{name} melakukan PARRY!");

        // Cari Sword_Riposte di child (SkillRoot_Sword)
        Sword_Riposte riposte = GetComponentInChildren<Sword_Riposte>();
        if (riposte != null)
            riposte.TriggerFollowUpDash();

        // Nonaktifkan stance setelah parry
        isRiposteStance = false;

        // Musuh yang diparry diberi stagger kecil
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

    // -------------------------------------------------------------
    // Mengaktifkan riposte stance (dipanggil dari Sword_Riposte)
    // -------------------------------------------------------------
    public void ActivateRiposte()
    {
        if (!canRiposte) return;

        isRiposteStance = true;
        canRiposte = false;
    }

    // Dipanggil setelah stance selesai oleh Sword_Riposte
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

    // -------------------------------------------------------------
    // Stagger / Knockback logic (manual untuk Kinematic)
    // -------------------------------------------------------------
    public void ApplyStagger(Vector2 direction, float force, float duration)
    {
        if (currentHP <= 0)
            return;

        StopAllCoroutines();
        StartCoroutine(StaggerRoutine(direction, force, duration));
    }

    private IEnumerator StaggerRoutine(Vector2 dir, float force, float duration)
    {
        isStaggered = true;

        if (rb.bodyType == RigidbodyType2D.Dynamic)
        {
            rb.AddForce(dir * force, ForceMode2D.Impulse);
        }
        else
        {
            yield return StartCoroutine(KinematicKnockback(dir, force, duration));
        }

        yield return new WaitForSeconds(duration);

        isStaggered = false;
    }

    private IEnumerator KinematicKnockback(Vector2 dir, float force, float time)
    {
        float timer = time;
        float speed = force;

        while (timer > 0)
        {
            timer -= Time.deltaTime;
            transform.position += (Vector3)(dir * speed * Time.deltaTime);
            yield return null;
        }
    }

    // -------------------------------------------------------------
    // Kematian karakter
    // -------------------------------------------------------------
    public virtual void Die()
    {
        Debug.Log($"{name} MATI");
        Destroy(gameObject);
    }
}
