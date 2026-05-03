using UnityEngine;
using UnityEngine.UI;

public class BowChargeBar : MonoBehaviour
{
    [Header("Target Follow")]
    [Tooltip("Player yang akan diikuti charge bar. Isi dengan Player_W2.")]
    public Transform player;

    [Tooltip("Camera utama. Kalau kosong, script akan memakai Camera.main.")]
    public Camera mainCamera;

    [Header("UI References")]
    [Tooltip("Root utama charge bar. Isi dengan ChargeBarRoot.")]
    public RectTransform barUI;

    [Tooltip("Background bar. Isi dengan object ChargeBar.")]
    public Image chargeBarBackground;

    [Tooltip("Isi bar. Isi dengan object ChargeBarFill.")]
    public Image chargeBarFill;

    [Tooltip("Garis tengah. Isi dengan object MidLine.")]
    public Image middleLine;

    [Header("Posisi Bar")]
    [Tooltip("Offset posisi bar dari player. X negatif = kiri player, Y positif = atas player.")]
    public Vector3 worldOffset = new Vector3(-0.35f, 0.55f, 0f);

    [Tooltip("Kalau aktif, bar akan pindah sisi mengikuti arah hadap player.")]
    public bool ikutArahHadap = true;

    [Tooltip("Referensi Player untuk baca arah hadap. Isi dengan Player_W2.")]
    public Player playerFacing;

    [Header("Ukuran Bar via Kode")]
    [Tooltip("Ukuran root keseluruhan charge bar.")]
    public Vector2 rootSize = new Vector2(22f, 74f);

    [Tooltip("Ukuran background bar vertikal.")]
    public Vector2 backgroundSize = new Vector2(8f, 52f);

    [Tooltip("Padding fill dari background. Makin besar, fill makin kecil.")]
    public Vector2 fillPadding = new Vector2(1f, 1f);

    [Tooltip("Ukuran garis tengah.")]
    public Vector2 middleLineSize = new Vector2(14f, 2f);

    [Header("Aturan Skill 2")]
    [Tooltip("Batas masuk Piercing. 0.5 berarti ketika isi melewati garis tengah.")]
    [Range(0f, 1f)]
    public float piercingThreshold = 0.5f;

    public float PiercingThreshold => piercingThreshold;

    [Header("Warna")]
    [Tooltip("Warna saat masih Full Draw.")]
    public Color warnaFullDraw = new Color(1f, 0.85f, 0.15f, 1f);

    [Tooltip("Warna saat sudah Piercing.")]
    public Color warnaPiercing = new Color(0.25f, 0.85f, 1f, 1f);

    [Tooltip("Warna background bar.")]
    public Color warnaBackground = new Color(0f, 0f, 0f, 0.65f);

    [Tooltip("Warna garis tengah.")]
    public Color warnaGarisTengah = Color.white;

    private Canvas parentCanvas;
    private RectTransform canvasRect;
    private float currentCharge01;
    private bool isVisible;

    private void Awake()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        parentCanvas = GetComponentInParent<Canvas>();

        if (parentCanvas != null)
            canvasRect = parentCanvas.GetComponent<RectTransform>();

        if (barUI == null)
            barUI = transform as RectTransform;
    }

    private void Start()
    {
        ApplyVisualSetup();
        SetCharge01(0f);
        HideImmediate();
    }

    private void LateUpdate()
    {
        if (!isVisible)
            return;

        FollowPlayer();
    }

    [ContextMenu("Apply Charge Bar Visual Setup")]
    public void ApplyVisualSetup()
    {
        if (barUI == null)
            barUI = transform as RectTransform;

        if (barUI != null)
        {
            barUI.sizeDelta = rootSize;
            barUI.localScale = Vector3.one;
        }

        if (chargeBarBackground != null)
        {
            RectTransform bgRect = chargeBarBackground.rectTransform;
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = backgroundSize;

            chargeBarBackground.color = warnaBackground;
            chargeBarBackground.raycastTarget = false;
        }

        if (chargeBarFill != null)
        {
            RectTransform fillRect = chargeBarFill.rectTransform;
            fillRect.anchorMin = new Vector2(0.5f, 0.5f);
            fillRect.anchorMax = new Vector2(0.5f, 0.5f);
            fillRect.pivot = new Vector2(0.5f, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;

            float fillWidth = Mathf.Max(1f, backgroundSize.x - fillPadding.x * 2f);
            float fillHeight = Mathf.Max(1f, backgroundSize.y - fillPadding.y * 2f);
            fillRect.sizeDelta = new Vector2(fillWidth, fillHeight);

            chargeBarFill.type = Image.Type.Filled;
            chargeBarFill.fillMethod = Image.FillMethod.Vertical;
            chargeBarFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            chargeBarFill.raycastTarget = false;
        }

        if (middleLine != null)
        {
            RectTransform lineRect = middleLine.rectTransform;
            lineRect.anchorMin = new Vector2(0.5f, 0.5f);
            lineRect.anchorMax = new Vector2(0.5f, 0.5f);
            lineRect.pivot = new Vector2(0.5f, 0.5f);
            lineRect.sizeDelta = middleLineSize;

            middleLine.color = warnaGarisTengah;
            middleLine.raycastTarget = false;
        }

        UpdateMiddleLinePosition();
    }

    public void Show()
    {
        isVisible = true;

        if (barUI != null)
            barUI.gameObject.SetActive(true);

        ApplyVisualSetup();
        FollowPlayer();
    }

    public void Hide()
    {
        isVisible = false;

        if (barUI != null)
            barUI.gameObject.SetActive(false);
    }

    public void HideImmediate()
    {
        isVisible = false;

        if (barUI != null)
            barUI.gameObject.SetActive(false);
    }

    public void SetCharge01(float value)
    {
        currentCharge01 = Mathf.Clamp01(value);

        if (chargeBarFill != null)
        {
            chargeBarFill.fillAmount = currentCharge01;
            chargeBarFill.color = currentCharge01 >= piercingThreshold
                ? warnaPiercing
                : warnaFullDraw;
        }

        UpdateMiddleLinePosition();
    }

    public bool IsPiercingReady()
    {
        return currentCharge01 >= piercingThreshold;
    }

    private void UpdateMiddleLinePosition()
    {
        if (middleLine == null)
            return;

        float height = backgroundSize.y;
        float y = Mathf.Lerp(-height * 0.5f, height * 0.5f, piercingThreshold);

        RectTransform lineRect = middleLine.rectTransform;
        lineRect.anchoredPosition = new Vector2(0f, y);
    }

    private void FollowPlayer()
    {
        if (player == null || barUI == null || parentCanvas == null || canvasRect == null)
            return;

        Vector3 offset = worldOffset;

        if (ikutArahHadap && playerFacing != null)
        {
            offset.x = playerFacing.isFacingRight
                ? -Mathf.Abs(worldOffset.x)
                : Mathf.Abs(worldOffset.x);
        }

        Vector3 worldPosition = player.position + offset;
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(mainCamera, worldPosition);

        if (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPoint,
                null,
                out Vector2 localPoint
            );

            barUI.anchoredPosition = localPoint;
        }
        else if (parentCanvas.renderMode == RenderMode.ScreenSpaceCamera)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPoint,
                parentCanvas.worldCamera,
                out Vector2 localPoint
            );

            barUI.anchoredPosition = localPoint;
        }
        else
        {
            barUI.position = worldPosition;
        }
    }
}