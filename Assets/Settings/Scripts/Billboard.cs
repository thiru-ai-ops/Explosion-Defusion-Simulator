using UnityEngine;

public class Billboard : MonoBehaviour
{
    void LateUpdate()
    {
        // Make the object face the camera
        transform.LookAt(transform.position + Camera.main.transform.forward);
    }
}
