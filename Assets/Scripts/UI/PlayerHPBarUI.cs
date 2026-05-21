using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHPBarUI : MonoBehaviour
{
    [Header("References")]
    public CharacterBase target;
    public Image fill;
    public TMP_Text hpText;

    private void Update()
    {
        if (target == null)
            return;

        float maxHp = Mathf.Max(1f, target.maxHP);
        float currentHp = Mathf.Clamp(target.currentHP, 0f, maxHp);

        if (fill != null)
            fill.fillAmount = currentHp / maxHp;

        if (hpText != null)
            hpText.text = $"{Mathf.RoundToInt(currentHp)}/{Mathf.RoundToInt(maxHp)}";
    }
}