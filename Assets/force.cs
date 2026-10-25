using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExplosionForce : MonoBehaviour
{
    // How to use the script:
    // 1. Place this script on the object you want to explode
    // 2. Add the explosion VFX of your likings to the explosion GameObject
    // 3. Adjust the fieldOfImpact and explosionForce directly in Unity
    // Info: When this object hits a collider, it will explode
    // Info: This does NOT work in 2D

    public float fieldOfImpact;
    public float explosionForce;
    public GameObject explosion;  // Reference to the explosion GameObject
    public AudioSource explosionSound;  // Reference to the explosion sound

    private void OnCollisionEnter(Collision collision)
    {
        // Play the explosion sound upon collision
        if (explosionSound != null)
        {
            explosionSound.Play();
        }

        // Trigger the explosion effect (visual + particle)
        Explosion();

        // Destroy the game object after the explosion
        Destroy(gameObject);
    }

    private void Explosion()
    {
        // Instantiate the explosion GameObject (which includes the particle system)
        GameObject _explosion = Instantiate(explosion, transform.position, transform.rotation);

        // Play the particle system attached to the explosion GameObject
        ParticleSystem explosionParticles = _explosion.GetComponent<ParticleSystem>();
        if (explosionParticles != null)
        {
            explosionParticles.Play();
        }

        // Apply explosion force to nearby objects
        Collider[] colliders = Physics.OverlapSphere(transform.position, fieldOfImpact);
        foreach (Collider target in colliders)
        {
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.AddExplosionForce(explosionForce, transform.position, fieldOfImpact);
            }
        }

        // Destroy the explosion GameObject after 3 seconds
        Destroy(_explosion, 3f);
    }
}

