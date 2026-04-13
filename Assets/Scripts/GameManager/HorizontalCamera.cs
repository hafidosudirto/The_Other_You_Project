using UnityEngine;

public class HorizontalCameraClamped : MonoBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private Transform playerTransform;
    
    [Header("Smoothing")]
    [SerializeField] private float smoothSpeed = 0.125f;
    [SerializeField] private Vector3 offset;

    [Header("Boundary Limits")]
    [SerializeField] private float minX; // Batas kiri
    [SerializeField] private float maxX; // Batas kanan

    void LateUpdate()
    {
        if (playerTransform == null) return;

        // 1. Kalkulasi posisi target berdasarkan player
        float targetX = playerTransform.position.x + offset.x;
        
        // 2. Clamp: Paksa targetX tetap berada di antara minX dan maxX
        targetX = Mathf.Clamp(targetX, minX, maxX);

        // 3. Eksekusi pergerakan
        float fixedY = transform.position.y;
        float fixedZ = transform.position.z;

        Vector3 desiredPosition = new Vector3(targetX, fixedY, fixedZ);
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        
        transform.position = smoothedPosition;
    }
}