using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyAnimation : MonoBehaviour
{
    [SerializeField] public Animator animator;

    private readonly int HashSlash1 = Animator.StringToHash("Slash1");
    private readonly int HashSlash2 = Animator.StringToHash("Slash2");
    private readonly int HashWhirlwind = Animator.StringToHash("Whirlwind");
    private readonly int HashRiposte = Animator.StringToHash("RiposteAttack");
    private readonly int HashHurt = Animator.StringToHash("Hurt");
    private readonly int HashDie = Animator.StringToHash("IsDead");

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    public void PlaySlash1() => animator.SetTrigger(HashSlash1);
    public void PlaySlash2() => animator.SetTrigger(HashSlash2);
    public void PlayWhirlwind() => animator.SetTrigger(HashWhirlwind);
    public void PlayRiposte() => animator.SetTrigger(HashRiposte);
    public void PlayHurt() => animator.SetTrigger(HashHurt);
    public void SetDead(bool v) => animator.SetBool(HashDie, v);
}
