using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Oculus.Interaction;

/// <summary>
/// Wire cutter for the bomb-defusal sequence (Meta XR / Interaction SDK version).
///
/// How it works:
///  * The player grabs the cutter (ISDK Grabbable - grip or ray grab).
///  * The player moves the cutter's jaws onto a wire and squeezes either INDEX TRIGGER.
///  * The wire that is closest to <see cref="cutPoint"/> inside <see cref="cutVolume"/> is cut.
///  * Wires must be cut step by step, following <see cref="sequence"/>. Wires inside the same
///    step can be cut in any order. Cutting a wire out of order, or a decoy wire, detonates the bomb.
///
/// Detection uses a physics overlap query when the trigger is pressed, so it works with a kinematic
/// cutter and static (even non-convex) wire colliders - no trigger colliders or tags are required.
/// </summary>
public class WireCutter : MonoBehaviour
{
    [Serializable]
    public class WireStep
    {
        [Tooltip("Shown in logs only, e.g. \"Detonator wires\".")]
        public string label;
        [Tooltip("Wires for this step. They may be cut in any order.")]
        public GameObject[] wires;
        [Tooltip("When every wire of this step is cut, the bomb timer stops.")]
        public bool stopsTimer;
    }

    [Header("Defusal sequence")]
    [Tooltip("Steps in order. Wires inside one step can be cut in any order.")]
    public List<WireStep> sequence = new List<WireStep>();
    [Tooltip("Wires that must NOT be cut. Cutting one detonates the bomb.")]
    public GameObject[] decoyWires = new GameObject[0];
    [Tooltip("Wires that are not linked to the explosives. Cutting one is harmless (it neither helps nor detonates).")]
    public GameObject[] safeWires = new GameObject[0];
    [Tooltip("Countdown on the bomb. Reaching zero detonates the bomb.")]
    public BDSBombTimer timer;

    [Header("Cut detection")]
    [Tooltip("Volume used to look for wires when the trigger is pressed. Defaults to the cutter's BoxCollider.")]
    public BoxCollider cutVolume;
    [Tooltip("Point between the jaws. The wire closest to this point is cut. Defaults to the cut volume centre.")]
    public Transform cutPoint;
    [Tooltip("Extra margin (metres) added around the cut volume.")]
    public float detectionPadding = 0.01f;
    [Tooltip("Only cut while the cutter is being held (any ISDK Grabbable on this object or its children).")]
    public bool requireHeld = true;
    [Tooltip("Ignore trigger presses for this long after the cutter is grabbed (ray grab uses the trigger too).")]
    public float grabCooldown = 0.25f;
    [Tooltip("Keyboard key that also cuts while testing in the Editor / XR Simulator.")]
    public KeyCode editorCutKey = KeyCode.C;

    [Header("Feedback")]
    public GameObject explosionPrefab;
    public GameObject burningFirePrefab;
    public AudioSource warningSound;
    public AudioClip cutSound;
    [Tooltip("Seconds between a wrong cut and the explosion.")]
    public float explosionDelay = 4f;
    public GameObject missionFailedCanvas;
    public GameObject missionPassedCanvas;
    [Tooltip("Freeze the game (Time.timeScale = 0) when the mission ends.")]
    public bool pauseOnEnd = false;
    public UnityEvent onWireCut;
    public UnityEvent onDefused;
    public UnityEvent onDetonated;

    private readonly Dictionary<Collider, GameObject> _colliderToWire = new Dictionary<Collider, GameObject>();
    private readonly HashSet<GameObject> _cutWires = new HashSet<GameObject>();
    private readonly Collider[] _overlapBuffer = new Collider[64];
    private Grabbable[] _grabbables = new Grabbable[0];
    private Animator _animator;
    private int _currentStep;
    private bool _finished;
    private bool _wasHeld;
    private float _heldSince;

    public int CurrentStep => _currentStep;
    public bool IsFinished => _finished;
    public bool IsHeldNow => IsHeld();

