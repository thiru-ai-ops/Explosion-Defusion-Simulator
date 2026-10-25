using UnityEngine;

public class sodiumSpark : MonoBehaviour
{
    public GameObject explosion;

    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.tag == "beaker")
        {
            explosion.SetActive(true);
        }
        
        
        
    }
}
