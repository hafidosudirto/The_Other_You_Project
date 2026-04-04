using UnityEngine;
using UnityEngine.UI;

public class HPBar_Follow : MonoBehaviour
{
    public Transform target;       // Player atau Enemy
    public CharacterBase stats;    // Script HP
    public Image fill;             // Fill image
    public Vector3 offset;         // Offset di atas kepala

    void LateUpdate()
    {
        if (target == null || stats == null)
        {
            Destroy(gameObject);
            return;
        }

        // FOLLOW POSISI (world-space canvas)
        transform.position = target.position + offset;

        // BAR UPDATE
        float ratio = stats.currentHP / stats.maxHP;
        fill.fillAmount = ratio;
    }
}
