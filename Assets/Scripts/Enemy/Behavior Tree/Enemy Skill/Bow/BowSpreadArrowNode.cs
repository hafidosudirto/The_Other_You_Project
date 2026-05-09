using UnityEngine;

public class BowSpreadArrowNode : Node
{
    private readonly NodeManager ai;

    public BowSpreadArrowNode(NodeManager ai)
    {
        this.ai = ai;
    }

    public override NodeState Evaluate()
    {
        // 1. Validasi referensi inti
        if (ai == null || ai.Combat == null || ai.playerTransform == null)
            return NodeState.Failure;

        // 2. Ambil referensi skill dari EnemyCombatController
        Enemy_Bow_SpreadArrow skill = ai.Combat.spreadArrowBow;
        if (skill == null)
            return NodeState.Failure;

        float distance = Vector2.Distance(ai.transform.position, ai.playerTransform.position);

        // 3. Set jarak gerak yang diinginkan ke AI Movement
        if (ai.Movement != null)
            ai.Movement.SetDesiredRange(skill.skillRange);

        // 4. Jika skill sedang berjalan, tahan di state Running
        if (skill.IsActive)
            return NodeState.Running;

        // 5. Jika terlalu jauh, tahan di state Running agar AI bergerak mendekat
        if (distance > skill.skillRange + skill.rangeTolerance)
            return NodeState.Running;

        // 6. Jika terlalu dekat, batalkan agar AI bisa mengambil jarak atau pakai skill lain
        if (distance < skill.minRange - skill.rangeTolerance)
            return NodeState.Failure;

        // 7. Cek batasan range spesifik dari skill
        if (!skill.IsInRange(distance))
            return NodeState.Failure;

        // 8. Cek apakah skill siap digunakan (cooldown, kondisi, dll)
        if (!skill.CanTrigger(distance))
            return NodeState.Failure;

        // 9. Eksekusi skill
        skill.Trigger();

        return NodeState.Running;
    }
}