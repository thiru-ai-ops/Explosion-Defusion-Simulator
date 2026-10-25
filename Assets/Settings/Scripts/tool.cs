using UnityEngine;

public class Tool : MonoBehaviour
{
    // Tag defined in Project Settings > Tags and Layers ("ToolHolder" was not a defined tag
    // and made CompareTag log an error on every trigger contact).
    private const string HolderTag = "toolholder";

    private bool isHeld = false; // Track whether the tool is sitting in a holder

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(HolderTag))
        {
            isHeld = true;
            transform.SetParent(other.transform);
            transform.localPosition = Vector3.zero;
            Rigidbody rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(HolderTag) && transform.parent == other.transform)
        {
            isHeld = false;
            transform.SetParent(null);
        }
    }

    public bool IsHeld()
    {
        return isHeld;
    }
}
