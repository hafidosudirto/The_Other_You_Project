using System;
using UnityEngine;

public class EnemySwordSkillController : MonoBehaviour
{
    [Header("References")]
    public EnemyMovementFSM enemyFSM;
    public EnemyAI enemyAI;

    [Header("Skills")]
    public Enemy_Sword_SlashCombo slashCombo;
    public Enemy_Sword_Whirlwind whirlwind;
    public Enemy_Sword_Riposte riposte;
    public Enemy_Sword_ChargedStrike chargedStrike;

    public bool isPerformingSkill = false;

    private void Awake()
    {
        enemyFSM = GetComponent<EnemyMovementFSM>();
        enemyAI = GetComponent<EnemyAI>();
    }

    public void InvokeSkillStart()
    {
        isPerformingSkill = true;
        enemyFSM.enabled = false; // MATIKAN Movement FSM
    }

    public void InvokeSkillEnd()
    {
        isPerformingSkill = false;
        enemyFSM.enabled = true; // HIDUPKAN Movement FSM
    }
}
