using UnityEngine;

/// <summary>
/// Config stagger per-skill — ditambahkan ke setiap skill sebagai [SerializeField].
/// Atur force dan durasi per skill di sini.
/// Immunity duration (cooldown stagger antar hit) diatur terpusat di StaggerCooldownSettings asset.
/// </summary>
[System.Serializable]
public class SkillStaggerConfig
{
    [Header("Stagger")]
    [Tooltip("Aktifkan/nonaktifkan stagger untuk skill ini.\n" +
             "Jika dinonaktifkan, skill tidak akan men-stagger enemy sama sekali.")]
    public bool aktif = true;

    [Tooltip("Kekuatan dorong ke belakang saat stagger.\n" +
             "Nilai lebih besar = enemy terpental lebih jauh.")]
    [Min(0f)] public float knockbackForce = 3f;

    [Tooltip("Durasi enemy terbeku / freeze setelah kena stagger (detik).\n" +
             "Selama freeze, enemy tidak bisa bergerak atau menyerang.\n" +
             "Immunity duration diatur di StaggerCooldownSettings.")]
    [Min(0f)] public float staggerDuration = 0.25f;

    /// <summary>
    /// Terapkan stagger ke target. Menghormati immunity window dari StaggerCooldownSettings.
    /// Gunakan ini di setiap hit method skill.
    /// </summary>
    /// <param name="target">Enemy yang kena hit.</param>
    /// <param name="direction">Arah dorong (normalnya dari attacker ke target atau arah panah).</param>
    public void Apply(CharacterBase target, Vector2 direction)
    {
        if (!aktif)
            return;

        if (target == null)
            return;

        target.TryApplyStagger(direction, knockbackForce, staggerDuration);
    }
}
