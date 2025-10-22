using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform player; // Assign the player in the inspector
    public Vector3 offset; // Adjust the offset if you want the camera not exactly on the player
    public float smoothSpeed = 0.125f; // How smooth the camera movement is

    void LateUpdate()
    {
        if (player == null) return;

        Vector3 desiredPosition = player.position + offset;
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        
    }
}

