using UnityEngine;
using UnityEngine.UI;

public class HPBar_Follow_Enemy : MonoBehaviour
{
    [Header("Target Runtime")]
    [SerializeField] private Transform enemyRoot;
    [SerializeField] private CharacterBase stats;

    [Header("UI")]
    [SerializeField] private Image fill;
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.5f, 0f);

    private Camera cam;
    private Canvas parentCanvas;
    private RectTransform rectTransform;
    private RectTransform canvasRect;

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

        if (targetCollider != null)
        {
            Bounds b = targetCollider.bounds;
            return new Vector3(b.center.x + offset.x, b.max.y + offset.y, enemyRoot.position.z + offset.z);
        }

        if (targetRenderer != null)
        {
            Bounds b = targetRenderer.bounds;
            return new Vector3(b.center.x + offset.x, b.max.y + offset.y, enemyRoot.position.z + offset.z);
        }

        return enemyRoot.position + offset;
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