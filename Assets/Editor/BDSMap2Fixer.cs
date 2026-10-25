#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// One-click scene fixer for the Meta XR (Interaction SDK) version of the bomb-defusal scene (MAP2).
/// Menu: Tools > BDS > Fix Tablet + Wire Cutter In Open Scene
/// Safe to run more than once.
/// </summary>
public static class BDSMap2Fixer
{
    private const string ExplosionPrefabGuid = "e1b08032bec16ba4eb1ae23cbd572664"; // BigExplosion
    private const string FirePrefabGuid = "bb98861c48ad2024ca93498c7dd93a6c";      // LargeFlames

    [MenuItem("Tools/BDS/Fix Tablet + Wire Cutter In Open Scene")]
    public static void FixOpenScene()
    {
        var log = new StringBuilder("[BDS Fixer]\n");
        var scene = EditorSceneManager.GetActiveScene();
        Undo.SetCurrentGroupName("BDS fix tablet + wire cutter");

        FixEventSystem(log);
        FixTablet(log);
        FixWireCutter(log);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        log.AppendLine("Scene saved: " + scene.path);
        Debug.Log(log.ToString());
    }

    [MenuItem("Tools/BDS/Run Play-Mode Self Test")]
    public static void RunSelfTest()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("[BDS] Enter Play mode first, then run Tools > BDS > Run Play-Mode Self Test.");
            return;
        }
        new GameObject("BDS Self Test").AddComponent<BDSSelfTest>();
    }

    // ------------------------------------------------------------------ helpers
    private static List<Transform> AllTransforms()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var list = new List<Transform>();
        foreach (var root in scene.GetRootGameObjects())
            list.AddRange(root.GetComponentsInChildren<Transform>(true));
        return list;
    }

    private static GameObject Find(string name, Transform under = null)
    {
        IEnumerable<Transform> pool = under != null ? under.GetComponentsInChildren<Transform>(true) : AllTransforms();
        return pool.FirstOrDefault(t => t.name.Trim() == name)?.gameObject;
    }

    private static void SetRef(Object target, string prop, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[BDS Fixer] {target.GetType().Name}.{prop} not found"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string prop, bool value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[BDS Fixer] {target.GetType().Name}.{prop} not found"); return; }
        p.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T Load<T>(string guid) where T : Object
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
    }

    // ------------------------------------------------------------------ 1. Event system
    private static void FixEventSystem(StringBuilder log)
    {
        // ISDK PointableCanvas components need a PointableCanvasModule on an EventSystem,
        // otherwise poke/ray events never reach the uGUI Buttons (NextPage/PreviousPage never fire).
        var es = Object.FindObjectOfType<EventSystem>(true);
        if (es == null)
        {
            var go = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
            es = go.AddComponent<EventSystem>();
            log.AppendLine("+ Created EventSystem");
        }
        if (es.GetComponent<PointableCanvasModule>() == null)
        {
            Undo.AddComponent<PointableCanvasModule>(es.gameObject);
            log.AppendLine("+ Added PointableCanvasModule to EventSystem");
        }
    }

    // ------------------------------------------------------------------ 2. Tablet
    private static void FixTablet(StringBuilder log)
    {
        GameObject tab = Find("MainTab");
        if (tab == null) { log.AppendLine("! MainTab not found - tablet skipped"); return; }

        // a) Remove leftover XR Interaction Toolkit grab (there are no XRI interactors in the Meta rig).
        foreach (var c in tab.GetComponents<Component>())
        {
            if (c != null && c.GetType().Name == "XRGrabInteractable")
            {
                Undo.DestroyObjectImmediate(c);
                log.AppendLine("- Removed XRGrabInteractable from MainTab");
            }
        }

        var rb = tab.GetComponent<Rigidbody>();
        var box = tab.GetComponent<BoxCollider>();
        Canvas rootCanvas = tab.GetComponentsInChildren<Canvas>(true).FirstOrDefault(c => c.transform.parent == tab.transform);
        if (rb == null || box == null || rootCanvas == null)
        {
            log.AppendLine("! MainTab is missing Rigidbody/BoxCollider/Canvas - tablet skipped");
            return;
        }

        // b) The page images and buttons were flipped (rotated 180 deg) so they read correctly from above,
        //    but the Canvas itself still faced downwards. That made the GraphicRaycaster treat both
        //    buttons as "reversed" (ignored) and made the ISDK ray/poke surfaces face the floor.
        //    Flip the canvas instead and un-flip the children - the result looks exactly the same.
        Vector3 viewerSide = rootCanvas.transform.position - box.bounds.center; // designer put the canvas on the screen side
        bool facingAway = Vector3.Dot(rootCanvas.transform.forward, viewerSide) > 0f; // UI is read from its -forward side
        if (facingAway)
        {
            var kids = new List<(Transform t, Vector3 p, Quaternion r)>();
            foreach (Transform child in rootCanvas.transform)
            {
                if (child.name.StartsWith("ISDK_")) continue; // interaction surfaces must turn with the canvas
                kids.Add((child, child.position, child.rotation));
                Undo.RecordObject(child, "Unflip child");
            }
            Undo.RecordObject(rootCanvas.transform, "Flip canvas");
            rootCanvas.transform.localRotation = rootCanvas.transform.localRotation * Quaternion.Euler(180f, 0f, 0f);
            foreach (var k in kids) k.t.SetPositionAndRotation(k.p, k.r);
            log.AppendLine("~ Canvas flipped to face the viewer; pages/buttons kept in place");
        }

        // c2) Lift the canvas just above the tablet's collider so ray hits on the buttons win over the
        //     tablet's own ray-grab collider (below).
        Undo.RecordObject(rootCanvas.transform, "Move canvas");
        float sign = Mathf.Sign(tab.transform.InverseTransformPoint(rootCanvas.transform.position).z - box.center.z);
        if (sign == 0) sign = 1;
        float lossyZ = Mathf.Abs(tab.transform.lossyScale.z);
        float targetZ = box.center.z + sign * (box.size.z * 0.5f + 0.0015f / Mathf.Max(lossyZ, 1e-4f));
        var lp = rootCanvas.transform.localPosition;
        rootCanvas.transform.localPosition = new Vector3(lp.x, lp.y, targetZ);
        log.AppendLine($"~ Canvas local z {lp.z:F4} -> {targetZ:F4}");

        // c) Pages are not clickable: stop them from swallowing UI raycasts.
        var switcher = rootCanvas.GetComponent<PageSwitcher>();
        if (switcher != null && switcher.pages != null)
        {
            foreach (var page in switcher.pages)
            {
                if (page == null) continue;
                foreach (var g in page.GetComponentsInChildren<Graphic>(true))
                {
                    if (!g.raycastTarget) continue;
                    Undo.RecordObject(g, "raycastTarget");
                    g.raycastTarget = false;
                }
            }
            log.AppendLine("~ Page images: raycastTarget off");
        }

        // d) Ray UI: one ISDK ray interactable per button, all wired to the ROOT canvas
        //    (PointableCanvasModule only accepts hits whose rootCanvas == the pointable canvas).
        GameObject next = Find("Nexbutton", rootCanvas.transform);
        GameObject prev = Find("Previousbutton", rootCanvas.transform);
        GameObject nextRay = next != null ? Find("ISDK_RayInteraction", next.transform) : null;
        GameObject nextPoke = next != null ? Find("ISDK_PokeInteraction", next.transform) : null;

        if (nextRay != null)
        {
            Undo.RecordObject(nextRay, "Enable");
            nextRay.SetActive(true);
            foreach (var pc in nextRay.GetComponentsInChildren<PointableCanvas>(true)) SetRef(pc, "_canvas", rootCanvas);
            foreach (var ps in nextRay.GetComponentsInChildren<PlaneSurface>(true)) SetBool(ps, "_doubleSided", true);
            log.AppendLine("~ Nexbutton ray -> root canvas");

            if (prev != null && Find("ISDK_RayInteraction", prev.transform) == null)
            {
                var copy = Object.Instantiate(nextRay, prev.transform, false);
                copy.name = "ISDK_RayInteraction";
                Undo.RegisterCreatedObjectUndo(copy, "Prev ray");
                foreach (var pc in copy.GetComponentsInChildren<PointableCanvas>(true)) SetRef(pc, "_canvas", rootCanvas);
                log.AppendLine("+ Previousbutton ray interaction added");
            }
        }
        if (nextPoke != null)
        {
            Undo.RecordObject(nextPoke, "Disable");
            nextPoke.SetActive(false); // the canvas-wide poke below already covers both buttons
            log.AppendLine("- Nexbutton poke (wired to nested canvas, never fired) disabled");
        }

        foreach (Transform child in rootCanvas.transform)
        {
            if (child.name == "ISDK_RayInteraction")
            {
                Undo.RecordObject(child.gameObject, "Disable");
                child.gameObject.SetActive(false); // canvas-wide ray would block ray-grabbing the tablet
                log.AppendLine("- Canvas-wide ray interaction disabled (buttons have their own)");
            }
            if (child.name == "ISDK_PokeInteraction")
            {
                Undo.RecordObject(child.gameObject, "Enable");
                child.gameObject.SetActive(true);
                foreach (var pc in child.GetComponentsInChildren<PointableCanvas>(true)) SetRef(pc, "_canvas", rootCanvas);
            }
        }

        // e) Ray grab for the tablet (same setup the wire cutter uses).
        Grabbable existingGrabbable = tab.GetComponentsInChildren<Grabbable>(true).FirstOrDefault();
        if (Find("ISDK_RayGrabInteraction", tab.transform) == null)
        {
            var go = new GameObject("ISDK_RayGrabInteraction");
            Undo.RegisterCreatedObjectUndo(go, "Tablet ray grab");
            go.transform.SetParent(tab.transform, false);

            var grabbable = go.AddComponent<Grabbable>();
            SetRef(grabbable, "_targetTransform", tab.transform);
            SetRef(grabbable, "_rigidbody", rb);

            var surface = go.AddComponent<ColliderSurface>();
            SetRef(surface, "_collider", box);

            var mover = go.AddComponent<MoveFromTargetProvider>();

            var ray = go.AddComponent<RayInteractable>();
            SetRef(ray, "_pointableElement", grabbable);
            SetRef(ray, "_surface", surface);
            SetRef(ray, "_movementProvider", mover);
            log.AppendLine("+ Tablet ray grab added (point at the tablet frame + trigger, or grip it directly)");
        }
        if (existingGrabbable == null) log.AppendLine("! Tablet had no hand/controller grab - only ray grab is set up");
    }

    // ------------------------------------------------------------------ 3. Wire cutter
    private static void FixWireCutter(StringBuilder log)
    {
        GameObject cutterGo = Find("wirecutter");
        GameObject bomb = Find("Suitcasebomb");
        if (cutterGo == null || bomb == null) { log.AppendLine("! wirecutter or Suitcasebomb not found - cutter skipped"); return; }

        // Wires the player can cut (pieces named cut001..cut011 under the suitcase).
        var wires = bomb.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name.Trim().StartsWith("cut"))
            .Select(t => t.gameObject)
            .OrderBy(g => g.name.Trim())
            .ToList();

        foreach (var w in wires)
        {
            // Old helper destroyed objects on trigger contact and bypassed the sequence.
            foreach (var legacy in w.GetComponents<MonoBehaviour>())
            {
                if (legacy != null && legacy.GetType().Name == "WireCuttingScript")
                {
                    Undo.DestroyObjectImmediate(legacy);
                    log.AppendLine($"- Removed WireCuttingScript from {w.name.Trim()}");
                }
            }
            // Make sure each wire has a solid collider that follows its mesh.
            if (w.GetComponents<Collider>().All(c => c.isTrigger))
            {
                var mf = w.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    var mc = Undo.AddComponent<MeshCollider>(w);
                    mc.sharedMesh = mf.sharedMesh;
                    log.AppendLine($"+ MeshCollider on {w.name.Trim()}");
                }
            }
        }

        var cutter = cutterGo.GetComponent<WireCutter>();
        if (cutter == null)
        {
            cutter = Undo.AddComponent<WireCutter>(cutterGo);
            log.AppendLine("+ Added WireCutter");
        }
        Undo.RecordObject(cutter, "Configure WireCutter");

        GameObject W(string n) => wires.FirstOrDefault(g => g.name.Trim() == n);

        // Default sequence (edit in the Inspector to change the puzzle):
        //   Step 1 - the two yellow wires that go to the detonator cylinders (any order)
        //   Step 2 - the white ribbon cable to the timer board
        //   Everything else is a decoy and detonates the bomb.
        if (cutter.sequence == null || cutter.sequence.Count == 0)
        {
            cutter.sequence = new List<WireCutter.WireStep>
            {
                new WireCutter.WireStep { label = "Detonator wires (any order)", wires = new[] { W("cut009"), W("cut011") }.Where(g => g != null).ToArray() },
                new WireCutter.WireStep { label = "Timer wire", wires = new[] { W("cut001") }.Where(g => g != null).ToArray() },
            };
            var used = new HashSet<GameObject>(cutter.sequence.SelectMany(s => s.wires));
            cutter.decoyWires = wires.Where(g => !used.Contains(g)).ToArray();
            log.AppendLine("~ WireCutter sequence: [cut009, cut011] -> [cut001]; decoys: " + string.Join(", ", cutter.decoyWires.Select(g => g.name.Trim())));
        }

        if (cutter.cutVolume == null) cutter.cutVolume = cutterGo.GetComponent<BoxCollider>();
        if (cutter.cutPoint == null && cutter.cutVolume != null)
        {
            // The jaws are at the +X end of the cutter model.
            var cp = new GameObject("CutPoint");
            Undo.RegisterCreatedObjectUndo(cp, "CutPoint");
            cp.transform.SetParent(cutterGo.transform, false);
            BoxCollider bc = cutter.cutVolume;
            cp.transform.localPosition = new Vector3(bc.center.x + bc.size.x * 0.5f - 0.015f, bc.center.y, bc.center.z);
            cutter.cutPoint = cp.transform;
            log.AppendLine("+ CutPoint added at the cutter jaws");
        }
        if (cutter.explosionPrefab == null) cutter.explosionPrefab = Load<GameObject>(ExplosionPrefabGuid);
        if (cutter.burningFirePrefab == null) cutter.burningFirePrefab = Load<GameObject>(FirePrefabGuid);
        EditorUtility.SetDirty(cutter);
        log.AppendLine("~ WireCutter configured (explosion: " + (cutter.explosionPrefab ? cutter.explosionPrefab.name : "none") + ")");
    }
}
#endif
