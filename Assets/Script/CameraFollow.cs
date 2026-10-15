    using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float distance = 10f;      // how far camera sits from target
    [SerializeField] private float mouseSensitivity = 3f;
    [SerializeField] private float minPitch = -20f;      // how far down you can look
    [SerializeField] private float maxPitch = 60f;        // how far up you can look

    private float yaw = 0f;   // rotation around Y axis (left/right)
    private float pitch = 20f; // rotation around X axis (up/down)

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        if (target == null)
        {
            PlayerMovement player = FindObjectOfType<PlayerMovement>();
            if (player != null)
                target = player.transform;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // Get raw mouse movement this frame
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Add mouse movement to our rotation tracking
        yaw += mouseX;
        pitch -= mouseY; // subtract so moving mouse up looks up, not down

        // Clamp pitch so the camera can't flip upside down
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        // Build a rotation from our yaw/pitch values
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Start at target position, then push the camera back along the rotated direction
        Vector3 desiredPosition = target.position - (rotation * Vector3.forward * distance);

        transform.position = desiredPosition;
        transform.LookAt(target.position);
    }
}