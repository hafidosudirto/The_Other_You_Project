using System;
using System.Reflection;
using UnityEngine;

[DisallowMultipleComponent]
public class BowAnimationEventRelay_Enemy : MonoBehaviour
{
    [Header("Bow Skills Enemy")]
    [Tooltip("Isi dengan komponen Enemy_Bow_QuickShot.")]
    public MonoBehaviour quickShot;

    [Tooltip("Isi dengan komponen Enemy_Bow_FullDraw.")]
    public MonoBehaviour fullDraw;

    [Tooltip("Isi dengan komponen Spread Arrow enemy. Jika belum ada class khusus SpreadArrow enemy, boleh sementara isi dengan komponen lama Enemy_Bow_PiercingShot sebagai fallback.")]
    public MonoBehaviour spreadArrow;

    [Tooltip("Isi dengan komponen Enemy_Bow_ConcussiveShot.")]
    public MonoBehaviour concussiveShot;

    [Header("Fallback / Compatibility")]
    [Tooltip("Jika SpreadArrow belum ditemukan, relay boleh memakai piercing lama sebagai fallback release.")]
    [SerializeField] private bool allowPiercingAsSpreadFallback = true;

    [Tooltip("Jika aktif, relay akan mencoba mencari referensi skill otomatis dari EnemyCombatController dan child SkillRoot_Bow.")]
    [SerializeField] private bool autoAssignOnAwake = true;

    [Header("Debug")]
    [SerializeField] private bool debugEvent = true;
    [SerializeField] private bool warningIfMissing = true;

    private void Awake()
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
            if (quickShot == null)
                quickShot = GetMemberAsBehaviour(combat, "quickShotBow", "quickShot", "enemyQuickShot");

            if (fullDraw == null)
                fullDraw = GetMemberAsBehaviour(combat, "fullDrawBow", "fullDraw", "enemyFullDraw");

            if (spreadArrow == null)
                spreadArrow = GetMemberAsBehaviour(
                    combat,
                    "spreadArrowBow",
                    "spreadBow",
                    "spreadArrow",
                    "spreadShotBow",
                    "spreadShot"
                );

            if (concussiveShot == null)
                concussiveShot = GetMemberAsBehaviour(combat, "concussiveBow", "concussiveShot", "enemyConcussive");

