using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NodeManager))]
public class Bow_AttackTree : MonoBehaviour
{
    private enum BowSkillIndex
    {
        QuickShot = 0,
        SpreadArrow = 1,
        FullDraw = 2,
        FullDrawFullCharge = 3,
        ConcussiveShot = 4
    }

    private const int BowSkillSlotTotal = 5;

    private NodeManager nodeManager;

    [Header("Bow Attributes")]
    public float attackRange = 8f;
    public float bowSenseRange = 15f;
    [SerializeField] private Vector2 attackRangeOffset = Vector2.zero;

    [Header("Legacy Bow Logic")]
    [Tooltip("Jika false, Bow memakai weighted DDA. Jika true, Bow memakai bag acak seimbang seperti mode uji lama.")]
    [SerializeField] private bool useBowBalancedBag = false;

    [Tooltip("Jika aktif, log pemilihan skill Bow akan muncul di Console.")]
    [SerializeField] private bool showDDADebugLog = true;

    [Header("Fallback DDA Weights")]
    [SerializeField] private float fallbackQuickWeight = 50f;
    [SerializeField] private float fallbackSpreadWeight = 0f;
    [SerializeField] private float fallbackFullDrawWeight = 50f;

    [Tooltip("Default 0 agar FullDrawFullCharge tidak muncul sebelum player benar-benar memakai full charge / piercing.")]
    [SerializeField] private float fallbackFullDrawFullChargeWeight = 0f;

    [Tooltip("Default 0 agar Concussive tidak muncul sebelum player benar-benar memakai Concussive.")]
    [SerializeField] private float fallbackConcussiveWeight = 0f;

    private readonly List<float> currentWeights = new List<float>
    {
        50f, // QuickShot
        0f,  // SpreadArrow
        50f, // FullDraw
        0f,  // FullDrawFullCharge
        0f   // ConcussiveShot
    };

    private readonly List<int> bowSkillBag = new List<int>();

    private int pendingBowSkillId = -1;
    private int lastBowSkillId = -1;
    private int cachedProfileVersion = -1;

    public void Initialize(NodeManager manager)
    {
        nodeManager = manager;
        SyncWeightsFromDDA(true);

        if (useBowBalancedBag)
            RefillBowSkillBag();
    }

    public void EvaluateTree()
    {
        if (nodeManager == null || nodeManager.playerTransform == null || nodeManager.Combat == null)
            return;

        if (nodeManager.isPerformingAction || nodeManager.Combat.IsBusy)
            return;

        SyncWeightsFromDDA();

        float distance = Vector2.Distance(transform.position, nodeManager.playerTransform.position);

        /*
         * Bow boleh mengejar walaupun belum boleh menyerang.
         * Karena itu, ketika player di luar sense range, AttackTree berhenti,
         * tetapi MovementFSM tetap berjalan dari NodeManager.
         */
        if (distance > bowSenseRange)
            return;

        /*
         * Meniru perilaku EnemyAI lama:
         * jangan menembak jika posisi Y belum sejajar.
         * Namun pending skill tetap dipertahankan, sehingga jika DDA memilih
         * Concussive 100%, ia tidak akan diganti menjadi SpreadArrow.
         */
        if (nodeManager.Movement != null && nodeManager.Movement.useVerticalAlign)
        {
            float absDistY = Mathf.Abs(nodeManager.playerTransform.position.y - transform.position.y);

            if (absDistY > nodeManager.Movement.verticalTolerance)
                return;
        }

        if (useBowBalancedBag)
        {
            TryExecuteBalancedBowSkill(distance);
        }
        else
        {
            TryExecuteWeightedBowSkill(distance);
        }
    }

