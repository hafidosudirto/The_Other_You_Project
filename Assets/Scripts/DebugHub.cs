using UnityEngine;

public static class DebugHub
{
    // ======================
    //   TOGGLE PER CATEGORY
    // ======================

    public static bool ENABLE_SKILL = true;
    public static bool ENABLE_PARRY = true;
    public static bool ENABLE_DDA   = true;
    public static bool ENABLE_ENEMY = true;
    public static bool ENABLE_WARN  = true;
    public static bool ENABLE_SYS   = true;
    public static bool ENABLE_BOW   = true;   // NEW: khusus log Bow

    // ======================
    //   CATEGORY METHODS
    // ======================

    // Skill casting (Slash, Whirlwind, Charged Strike, Bow, Riposte CAST)
    public static void Skill(string msg)
    {
        if (!ENABLE_SKILL) return;
        Debug.Log($"<color=#5AFF5A>[Skill]</color> {msg}");
    }

    // Riposte & Parry (detailed)
    public static void Parry(string msg)
    {
        if (!ENABLE_PARRY) return;
        Debug.Log($"<color=#00E5FF>[Parry]</color> {msg}");
    }

    // DDA System (DataTracker activity)
    public static void DDA(string msg)
    {
        if (!ENABLE_DDA) return;
        Debug.Log($"<color=#FFD700>[DDA]</color> {msg}");
    }

    // Bow-specific logs (arrow spawn, velocity, damage, dll)
    public static void Bow(string msg)
    {
        if (!ENABLE_BOW) return;
        Debug.Log($"<color=#3CD2FF>[Bow]</color> {msg}");
    }

    // Enemy Logs (attack type, AI, alerts)
    public static void Enemy(string msg)
    {
        if (!ENABLE_ENEMY) return;
        Debug.Log($"<color=#FF6A6A>[Enemy]</color> {msg}");
    }

    // Warnings
    public static void Warning(string msg)
    {
        if (!ENABLE_WARN) return;
        Debug.LogWarning($"[Warning] {msg}");
    }

    // System logs (swap player, general system events)
    public static void System(string msg)
    {
        if (!ENABLE_SYS) return;
        Debug.Log($"<color=#B0B0B0>[System]</color> {msg}");
    }
}
