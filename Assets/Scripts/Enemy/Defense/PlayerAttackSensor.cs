using UnityEngine;

public sealed class PlayerAttackSensor : MonoBehaviour
{
    [Header("Referensi Animator")]
    [SerializeField] private Animator playerAnimator;

    [Header("Parameter Bool Player")]
    [SerializeField] private string chargingBoolParameter = "isCharging";

    [Header("Konfigurasi Layer")]
    [SerializeField] private int layerIndex = 0;
    [SerializeField] private string layerNameForIsName = "Base Layer";

    [Header("Nama State Serangan")]
    [SerializeField] private string[] attackStateNames = new[] { "Slash1", "Slash2", "Whirlwind" };

    public bool IsAttacking { get; private set; }
    public int AttackId { get; private set; } = 0;

    private bool _wasAttacking;
    private int _lastAttackStateHash;

    private void Awake()
    {
        if (playerAnimator == null)
            playerAnimator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (playerAnimator == null) return;

        // isCharging adalah bool parameter pada Animator. :contentReference[oaicite:19]{index=19}
        bool isCharging = playerAnimator.GetBool(chargingBoolParameter);

        // Ambil state aktif Animator pada layer tertentu. :contentReference[oaicite:20]{index=20}
        var st = playerAnimator.GetCurrentAnimatorStateInfo(layerIndex);

        bool inAttackState = IsInAttackState(st);

        IsAttacking = isCharging || inAttackState;

        // AttackId naik saat memasuki window menyerang (false -> true),
        // dan juga naik ketika berpindah antar state serangan (Slash1 -> Slash2),
        // supaya enemy dapat bereaksi per ayunan jika animasi Anda memang demikian.
        if (IsAttacking)
        {
            if (!_wasAttacking)
            {
                AttackId++;
            }
            else if (inAttackState)
            {
                int h = st.fullPathHash;
                if (_lastAttackStateHash != 0 && h != _lastAttackStateHash)
                    AttackId++;
                _lastAttackStateHash = h;
            }
        }
        else
        {
            _lastAttackStateHash = 0;
        }

        _wasAttacking = IsAttacking;
    }

    private bool IsInAttackState(AnimatorStateInfo st)
    {
        if (attackStateNames == null) return false;

        for (int i = 0; i < attackStateNames.Length; i++)
        {
            string s = attackStateNames[i];
            if (string.IsNullOrWhiteSpace(s)) continue;

            // IsName mengecek kecocokan nama state aktif; format yang disarankan adalah "Layer.Name"
            if (st.IsName(s)) return true;

            string full = $"{layerNameForIsName}.{s}";
            if (st.IsName(full)) return true;
        }

        return false;
    }
}