    public void SyncWeightsFromDDA(bool force = false)
    {
        if (nodeManager == null)
            return;

        DDAController dda = DDAController.Instance;

        if (!force && dda != null && cachedProfileVersion == dda.ProfileVersion)
            return;

        if (dda != null)
            cachedProfileVersion = dda.ProfileVersion;

        ApplyFallbackWeightsToCache();

        bool hasValidWeights = false;

        /*
         * Prioritas utama: DDAController baru, karena sudah memakai 5 slot.
         */
        if (dda != null)
        {
            float[] weights = dda.GetCurrentBowSkillWeightsCopy();

            if (weights != null && weights.Length > 0)
            {
                ApplyIncomingBowWeights(weights);
                hasValidWeights = true;
            }
        }

        /*
         * Fallback kompatibilitas:
         * Jika adaptiveProfile masih ada dan DDAController belum memberi data,
         * tetap bisa dibaca. Jika datanya 4 slot, slot ke-3 dianggap Concussive lama,
         * lalu dipetakan ke indeks 4.
         */
        if (!hasValidWeights && nodeManager.adaptiveProfile != null)
        {
            nodeManager.adaptiveProfile.RefreshFromDDA();

            IReadOnlyList<float> weights = nodeManager.adaptiveProfile.GetBowSkillWeights();

            if (weights != null && weights.Count > 0)
            {
                ApplyIncomingBowWeights(weights);
                hasValidWeights = true;
            }
        }

        ZeroInvalidSkillWeights();

        float totalWeight = GetTotalWeight();

        if (totalWeight <= 0.001f)
        {
            ApplyFallbackWeightsToCache();
            ZeroInvalidSkillWeights();
        }

        if (showDDADebugLog)
        {
            Debug.Log(
                $"[Bow_AttackTree] Weights 5-slot | " +
                $"Quick={currentWeights[(int)BowSkillIndex.QuickShot]:F2}, " +
                $"Spread={currentWeights[(int)BowSkillIndex.SpreadArrow]:F2}, " +
                $"FullDraw={currentWeights[(int)BowSkillIndex.FullDraw]:F2}, " +
                $"FullCharge={currentWeights[(int)BowSkillIndex.FullDrawFullCharge]:F2}, " +
                $"Concussive={currentWeights[(int)BowSkillIndex.ConcussiveShot]:F2}, " +
                $"Pending={GetSkillName(pendingBowSkillId)}"
            );
        }
    }

    private void ApplyIncomingBowWeights(IReadOnlyList<float> weights)
    {
        if (weights == null || weights.Count <= 0)
            return;

        /*
         * Format baru:
         * [0] QuickShot
         * [1] SpreadArrow
         * [2] FullDraw
         * [3] FullDrawFullCharge / Piercing
         * [4] ConcussiveShot
         */
        if (weights.Count >= BowSkillSlotTotal)
        {
            for (int i = 0; i < BowSkillSlotTotal; i++)
                currentWeights[i] = Mathf.Max(0f, weights[i]);

            return;
        }

        /*
         * Format lama:
         * [0] QuickShot
         * [1] SpreadArrow
         * [2] FullDraw
         * [3] ConcussiveShot
         *
         * Dalam format baru, Concussive harus dipindah ke indeks 4.
         */
        if (weights.Count == 4)
        {
            currentWeights[(int)BowSkillIndex.QuickShot] = Mathf.Max(0f, weights[0]);
            currentWeights[(int)BowSkillIndex.SpreadArrow] = Mathf.Max(0f, weights[1]);
            currentWeights[(int)BowSkillIndex.FullDraw] = Mathf.Max(0f, weights[2]);
            currentWeights[(int)BowSkillIndex.FullDrawFullCharge] = 0f;
            currentWeights[(int)BowSkillIndex.ConcussiveShot] = Mathf.Max(0f, weights[3]);
            return;
        }

        int copyCount = Mathf.Min(weights.Count, BowSkillSlotTotal);

        for (int i = 0; i < copyCount; i++)
            currentWeights[i] = Mathf.Max(0f, weights[i]);
    }

    private void ZeroInvalidSkillWeights()
    {
        if (nodeManager == null || nodeManager.Combat == null)
            return;

        if (nodeManager.Combat.quickShotBow == null)
            currentWeights[(int)BowSkillIndex.QuickShot] = 0f;

        if (nodeManager.Combat.spreadArrowBow == null)
            currentWeights[(int)BowSkillIndex.SpreadArrow] = 0f;

        if (nodeManager.Combat.fullDrawBow == null)
        {
            currentWeights[(int)BowSkillIndex.FullDraw] = 0f;
            currentWeights[(int)BowSkillIndex.FullDrawFullCharge] = 0f;
        }

        if (nodeManager.Combat.concussiveBow == null)
            currentWeights[(int)BowSkillIndex.ConcussiveShot] = 0f;
    }