    private void Awake()
    {
        if (cutVolume == null) cutVolume = GetComponent<BoxCollider>();
        _grabbables = GetComponentsInChildren<Grabbable>(true);
        _animator = GetComponent<Animator>();
        if (_animator != null) _animator.enabled = false;
        RegisterWires();
    }

    private void Start()
    {
        if (missionFailedCanvas != null) missionFailedCanvas.SetActive(false);
        if (missionPassedCanvas != null) missionPassedCanvas.SetActive(false);
        Debug.Log($"[WireCutter] Ready. {sequence.Count} step(s), {_colliderToWire.Count} wire collider(s), {_grabbables.Length} grabbable(s).");
    }

    private void RegisterWires()
    {
        _colliderToWire.Clear();
        foreach (WireStep step in sequence)
        {
            if (step == null || step.wires == null) continue;
            foreach (GameObject wire in step.wires) RegisterWire(wire);
        }
        if (decoyWires != null)
        {
            foreach (GameObject wire in decoyWires) RegisterWire(wire);
        }
        if (safeWires != null)
        {
            foreach (GameObject wire in safeWires) RegisterWire(wire);
        }
    }

    private void RegisterWire(GameObject wire)
    {
        if (wire == null) return;
        wire.SetActive(true);
        Collider[] colliders = wire.GetComponentsInChildren<Collider>(true);
        if (colliders.Length == 0)
        {
            // Give the wire a collider so it can be detected.
            MeshFilter mf = wire.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                MeshCollider mc = wire.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                colliders = new Collider[] { mc };
            }
            else
            {
                Debug.LogWarning($"[WireCutter] Wire '{wire.name}' has no collider and no mesh - it can't be cut.", wire);
            }
        }
        foreach (Collider c in colliders) _colliderToWire[c] = wire;
    }

    private bool IsHeld()
    {
        if (!requireHeld || _grabbables.Length == 0) return true;
        foreach (Grabbable g in _grabbables)
        {
            if (g != null && g.isActiveAndEnabled && g.SelectingPointsCount > 0) return true;
        }
        return false;
    }

    private void Update()
    {
        if (_finished) return;

        bool held = IsHeld();
        if (held && !_wasHeld) _heldSince = Time.time;
        _wasHeld = held;

        bool pressed =
            OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.Touch) ||
            OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger, OVRInput.Controller.Touch) ||
            OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger) ||
            OVRInput.GetDown(OVRInput.Button.SecondaryIndexTrigger);
#if UNITY_EDITOR || ENABLE_LEGACY_INPUT_MANAGER
        try { pressed |= editorCutKey != KeyCode.None && Input.GetKeyDown(editorCutKey); }
        catch (InvalidOperationException) { /* legacy input disabled */ }
