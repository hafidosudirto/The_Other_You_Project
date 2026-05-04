using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform player;

    [Header("Follow Settings")]
    [SerializeField] private float smoothTime = 0.15f;

    [Tooltip("Kamera baru bergerak jika player keluar dari area deadzone ini.")]
    [SerializeField] private float deadZoneHalfWidth = 1.5f;

    [Header("Camera Center Limit")]
    [Tooltip("Batas posisi X paling kiri untuk titik tengah kamera.")]
    [SerializeField] private float minCameraX = -9f;

    [Tooltip("Batas posisi X paling kanan untuk titik tengah kamera.")]
    [SerializeField] private float maxCameraX = 28f;

    [Header("Fixed Camera Position")]
    [Tooltip("Jika aktif, kamera memakai posisi Y awal sebagai Y tetap.")]
    [SerializeField] private bool useInitialY = true;

    [Tooltip("Dipakai jika useInitialY dimatikan.")]
    [SerializeField] private float fixedY = 0f;

    private float xVelocity;
    private float currentY;

    private void Awake()
    {
        if (useInitialY)
        {
            currentY = transform.position.y;
        }
        else
        {
            currentY = fixedY;
        }

        Vector3 startPosition = transform.position;
        startPosition.x = Mathf.Clamp(startPosition.x, minCameraX, maxCameraX);
        startPosition.y = currentY;
        transform.position = startPosition;
    }

    private void LateUpdate()
    {
        if (player == null)
        {
            return;
        }

        float cameraX = transform.position.x;
        float targetX = cameraX;

        float leftDeadZone = cameraX - deadZoneHalfWidth;
        float rightDeadZone = cameraX + deadZoneHalfWidth;

        if (player.position.x > rightDeadZone)
        {
            targetX = player.position.x - deadZoneHalfWidth;
        }
        else if (player.position.x < leftDeadZone)
        {
            targetX = player.position.x + deadZoneHalfWidth;
        }

        targetX = Mathf.Clamp(targetX, minCameraX, maxCameraX);

        float smoothedX = Mathf.SmoothDamp(
            cameraX,
            targetX,
            ref xVelocity,
            smoothTime
        );

        smoothedX = Mathf.Clamp(smoothedX, minCameraX, maxCameraX);

        transform.position = new Vector3(
            smoothedX,
            currentY,
            transform.position.z
        );
    }

    public void SetTarget(Transform newTarget)
    {
        player = newTarget;
    }
}