    public float GetDesiredRangeForMovement(float fallback)
    {
        if (nodeManager == null || nodeManager.Combat == null)
            return fallback;

        if (useBowBalancedBag)
        {
            if (bowSkillBag.Count == 0)
                RefillBowSkillBag();

            if (bowSkillBag.Count > 0)
                return GetSkillRange(bowSkillBag[0], fallback);

            return fallback;
        }

        EnsurePendingBowSkillSelected();

        if (pendingBowSkillId == -1)
            return fallback;

        return GetSkillRange(pendingBowSkillId, fallback);
    }

    public float GetMinimumRangeForMovement(float fallback)
    {
        if (nodeManager == null || nodeManager.Combat == null)
            return fallback;

        if (useBowBalancedBag)
        {
            if (bowSkillBag.Count == 0)
                RefillBowSkillBag();

            if (bowSkillBag.Count > 0)
                return GetSkillMinimumRange(bowSkillBag[0], fallback);

            return fallback;
        }

        EnsurePendingBowSkillSelected();

        if (pendingBowSkillId == -1)
            return fallback;

        return GetSkillMinimumRange(pendingBowSkillId, fallback);
    }

    private void TryExecuteWeightedBowSkill(float distance)
    {
        EnsurePendingBowSkillSelected();

        if (pendingBowSkillId == -1)
            return;

        /*
         * Perbaikan penting:
         * Jika DDA memilih Concussive, tetapi jarak belum valid, jangan memilih
         * skill lain. Pending Concussive tetap ditahan sampai Movement membawa
         * enemy ke jarak yang benar.
         */
        if (CanUseBowSkillId(pendingBowSkillId, distance))
        {
            TriggerBowSkillId(pendingBowSkillId);

            if (showDDADebugLog)
            {
                Debug.Log(
                    $"[Bow_AttackTree] Trigger Bow skill -> {GetSkillName(pendingBowSkillId)} | " +
                    $"Distance={distance:F2}"
                );
            }

            lastBowSkillId = pendingBowSkillId;
            pendingBowSkillId = -1;
        }
    }

    private void TryExecuteBalancedBowSkill(float distance)
    {
        if (bowSkillBag.Count == 0)
            RefillBowSkillBag();

        if (bowSkillBag.Count == 0)
            return;

        int nextSkillId = bowSkillBag[0];

        if (CanUseBowSkillId(nextSkillId, distance))
        {
            TriggerBowSkillId(nextSkillId);

            if (showDDADebugLog)
            {
                Debug.Log(
                    $"[Bow_AttackTree] Trigger balanced Bow skill -> {GetSkillName(nextSkillId)} | " +
                    $"Distance={distance:F2}"
                );
            }

            lastBowSkillId = nextSkillId;
            bowSkillBag.RemoveAt(0);
        }
    }

    private void EnsurePendingBowSkillSelected()
    {
        if (pendingBowSkillId != -1)
            return;

        SyncWeightsFromDDA();

        float total = GetTotalWeight();

        if (total <= 0.001f)
            return;

        float roll = Random.Range(0f, total);
        float cumulative = 0f;

        for (int i = 0; i < currentWeights.Count; i++)
        {
            float weight = Mathf.Max(0f, currentWeights[i]);

            if (weight <= 0f)
                continue;

            cumulative += weight;

            if (roll <= cumulative)
            {
                pendingBowSkillId = i;
                break;
            }
        }

        if (pendingBowSkillId == -1)
            return;

        if (showDDADebugLog)
        {
            Debug.Log(
                $"[Bow_AttackTree] Pending Bow skill dari DDA -> {GetSkillName(pendingBowSkillId)} | " +
                $"Weight={currentWeights[pendingBowSkillId]:F2}, " +
                $"MovementRange={GetSkillRange(pendingBowSkillId, attackRange):F2}, " +
                $"MinimumRange={GetSkillMinimumRange(pendingBowSkillId, 0f):F2}"
            );
        }
    }

