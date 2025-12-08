    using UnityEngine;

    public class ArrowDamage : MonoBehaviour
    {
        [Header("Base Stats")]
        [Tooltip("Damage dasar panah. Bisa dioverride oleh skill lewat SetStats().")]
        public float baseDamage = 10f;

        [Tooltip("Kekuatan knockback. 0 = tidak ada knockback.")]
        public float knockbackForce = 0f;

        [Tooltip("Durasi stun (detik). 0 = tidak ada stun.")]
        public float stunDuration = 0f;

        [Header("Behaviour Flags")]
        [Tooltip("Jika true, panah hancur setelah kena target (kecuali piercing).")]
        public bool destroyOnHit = true;

        [Tooltip("Jika true, panah akan menembus target (tidak hancur saat hit).")]
        public bool piercing = false;

        [Tooltip("Jika true, panah bertipe concussive (ada efek AoE).")]
        public bool concussive = false;

        [Tooltip("Radius AoE untuk concussive shot.")]
        public float explosionRadius = 1.5f;

        [Header("Debug / Owner")]
        [Tooltip("Siapa pemilik panah ini (player / enemy)")]
        [SerializeField] public CharacterBase owner;

        private Rigidbody2D rb;

        // -------------------------------------------------------------
        //  INIT
        // -------------------------------------------------------------
        void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
        }

        /// <summary>
        /// Dipanggil dari skill bow untuk mendaftarkan pemilik panah.
        /// Player mewarisi CharacterBase, jadi parameter-nya pakai base class.
        /// </summary>
        public void SetOwner(CharacterBase ownerCharacter)
        {
            owner = ownerCharacter;
        }

        /// <summary>
        /// API sederhana untuk override stat panah per-skill.
        /// Contoh:
        ///   arrow.SetStats(8, 0, 0, false);              // Quick Shot
        ///   arrow.SetStats(14, 4, 0, false);             // Full Draw
        ///   arrow.SetStats(10, 0, 0, true);              // Piercing
        ///   arrow.SetStats(8, 3, 1.5f, false, true);     // Concussive
        /// </summary>
        public void SetStats(
            float damage,
            float knockback,
            float stun,
            bool isPiercing,
            bool isConcussive = false
        )
        {
            baseDamage    = damage;
            knockbackForce = knockback;
            stunDuration  = stun;
            piercing      = isPiercing;
            concussive    = isConcussive;
        }

        // -------------------------------------------------------------
        //  COLLISION
        // -------------------------------------------------------------
        private void OnTriggerEnter2D(Collider2D other)
        {
            // Cari target yang punya CharacterBase
            CharacterBase target = other.GetComponent<CharacterBase>();
            if (target == null)
                return;

            // Jangan hit owner sendiri
            if (owner != null && other.gameObject == owner.gameObject)
                return;

            // Hit utama
            Vector2 hitPoint = other.ClosestPoint(transform.position);
            ApplyEffectsToTarget(target, hitPoint, applyDamage: true);

            // Efek concussive AoE (knockback + stun ke sekitar)
            if (concussive && explosionRadius > 0f)
            {
                Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
                foreach (var h in hits)
                {
                    if (h == other) continue; // yang sudah kena di atas

                    CharacterBase extraTarget = h.GetComponent<CharacterBase>();
                    if (extraTarget == null || extraTarget == target)
                        continue;

                    Vector2 extraPoint = h.ClosestPoint(transform.position);
                    // Di AoE biasanya cuma knockback + stun, damage opsional (di sini: tidak tambah damage)
                    ApplyEffectsToTarget(extraTarget, extraPoint, applyDamage: false);
                }
            }

            // Kalau bukan piercing, panah hancur setelah hit
            if (!piercing && destroyOnHit)
            {
                Destroy(gameObject);
            }
        }

        // -------------------------------------------------------------
        //  EFFECT HELPER
        // -------------------------------------------------------------
        private void ApplyEffectsToTarget(CharacterBase target, Vector2 hitPoint, bool applyDamage)
        {
            // Damage
            if (applyDamage && baseDamage > 0f)
            {
                GameObject source = owner != null ? owner.gameObject : gameObject;
                target.TakeDamage(baseDamage, source);
            }

            // Knockback
            if (knockbackForce > 0f)
            {
                Vector2 dir = ((Vector2)target.transform.position - hitPoint).normalized;

                // Kalau arahnya aneh (misal overlap persis), fallback dari velocity panah
                if (dir.sqrMagnitude < 0.0001f && rb != null && rb.velocity.sqrMagnitude > 0.0001f)
                    dir = rb.velocity.normalized;

                target.ApplyKnockback(dir, knockbackForce);
            }

            // Stun
            if (stunDuration > 0f)
            {
                target.ApplyStun(stunDuration);
            }
        }

    #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (concussive && explosionRadius > 0f)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, explosionRadius);
            }
        }
    #endif
    }
