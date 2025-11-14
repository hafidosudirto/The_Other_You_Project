using UnityEngine;
using System.Collections;

public class Sword_SlashCombo : MonoBehaviour, ISkill
{
    [Header("Slash Combo Settings")]
    public float attackRadius = 1.5f;
    public float attackAngle = 90f;

    public float firstDelay = 0.1f;      // jeda sebelum hit pertama
    public float chain1Delay = 0.2f;
    public float chain2Delay = 0.25f;

    public float chainWindow = 0.6f;     // batas input chain berikutnya
    public Color chain1Color = Color.yellow;
    public Color chain2Color = new Color(1f, 0.4f, 0f);

    public int arcSegments = 20;

    private CharacterBase character;
    private SkillBase skillBase;

    // private bool waitingForChain = false;
    private int currentChain = 0;

    void Awake()
    {
        character = GetComponentInParent<CharacterBase>();
        skillBase = GetComponentInParent<SkillBase>();
    }

    public void TriggerSkill()
    {
        if (character == null || skillBase == null)
            return;

        if (!character.CanAct())
            return;

        // Mulai Combo
        currentChain = 1;
        StartCoroutine(ComboRoutine());
    }

    private IEnumerator ComboRoutine()
    {
        // Slash 1
        yield return new WaitForSeconds(firstDelay);
        PerformSlash(chain1Color);

        // waitingForChain = true;

        float timer = 0f;
        bool chainPressed = false;

        // Window input chain 2
        while (timer < chainWindow)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                chainPressed = true;
                break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // waitingForChain = false;

        if (!chainPressed)
        {
            // Tidak ada chain → COMBO selesai
            currentChain = 0;
            skillBase.ReleaseLock();
            yield break;
        }

        // ===== SLASH 2 =====
        currentChain = 2;
        yield return new WaitForSeconds(chain1Delay);
        PerformSlash(chain2Color);

        // waitingForChain = true;
        timer = 0f;
        chainPressed = false;

        // Window input chain 3
        while (timer < chainWindow)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                chainPressed = true;
                break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        // waitingForChain = false;

        if (!chainPressed)
        {
            // Combo berhenti di Slash 2
            currentChain = 0;
            skillBase.ReleaseLock();
            yield break;
        }

        // ===== SLASH 3 =====
        currentChain = 3;
        yield return new WaitForSeconds(chain2Delay);
        PerformSlash(chain2Color);

        // Combo selesai
        currentChain = 0;
        skillBase.ReleaseLock();
    }

    private void PerformSlash(Color color)
    {
        if (character == null)
            return;

        Vector3 origin = character.transform.position;
        Vector3 dir = character.isFacingRight ? Vector3.right : Vector3.left;

        // Hit detection
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, attackRadius);
        foreach (var hit in hits)
        {
            CharacterBase enemy = hit.GetComponent<CharacterBase>();
            if (enemy == null || enemy == character) 
                continue;

            // Cek sudut
            Vector2 toTarget = (enemy.transform.position - origin).normalized;
            float angleCheck = Vector2.Angle(dir, toTarget);

            if (angleCheck <= attackAngle * 0.5f)
            {
                enemy.TakeDamage(character.attack);
                // Debug.Log("Slash Combo hit: " + enemy.name);
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!Application.isPlaying)
            return;

        if (currentChain <= 0) 
            return;

        CharacterBase c = character;
        if (c == null)
            return;

        Vector3 origin = c.transform.position;
        Vector3 dir = c.isFacingRight ? Vector3.right : Vector3.left;

        Gizmos.color = (currentChain == 1) ? chain1Color : chain2Color;

        float startAngle = -attackAngle * 0.5f;
        float step = attackAngle / arcSegments;

        Vector3 prev = origin + Quaternion.Euler(0, 0, startAngle) * dir * attackRadius;

        for (int i = 1; i <= arcSegments; i++)
        {
            float current = startAngle + step * i;
            Vector3 next = origin + Quaternion.Euler(0, 0, current) * dir * attackRadius;
            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
#endif
}