    private bool CanUseBowSkillId(int skillId, float distance)
    {
        if (nodeManager == null || nodeManager.Combat == null)
            return false;

        switch ((BowSkillIndex)skillId)
        {
            case BowSkillIndex.QuickShot:
                return nodeManager.Combat.quickShotBow != null &&
                       nodeManager.Combat.quickShotBow.CanTrigger(distance);

            case BowSkillIndex.SpreadArrow:
                return nodeManager.Combat.spreadArrowBow != null &&
                       nodeManager.Combat.spreadArrowBow.CanTrigger(distance);

            case BowSkillIndex.FullDraw:
                return nodeManager.Combat.fullDrawBow != null &&
                       nodeManager.Combat.fullDrawBow.CanTriggerNormal(distance);

            case BowSkillIndex.FullDrawFullCharge:
                return nodeManager.Combat.fullDrawBow != null &&
                       nodeManager.Combat.fullDrawBow.CanTriggerFullCharge(distance);

            case BowSkillIndex.ConcussiveShot:
                return nodeManager.Combat.concussiveBow != null &&
                       nodeManager.Combat.concussiveBow.CanTrigger(distance);

            default:
                return false;
        }
    }

    private void TriggerBowSkillId(int skillId)
    {
        if (nodeManager == null || nodeManager.Combat == null)
            return;

        switch ((BowSkillIndex)skillId)
        {
            case BowSkillIndex.QuickShot:
                nodeManager.Combat.quickShotBow?.Trigger();
                break;

            case BowSkillIndex.SpreadArrow:
                nodeManager.Combat.spreadArrowBow?.Trigger();
                break;

            case BowSkillIndex.FullDraw:
                nodeManager.Combat.StartBowFullDrawNormal();
                break;

            case BowSkillIndex.FullDrawFullCharge:
                nodeManager.Combat.StartBowFullDrawFullCharge();
                break;

            case BowSkillIndex.ConcussiveShot:
                nodeManager.Combat.StartConcussive();
                break;
        }
    }

    private float GetSkillRange(int skillId, float fallback)
    {
        if (nodeManager == null || nodeManager.Combat == null)
            return fallback;

        switch ((BowSkillIndex)skillId)
        {
            case BowSkillIndex.QuickShot:
                return nodeManager.Combat.quickShotBow != null
                    ? nodeManager.Combat.quickShotBow.skillRange
                    : fallback;

            case BowSkillIndex.SpreadArrow:
                return nodeManager.Combat.spreadArrowBow != null
                    ? nodeManager.Combat.spreadArrowBow.skillRange
                    : fallback;

            case BowSkillIndex.FullDraw:
                return nodeManager.Combat.fullDrawBow != null
                    ? nodeManager.Combat.fullDrawBow.GetRangeForMode(Enemy_Bow_FullDraw.FullDrawMode.Normal)
                    : fallback;

            case BowSkillIndex.FullDrawFullCharge:
                return nodeManager.Combat.fullDrawBow != null
                    ? nodeManager.Combat.fullDrawBow.GetRangeForMode(Enemy_Bow_FullDraw.FullDrawMode.FullChargePiercing)
                    : fallback;

            case BowSkillIndex.ConcussiveShot:
                return nodeManager.Combat.concussiveBow != null
                    ? nodeManager.Combat.concussiveBow.skillRange
                    : fallback;

            default:
                return fallback;
        }
    }

    private float GetSkillMinimumRange(int skillId, float fallback)
    {
        if (nodeManager == null || nodeManager.Combat == null)
            return fallback;

        switch ((BowSkillIndex)skillId)
        {
            case BowSkillIndex.QuickShot:
                return nodeManager.Combat.quickShotBow != null
                    ? nodeManager.Combat.quickShotBow.minRange
                    : fallback;

            case BowSkillIndex.SpreadArrow:
                return nodeManager.Combat.spreadArrowBow != null
                    ? nodeManager.Combat.spreadArrowBow.minRange
                    : fallback;

            case BowSkillIndex.FullDraw:
                return nodeManager.Combat.fullDrawBow != null
                    ? nodeManager.Combat.fullDrawBow.GetMinRangeForMode(Enemy_Bow_FullDraw.FullDrawMode.Normal)
                    : fallback;

            case BowSkillIndex.FullDrawFullCharge:
                return nodeManager.Combat.fullDrawBow != null
                    ? nodeManager.Combat.fullDrawBow.GetMinRangeForMode(Enemy_Bow_FullDraw.FullDrawMode.FullChargePiercing)
                    : fallback;

            case BowSkillIndex.ConcussiveShot:
                return nodeManager.Combat.concussiveBow != null
                    ? nodeManager.Combat.concussiveBow.minRange
                    : fallback;

            default:
                return fallback;
        }
    }

