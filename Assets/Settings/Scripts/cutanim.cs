using UnityEngine;
using System.Collections;

public class WireCutterAnimationController : MonoBehaviour
{
    private Animator wireCutterAnimator;

    private void Start()
    {
        wireCutterAnimator = GetComponent<Animator>();
        if (wireCutterAnimator == null)
        {
            Debug.LogError("Animator not found on WireCutter!");
        }
        else
        {
            wireCutterAnimator.enabled = false; // Ensure it's disabled initially
        }
    }

    private void Update()
    {
        // Check if the player presses the primary index trigger
        if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) ||
            OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger))
        {
            StartCoroutine(PlayAnimationOnce());
        }
    }

    private IEnumerator PlayAnimationOnce()
    {
        if (wireCutterAnimator != null)
        {
            wireCutterAnimator.enabled = true; // Enable Animator
            wireCutterAnimator.SetTrigger("Cut"); // Play Animation

            // Wait for animation to complete
            yield return new WaitForSeconds(wireCutterAnimator.GetCurrentAnimatorStateInfo(0).length);

            wireCutterAnimator.enabled = false; // Disable Animator after animation
        }
    }
}
