using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Mission-end screen ("Bomb defused" / "Bomb exploded").
/// When shown it floats in front of the player's eyes, facing them.
/// Its button (ray + trigger), or the A / X controller button, restarts the scene from the start.
/// </summary>
public class EndGamePanel : MonoBehaviour
{
    public enum Outcome { Defused, Exploded }

    public Outcome outcome = Outcome.Defused;
    [Tooltip("Distance in front of the player's eyes (metres).")]
    public float distance = 1.3f;
    [Tooltip("Vertical offset from eye height (metres).")]
    public float heightOffset = -0.05f;
    [Tooltip("Player's head. Defaults to the OVR Camera Rig's centre eye.")]
    public Transform head;
    [Tooltip("Also restart when A (right) or X (left) is pressed while the panel is visible.")]
    public bool controllerShortcut = true;
    [Tooltip("Ignore input for this long after the panel appears, so a held trigger doesn't click it straight away.")]
    public float inputDelay = 1.0f;

    private float _shownAt;
    private bool _restarting;

    /// <summary>Places the panel in front of the player and shows it.</summary>
    public void Show()
    {
        if (head == null) head = FindHead();
        if (head != null)
        {
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(head.up, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();
            transform.position = head.position + forward * distance + Vector3.up * heightOffset;
            // A world-space canvas is read from its -forward side, so point it away from the player.
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
        _shownAt = Time.unscaledTime;
        _restarting = false;
        gameObject.SetActive(true);
    }

    public void Hide() => gameObject.SetActive(false);

    /// <summary>Changes the panel's message text (the child TextMeshPro object named "Message").</summary>
    public void SetMessage(string message)
    {
        foreach (TMPro.TMP_Text text in GetComponentsInChildren<TMPro.TMP_Text>(true))
        {
            if (text.name == "Message") { text.text = message; return; }
        }
    }

    private void Update()
    {
        if (!controllerShortcut || _restarting) return;
        if (Time.unscaledTime - _shownAt < inputDelay) return;

        bool pressed = OVRInput.GetDown(OVRInput.Button.One) || OVRInput.GetDown(OVRInput.Button.Three);
#if UNITY_EDITOR
        try { pressed |= Input.GetKeyDown(KeyCode.R); } catch (System.InvalidOperationException) { }
#endif
        if (pressed) OnButtonPressed();
    }

    /// <summary>Called by the panel's button (Play Again / Retry).</summary>
    public void OnButtonPressed()
    {
        if (_restarting) return;
        if (Time.unscaledTime - _shownAt < inputDelay) return;
        _restarting = true;
        Debug.Log($"[EndGamePanel] {(outcome == Outcome.Defused ? "Play Again" : "Retry")} - restarting the scene.");
        RestartScene();
    }

    /// <summary>Reloads the current scene from the start.</summary>
    public static void RestartScene()
    {
        Time.timeScale = 1f;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.buildIndex >= 0)
        {
            SceneManager.LoadScene(scene.buildIndex);
            return;
        }
#if UNITY_EDITOR
        // Scene not in Build Settings: still allow restarting while testing in the Editor.
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            scene.path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(scene.name);
#endif
    }

    private static Transform FindHead()
    {
        OVRCameraRig rig = FindObjectOfType<OVRCameraRig>();
        if (rig != null && rig.centerEyeAnchor != null) return rig.centerEyeAnchor;
        return Camera.main != null ? Camera.main.transform : null;
    }
}