            if (allowPiercingAsSpreadFallback && spreadArrow == null)
                spreadArrow = GetMemberAsBehaviour(combat, "piercingBow", "piercingShot", "enemyPiercing");
        }

        if (quickShot == null)
            quickShot = FindBehaviourByTypeName("Enemy_Bow_QuickShot");

        if (fullDraw == null)
            fullDraw = FindBehaviourByTypeName("Enemy_Bow_FullDraw");

        if (spreadArrow == null)
        {
            spreadArrow = FindBehaviourByTypeName(
                "Enemy_Bow_SpreadArrow",
                "Enemy_Bow_SpreadShot",
                "Enemy_Bow_Spread"
            );
        }

        if (allowPiercingAsSpreadFallback && spreadArrow == null)
            spreadArrow = FindBehaviourByTypeName("Enemy_Bow_PiercingShot");

        if (concussiveShot == null)
            concussiveShot = FindBehaviourByTypeName("Enemy_Bow_ConcussiveShot");
    }

    // =========================================================
    // EVENT NAMA BARU - SESUAI KLIP PLAYER YANG ANDA MIRROR
    // =========================================================

    public void AE_BowQuickShot_Release()
    {
        LogEvent(nameof(AE_BowQuickShot_Release));

        InvokeSkillMethod(
            quickShot,
            nameof(AE_BowQuickShot_Release),
            "ReleaseFromAnimationEvent",
            "ReleaseQuickShot",
            "Release",
            "Shoot"
        );
    }

    public void AE_BowFullDraw_Release()
    {
        LogEvent(nameof(AE_BowFullDraw_Release));

        InvokeSkillMethod(
            fullDraw,
            nameof(AE_BowFullDraw_Release),
            "ReleaseFromAnimationEvent",
            "ReleaseFullDraw",
            "Release",
            "Shoot"
        );
    }

    public void AE_BowSpreadArrow_StartBackDash()
    {
        LogEvent(nameof(AE_BowSpreadArrow_StartBackDash));

        InvokeSkillMethod(
            spreadArrow,
            nameof(AE_BowSpreadArrow_StartBackDash),
            "StartBackDash",
            "StartBackDashFromAnimationEvent",
            "BeginBackDash",
            "BeginBackDashFromAnimationEvent"
        );
    }

    public void AE_BowSpreadArrow_Release()
    {
        LogEvent(nameof(AE_BowSpreadArrow_Release));

        InvokeSkillMethod(
            spreadArrow,
            nameof(AE_BowSpreadArrow_Release),
            "ReleaseSpreadArrow",
            "ReleaseFromAnimationEvent",
            "ReleaseSpread",
            "Release",
            "Shoot"
        );
    }

    public void AE_BowSpreadArrow_EndRecovery()
    {
        LogEvent(nameof(AE_BowSpreadArrow_EndRecovery));

        InvokeSkillMethod(
            spreadArrow,
            nameof(AE_BowSpreadArrow_EndRecovery),
            "EndSpreadArrowRecovery",
            "EndRecoveryFromAnimationEvent",
            "EndRecovery",
            "FinishRecovery",
            "Finish"
        );
    }

    public void AE_BowConcussive_StartPop()
    {
        LogEvent(nameof(AE_BowConcussive_StartPop));

        InvokeSkillMethod(
            concussiveShot,
            nameof(AE_BowConcussive_StartPop),
            "StartPopFromAnimationEvent",
            "StartPop",
            "BeginPop",
            "BeginPopFromAnimationEvent"
        );
    }

    public void AE_BowConcussive_Release()
    {
        LogEvent(nameof(AE_BowConcussive_Release));

        InvokeSkillMethod(
            concussiveShot,
            nameof(AE_BowConcussive_Release),
            "ReleaseFromAnimationEvent",
            "ReleaseConcussive",
            "Release",
            "Shoot"
        );
    }

    public void AE_BowConcussive_EndRecovery()
    {
        LogEvent(nameof(AE_BowConcussive_EndRecovery));

        InvokeSkillMethod(
            concussiveShot,
            nameof(AE_BowConcussive_EndRecovery),
            "EndRecoveryFromAnimationEvent",
            "EndConcussiveRecovery",
            "EndRecovery",
            "FinishRecovery",
            "Finish"
        );
    }

    // =========================================================
    // ALIAS LAMA - SUPAYA KLIP LAMA TIDAK LANGSUNG ERROR
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

    // Piercing lama diarahkan ke SpreadArrow.
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
    // INTERNAL UTILITIES
    // =========================================================

    private void InvokeSkillMethod(MonoBehaviour target, string eventName, params string[] methodNames)
    {
        if (target == null)
        {
            AutoAssign();

            target = ResolveTargetForEvent(eventName);
        }

        if (target == null)
        {
            if (warningIfMissing)
            {
                Debug.LogWarning(
                    $"[BowAnimationEventRelay_Enemy] {eventName} gagal. Referensi skill belum diisi pada {name}.",
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
                    this
                );

                return;
            }
        }

        if (warningIfMissing)
        {
            Debug.LogWarning(
                $"[BowAnimationEventRelay_Enemy] {eventName} gagal. Tidak ada method release yang cocok pada {type.Name}.",
                target
            );
        }
    }

    private MonoBehaviour ResolveTargetForEvent(string eventName)
    {
        if (eventName.Contains("QuickShot"))
            return quickShot;

        if (eventName.Contains("FullDraw"))
            return fullDraw;

        if (eventName.Contains("SpreadArrow") || eventName.Contains("Piercing"))
            return spreadArrow;

        if (eventName.Contains("Concussive"))
            return concussiveShot;

        return null;
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

    private MonoBehaviour FindBehaviourByTypeName(params string[] typeNames)
    {
        Transform root = transform.root;

        MonoBehaviour found = FindBehaviourByTypeNameInRoot(transform, typeNames);

        if (found != null)
            return found;

        if (root != null && root != transform)
            return FindBehaviourByTypeNameInRoot(root, typeNames);

        return null;
    }

    private MonoBehaviour FindBehaviourByTypeNameInRoot(Transform searchRoot, params string[] typeNames)
    {
        if (searchRoot == null)
            return null;

        MonoBehaviour[] behaviours = searchRoot.GetComponentsInChildren<MonoBehaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];

            if (behaviour == null)
                continue;

            string typeName = behaviour.GetType().Name;

            for (int j = 0; j < typeNames.Length; j++)
            {
                if (typeName == typeNames[j])
                    return behaviour;
            }
        }

        return null;
    }

    private void LogEvent(string eventName)
    {
        if (!debugEvent)
            return;

        Debug.Log($"[BowAnimationEventRelay_Enemy] {eventName}", this);
    }
}