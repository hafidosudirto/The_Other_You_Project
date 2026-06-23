using UnityEngine;
using UnityEngine.UI;

public class HPBar_Follow_Enemy : MonoBehaviour
{
    [Header("Target Runtime")]
    [SerializeField] private Transform enemyRoot;
    [SerializeField] private CharacterBase stats;

    [Header("UI")]
    [SerializeField] private Image fill;
    [Tooltip("Koreksi tambahan dari posisi anchor (default: posisi HP bar di prefab). Naikkan jika ingin bar lebih tinggi dari posisi prefab.")]
    [SerializeField] private Vector3 offset = Vector3.zero;

    private Camera cam;
    private Canvas parentCanvas;
    private RectTransform rectTransform;
    private RectTransform canvasRect;

    // Anchor statis yang disimpan sekali saat Awake/SetTarget agar tidak berubah tiap frame.
    // Untuk World Space canvas (HP bar child dari musuh): offset lokal relatif terhadap musuh.
    // Untuk Screen Space canvas: posisi world anchor yang diinginkan (di-copy sekali).
    private bool _anchorCaptured;
    private Vector3 _localAnchorFromEnemy; // World Space mode (prefab child)
    private Vector3 _worldAnchor;          // Screen Space mode (UI terpisah)
    private Vector3 _capturedEnemyPos;     // posisi musuh saat anchor di-capture (untuk delta)

    private Collider2D targetCollider;
    private Renderer targetRenderer;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        parentCanvas = GetComponentInParent<Canvas>();
        canvasRect = parentCanvas != null ? parentCanvas.transform as RectTransform : null;

        // PERBAIKAN 1: Auto-Detect target jika HP Bar adalah Child dari Enemy (World Space)
        if (enemyRoot == null)
        {
            CharacterBase parentStats = GetComponentInParent<CharacterBase>();
            if (parentStats != null)
            {
                enemyRoot = parentStats.transform;
            }
        }

        RefreshCamera();
        CaptureAnchor();

