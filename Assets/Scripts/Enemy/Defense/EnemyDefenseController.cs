using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mengatur logika *defense* enemy ketika terkena serangan player.
/// Opsi 2: jika enemy sedang menyerang (*isPerformingAction*),
/// maka tidak bisa *defend* dan langsung menerima damage.
/// Jika tidak, ia mencoba melakukan *defense* (dash/dodge/parry).
/// </summary>
[RequireComponent(typeof(EnemyAI))]
public class EnemyDefenseController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyAI ai;
    [SerializeField] private CharacterBase characterBase;

    [Header("Defense Chance Settings")]
    [Range(0f, 1f)]
    public float baseDefenseChance = 0.6f; // peluang enemy mencoba defense

    // Behavior Tree khusus defense (boleh sangat sederhana)
    private Node defenseTree;

    private void Awake()
    {
        if (ai == null)
            ai = GetComponent<EnemyAI>();

        if (characterBase == null)
            characterBase = GetComponent<CharacterBase>();

        BuildDefenseTree();
    }

    /// <summary>
    /// Dipanggil oleh *hitbox* player ketika mengenai enemy.
    /// </summary>
    public void OnHitByPlayer(AttackPayload payload)
    {
        // Jika enemy sedang sibuk menyerang → tidak bisa defense
        if (ai.isPerformingAction)
        {
            TakeDamage(payload);
            return;
        }

        // Coba BT Defense (IsNotBusyNode adalah guard pertama)
        NodeState result = defenseTree.Evaluate();

        if (result == NodeState.Success)
        {
            // Enemy berhasil defense → tidak kena damage
            return;
        }

        // Jika defense gagal → kena damage
        TakeDamage(payload);

    }


    private void TakeDamage(AttackPayload payload)
    {
        characterBase.TakeDamage(payload.damage);
    }

    /// <summary>
    /// Membangun *behavior tree* sederhana untuk defense.
    /// Untuk sekarang: Parry, Dodge, atau DashOut.
    /// </summary>
    private void BuildDefenseTree()
    {
        var isNotBusy = new IsNotBusyNode(ai);

        var selector = new WeightedRandomSelector(
            new List<Node>
            {
            new DefenseParryNode(ai),
            new DefenseDodgeNode(ai),
            new DefenseDashOutNode(ai)
            },
            new List<float> { 0.2f, 0.4f, 0.4f }
        );

        // Sequence Defense:
        defenseTree = new Sequence(new List<Node>
    {
        isNotBusy,   // Cek apakah enemy sedang tidak sibuk
        selector     // Jika ya, jalankan defense selector
    });
    }
}

