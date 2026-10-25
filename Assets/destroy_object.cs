using UnityEngine;

public class BeakerCollision : MonoBehaviour
{
    void OnCollisionEnter(Collision collision)
    {
        // Destroy the colliding object
        Destroy(collision.gameObject);
    }
}
