using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    public Transform target; // The object to orbit around
    public float distance = 5.0f; // Initial distance from the target
    public float orbitSpeed = 200.0f; // Speed of orbiting
    public float zoomSpeed = 2.0f; // Speed of zooming
    public float minDistance = 2.0f; // Minimum distance to target
    public float maxDistance = 10.0f; // Maximum distance to target

    private float currentX = 0.0f; // Current rotation on the X axis
    private float currentY = 0.0f; // Current rotation on the Y axis

    void Update()
    {
        if (Input.GetMouseButton(1)) // Right mouse button for orbiting
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            currentX += mouseX * orbitSpeed * Time.deltaTime;
            currentY -= mouseY * orbitSpeed * Time.deltaTime;
            currentY = Mathf.Clamp(currentY, -85, 85); // Clamp Y rotation
        }

        float scroll = Input.GetAxis("Mouse ScrollWheel");
        distance -= scroll * zoomSpeed;
        distance = Mathf.Clamp(distance, minDistance, maxDistance); // Clamp zoom
    }

    void LateUpdate()
    {
        if (target != null)
        {
            Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
            Vector3 direction = new Vector3(0, 0, -distance);
            transform.position = target.position + rotation * direction;
            transform.LookAt(target);
        }
    }
}
