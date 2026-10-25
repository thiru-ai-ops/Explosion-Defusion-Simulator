using UnityEngine;
using UnityEngine.UI;

public class ArrowDirectionUI : MonoBehaviour
{
    public Transform target; // The object you want the arrow to point towards
    public Transform player; // The player object
    public RectTransform arrowUI; // The UI Raw Image representing the arrow

    private void Update()
    {
        if (target == null || player == null || arrowUI == null) return;

        // Calculate the direction from the player to the target
        Vector3 directionToTarget = target.position - player.position;
        directionToTarget.y = 0; // Ignore vertical difference for simplicity

        // Calculate the angle between player's forward and the target
        float angle = Vector3.SignedAngle(player.forward, directionToTarget, Vector3.up);

        // Rotate the arrow UI accordingly
        arrowUI.localRotation = Quaternion.Euler(0, 0, -angle);
    }
}
