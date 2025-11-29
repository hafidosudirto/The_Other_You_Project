using UnityEngine;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;

public class HPBar_Follow_Enemy : MonoBehaviour
{
    public Transform enemy;
    public CharacterBase stats;
    public Image fill;
    public Vector3 offset = new Vector3(0, 1.5f, 0);

    private Camera cam;

    private void Awake()
    {
        cam = Camera.main;
    }

    // StageManager akan memanggil ini saat enemy spawn
    public void SetTarget(Transform newTarget, Vector3 worldOffset)
    {
        enemy = newTarget;
        offset = worldOffset;

        // AUTO AMBIL HP
        stats = newTarget.GetComponent<CharacterBase>();
        if (stats == null)
            Debug.LogError("[HPBar] CharacterBase tidak ditemukan di enemy!");
    }

    void LateUpdate()
    {
        if (enemy == null)
            return;

        transform.position = enemy.position + offset;

        if (stats != null)
        {
            float ratio = stats.currentHP / stats.maxHP;
            fill.fillAmount = ratio;
        }
    }
}

