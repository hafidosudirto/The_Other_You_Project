using UnityEngine;
using UnityEngine.UI;

public class PlayerHPBarUI : MonoBehaviour
{
    [Header("References")]
    public CharacterBase target;
    public Image fill;

    private void Update()
    {
        if (target == null || fill == null)
            return;

        if (target.maxHP <= 0f)
        {
            fill.fillAmount = 0f;
            return;
        }

        float ratio = target.currentHP / target.maxHP;
        fill.fillAmount = Mathf.Clamp01(ratio);
    }
}