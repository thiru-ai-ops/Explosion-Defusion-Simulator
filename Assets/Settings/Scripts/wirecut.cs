using UnityEngine;

public class WireCuttingScript : MonoBehaviour
{
    [SerializeField] private LayerMask cuttableLayer; // Select the layer in the Inspector

    private void OnTriggerEnter(Collider other)
    {
        if (((1 << other.gameObject.layer) & cuttableLayer) != 0) // Check if in specified layer
        {
            Destroy(other.gameObject);
            Debug.Log(other.gameObject.name + " has been cut!");
        }
    }
}