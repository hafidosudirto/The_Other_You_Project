using UnityEngine;

public class MuzzlePointMirror_Enemy : MonoBehaviour
{
    [Header("References")]
    public EnemyAI enemyAI;
    public CharacterBase owner;

    [Header("Options")]
    [Tooltip("Jika aktif, posisi lokal X akan dicerminkan mengikuti arah hadap enemy.")]
    public bool mirrorLocalPositionX = true;

    [Tooltip("Jika aktif, skala lokal X juga akan dicerminkan mengikuti arah hadap enemy.")]
    public bool mirrorLocalScaleX = false;

    private Vector3 baseLocalPos;
    private Vector3 baseLocalScale;

    private void Awake()
    {
        baseLocalPos = transform.localPosition;
        baseLocalScale = transform.localScale;

        if (enemyAI == null)
            enemyAI = GetComponentInParent<EnemyAI>();

        if (owner == null)
            owner = GetComponentInParent<CharacterBase>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            if (enemyAI == null)
                enemyAI = GetComponentInParent<EnemyAI>();

            if (owner == null)
                owner = GetComponentInParent<CharacterBase>();
        }
    }
#endif

    private void LateUpdate()
    {
        bool facingRight = GetFacingRight();

        if (mirrorLocalPositionX)
        {
            Vector3 p = baseLocalPos;
            p.x = Mathf.Abs(baseLocalPos.x) * (facingRight ? 1f : -1f);
            transform.localPosition = p;
        }

        if (mirrorLocalScaleX)
        {
            Vector3 s = baseLocalScale;
            s.x = Mathf.Abs(baseLocalScale.x) * (facingRight ? 1f : -1f);
            transform.localScale = s;
        }
    }

    private bool GetFacingRight()
    {
        if (enemyAI != null)
            return enemyAI.IsFacingRight;

        if (owner != null)
            return owner.isFacingRight;

        return true;
    }
}