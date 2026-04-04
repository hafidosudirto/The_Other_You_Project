using UnityEngine;

public class EnemySwordSkillController : MonoBehaviour
{
    [Header("References")]
    public EnemyMovementFSM enemyFSM;
    public EnemyAI enemyAI;
    public EnemyCombatController combat;

    public bool isPerformingSkill = false;

    private void Awake()
    {
        enemyFSM = GetComponent<EnemyMovementFSM>();
        enemyAI = GetComponent<EnemyAI>();
        combat = GetComponent<EnemyCombatController>();
    }

    private void OnEnable()
    {
        if (combat != null)
        {
            combat.OnSkillStart += InvokeSkillStart;
            combat.OnSkillEnd += InvokeSkillEnd;
        }
    }

    private void OnDisable()
    {
        if (combat != null)
        {
            combat.OnSkillStart -= InvokeSkillStart;
            combat.OnSkillEnd -= InvokeSkillEnd;
        }
    }

    public void InvokeSkillStart()
    {
        isPerformingSkill = true;

        // Anda boleh memilih salah satu:
        // (1) disable FSM total (cepat tapi bisa ada efek samping state),
        // (2) jika FSM Anda punya mekanisme lock internal, panggil itu di sini.
        if (enemyFSM != null) enemyFSM.enabled = false;
    }

    public void InvokeSkillEnd()
    {
        isPerformingSkill = false;
        if (enemyFSM != null) enemyFSM.enabled = true;
    }
}
