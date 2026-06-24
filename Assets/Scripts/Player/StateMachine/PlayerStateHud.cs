using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// HUD diagnostik HFSM pemain untuk Game View.
///
/// Komponen ini MURNI membaca PlayerStateController (read-only) lalu menggambar
/// lewat OnGUI, sehingga TIDAK butuh setup Canvas/prefab/TextMeshPro apa pun.
/// Cukup pasang komponen ini pada satu GameObject di scene (mis. GameObject
/// Player yang sama dengan PlayerStateController) dan ia akan otomatis mencari
/// referensinya.
///
/// Dua tampilan yang bisa difoto sebagai bukti:
///  - Label melayang di atas kepala pemain  -> untuk screenshot gameplay.
///  - Panel pojok layar (state aktif, transisi terakhir, pemicu, riwayat)
///    -> untuk screenshot teknis mirip Inspector tapi tampil in-game.
///
/// Default MATI agar gameplay normal tidak terganggu. Tekan toggleKey (default
/// F1) saat Play Mode untuk menyalakan/mematikan.
/// </summary>
[DisallowMultipleComponent]
public class PlayerStateHud : MonoBehaviour
{
    public enum ScreenCorner { TopLeft, TopRight, BottomLeft, BottomRight }

    [Header("Aktivasi")]
    [Tooltip("Master switch HUD. Default mati. Bisa di-toggle saat Play dengan toggleKey.")]
    [SerializeField] private bool enableHud = false;

