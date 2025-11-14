using UnityEngine;
using UnityEngine.UI;

public class HPBar_Follow_Enemy : MonoBehaviour
{
    public Transform enemy;        // Enemy transform
    public CharacterBase stats;    // HP musuh
    public Image fill;             // Fill image
    public Vector3 offset = new Vector3(0, 1.5f, 0);

    void LateUpdate()
    {
        if (enemy == null || stats == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = enemy.position + offset;

        float ratio = stats.currentHP / stats.maxHP;
        fill.fillAmount = ratio;
    }
}
