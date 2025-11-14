using UnityEngine;

// Script ini menangani serangan dasar Enemy.
// Script ini bekerja bersama Enemy.cs dan Move_Chase.
// Enemy memakai Kinematic Rigidbody sehingga serangan dan knockback
// ditangani oleh CharacterBase (knockback manual).
//
// Flow serangan:
// - Jika Player berada dalam radius serang → AI akan menyerang.
// - Primary Attack memakai damage dasar.
// - Heavy Attack memakai damage lebih besar.
// - Attack diarahkan sesuai facing direction Enemy.
// - Parry Player otomatis diproses oleh CharacterBase Player.
//
// Gizmo arc digunakan untuk debugging jangkauan serang.

[RequireComponent(typeof(Enemy))]
public class Basic_Combat_Enemy : MonoBehaviour
{
    private Enemy enemy;
    private Transform player;

    // Range serangan
    public float attackRadius = 1.5f;
    public float attackAngle = 100f;

    // Cooldown serangan
    public float cooldownTime = 1.2f;
    private float cooldownTimer = 0f;

    // Gizmo arc
    public int arcSegments = 20;
    public Color primaryColor = Color.yellow;
    public Color heavyColor = new Color(1f, 0.5f, 0f);

    private bool showGizmo = false;
    private float gizmoTimer = 0f;
    private float gizmoDuration = 0.2f;
    private Color gizmoColor;

    void Awake()
    {
        enemy = GetComponent<Enemy>();

        // Mencari Player berdasarkan tag (aman untuk Player swap)
        GameObject target = GameObject.FindGameObjectWithTag("Player");
        if (target != null)
            player = target.transform;
    }

    void Update()
    {
        // Jika Player diswap, cari ulang berdasarkan tag
        if (player == null)
        {
            GameObject target = GameObject.FindGameObjectWithTag("Player");
            if (target != null) player = target.transform;
        }

        if (player == null)
            return;

        // Jika sedang stagger atau mati, hentikan serangan
        if (!enemy.CanAct())
            return;

        // Update gizmo
        if (showGizmo)
        {
            gizmoTimer -= Time.deltaTime;
            if (gizmoTimer <= 0f)
                showGizmo = false;
        }

        // Cooldown
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
            return;
        }

        float distance = Vector2.Distance(transform.position, player.position);

        if (distance <= attackRadius)
        {
            PerformAttackLogic();
        }
    }

    // Menentukan apakah melakukan primary atau heavy attack
    void PerformAttackLogic()
    {
        int type = Random.Range(0, 2); // 0 = primary, 1 = heavy
        float dmg = enemy.attack;

        if (type == 0)
        {
            dmg = enemy.attack;
            gizmoColor = primaryColor;
            Debug.Log(name + " melakukan Primary Attack");
        }
        else
        {
            dmg = enemy.attack * 1.5f;
            gizmoColor = heavyColor;
            Debug.Log(name + " melakukan Heavy Attack");
        }

        TriggerAttackGizmo();
        TryHitPlayer(dmg);

        cooldownTimer = cooldownTime;
    }

    // Cek apakah Player berada dalam arc serangan
    void TryHitPlayer(float damage)
    {
        if (player == null) return;

        Vector3 origin = transform.position;
        Vector3 facingDir = enemy.isFacingRight ? Vector3.right : Vector3.left;
        Vector3 dirToPlayer = (player.position - origin).normalized;

        float angle = Vector3.Angle(facingDir, dirToPlayer);

        if (angle <= attackAngle * 0.5f)
        {
            CharacterBase target = player.GetComponent<CharacterBase>();
            if (target != null)
            {
                // Player.TakeDamage() akan otomatis memproses parry stance
                target.TakeDamage(damage, gameObject);
            }
        }
    }

    // Menyalakan gizmo arc sesaat
    void TriggerAttackGizmo()
    {
        showGizmo = true;
        gizmoTimer = gizmoDuration;
    }

    // Arc gizmo serangan seperti SlashCombo
    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !showGizmo)
            return;

        if (enemy == null)
            enemy = GetComponent<Enemy>();

        Vector3 center = transform.position;
        Vector3 facingDir = enemy.isFacingRight ? Vector3.right : Vector3.left;

        float radius = attackRadius;
        float angle = attackAngle;

        float startAngle = -angle * 0.5f;
        float step = angle / arcSegments;

        Gizmos.color = gizmoColor;

        Vector3 prev = center + Quaternion.Euler(0, 0, startAngle) * facingDir * radius;

        for (int i = 1; i <= arcSegments; i++)
        {
            float current = startAngle + step * i;
            Vector3 next = center + Quaternion.Euler(0, 0, current) * facingDir * radius;

            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}
