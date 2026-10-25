using System.Collections;
using UnityEngine;

public class ExplosionDelay : MonoBehaviour
{
    public ParticleSystem sparks;
    public ParticleSystem fire;
    public ParticleSystem flash;
    public ParticleSystem smoke;  // Assign the explosion particle system in the Inspector
    public AudioSource explosionAudio;  // Assign the audio source in the Inspector
    public float delay = 2f;  // Set the delay time in seconds

    void Start()
    {
        StartCoroutine(PlayExplosionAfterDelay());
    }

    IEnumerator PlayExplosionAfterDelay()
    {
        yield return new WaitForSeconds(delay);  // Wait for the specified delay

        // Play all particle systems
        sparks.Play();
        fire.Play();
        flash.Play();
        smoke.Play();

        // Play the audio after particles have started
        if (explosionAudio != null)
        {
            yield return new WaitForSeconds(0.5f);  // Optional small delay for audio
            explosionAudio.Play();
        }
    }
}
