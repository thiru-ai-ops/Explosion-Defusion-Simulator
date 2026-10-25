using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Play-mode self test for the tablet (grab + page buttons) and the wire cutter.
/// Start it from Tools > BDS > Run Play-Mode Self Test while in Play mode.
/// It drives the real Interaction SDK components with synthetic pointer events, so it works
/// without a headset. Everything it changes is put back at the end.
/// </summary>
public class BDSSelfTest : MonoBehaviour
{
    private readonly StringBuilder _report = new StringBuilder();
    private int _pass, _fail, _nextId = 9000;

    private void Check(bool ok, string message)
    {
        if (ok) _pass++; else _fail++;
        _report.AppendLine((ok ? "PASS  " : "FAIL  ") + message);
    }

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject); // survives the scene restarts in TestEndGame
        yield return null;
        yield return null;
        yield return TestTablet();
        yield return TestCutter();
        yield return TestEndGame();
        string summary = $"[BDS SelfTest] {_pass} passed, {_fail} failed\n{_report}";
        if (_fail == 0) Debug.Log(summary); else Debug.LogError(summary);
        Destroy(gameObject);
    }

    private static T FindByName<T>(string n) where T : Component =>
        FindObjectsOfType<T>(true).FirstOrDefault(c => c.name.Trim() == n);

    private static int ActivePage(PageSwitcher ps)
    {
        for (int i = 0; i < ps.pages.Length; i++) if (ps.pages[i] != null && ps.pages[i].activeSelf) return i;
        return -1;
    }

    private static Vector3 RectCenter(RectTransform rt) => rt.TransformPoint(rt.rect.center);

    private IEnumerator Click(PointableElement pe, Vector3 pos)
    {
        int id = _nextId++;
        var pose = new Pose(pos, Quaternion.identity);
        pe.ProcessPointerEvent(new PointerEvent(id, PointerEventType.Hover, pose)); yield return null;
        pe.ProcessPointerEvent(new PointerEvent(id, PointerEventType.Select, pose)); yield return null;
        pe.ProcessPointerEvent(new PointerEvent(id, PointerEventType.Unselect, pose)); yield return null;
        pe.ProcessPointerEvent(new PointerEvent(id, PointerEventType.Unhover, pose)); yield return null;
        yield return new WaitForSecondsRealtime(0.3f);
    }

    // ------------------------------------------------------------------ tablet
    private IEnumerator TestTablet()
    {
        Transform tab = FindByName<Transform>("MainTab");
        Check(tab != null, "MainTab found");
        if (tab == null) yield break;

        Canvas canvas = tab.GetComponentsInChildren<Canvas>(true).FirstOrDefault(c => c.transform.parent == tab);
        PageSwitcher ps = canvas != null ? canvas.GetComponent<PageSwitcher>() : null;
        Check(ps != null, "PageSwitcher on tablet canvas");
        if (ps == null) yield break;
        Check(FindObjectOfType<PointableCanvasModule>() != null, "PointableCanvasModule present in scene");

        Vector3 viewer = -canvas.transform.forward;                       // side the UI is read from
        Check(Vector3.Dot(viewer, Vector3.up) > 0.9f, "Tablet UI is readable from above (canvas faces the player) " + viewer.ToString("F2"));

        RectTransform next = FindByName<RectTransform>("Nexbutton");
        RectTransform prev = FindByName<RectTransform>("Previousbutton");
        RayInteractable nextRay = next.GetComponentsInChildren<RayInteractable>(true).FirstOrDefault();
        RayInteractable prevRay = prev.GetComponentsInChildren<RayInteractable>(true).FirstOrDefault();
        Transform grabT = tab.Find("ISDK_RayGrabInteraction");
        RayInteractable tabRay = grabT != null ? grabT.GetComponent<RayInteractable>() : null;
        Check(nextRay != null && nextRay.isActiveAndEnabled, "Next button has an active ray interactable");
        Check(prevRay != null && prevRay.isActiveAndEnabled, "Previous button has an active ray interactable");
        Check(tabRay != null && tabRay.isActiveAndEnabled, "Tablet has an active ray-grab interactable");
        if (nextRay == null || prevRay == null || tabRay == null) yield break;

        // Ray priority: on a button the UI must be hit before the tablet's grab collider.
        Vector3 nc = RectCenter(next);
        var rayOnBtn = new Ray(nc + viewer * 0.5f, -viewer);
        bool hb = nextRay.Surface.Raycast(rayOnBtn, out SurfaceHit hB);
        bool ht = tabRay.Surface.Raycast(rayOnBtn, out SurfaceHit hT);
        Check(hb && (!ht || hB.Distance < hT.Distance), $"Ray at Next button hits the button first (btn {(hb ? hB.Distance : -1):F4}m, tablet {(ht ? hT.Distance : -1):F4}m)");
        var rayOnScreen = new Ray(tab.position + viewer * 0.5f, -viewer);
        Check(!nextRay.Surface.Raycast(rayOnScreen, out _) && tabRay.Surface.Raycast(rayOnScreen, out _), "Ray at tablet centre hits the tablet (grab), not a button");

        // Page buttons through the real ISDK -> PointableCanvasModule -> uGUI Button path.
        PointableElement nextPointable = nextRay.GetComponent<PointableCanvas>();
        PointableElement prevPointable = prevRay.GetComponent<PointableCanvas>();
        PointableElement poke = canvas.transform.Cast<Transform>()
            .Where(t => t.name == "ISDK_PokeInteraction" && t.gameObject.activeInHierarchy)
            .Select(t => (PointableElement)t.GetComponent<PointableCanvas>()).FirstOrDefault();

        int p0 = ActivePage(ps);
        yield return Click(nextPointable, nc);
        int p1 = ActivePage(ps);
        Check(p1 == p0 + 1, $"Ray click on Next: page {p0 + 1} -> {p1 + 1}");

        if (poke != null)
        {
            yield return Click(poke, nc);
            int p2 = ActivePage(ps);
            Check(p2 == p1 + 1, $"Poke on Next: page {p1 + 1} -> {p2 + 1}");
        }
        else Check(false, "Canvas poke interaction present");

        int p3 = ActivePage(ps);
        yield return Click(prevPointable, RectCenter(prev));
        int p4 = ActivePage(ps);
        Check(p4 == p3 - 1, $"Ray click on Previous: page {p3 + 1} -> {p4 + 1}");

        if (poke != null)
        {
            yield return Click(poke, RectCenter(prev));
            Check(ActivePage(ps) == p4 - 1, $"Poke on Previous: page {p4 + 1} -> {ActivePage(ps) + 1}");
        }

        // Grab: select + move the tablet's Grabbable like the ray interactor would.
        Grabbable g = tabRay.GetComponent<Grabbable>();
        Vector3 start = tab.position; Quaternion startRot = tab.rotation;
        int id = _nextId++;
        var pose0 = new Pose(start, startRot);
        g.ProcessPointerEvent(new PointerEvent(id, PointerEventType.Hover, pose0));
        g.ProcessPointerEvent(new PointerEvent(id, PointerEventType.Select, pose0));
        yield return null;
        var pose1 = new Pose(start + Vector3.up * 0.2f + Vector3.right * 0.1f, startRot);
        g.ProcessPointerEvent(new PointerEvent(id, PointerEventType.Move, pose1));
        yield return null;
        float err = (tab.position - pose1.position).magnitude;
        Check(err < 0.01f, $"Tablet follows the grab (error {err * 100f:F1} cm)");
        g.ProcessPointerEvent(new PointerEvent(id, PointerEventType.Unselect, pose1));
        g.ProcessPointerEvent(new PointerEvent(id, PointerEventType.Unhover, pose1));
        yield return null;
        tab.SetPositionAndRotation(start, startRot);
        var rb = tab.GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
    }

    // ------------------------------------------------------------------ wire cutter
    private IEnumerator TestCutter()
    {
        WireCutter wc = FindObjectOfType<WireCutter>();
        Check(wc != null, "WireCutter component found (script no longer missing)");
        if (wc == null) yield break;

        Check(!wc.IsHeldNow, "Cutter does not cut while lying on the table (not held)");

        Transform t = wc.transform;
        Vector3 startPos = t.position; Quaternion startRot = t.rotation;
        Grabbable grab = wc.GetComponentsInChildren<Grabbable>(true).FirstOrDefault();
        int id = _nextId++;
        var pose = new Pose(startPos, startRot);
        grab.ProcessPointerEvent(new PointerEvent(id, PointerEventType.Hover, pose));
        grab.ProcessPointerEvent(new PointerEvent(id, PointerEventType.Select, pose));
        yield return null;
        Check(wc.IsHeldNow, "Cutter reports held after a grab");

        var safe = (wc.safeWires ?? new GameObject[0]).Where(w => w != null).ToList();
        var allWires = wc.sequence.SelectMany(s => s.wires).Concat(wc.decoyWires).Concat(safe).Where(w => w != null).ToList();
        var order = wc.sequence.SelectMany(s => s.wires).Where(w => w != null).ToList();

        // Manual procedure: red power wire -> both yellow detonator wires (timer stops) -> timer ribbon.
        Check(wc.sequence.Count == 3 && wc.sequence[1].wires.Length == 2 && wc.sequence[1].stopsTimer,
              "Sequence follows the tablet manual: power, detonator pair (stops timer), timer wire");
        Check(IsColour(wc.sequence[0].wires[0], c => c.r > 0.6f && c.g < 0.3f && c.b < 0.3f), "Power wire is red");
        Check(safe.Count == 1 && IsColour(safe[0], c => c.b > 0.6f && c.r < 0.3f), "Blue wire is the safe (not linked) wire");
        Check(allWires.All(w => !HasVisibleDuplicate(w)), "No duplicate copy of any cuttable wire is left visible");
        BDSBombTimer timer = wc.timer;
        Check(timer != null && timer.display != null && timer.IsRunning, "Bomb timer is counting down on the LED display " + (timer != null ? timer.Display : ""));
        int stopStep = wc.sequence.FindIndex(s => s.stopsTimer);

        // Correct order.
        foreach (GameObject w in order)
        {
            if (!MoveJawsOnto(wc, w, allWires)) { Check(false, $"Could not find a clean spot on {w.name.Trim()}"); continue; }
            wc.TryCut();
            yield return null;
            Check(!w.activeSelf, $"Cut {w.name.Trim()} (step now {wc.CurrentStep + 1})");
            if (timer != null && stopStep >= 0)
            {
                bool shouldRun = wc.CurrentStep <= stopStep;
                Check(timer.IsRunning == shouldRun, shouldRun ? "Timer still running" : $"Timer stopped at {timer.Display} after the detonator wires");
            }
        }
        Check(wc.IsFinished && wc.CurrentStep == wc.sequence.Count, "Bomb defused after the full sequence");

        // Blue (safe) wire: harmless.
        if (safe.Count > 0)
        {
            wc.ResetPuzzle();
            bool boom = false;
            UnityEngine.Events.UnityAction onBoom = () => boom = true;
            wc.onDetonated.AddListener(onBoom);
            if (MoveJawsOnto(wc, safe[0], allWires))
            {
                wc.TryCut();
                yield return new WaitForSeconds(wc.explosionDelay > 1f ? 1f : wc.explosionDelay + 0.5f);
                Check(!safe[0].activeSelf && !boom && !wc.IsFinished && wc.CurrentStep == 0, "Cutting the blue wire is harmless");
            }
            else Check(false, "Could not find a clean spot on the blue wire");
            wc.onDetonated.RemoveListener(onBoom);
        }

        // Timer runs out -> detonation with a "time ran out" message.
        if (timer != null)
        {
            wc.ResetPuzzle();
            bool boom = false;
            UnityEngine.Events.UnityAction onBoom = () => boom = true;
            wc.onDetonated.AddListener(onBoom);
            timer.SetRemaining(0.3f);
            yield return new WaitForSeconds(0.8f);
            var msg = wc.missionFailedCanvas != null
                ? wc.missionFailedCanvas.GetComponentsInChildren<TMPro.TMP_Text>(true).FirstOrDefault(x => x.name == "Message") : null;
            Check(boom && timer.IsExpired && msg != null && msg.text.Contains("Time ran out"), "Timer reaching 00:00 detonates the bomb (\"Time ran out\")");
            wc.onDetonated.RemoveListener(onBoom);
        }

        // Wrong wire -> detonation.
        wc.ResetPuzzle();
        Check(allWires.All(w => w.activeSelf) && !wc.IsFinished, "ResetPuzzle restores all wires");
        bool detonated = false;
        wc.onDetonated.AddListener(() => detonated = true);
        float oldDelay = wc.explosionDelay;
        wc.explosionDelay = 0.5f;
        GameObject wrong = wc.sequence.Count > 1 ? wc.sequence[1].wires.FirstOrDefault() : wc.decoyWires.FirstOrDefault();
        if (wrong != null && MoveJawsOnto(wc, wrong, allWires))
        {
            wc.TryCut();
            yield return new WaitForSeconds(1f);
            Check(detonated, $"Cutting {wrong.name.Trim()} out of order detonates the bomb");
        }
        GameObject decoy = wc.decoyWires.FirstOrDefault(w => w != null);
        wc.ResetPuzzle();
        detonated = false;
        if (decoy != null && MoveJawsOnto(wc, decoy, allWires))
        {
            wc.TryCut();
            yield return new WaitForSeconds(1f);
            Check(detonated, $"Cutting decoy {decoy.name.Trim()} detonates the bomb");
        }

        // Clean up: remove explosions, reset, release and put the cutter back.
        foreach (var go in FindObjectsOfType<ParticleSystem>().Select(p => p.transform.root.gameObject).Distinct())
        {
            if (go.name.Contains("(Clone)")) Destroy(go);
        }
        wc.explosionDelay = oldDelay;
        wc.ResetPuzzle();
        grab.ProcessPointerEvent(new PointerEvent(id, PointerEventType.Unselect, pose));
        grab.ProcessPointerEvent(new PointerEvent(id, PointerEventType.Unhover, pose));
        yield return null;
        t.SetPositionAndRotation(startPos, startRot);
    }

    // ------------------------------------------------------------------ end-game panels
    private IEnumerator TestEndGame()
    {
        WireCutter wc = FindObjectOfType<WireCutter>();
        if (wc == null) yield break;
        wc.ResetPuzzle();
        GameObject passed = wc.missionPassedCanvas, failed = wc.missionFailedCanvas;
        Check(passed != null && failed != null, "End panels are linked to the WireCutter");
        if (passed == null || failed == null) yield break;
        Check(!passed.activeSelf && !failed.activeSelf, "End panels are hidden while playing");

        // Defuse -> congratulations panel.
        foreach (GameObject w in wc.sequence.SelectMany(st => st.wires).Where(w => w != null).ToList()) wc.CutWire(w);
        yield return null;
        Check(passed.activeSelf && !failed.activeSelf, "Congratulations panel appears after defusing");
        Transform head = FindObjectOfType<OVRCameraRig>()?.centerEyeAnchor ?? Camera.main?.transform;
        if (head != null)
        {
            float d = Vector3.Distance(head.position, passed.transform.position);
            float facing = Vector3.Dot(passed.transform.forward, (passed.transform.position - head.position).normalized);
            Check(d > 0.8f && d < 2f && facing > 0.9f, $"Congratulations panel floats in front of the player, readable ({d:F2} m)");
        }

        yield return new WaitForSecondsRealtime(1.2f); // panel ignores clicks for its first second
        bool loaded = false;
        UnityEngine.Events.UnityAction<Scene, LoadSceneMode> onLoaded = (sc, m) => loaded = true;
        SceneManager.sceneLoaded += onLoaded;
        yield return ClickPanelButton(passed);
        float t = 0;
        while (!loaded && t < 15f) { t += Time.unscaledDeltaTime; yield return null; }
        Check(loaded, "Play Again (ray click on the button) restarts the scene");
        yield return null; yield return null; yield return null;

        wc = FindObjectOfType<WireCutter>();
        Check(wc != null && !wc.IsFinished && wc.CurrentStep == 0 && !wc.missionPassedCanvas.activeSelf,
              "After restart the puzzle starts over with all wires");
        if (wc == null) { SceneManager.sceneLoaded -= onLoaded; yield break; }

        // Wrong wire -> explosion -> retry panel.
        wc.explosionDelay = 0.5f;
        GameObject decoy = wc.decoyWires.FirstOrDefault(w => w != null);
        wc.CutWire(decoy);
        yield return new WaitForSeconds(0.2f);
        Check(!wc.missionFailedCanvas.activeSelf, "Retry panel waits for the explosion");
        yield return new WaitForSeconds(1.0f);
        failed = wc.missionFailedCanvas;
        Check(failed.activeSelf && !wc.missionPassedCanvas.activeSelf, "Mission-failed panel with Retry appears after the explosion");

        yield return new WaitForSecondsRealtime(1.2f);
        loaded = false;
        yield return ClickPanelButton(failed);
        t = 0;
        while (!loaded && t < 15f) { t += Time.unscaledDeltaTime; yield return null; }
        Check(loaded, "Retry (ray click on the button) restarts the scene");
        SceneManager.sceneLoaded -= onLoaded;
        yield return null; yield return null;
        wc = FindObjectOfType<WireCutter>();
        Check(wc != null && !wc.IsFinished && !wc.missionFailedCanvas.activeSelf, "After Retry the bomb is back to its starting state");
    }

    private IEnumerator ClickPanelButton(GameObject panel)
    {
        var pointable = panel.GetComponentInChildren<PointableCanvas>(true);
        var button = panel.GetComponentInChildren<Button>(true);
        Check(pointable != null && button != null, $"{panel.name} has a ray-clickable button");
        if (pointable == null || button == null) yield break;
        yield return Click(pointable, RectCenter((RectTransform)button.transform));
    }

    private static bool IsColour(GameObject wire, System.Func<Color, bool> test)
    {
        var r = wire != null ? wire.GetComponent<Renderer>() : null;
        if (r == null || r.sharedMaterial == null) return false;
        Color c = r.sharedMaterial.HasProperty("_BaseColor") ? r.sharedMaterial.GetColor("_BaseColor") : r.sharedMaterial.color;
        return test(c);
    }

    /// <summary>True if the bomb model still shows its own copy of this wire (which would hide the cut).</summary>
    private static bool HasVisibleDuplicate(GameObject wire)
    {
        var mf = wire.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null || wire.transform.parent == null) return false;
        foreach (Transform sib in wire.transform.parent)
        {
            if (sib.gameObject != wire && sib.name == mf.sharedMesh.name && sib.gameObject.activeInHierarchy) return true;
        }
        return false;
    }

    /// <summary>Moves the cutter so its jaw point sits on a spot that touches only the target wire.</summary>
    private static bool MoveJawsOnto(WireCutter wc, GameObject target, List<GameObject> allWires)
    {
        var targetCols = target.GetComponentsInChildren<Collider>(true);
        var others = new HashSet<Collider>(allWires.Where(w => w != target).SelectMany(w => w.GetComponentsInChildren<Collider>(true)));
        Bounds b = targetCols[0].bounds;
        foreach (var c in targetCols) b.Encapsulate(c.bounds);
        const int N = 14;
        var buf = new Collider[32];
        for (int ix = 0; ix <= N; ix++)
        for (int iy = 0; iy <= N; iy++)
        for (int iz = 0; iz <= N; iz++)
        {
            Vector3 p = b.min + Vector3.Scale(b.size, new Vector3(ix, iy, iz) / N);
            int n = Physics.OverlapSphereNonAlloc(p, 0.005f, buf, ~0, QueryTriggerInteraction.Collide);
            bool hitsTarget = false, hitsOther = false;
            for (int i = 0; i < n; i++)
            {
                if (targetCols.Contains(buf[i])) hitsTarget = true;
                if (others.Contains(buf[i])) hitsOther = true;
            }
            if (!hitsTarget || hitsOther) continue;
            // Also make sure no other wire is within the second search radius.
            n = Physics.OverlapSphereNonAlloc(p, 0.013f, buf, ~0, QueryTriggerInteraction.Collide);
            bool crowded = false;
            for (int i = 0; i < n; i++) if (others.Contains(buf[i])) crowded = true;
            if (crowded) continue;

            Vector3 jaw = wc.cutPoint != null ? wc.cutPoint.position : wc.cutVolume.transform.TransformPoint(wc.cutVolume.center);
            wc.transform.position += p - jaw;
            Physics.SyncTransforms();
            return true;
        }
        return false;
    }
}