    [Tooltip("Tombol untuk menyalakan/mematikan HUD saat Play Mode.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;

    [Header("Komponen yang Ditampilkan")]
    [Tooltip("Label state melayang di atas kepala pemain.")]
    [SerializeField] private bool showHeadLabel = true;

    [Tooltip("Panel detail (state, transisi, pemicu, riwayat) di pojok layar.")]
    [SerializeField] private bool showCornerPanel = true;

    [Header("Referensi (auto-isi bila kosong)")]
    [Tooltip("Sumber data state. Bila kosong, dicari otomatis di scene.")]
    [SerializeField] private PlayerStateController controller;

    [Tooltip("Transform yang diikuti label kepala. Bila kosong, pakai transform controller.")]
    [SerializeField] private Transform followTarget;

    [Tooltip("Kamera untuk konversi world->screen. Bila kosong, pakai Camera.main.")]
    [SerializeField] private Camera worldCamera;

    [Header("Tata Letak")]
    [Tooltip("Offset label di atas kepala (unit world).")]
    [SerializeField] private Vector3 headWorldOffset = new Vector3(0f, 1.6f, 0f);

    [Tooltip("Pojok layar tempat panel digambar.")]
    [SerializeField] private ScreenCorner panelCorner = ScreenCorner.TopLeft;

    [Tooltip("Ukuran font dasar HUD. Naikkan bila layar resolusi tinggi.")]
    [SerializeField, Min(8)] private int baseFontSize = 14;

    [Tooltip("Tampilkan baris teknis 'Status FSM' (jalur state) di panel. Matikan untuk tampilan paling awam.")]
    [SerializeField] private bool showTechnicalLine = true;

    // --- runtime style cache ---
    private GUIStyle headStyle;
    private GUIStyle panelStyle;
    private GUIStyle panelTitleStyle;
    private Texture2D bgTexture;
    private bool stylesReady;

    private const float MinPanelWidth = 240f;
    private const float Margin = 10f;

    private void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (controller == null)
            controller = FindObjectOfType<PlayerStateController>();

        if (followTarget == null && controller != null)
            followTarget = controller.transform;

        if (worldCamera == null)
            worldCamera = Camera.main;
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
            enableHud = !enableHud;

        // Re-resolve bila player respawn / ganti prefab / kamera berganti.
        if (controller == null || followTarget == null || worldCamera == null)
            ResolveReferences();
    }

    private void OnGUI()
    {
        if (!enableHud || controller == null)
            return;

        EnsureStyles();

        if (showHeadLabel)
            DrawHeadLabel();

        if (showCornerPanel)
            DrawCornerPanel();
    }

    // -------------------------------------------------------------------------

    private void DrawHeadLabel()
    {
        if (followTarget == null)
            return;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
            return;

        Vector3 screen = cam.WorldToScreenPoint(followTarget.position + headWorldOffset);
        if (screen.z < 0f) // target di belakang kamera
            return;

        string path = SafePath();
        string text = "● " + PlayerStateLabels.State(path);
        headStyle.normal.textColor = CategoryColor(path);

        Vector2 size = headStyle.CalcSize(new GUIContent(text));
        float w = size.x + 14f;
        float h = size.y + 6f;
        float x = screen.x - w * 0.5f;
        float y = (Screen.height - screen.y) - h; // OnGUI: y dari atas

        Rect rect = new Rect(x, y, w, h);
        GUI.DrawTexture(rect, bgTexture);
        GUI.Label(rect, text, headStyle);
    }

    private void DrawCornerPanel()
    {
        string nowPath = SafePath();
        const string title = "STATUS PEMAIN — HFSM   (F1)";

        List<string> lines = new List<string>
        {
            "Sekarang   : " + PlayerStateLabels.State(nowPath),
        };

        if (showTechnicalLine)
            lines.Add("Status FSM : " + nowPath);

        lines.Add("Penyebab   : " + PlayerStateLabels.Trigger(controller.LastTrigger));
        lines.Add("Sebelumnya : " + PlayerStateLabels.State(controller.PreviousStatePath));
        lines.Add("");
        lines.Add("Aksi terakhir:");

        string recent = controller.RecentTransitions;
        if (string.IsNullOrEmpty(recent))
        {
            lines.Add("   (belum ada)");
        }
        else
        {
            string[] rows = recent.Split('\n');
            for (int i = 0; i < rows.Length; i++)
                lines.Add("   " + PlayerStateLabels.TransitionLine(rows[i]));
        }

        // Lebar dinamis mengikuti baris terpanjang agar teks tidak terpotong.
        float width = panelTitleStyle.CalcSize(new GUIContent(title)).x;
        for (int i = 0; i < lines.Count; i++)
            width = Mathf.Max(width, panelStyle.CalcSize(new GUIContent(lines[i])).x);

        width = Mathf.Clamp(width + 8f, MinPanelWidth, Screen.width - 2f * Margin);

        GUIContent titleContent = new GUIContent(title);
        GUIContent bodyContent = new GUIContent(string.Join("\n", lines));

        float titleH = panelTitleStyle.CalcHeight(titleContent, width);
        float bodyH = panelStyle.CalcHeight(bodyContent, width);
        float totalH = titleH + bodyH + 12f;

        Vector2 origin = CornerOrigin(width, totalH);
        Rect panelRect = new Rect(origin.x, origin.y, width, totalH);

        GUI.DrawTexture(panelRect, bgTexture);

        Rect titleRect = new Rect(origin.x, origin.y + 4f, width, titleH);
        Rect bodyRect = new Rect(origin.x, origin.y + 4f + titleH, width, bodyH);

        panelTitleStyle.normal.textColor = CategoryColor(nowPath);
        GUI.Label(titleRect, titleContent, panelTitleStyle);
        GUI.Label(bodyRect, bodyContent, panelStyle);
    }

    // -------------------------------------------------------------------------

    private Vector2 CornerOrigin(float w, float h)
    {
        switch (panelCorner)
        {
            case ScreenCorner.TopRight:
                return new Vector2(Screen.width - w - Margin, Margin);
            case ScreenCorner.BottomLeft:
                return new Vector2(Margin, Screen.height - h - Margin);
            case ScreenCorner.BottomRight:
                return new Vector2(Screen.width - w - Margin, Screen.height - h - Margin);
            default: // TopLeft
                return new Vector2(Margin, Margin);
        }
    }

    private string SafePath()
    {
        string p = controller.CurrentStatePath;
        return string.IsNullOrEmpty(p) ? "-" : p;
    }

    /// <summary>Warna berdasarkan kategori super-state agar mudah dibaca di foto.</summary>
    private static Color CategoryColor(string path)
    {
        if (string.IsNullOrEmpty(path))
            return Color.white;

        if (path.StartsWith("Interrupt"))
            return new Color(1f, 0.45f, 0.45f);   // merah: stagger/mati
        if (path.StartsWith("Combat"))
            return new Color(1f, 0.78f, 0.32f);    // oranye: aksi tempur
        if (path.StartsWith("Locomotion"))
            return new Color(0.5f, 1f, 0.62f);     // hijau: gerak normal

        return Color.white;
    }

    private void EnsureStyles()
    {
        if (stylesReady)
            return;

        bgTexture = new Texture2D(1, 1);
        bgTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.74f));
        bgTexture.Apply();

        headStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = baseFontSize + 2,
            richText = false
        };

        panelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = baseFontSize,
            richText = false,
            wordWrap = false,
            padding = new RectOffset(10, 10, 2, 6)
        };
        panelStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f);

        panelTitleStyle = new GUIStyle(panelStyle)
        {
            fontStyle = FontStyle.Bold,
            fontSize = baseFontSize + 1,
            padding = new RectOffset(10, 10, 4, 2)
        };

        stylesReady = true;
    }
}