        // PERBAIKAN 2: Jangan langsung sembunyikan bar jika target sebenarnya berhasil dideteksi
        if (enemyRoot == null)
        {
            HideBar();
            if (fill != null) fill.fillAmount = 0f;
        }
    }

    private void OnEnable()
    {
        // PERBAIKAN 3: Refresh referensi stats jika game menggunakan sistem Object Pooling (aktif/non-aktif)
        if (enemyRoot != null)
        {
            ResolveStats();
            ResolveVisualAnchor();
        }
    }

    private void LateUpdate()
    {
        // Jika Enemy terhapus saat Play Again, sembunyikan UI
        if (enemyRoot == null)
        {
            HideBar();
            if (fill != null) fill.fillAmount = 0f;
            return;
        }

        if (cam == null) RefreshCamera();
        if (stats == null) ResolveStats();
        if (targetCollider == null && targetRenderer == null) ResolveVisualAnchor();

        UpdatePosition();
        UpdateFill();
    }

    // Fungsi ini wajib dipanggil oleh Spawner jika UI HP Bar berada di Screen Space Canvas terpisah
    public void SetTarget(Transform newTarget, Vector3 worldOffset)
    {
        enemyRoot = newTarget;
        offset = worldOffset;

        gameObject.SetActive(true); // Pastikan UI kembali aktif

        RefreshCamera();
        ResolveStats();
        ResolveVisualAnchor();
        CaptureAnchor();
        UpdatePosition();
        UpdateFill();
    }

    private void RefreshCamera()
    {
        if (parentCanvas != null &&
            parentCanvas.renderMode == RenderMode.ScreenSpaceCamera &&
            parentCanvas.worldCamera != null)
        {
            cam = parentCanvas.worldCamera;
            return;
        }
        cam = Camera.main;
    }

    private void ResolveStats()
    {
        stats = null;
        if (enemyRoot == null) return;

        stats = enemyRoot.GetComponent<CharacterBase>();
        if (stats == null) stats = enemyRoot.GetComponentInChildren<CharacterBase>(true);
        if (stats == null) stats = enemyRoot.GetComponentInParent<CharacterBase>();

        if (stats == null)
            Debug.LogError("[HPBar_Follow_Enemy] CharacterBase tidak ditemukan pada enemy root/child/parent.");
    }

    private void ResolveVisualAnchor()
    {
        // PERBAIKAN 4: Kosongkan referensi lama sebelum mencari yang baru
        targetCollider = null;
        targetRenderer = null;

        if (enemyRoot == null) return;

        targetCollider = enemyRoot.GetComponent<Collider2D>();
        if (targetCollider == null) targetCollider = enemyRoot.GetComponentInChildren<Collider2D>(true);

        targetRenderer = enemyRoot.GetComponent<Renderer>();
        if (targetRenderer == null) targetRenderer = enemyRoot.GetComponentInChildren<Renderer>(true);
    }

    private void UpdatePosition()
    {
        Vector3 worldPos = GetWorldAnchorPosition();

        if (parentCanvas == null || rectTransform == null || parentCanvas.renderMode == RenderMode.WorldSpace)
        {
            transform.position = worldPos;
            return;
        }

        if (cam == null)
        {
            HideBar();
            return;
        }

        Vector3 screenPos = cam.WorldToScreenPoint(worldPos);

        if (screenPos.z <= 0f)
        {
            HideBar();
            return;
        }

        Camera uiCamera = null;
        if (parentCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            uiCamera = parentCanvas.worldCamera != null ? parentCanvas.worldCamera : cam;

        if (canvasRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, uiCamera, out Vector2 localPoint))
        {
            rectTransform.anchoredPosition = localPoint;
        }
        else
        {
            rectTransform.position = screenPos;
        }
    }

    private Vector3 GetWorldAnchorPosition()
    {
        if (enemyRoot == null) return Vector3.zero;

        // Pastikan anchor sudah ter-capture sekali.
        if (!_anchorCaptured)
            CaptureAnchor();

        // World Space canvas: HP bar adalah child dari musuh, jadi pakai offset lokal
        // relatif ke musuh (di-rotasi/di-translate bersama musuh).
        if (parentCanvas != null && parentCanvas.renderMode == RenderMode.WorldSpace)
        {
            return enemyRoot.TransformPoint(_localAnchorFromEnemy) + offset;
        }

        // Screen Space / Screen Space Camera: posisi world anchor disimpan sekali saat Awake/SetTarget,
        // ditranslasikan mengikuti pergerakan musuh (tanpa rotasi).
        return _worldAnchor + (enemyRoot.position - _capturedEnemyPos) + offset;
    }

    /// <summary>
    /// Menangkap anchor statis SEKALI (tidak dibaca ulang tiap frame) agar posisi HP bar yang
    /// kamu tata di prefab dihormati, bukan "mengejar dirinya sendiri" yang baru dipindahkan
    /// oleh UpdatePosition() di frame sebelumnya.
    /// </summary>
    private void CaptureAnchor()
    {
        if (rectTransform == null)
            return;

        if (enemyRoot != null && parentCanvas != null && parentCanvas.renderMode == RenderMode.WorldSpace)
        {
            // Offset HP bar relatif terhadap musuh (agar ikut rotasi parent).
            _localAnchorFromEnemy = enemyRoot.InverseTransformPoint(rectTransform.position);
        }
        else
        {
            // Posisi world absolut saat prefab/HP bar dipasang di Scene.
            _worldAnchor = rectTransform.position;
        }

        if (enemyRoot != null)
            _capturedEnemyPos = enemyRoot.position;

        _anchorCaptured = true;
    }

    private void UpdateFill()
    {
        if (fill == null || stats == null || stats.maxHP <= 0f)
        {
            if (fill != null) fill.fillAmount = 0f;
            return;
        }

        float ratio = stats.currentHP / stats.maxHP;
        fill.fillAmount = Mathf.Clamp01(ratio);
    }

    private void HideBar()
    {
        if (rectTransform != null)
            rectTransform.anchoredPosition = new Vector2(-10000f, -10000f);
    }
}