using UnityEngine;
using System;

public class EnemyCombatController : MonoBehaviour
{
    // Event untuk Action-Lock (EnemyAI / MovementFSM dengar ini)
    public event Action OnSkillStart;
    public event Action OnSkillEnd;

    [Header("Stats (ambil dari CharacterBase)")]
    public CharacterBase stats;      // assign otomatis di Awake

    public float AttackPower => stats != null ? stats.attack : 10f;

    [Header("Sword Skills")]
    public Enemy_Sword_SlashCombo slashCombo;
    public Enemy_Sword_Whirlwind whirlwind;
    public Enemy_Sword_ChargedStrike chargedStrike;
    public Enemy_Sword_Riposte riposte;

    private void Awake()
    {
        if (stats == null)
            stats = GetComponent<CharacterBase>();

        if (slashCombo == null)
            slashCombo = GetComponentInChildren<Enemy_Sword_SlashCombo>();

        if (whirlwind == null)
            whirlwind = GetComponentInChildren<Enemy_Sword_Whirlwind>();

        if (chargedStrike == null)
            chargedStrike = GetComponentInChildren<Enemy_Sword_ChargedStrike>();

        if (riposte == null)
            riposte = GetComponentInChildren<Enemy_Sword_Riposte>();
    }

    // Dipanggil skill saat dimulai
    public void InvokeSkillStart()
    {
        OnSkillStart?.Invoke();
    }

    // Dipanggil skill saat selesai
    public void InvokeSkillEnd()
    {
        OnSkillEnd?.Invoke();
    }
}
