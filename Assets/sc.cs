using UnityEngine;

public class Pendulum : MonoBehaviour
{
    public Transform pivot; // Pivot point
    public Transform bob;   // Pendulum bob
    private float length;
    private float gravity = 9.81f;

    void Start()
    {
        // Calculate initial string length
        length = Vector3.Distance(pivot.position, bob.position);
    }

    void Update()
    {
        // Calculate period of oscillation
        float period = 2 * Mathf.PI * Mathf.Sqrt(length / gravity);
        Debug.Log("Period: " + period.ToString("F2") + " seconds");
    }
}
