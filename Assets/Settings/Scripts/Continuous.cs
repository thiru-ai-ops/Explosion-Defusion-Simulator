using UnityEngine;

/// <summary>
/// Smooth locomotion for the OVR camera rig.
/// Left thumbstick: move (relative to where the player is looking). Right thumbstick X: turn.
/// The old version read the legacy Input axis "Oculus_CrossPlatform_SecondaryThumbstickHorizontal",
/// which is not defined in this project's Input Manager and threw an exception every frame.
/// </summary>
public class ContinuousMovement : MonoBehaviour
{
    public float moveSpeed = 3.0f;       // metres per second
    public float rotationSpeed = 90.0f;  // degrees per second
    [Tooltip("Head transform used for the move direction. Defaults to the rig's CenterEyeAnchor.")]
    public Transform head;
    [Range(0f, 0.5f)] public float deadZone = 0.15f;

    void Update()
    {
        if (head == null)
        {
            var rig = GetComponent<OVRCameraRig>();
            if (rig != null) head = rig.centerEyeAnchor;
        }

        MovePlayer();
        RotatePlayer();
    }

    void MovePlayer()
    {
        Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick);
        stick += KeyboardMove();
        if (stick.magnitude < deadZone) return;

        Transform basis = head != null ? head : transform;
        Vector3 forward = Vector3.ProjectOnPlane(basis.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(basis.right, Vector3.up).normalized;
        Vector3 move = (forward * stick.y + right * stick.x) * moveSpeed * Time.deltaTime;
        transform.Translate(move, Space.World);
    }

    void RotatePlayer()
    {
        float x = OVRInput.Get(OVRInput.Axis2D.SecondaryThumbstick).x;
        if (Mathf.Abs(x) < deadZone) return;

        // Turn around the player's head so the world doesn't swing around the rig origin.
        Vector3 pivot = head != null ? head.position : transform.position;
        transform.RotateAround(pivot, Vector3.up, x * rotationSpeed * Time.deltaTime);
    }

    static Vector2 KeyboardMove()
    {
#if UNITY_EDITOR
        try
        {
            return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
        }
        catch (System.Exception) { }
#endif
        return Vector2.zero;
    }
}