#endif
        if (!pressed) return;
        if (!held) return;
        if (Time.time - _heldSince < grabCooldown) return;

        TryCut();
    }

    /// <summary>Plays the cut animation and cuts the wire currently between the jaws (if any).</summary>
    public void TryCut()
    {
        if (_finished) return;
        PlayCutAnimation();
        GameObject wire = FindWireInJaws();
        if (wire == null)
        {
            Debug.Log("[WireCutter] Snip - no wire between the jaws.");
            return;
        }
        CutWire(wire);
    }

    private static readonly float[] JawRadii = { 0.006f, 0.012f, 0.025f, 0.045f };

    private GameObject FindWireInJaws()
    {
        if (cutVolume == null) return null;
        Transform t = cutVolume.transform;
        Vector3 center = t.TransformPoint(cutVolume.center);
        Vector3 jaw = cutPoint != null ? cutPoint.position : center;

        // 1) Growing spheres around the jaw point: picks the wire that is really between the jaws.
        foreach (float r in JawRadii)
        {
            int n = Physics.OverlapSphereNonAlloc(jaw, r, _overlapBuffer, ~0, QueryTriggerInteraction.Collide);
            GameObject hit = ClosestWire(n, jaw);
            if (hit != null) return hit;
        }

        // 2) Fallback: anything touching the cutter's volume.
        Vector3 halfExtents = Vector3.Scale(cutVolume.size * 0.5f, Abs(t.lossyScale)) + Vector3.one * detectionPadding;
        int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapBuffer, t.rotation, ~0, QueryTriggerInteraction.Collide);
        return ClosestWire(count, jaw);
    }

    private GameObject ClosestWire(int count, Vector3 jaw)
    {
        GameObject best = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < count; i++)
        {
            Collider c = _overlapBuffer[i];
            if (c == null || !_colliderToWire.TryGetValue(c, out GameObject wire)) continue;
            if (wire == null || !wire.activeInHierarchy || _cutWires.Contains(wire)) continue;
            float d = (c.bounds.ClosestPoint(jaw) - jaw).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = wire; }
        }
        return best;
    }

    private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    private int StepIndexOf(GameObject wire)
    {
        for (int i = 0; i < sequence.Count; i++)
        {
            if (sequence[i]?.wires != null && Array.IndexOf(sequence[i].wires, wire) >= 0) return i;
        }
        return -1;
    }

    /// <summary>Cuts the given wire and evaluates the defusal sequence.</summary>
    public void CutWire(GameObject wire)
    {
        if (_finished || wire == null || _cutWires.Contains(wire)) return;

        _cutWires.Add(wire);
        wire.SetActive(false);
        if (cutSound != null) AudioSource.PlayClipAtPoint(cutSound, wire.transform.position);
        onWireCut?.Invoke();

        if (safeWires != null && Array.IndexOf(safeWires, wire) >= 0)
        {
            Debug.Log($"[WireCutter] Safe wire cut: {wire.name.Trim()} (not linked to the explosives - nothing happens).");
            StartCoroutine(HapticPulse(0.08f, 0.3f));
            return;
        }

        int stepIndex = StepIndexOf(wire);
        if (stepIndex == _currentStep)
        {
            Debug.Log($"[WireCutter] Correct wire cut: {wire.name.Trim()} (step {_currentStep + 1}/{sequence.Count} '{sequence[_currentStep].label}').");
            StartCoroutine(HapticPulse(0.15f, 0.5f));
            AdvanceIfStepComplete();
        }
        else
        {
            string why = stepIndex < 0 ? "decoy wire" : $"belongs to step {stepIndex + 1}, current step is {_currentStep + 1}";
            Debug.Log($"[WireCutter] WRONG wire cut: {wire.name.Trim()} ({why}). Detonating!");
            SetFailMessage("Wrong wire - mission failed.\nCheck the manual on the tablet\nand try again.");
            StartCoroutine(HapticPulse(0.5f, 1f));
            StartCoroutine(Detonate(wire.transform.position));
        }
    }

    private void AdvanceIfStepComplete()
    {
        while (_currentStep < sequence.Count && StepComplete(sequence[_currentStep]))
        {
            if (sequence[_currentStep].stopsTimer && timer != null && timer.IsRunning)
            {
                timer.Stop();
                Debug.Log($"[WireCutter] Timer stopped at {timer.Display} - now cut the wire connected to the timer.");
            }
            _currentStep++;
        }
        if (_currentStep >= sequence.Count)
        {
            _finished = true;
            if (timer != null) timer.Stop();
            Debug.Log("[WireCutter] All wires cut in the right order - bomb defused!");
            ShowEndPanel(missionPassedCanvas);
            onDefused?.Invoke();
            if (pauseOnEnd) Time.timeScale = 0f;
        }
    }

    private bool StepComplete(WireStep step)
    {
        if (step?.wires == null) return true;
        foreach (GameObject w in step.wires)
        {
            if (w != null && !_cutWires.Contains(w)) return false;
        }
        return true;
    }

    /// <summary>Called by the <see cref="BDSBombTimer"/> when it reaches zero.</summary>
    public void OnTimerExpired()
    {
        if (_finished) return;
        Debug.Log("[WireCutter] Time is up - detonating!");
        SetFailMessage("Time ran out - mission failed.\nFollow the manual on the tablet\nfaster and try again.");
        StartCoroutine(HapticPulse(0.5f, 1f));
        Vector3 pos = timer != null ? timer.transform.position : transform.position;
        StartCoroutine(Detonate(pos, 0f));
    }

    private void SetFailMessage(string message)
    {
        if (missionFailedCanvas == null) return;
        EndGamePanel panel = missionFailedCanvas.GetComponent<EndGamePanel>();
        if (panel != null) panel.SetMessage(message);
    }

    private IEnumerator Detonate(Vector3 position) => Detonate(position, explosionDelay);

    private IEnumerator Detonate(Vector3 position, float delay)
    {
        _finished = true;
        if (timer != null) timer.Stop();
        if (warningSound != null) warningSound.Play();
        if (delay > 0f) yield return new WaitForSeconds(delay);
        if (warningSound != null) warningSound.Stop();
        if (explosionPrefab != null) Instantiate(explosionPrefab, position, Quaternion.identity);
        if (burningFirePrefab != null) Instantiate(burningFirePrefab, position, Quaternion.identity);
        ShowEndPanel(missionFailedCanvas);
        onDetonated?.Invoke();
        if (pauseOnEnd) Time.timeScale = 0f;
    }

    /// <summary>Shows a mission-end canvas in front of the player (EndGamePanel) or just enables it.</summary>
    private static void ShowEndPanel(GameObject panel)
    {
        if (panel == null) return;
        EndGamePanel endPanel = panel.GetComponent<EndGamePanel>();
        if (endPanel != null) endPanel.Show();
        else panel.SetActive(true);
    }

    private void PlayCutAnimation()
    {
        if (_animator == null || _animator.runtimeAnimatorController == null) return;
        StartCoroutine(PlayAnimationOnce());
    }

    private IEnumerator PlayAnimationOnce()
    {
        _animator.enabled = true;
        _animator.SetTrigger("Cut");
        yield return null;
        yield return new WaitForSeconds(Mathf.Max(0.1f, _animator.GetCurrentAnimatorStateInfo(0).length));
        _animator.enabled = false;
    }

    private IEnumerator HapticPulse(float duration, float strength)
    {
        OVRInput.SetControllerVibration(1, strength, OVRInput.Controller.RTouch);
        OVRInput.SetControllerVibration(1, strength, OVRInput.Controller.LTouch);
        yield return new WaitForSecondsRealtime(duration);
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
    }

    /// <summary>Restores every wire and restarts the sequence (e.g. for a "Try again" button).</summary>
    public void ResetPuzzle()
    {
        StopAllCoroutines();
        foreach (GameObject w in _cutWires) if (w != null) w.SetActive(true);
        _cutWires.Clear();
        _currentStep = 0;
        _finished = false;
        if (timer != null) timer.ResetTimer();
        if (missionFailedCanvas != null) missionFailedCanvas.SetActive(false);
        if (missionPassedCanvas != null) missionPassedCanvas.SetActive(false);
        if (pauseOnEnd) Time.timeScale = 1f;
    }

    // ---------- Debug helpers (right-click the component header in Play mode) ----------

    [ContextMenu("Debug/Cut next correct wire")]
    private void DebugCutNextCorrect()
    {
        if (_currentStep >= sequence.Count) return;
        foreach (GameObject w in sequence[_currentStep].wires)
        {
            if (w != null && !_cutWires.Contains(w)) { CutWire(w); return; }
        }
    }

    [ContextMenu("Debug/Cut first decoy wire")]
    private void DebugCutDecoy()
    {
        foreach (GameObject w in decoyWires)
        {
            if (w != null && !_cutWires.Contains(w)) { CutWire(w); return; }
        }
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider box = cutVolume != null ? cutVolume : GetComponent<BoxCollider>();
        if (box == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.6f);
        Gizmos.matrix = box.transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
        Gizmos.matrix = Matrix4x4.identity;
        if (cutPoint != null) Gizmos.DrawWireSphere(cutPoint.position, 0.01f);
    }
}
