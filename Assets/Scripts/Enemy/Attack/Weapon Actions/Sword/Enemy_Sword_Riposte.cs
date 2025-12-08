using UnityEngine;
using System.Collections;

public class Enemy_Sword_Riposte : MonoBehaviour
{
    public float stanceDuration = 0.5f;
    public LayerMask hitMask;

    private EnemyAI ai;
    private EnemyCombatController combat;

    private void Awake()
    {
        ai = GetComponentInParent<EnemyAI>();
        combat = GetComponentInParent<EnemyCombatController>();
    }

    public void Trigger()
    {
        StartCoroutine(RiposteRoutine());
    }

    private IEnumerator RiposteRoutine()
    {
        combat.InvokeSkillStart();

        yield return new WaitForSeconds(stanceDuration);

        combat.InvokeSkillEnd();
    }
}