    private void RefillBowSkillBag()
    {
        bowSkillBag.Clear();

        if (nodeManager != null && nodeManager.Combat != null)
        {
            if (nodeManager.Combat.quickShotBow != null)
                bowSkillBag.Add((int)BowSkillIndex.QuickShot);

            if (nodeManager.Combat.spreadArrowBow != null)
                bowSkillBag.Add((int)BowSkillIndex.SpreadArrow);

            if (nodeManager.Combat.fullDrawBow != null)
            {
                bowSkillBag.Add((int)BowSkillIndex.FullDraw);
                bowSkillBag.Add((int)BowSkillIndex.FullDrawFullCharge);
            }

            /*
             * Balanced bag juga boleh memasukkan Concussive jika memang ingin uji semua skill.
             * Namun default useBowBalancedBag biasanya false, sehingga DDA tetap menjadi sumber utama.
             */
            if (nodeManager.Combat.concussiveBow != null)
                bowSkillBag.Add((int)BowSkillIndex.ConcussiveShot);
        }

        for (int i = 0; i < bowSkillBag.Count; i++)
        {
            int j = Random.Range(i, bowSkillBag.Count);
            int temp = bowSkillBag[i];
            bowSkillBag[i] = bowSkillBag[j];
            bowSkillBag[j] = temp;
        }

        if (bowSkillBag.Count > 1 && bowSkillBag[0] == lastBowSkillId)
        {
            int temp = bowSkillBag[0];
            bowSkillBag[0] = bowSkillBag[1];
            bowSkillBag[1] = temp;
        }
    }

    private void ApplyFallbackWeightsToCache()
    {
        currentWeights[(int)BowSkillIndex.QuickShot] = nodeManager != null &&
                                                       nodeManager.Combat != null &&
                                                       nodeManager.Combat.quickShotBow != null
            ? Mathf.Max(0f, fallbackQuickWeight)
            : 0f;

        currentWeights[(int)BowSkillIndex.SpreadArrow] = nodeManager != null &&
                                                        nodeManager.Combat != null &&
                                                        nodeManager.Combat.spreadArrowBow != null
            ? Mathf.Max(0f, fallbackSpreadWeight)
            : 0f;

        currentWeights[(int)BowSkillIndex.FullDraw] = nodeManager != null &&
                                                     nodeManager.Combat != null &&
                                                     nodeManager.Combat.fullDrawBow != null
            ? Mathf.Max(0f, fallbackFullDrawWeight)
            : 0f;

        currentWeights[(int)BowSkillIndex.FullDrawFullCharge] = nodeManager != null &&
                                                               nodeManager.Combat != null &&
                                                               nodeManager.Combat.fullDrawBow != null
            ? Mathf.Max(0f, fallbackFullDrawFullChargeWeight)
            : 0f;

        currentWeights[(int)BowSkillIndex.ConcussiveShot] = nodeManager != null &&
                                                           nodeManager.Combat != null &&
                                                           nodeManager.Combat.concussiveBow != null
            ? Mathf.Max(0f, fallbackConcussiveWeight)
            : 0f;
    }

    private float GetTotalWeight()
    {
        float total = 0f;

        for (int i = 0; i < currentWeights.Count; i++)
            total += Mathf.Max(0f, currentWeights[i]);

        return total;
    }

    private string GetSkillName(int index)
    {
        if (index < 0)
            return "None";

        switch ((BowSkillIndex)index)
        {
            case BowSkillIndex.QuickShot:
                return "QuickShot";

            case BowSkillIndex.SpreadArrow:
                return "SpreadArrow";

            case BowSkillIndex.FullDraw:
                return "FullDraw";

            case BowSkillIndex.FullDrawFullCharge:
                return "FullDrawFullCharge";

            case BowSkillIndex.ConcussiveShot:
                return "ConcussiveShot";

            default:
                return "Unknown";
        }
    }

    public Vector2 GetAttackRangeCenter()
    {
        return (Vector2)transform.position + attackRangeOffset;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, bowSenseRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(GetAttackRangeCenter(), attackRange);
    }
}