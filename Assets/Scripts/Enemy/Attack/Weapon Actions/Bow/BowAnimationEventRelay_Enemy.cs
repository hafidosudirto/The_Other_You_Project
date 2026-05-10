using System;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class BowAnimationEventRelay_Enemy : MonoBehaviour
{
    [Header("Bow Skills Enemy")]
    public MonoBehaviour quickShot;
    public MonoBehaviour fullDraw;
    public MonoBehaviour spreadArrow;
    public MonoBehaviour concussiveShot;

    [Header("Auto Assign")]
    [SerializeField] private bool autoAssignOnAwake = true;
    [SerializeField] private bool autoAssignBeforeEveryEvent = true;

    [Header("Debug")]
    [SerializeField] private bool debugEvent = false;
    [SerializeField] private bool warningIfMissing = true;

    private static readonly string[] QuickShotMethods =
    {
        "ReleaseFromAnimationEvent",
        "ReleaseQuickShot",
        "Release",
        "Shoot"
    };

    private static readonly string[] FullDrawMethods =
    {
        "ReleaseFromAnimationEvent",
        "ReleaseFullDraw",
        "Release",
        "Shoot"
    };

    private static readonly string[] SpreadStartBackDashMethods =
    {
        "StartBackDash",
        "StartBackDashFromAnimationEvent",
        "BeginBackDash",
        "BeginBackDashFromAnimationEvent"
    };

    private static readonly string[] SpreadReleaseMethods =
    {
        "ReleaseSpreadArrow",
        "ReleaseFromAnimationEvent",
        "ReleaseSpread",
        "Release",
        "Shoot"
    };

    private static readonly string[] SpreadEndRecoveryMethods =
    {
        "EndSpreadArrowRecovery",
        "EndRecoveryFromAnimationEvent",
        "EndRecovery",
        "FinishRecovery",
        "Finish"
    };

    private static readonly string[] ConcussiveStartPopMethods =
    {
        "StartPopFromAnimationEvent",
        "StartPop",
        "BeginPop",
        "BeginPopFromAnimationEvent"
    };

    private static readonly string[] ConcussiveReleaseMethods =
    {
        "ReleaseFromAnimationEvent",
        "ReleaseConcussive",
        "Release",
        "Shoot"
    };

    private static readonly string[] ConcussiveEndRecoveryMethods =
    {
        "EndRecoveryFromAnimationEvent",
        "EndConcussiveRecovery",
        "EndRecovery",
        "FinishRecovery",
        "Finish"
    };

    private void Awake()
    {
        if (autoAssignOnAwake)
            AutoAssign();
    }

    private void OnEnable()
    {
        if (autoAssignOnAwake)
            AutoAssign();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
            AutoAssign();
    }
#endif

    [ContextMenu("Auto Assign Enemy Bow Skills")]
    public void AutoAssign()
    {
        EnemyCombatController combat = GetComponentInParent<EnemyCombatController>(true);

        if (combat != null)
        {
            quickShot = ResolveCombatMemberIfCompatible(
                combat,
                quickShot,
                QuickShotMethods,
                "quickShotBow",
                "quickShot",
                "enemyQuickShot"
            );

            fullDraw = ResolveCombatMemberIfCompatible(
                combat,
                fullDraw,
                FullDrawMethods,
                "fullDrawBow",
                "fullDraw",
                "enemyFullDraw"
            );

            spreadArrow = ResolveCombatMemberIfCompatible(
                combat,
                spreadArrow,
                SpreadReleaseMethods,
                "spreadArrowBow",
                "spreadBow",
                "spreadArrow",
                "spreadShotBow",
                "spreadShot"
            );

            concussiveShot = ResolveCombatMemberIfCompatible(
                combat,
                concussiveShot,
                ConcussiveReleaseMethods,
                "concussiveBow",
                "concussiveShot",
                "enemyConcussive"
            );
        }

        if (!HasAnyMethod(quickShot, QuickShotMethods))
        {
            quickShot = FindCompatibleBehaviour(
                QuickShotMethods,
                "Enemy_Bow_QuickShot"
            );
        }

        if (!HasAnyMethod(fullDraw, FullDrawMethods))
        {
            fullDraw = FindCompatibleBehaviour(
                FullDrawMethods,
                "Enemy_Bow_FullDraw"
            );
        }

        /*
         * Bagian penting:
         * Spread Arrow tidak boleh memakai Enemy_SkillBase jika base itu
         * tidak punya ReleaseSpreadArrow / StartBackDash / EndSpreadArrowRecovery.
         */
        if (!HasAnyMethod(spreadArrow, SpreadReleaseMethods))
        {
            spreadArrow = FindCompatibleBehaviour(
                SpreadReleaseMethods,
                "Enemy_Bow_SpreadArrow",
                "Enemy_Bow_SpreadShot",
                "Enemy_Bow_Spread"
            );
        }

        if (!HasAnyMethod(concussiveShot, ConcussiveReleaseMethods))
        {
            concussiveShot = FindCompatibleBehaviour(
                ConcussiveReleaseMethods,
                "Enemy_Bow_ConcussiveShot"
            );
        }
    }

    // =========================================================
    // ANIMATION EVENTS - BOW
    // =========================================================

    public void AE_BowQuickShot_Release()
    {
        LogEvent(nameof(AE_BowQuickShot_Release));

        InvokeSkillMethod(
            ref quickShot,
            nameof(AE_BowQuickShot_Release),
            QuickShotMethods,
            "Enemy_Bow_QuickShot"
        );
    }

    public void AE_BowFullDraw_Release()
    {
        LogEvent(nameof(AE_BowFullDraw_Release));

        InvokeSkillMethod(
            ref fullDraw,
            nameof(AE_BowFullDraw_Release),
            FullDrawMethods,
            "Enemy_Bow_FullDraw"
        );
    }

    public void AE_BowSpreadArrow_StartBackDash()
    {
        LogEvent(nameof(AE_BowSpreadArrow_StartBackDash));

        InvokeSkillMethod(
            ref spreadArrow,
            nameof(AE_BowSpreadArrow_StartBackDash),
            SpreadStartBackDashMethods,
            "Enemy_Bow_SpreadArrow",
            "Enemy_Bow_SpreadShot",
            "Enemy_Bow_Spread"
        );
    }

    public void AE_BowSpreadArrow_Release()
    {
        LogEvent(nameof(AE_BowSpreadArrow_Release));

        InvokeSkillMethod(
            ref spreadArrow,
            nameof(AE_BowSpreadArrow_Release),
            SpreadReleaseMethods,
            "Enemy_Bow_SpreadArrow",
            "Enemy_Bow_SpreadShot",
            "Enemy_Bow_Spread"
        );
    }

    public void AE_BowSpreadArrow_EndRecovery()
    {
        LogEvent(nameof(AE_BowSpreadArrow_EndRecovery));

        InvokeSkillMethod(
            ref spreadArrow,
            nameof(AE_BowSpreadArrow_EndRecovery),
            SpreadEndRecoveryMethods,
            "Enemy_Bow_SpreadArrow",
            "Enemy_Bow_SpreadShot",
            "Enemy_Bow_Spread"
        );
    }

    public void AE_BowConcussive_StartPop()
    {
        LogEvent(nameof(AE_BowConcussive_StartPop));

        InvokeSkillMethod(
            ref concussiveShot,
            nameof(AE_BowConcussive_StartPop),
            ConcussiveStartPopMethods,
            "Enemy_Bow_ConcussiveShot"
        );
    }

    public void AE_BowConcussive_Release()
    {
        LogEvent(nameof(AE_BowConcussive_Release));

        InvokeSkillMethod(
            ref concussiveShot,
            nameof(AE_BowConcussive_Release),
            ConcussiveReleaseMethods,
            "Enemy_Bow_ConcussiveShot"
        );
    }

    public void AE_BowConcussive_EndRecovery()
    {
        LogEvent(nameof(AE_BowConcussive_EndRecovery));

        InvokeSkillMethod(
            ref concussiveShot,
            nameof(AE_BowConcussive_EndRecovery),
            ConcussiveEndRecoveryMethods,
            "Enemy_Bow_ConcussiveShot"
        );
    }

    // =========================================================
    // ALIAS LAMA / KOMPATIBILITAS
    // =========================================================

    public void AE_QuickShotRelease()
    {
        AE_BowQuickShot_Release();
    }

    public void AE_FullDrawRelease()
    {
        AE_BowFullDraw_Release();
    }

    public void AE_ConcussiveRelease()
    {
        AE_BowConcussive_Release();
    }

    public void AE_PiercingRelease()
    {
        AE_BowSpreadArrow_Release();
    }

    public void AE_BowPiercing_Release()
    {
        AE_BowSpreadArrow_Release();
    }

    public void AE_BowPiercingStandalone_Release()
    {
        AE_BowSpreadArrow_Release();
    }

    // =========================================================
    // INTERNAL
    // =========================================================

    private void InvokeSkillMethod(
        ref MonoBehaviour target,
        string eventName,
        string[] methodNames,
        params string[] preferredTypeNames
    )
    {
        if (autoAssignBeforeEveryEvent)
            AutoAssign();

        if (!HasAnyMethod(target, methodNames))
        {
            target = FindCompatibleBehaviour(methodNames, preferredTypeNames);
        }

        if (target == null)
        {
            if (warningIfMissing)
            {
                Debug.LogWarning(
                    $"[BowAnimationEventRelay_Enemy] {eventName} gagal. Target skill tidak ditemukan atau belum punya method kompatibel.",
                    this
                );
            }

            return;
        }

        Type type = target.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int i = 0; i < methodNames.Length; i++)
        {
            MethodInfo method = type.GetMethod(methodNames[i], flags, null, Type.EmptyTypes, null);

            if (method == null)
                continue;

            try
            {
                method.Invoke(target, null);

                if (debugEvent)
                {
                    Debug.Log(
                        $"[BowAnimationEventRelay_Enemy] {eventName} -> {type.Name}.{method.Name}()",
                        this
                    );
                }

                return;
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[BowAnimationEventRelay_Enemy] Error saat menjalankan {type.Name}.{method.Name}() dari {eventName}: {e.Message}",
                    target
                );

                return;
            }
        }

        if (warningIfMissing)
        {
            Debug.LogWarning(
                $"[BowAnimationEventRelay_Enemy] {eventName} gagal. Tidak ada method kompatibel pada {type.Name}.",
                target
            );
        }
    }

    private MonoBehaviour ResolveCombatMemberIfCompatible(
        object source,
        MonoBehaviour current,
        string[] requiredMethods,
        params string[] memberNames
    )
    {
        if (HasAnyMethod(current, requiredMethods))
            return current;

        MonoBehaviour candidate = GetMemberAsBehaviour(source, memberNames);

        if (HasAnyMethod(candidate, requiredMethods))
            return candidate;

        /*
         * Jika field combat menunjuk ke Enemy_SkillBase, jangan langsung dipakai.
         * Cari komponen konkret di object yang sama atau anak-anaknya.
         */
        if (candidate != null)
        {
            MonoBehaviour nestedCandidate = FindCompatibleBehaviourInRoot(
                candidate.transform,
                requiredMethods
            );

            if (nestedCandidate != null)
                return nestedCandidate;
        }

        return current;
    }

    private MonoBehaviour GetMemberAsBehaviour(object source, params string[] memberNames)
    {
        if (source == null)
            return null;

        Type type = source.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int i = 0; i < memberNames.Length; i++)
        {
            FieldInfo field = type.GetField(memberNames[i], flags);

            if (field != null)
            {
                MonoBehaviour behaviour = field.GetValue(source) as MonoBehaviour;

                if (behaviour != null)
                    return behaviour;
            }

            PropertyInfo property = type.GetProperty(memberNames[i], flags);

            if (property != null && property.CanRead)
            {
                MonoBehaviour behaviour = property.GetValue(source, null) as MonoBehaviour;

                if (behaviour != null)
                    return behaviour;
            }
        }

        return null;
    }

    private MonoBehaviour FindCompatibleBehaviour(string[] requiredMethods, params string[] preferredTypeNames)
    {
        Transform root = transform.root;

        MonoBehaviour found = FindCompatibleBehaviourInRoot(
            transform,
            requiredMethods,
            preferredTypeNames
        );

        if (found != null)
            return found;

        if (root != null && root != transform)
        {
            found = FindCompatibleBehaviourInRoot(
                root,
                requiredMethods,
                preferredTypeNames
            );

            if (found != null)
                return found;
        }

        return null;
    }

    private MonoBehaviour FindCompatibleBehaviourInRoot(
        Transform searchRoot,
        string[] requiredMethods,
        params string[] preferredTypeNames
    )
    {
        if (searchRoot == null)
            return null;

        MonoBehaviour[] behaviours = searchRoot.GetComponentsInChildren<MonoBehaviour>(true);

        /*
         * Prioritas 1:
         * Cari berdasarkan nama class yang diharapkan dan method yang benar.
         */
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null)
                continue;

            if (!IsPreferredType(behaviour, preferredTypeNames))
                continue;

            if (HasAnyMethod(behaviour, requiredMethods))
                return behaviour;
        }

        /*
         * Prioritas 2:
         * Cari komponen apa pun yang punya method yang dibutuhkan.
         * Ini membuat relay tetap aman walaupun nama class berubah.
         */
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null)
                continue;

            if (HasAnyMethod(behaviour, requiredMethods))
                return behaviour;
        }

        return null;
    }

    private bool IsPreferredType(MonoBehaviour behaviour, params string[] preferredTypeNames)
    {
        if (behaviour == null || preferredTypeNames == null || preferredTypeNames.Length == 0)
            return false;

        string typeName = behaviour.GetType().Name;

        for (int i = 0; i < preferredTypeNames.Length; i++)
        {
            if (typeName == preferredTypeNames[i])
                return true;
        }

        return false;
    }

    private bool HasAnyMethod(MonoBehaviour target, params string[] methodNames)
    {
        if (target == null || methodNames == null)
            return false;

        Type type = target.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        for (int i = 0; i < methodNames.Length; i++)
        {
            if (type.GetMethod(methodNames[i], flags, null, Type.EmptyTypes, null) != null)
                return true;
        }

        return false;
    }

    private void LogEvent(string eventName)
    {
        if (!debugEvent)
            return;

        Debug.Log($"[BowAnimationEventRelay_Enemy] {eventName}", this);
    }
}