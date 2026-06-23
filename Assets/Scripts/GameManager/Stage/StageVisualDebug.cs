using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MODUL KOMPOSISI (Utilitas).
///
/// Mengurus seluruh VISUAL DEBUG runtime:
/// <list type="bullet">
/// <item>Panel <c>OnGUI</c> ("STAGE RUNTIME DEBUG") berisi ringkasan stage, token, dan daftar enemy.</item>
/// <item>Label dunia mengambang di atas tiap enemy (status token + perubahan stat).</item>
/// <item>Antrian <see cref="StageFloatingDebugLine"/> (log event yang otomatis kedaluwarsa).</item>
/// <item>Gizmos opsional di Scene view (spawn point &amp; garis transisi stage) untuk mempermudah setup.</item>
/// </list>
///
/// Dipasang pada GameObject yang sama dengan <see cref="StageManager"/> (KOMPOSISI).
/// Membaca data dari <see cref="StageManager"/> dan <see cref="StageStatManager"/>; tidak menyimpan
/// logika gameplay apa pun.
/// </summary>
[DisallowMultipleComponent]
public class StageVisualDebug : MonoBehaviour
{
    [Serializable]
    public struct StageFloatingDebugLine
    {
        public string message;
        public float createdAt;
    }

    [Header("Visual Debug Overlay")]
    public bool showStageRuntimeDebug = true;
    public bool showEnemyWorldDebugLabels = true;
    public KeyCode toggleDebugOverlayKey = KeyCode.F6;
    public Vector2 debugPanelPosition = new Vector2(16f, 16f);
    public float debugPanelWidth = 430f;
    public int maxEnemyRowsInDebugPanel = 14;
    public int maxFloatingDebugLines = 12;
    public float floatingDebugLineDuration = 8f;

    [Header("Scene Gizmos (Editor saja, tidak memengaruhi game)")]
    [Tooltip("Jika true, menggambar gizmo spawn point minion/boss dan garis rightTransitionX di Scene view.")]
    public bool drawSceneGizmos = false;

    private readonly List<StageFloatingDebugLine> floatingDebugLines = new List<StageFloatingDebugLine>();

    private GUIStyle debugPanelStyle;
    private GUIStyle debugHeaderStyle;
    private GUIStyle debugTextStyle;
    private GUIStyle debugWarningStyle;
    private GUIStyle worldDebugLabelStyle;

    private StageManager manager;

    // ---------------------------------------------------------------------
    // LIFECYCLE / SETUP
    // ---------------------------------------------------------------------

    public void Initialize(StageManager owner)
    {
        manager = owner;
    }

    public void ResetAll()
    {
        floatingDebugLines.Clear();
    }

    private void Update()
    {
        // Toggle overlay (sebelumnya ditangani StageManager.Update).
        if (Input.GetKeyDown(toggleDebugOverlayKey))
        {
            showStageRuntimeDebug = !showStageRuntimeDebug;
            showEnemyWorldDebugLabels = showStageRuntimeDebug;
        }
    }

    // ---------------------------------------------------------------------
    // FLOATING DEBUG LINES
    // ---------------------------------------------------------------------

    public void AddFloatingDebugLine(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        floatingDebugLines.Add(new StageFloatingDebugLine
        {
            message = message,
            createdAt = Time.time
        });

        while (floatingDebugLines.Count > maxFloatingDebugLines)
        {
            floatingDebugLines.RemoveAt(0);
        }
    }

    public void PruneFloatingLines()
    {
        float now = Time.time;

        for (int i = floatingDebugLines.Count - 1; i >= 0; i--)
        {
            if (now - floatingDebugLines[i].createdAt > floatingDebugLineDuration)
            {
                floatingDebugLines.RemoveAt(i);
            }
        }
    }

    // ---------------------------------------------------------------------
    // ON GUI
    // ---------------------------------------------------------------------

    private void OnGUI()
    {
        if (manager == null)
            return;

        if (!showStageRuntimeDebug && !showEnemyWorldDebugLabels)
            return;

        InitializeDebugGUIStyles();

        if (showStageRuntimeDebug)
        {
            DrawStageDebugPanel();
        }

        if (showEnemyWorldDebugLabels)
        {
            DrawEnemyWorldDebugLabels();
        }
    }

    private void InitializeDebugGUIStyles()
    {
        if (debugTextStyle != null)
            return;

        debugPanelStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(12, 12, 10, 10)
        };

        debugHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        debugTextStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            normal = { textColor = Color.white },
            wordWrap = true
        };

        debugWarningStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.yellow },
            wordWrap = true
        };

        worldDebugLabelStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
            wordWrap = true
        };
    }

    private void DrawStageDebugPanel()
    {
        StageStatManager stats = manager.Stats;
        if (stats == null)
            return;

        int activeTokens = stats.ActiveConcurrentAttackTokens;
        int tokenLimit = stats.ConcurrentAttackTokenLimit;

        float panelHeight = Mathf.Min(Screen.height - debugPanelPosition.y - 12f, 520f);

        GUILayout.BeginArea(
            new Rect(debugPanelPosition.x, debugPanelPosition.y, debugPanelWidth, panelHeight),
            debugPanelStyle
        );

        GUILayout.Label("STAGE RUNTIME DEBUG", debugHeaderStyle);
        GUILayout.Label($"Toggle: {toggleDebugOverlayKey} | State: {manager.CurrentState}", debugTextStyle);
        GUILayout.Space(4f);

        GUILayout.Label(
            $"Stage Tampil: {manager.GetDisplayedStageNumber()} | Stage Internal: {manager.CurrentStageInternal}",
            debugTextStyle
        );
        GUILayout.Label($"Playstyle DDA: {stats.lastStagePlaystyle}", debugTextStyle);
        GUILayout.Label(
            $"Spawn Token: {stats.lastStageTotalMinions} | Melee: {stats.lastStageMeleeCount} | Range: {stats.lastStageRangeCount}",
            debugTextStyle
        );
        GUILayout.Label(
            $"Stat Bertambah: x{stats.lastStageStatMultiplier:0.00} (+{stats.GetStatIncreasePercent(stats.lastStageStatMultiplier):0.#}%)",
            debugWarningStyle
        );
        GUILayout.Label(
            $"Token Serangan Bersamaan: {activeTokens}/{stats.lastStageMinionAttackTokens} aktif",
            debugWarningStyle
        );

        CharacterBase playerCharacter = manager.GetPlayerCharacter();

        if (playerCharacter != null)
        {
            GUILayout.Label(
                $"Player HP: {playerCharacter.currentHP:0.#}/{playerCharacter.maxHP:0.#} | Regen Terakhir: +{stats.lastPlayerRegenAmount:0.#}",
                debugTextStyle
            );
        }
        else
        {
            GUILayout.Label("Player HP: CharacterBase tidak ditemukan.", debugWarningStyle);
        }

        GUILayout.Label(
            "Makna Token: batas minion yang boleh berada di state Attack secara bersamaan",
            debugTextStyle
        );
        GUILayout.Label($"Token Debug: {stats.LastTokenDebugMessage}", debugTextStyle);
        GUILayout.Space(6f);

        GUILayout.Label("Enemy Runtime", debugHeaderStyle);

        int shownRows = 0;

        foreach (StageStatManager.StageEnemyRuntimeDebugData data in stats.EnemyRuntimeData)
        {
            if (!stats.IsValidAliveRuntimeEnemy(data))
                continue;

            if (shownRows >= maxEnemyRowsInDebugPanel)
            {
                GUILayout.Label("...enemy lain disembunyikan karena batas baris debug.", debugTextStyle);
                break;
            }

            string enemyType = data.isBoss ? "BOSS" : "MINION";
            string tokenText = data.isBoss
                ? "Token: boss bypass"
                : data.isHoldingConcurrentAttackToken
                    ? $"Token: ATTACKING ({activeTokens}/{tokenLimit})"
                    : $"Token: waiting/free ({activeTokens}/{tokenLimit})";

            GUILayout.Label(
                $"{enemyType} {data.enemy.name} | {tokenText} | HP {data.character.currentHP:0.#}/{data.character.maxHP:0.#} | ATK {data.character.attack:0.#}",
                data.isHoldingConcurrentAttackToken && !data.isBoss ? debugWarningStyle : debugTextStyle
            );
            GUILayout.Label(
                $"  Stat naik: HP {data.beforeStats.maxHP:0.#}->{data.afterStats.maxHP:0.#} | " +
                $"ATK {data.beforeStats.attack:0.#}->{data.afterStats.attack:0.#} | " +
                $"DEF {data.beforeStats.defense:0.#}->{data.afterStats.defense:0.#} | " +
                $"SPD {data.beforeStats.moveSpeed:0.#}->{data.afterStats.moveSpeed:0.#}",
                debugTextStyle
            );

            shownRows++;
        }

        if (shownRows == 0)
        {
            GUILayout.Label("Tidak ada enemy hidup yang terdaftar.", debugTextStyle);
        }

        GUILayout.Space(6f);
        GUILayout.Label("Floating Debug", debugHeaderStyle);

        foreach (StageFloatingDebugLine line in floatingDebugLines)
        {
            float age = Time.time - line.createdAt;
            GUILayout.Label($"[{age:0.0}s] {line.message}", debugTextStyle);
        }

        GUILayout.EndArea();
    }

    private void DrawEnemyWorldDebugLabels()
    {
        StageStatManager stats = manager.Stats;
        if (stats == null)
            return;

        Camera mainCamera = Camera.main;

        if (mainCamera == null)
            return;

        int activeTokens = stats.ActiveConcurrentAttackTokens;
        int tokenLimit = stats.ConcurrentAttackTokenLimit;

        foreach (StageStatManager.StageEnemyRuntimeDebugData data in stats.EnemyRuntimeData)
        {
            if (!stats.IsValidAliveRuntimeEnemy(data))
                continue;

            Vector3 worldPosition = data.enemy.transform.position + Vector3.up * 1.7f;
            Vector3 screenPosition = mainCamera.WorldToScreenPoint(worldPosition);

            if (screenPosition.z < 0f)
                continue;

            float labelWidth = data.isBoss ? 190f : 160f;
            float labelHeight = data.isBoss ? 112f : 122f;
            float x = screenPosition.x - (labelWidth * 0.5f);
            float y = Screen.height - screenPosition.y - labelHeight;

            string labelText;

            if (data.isBoss)
            {
                labelText =
                    $"BOSS\n" +
                    $"HP {data.character.currentHP:0.#}/{data.character.maxHP:0.#}\n" +
                    $"Stat x{data.statMultiplier:0.00} (+{stats.GetStatIncreasePercent(data.statMultiplier):0.#}%)\n" +
                    $"HP+ {data.beforeStats.maxHP:0.#}->{data.afterStats.maxHP:0.#}\n" +
                    $"ATK {data.beforeStats.attack:0.#}->{data.character.attack:0.#}";
            }
            else
            {
                labelText =
                    $"MINION\n" +
                    $"Token {(data.isHoldingConcurrentAttackToken ? "ATTACKING" : "READY/WAIT")}\n" +
                    $"Slot {activeTokens}/{tokenLimit}\n" +
                    $"HP {data.character.currentHP:0.#}/{data.character.maxHP:0.#}\n" +
                    $"Stat x{data.statMultiplier:0.00} (+{stats.GetStatIncreasePercent(data.statMultiplier):0.#}%)\n" +
                    $"HP+ {data.beforeStats.maxHP:0.#}->{data.afterStats.maxHP:0.#}\n" +
                    $"ATK {data.beforeStats.attack:0.#}->{data.character.attack:0.#}";
            }

            GUI.Label(new Rect(x, y, labelWidth, labelHeight), labelText, worldDebugLabelStyle);
        }
    }

    // ---------------------------------------------------------------------
    // GIZMOS (Scene view editor saja — tidak memengaruhi output game)
    // ---------------------------------------------------------------------

    private void OnDrawGizmos()
    {
        if (!drawSceneGizmos)
            return;

        StageManager mgr = manager != null ? manager : GetComponent<StageManager>();
        if (mgr == null)
            return;

        // Spawn point minion.
        Gizmos.color = Color.cyan;
        if (mgr.minionSpawnPoints != null)
        {
            foreach (Transform sp in mgr.minionSpawnPoints)
            {
                if (sp != null)
                    Gizmos.DrawWireSphere(sp.position, 0.4f);
            }
        }

        // Spawn point boss.
        Gizmos.color = Color.red;
        if (mgr.bossSpawnPoint != null)
            Gizmos.DrawWireCube(mgr.bossSpawnPoint.position, Vector3.one * 0.8f);

        // Spawn point player.
        Gizmos.color = Color.green;
        if (mgr.playerSpawnPoint != null)
            Gizmos.DrawWireSphere(mgr.playerSpawnPoint.position, 0.5f);

        // Garis batas transisi stage (rightTransitionX).
        Gizmos.color = Color.yellow;
        float refY = mgr.playerSpawnPoint != null ? mgr.playerSpawnPoint.position.y : transform.position.y;
        Gizmos.DrawLine(
            new Vector3(mgr.rightTransitionX, refY - 5f, 0f),
            new Vector3(mgr.rightTransitionX, refY + 5f, 0f)
        );
    }
}