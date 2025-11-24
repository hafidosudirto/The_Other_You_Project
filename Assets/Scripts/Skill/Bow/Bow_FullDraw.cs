using UnityEngine;
using System.Collections;

public class Bow_FullDraw : MonoBehaviour, ISkill
{
    [Header("Full Draw Settings")]
    public GameObject arrowPrefab;
    public Transform muzzlePoint;

    public float minChargeTime = 0.3f;
    public float maxChargeTime = 2f;

    public float minArrowSpeed = 8f;
    public float maxArrowSpeed = 18f;

    public float baseDamage = 10f;
    public float maxExtraDamage = 6f;
    public float maxKnockback = 4f;

    [Header("Visual")]
    public Color arrowColor = Color.white;

    private CharacterBase character;
    // private float lastCastTime = -999f;

    void Awake()
    {
        character = GetComponentInParent<CharacterBase>();
    }

    public void TriggerSkill(int slotIndex)
    {
        if (!character || !character.CanAct())
            return;

        StartCoroutine(FullDrawRoutine());
    }

    private IEnumerator FullDrawRoutine()
    {
        float timer = 0f;

        DebugHub.Bow("FullDraw → CHARGE START");

        while (Input.GetKey(KeyCode.Alpha2))
        {
            timer += Time.deltaTime;
            yield return null;
        }

        float charge01 = Mathf.Clamp01(timer / maxChargeTime);

        DebugHub.Bow($"FullDraw → RELEASE (Power={charge01:0.00})");
        FireChargedArrow(charge01);
    }

    private void FireChargedArrow(float t)
    {
        if (!arrowPrefab || !muzzlePoint)
            return;

        GameObject arrowObj = Instantiate(
            arrowPrefab,
            muzzlePoint.position,
            muzzlePoint.rotation
        );

        SpriteRenderer sr = arrowObj.GetComponent<SpriteRenderer>();
        if (sr != null)
            sr.color = arrowColor;

        Vector2 dir = character.isFacingRight ? Vector2.right : Vector2.left;

        Rigidbody2D rb = arrowObj.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.velocity = dir * Mathf.Lerp(minArrowSpeed, maxArrowSpeed, t);

        ArrowDamage dmg = arrowObj.GetComponent<ArrowDamage>();
        if (dmg != null)
        {
            float dmgValue = baseDamage + maxExtraDamage * t;
            float kb = maxKnockback * t;

            dmg.SetOwner(character);
            dmg.SetStats(dmgValue, kb, 0f, false, false);
        }

        DebugHub.Bow($"FullDraw Arrow → Speed={Mathf.Lerp(minArrowSpeed, maxArrowSpeed, t):0.0}");
    }
}
