using UnityEngine;

public class MuzzlePointMirror : MonoBehaviour
{
    public Player player;

    private Vector3 baseLocalPos;

    private void Awake()
    {
        baseLocalPos = transform.localPosition;

        if (player == null)
            player = GetComponentInParent<Player>();
    }

    private void LateUpdate()
    {
        if (player == null) return;

        Vector3 p = baseLocalPos;
        p.x = Mathf.Abs(baseLocalPos.x) * (player.isFacingRight ? 1f : -1f);
        transform.localPosition = p;
    }
}