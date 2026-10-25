#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes the bomb follow the procedure on the tablet manual (pages 5 and 6):
///   3. Power      - cut the thick RED wire                     (cut008, recoloured red)
///   4. Identify   - the thick BLUE wire is not linked to the explosives: harmless (cut010, recoloured blue)
///   5. Detonator  - cut both YELLOW wires, any order           (cut009, cut011) -> the timer stops
///   6. Timer      - cut the white ribbon to the timer display  (cut001)
///   Wrong order / orange C4 wires -> detonation.
/// Also hides the bomb model's own copies of the cuttable wires (they hid every cut) and adds a
/// countdown on the bomb's LED display. Runs once automatically for MAP2; re-run from
/// Tools > BDS > Apply Tablet-Manual Wire Sequence.
/// </summary>
[InitializeOnLoad]
public static class BDSManualSequenceSetup
{
    private const string Marker = "ProjectSettings/BDS_ManualSequence.done";
    private const string MatFolder = "Assets/Settings/Materials";
    private const string TimerName = "BDS Bomb Timer";
    private static readonly string LogPath = Path.Combine("Logs", "BDS_ManualSequence.txt");

    static BDSManualSequenceSetup()
    {
        if (File.Exists(Marker)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SceneManager.GetActiveScene().name != "MAP2") return;
            if (Apply()) File.WriteAllText(Marker, System.DateTime.Now.ToString("s"));
        };
    }

    [MenuItem("Tools/BDS/Apply Tablet-Manual Wire Sequence")]
    public static void ApplyMenu() => Apply();

    public static bool Apply()
    {
        var log = new System.Text.StringBuilder();
        try
        {
            WireCutter wc = Object.FindObjectOfType<WireCutter>(true);
            if (wc == null) { Write(log.AppendLine("No WireCutter in the open scene.")); return false; }

            var all = Object.FindObjectsOfType<Transform>(true);
            GameObject W(string n) => all.FirstOrDefault(t => t.name.Trim() == n && t.GetComponent<MeshRenderer>() != null)?.gameObject;
            GameObject red = W("cut008"), blue = W("cut010"), y1 = W("cut009"), y2 = W("cut011"), ribbon = W("cut001");
            var decoys = new[] { "cut002", "cut003", "cut004", "cut005" }.Select(W).Where(g => g != null).ToArray();
            if (red == null || blue == null || y1 == null || y2 == null || ribbon == null)
            {
                Write(log.AppendLine("A wire was not found (cut008/cut010/cut009/cut011/cut001)."));
                return false;
            }

            Undo.RecordObject(wc, "BDS manual sequence");

            // 1) Colours: thick red power wire, thick blue safe wire.
            if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/Settings", "Materials");
            Material redMat = LitMaterial("BDS_Wire_Red", new Color(0.85f, 0.05f, 0.04f));
            Material blueMat = LitMaterial("BDS_Wire_Blue", new Color(0.05f, 0.25f, 0.95f));
            SetMaterial(red, redMat);
            SetMaterial(blue, blueMat);
            log.AppendLine($"Recoloured {red.name.Trim()} red and {blue.name.Trim()} blue.");

            // 2) Hide the model's own copy of every cuttable wire, so a cut wire actually disappears.
            foreach (GameObject wire in new[] { red, blue, y1, y2, ribbon }.Concat(decoys))
            {
                Mesh mesh = wire.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null || wire.transform.parent == null) continue;
                foreach (Transform sib in wire.transform.parent)
                {
                    if (sib.gameObject == wire || sib.name != mesh.name || !sib.gameObject.activeSelf) continue;
                    Undo.RecordObject(sib.gameObject, "Hide duplicate wire");
                    sib.gameObject.SetActive(false);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(sib.gameObject);
                    log.AppendLine($"Hid duplicate '{sib.name}' under {wire.name.Trim()}.");
                }
            }

            // 3) Sequence from the manual.
            wc.sequence = new List<WireCutter.WireStep>
            {
                new WireCutter.WireStep { label = "Power - thick red wire", wires = new[] { red } },
                new WireCutter.WireStep { label = "Detonator - both yellow wires (any order)", wires = new[] { y1, y2 }, stopsTimer = true },
                new WireCutter.WireStep { label = "Timer - white ribbon to the timer display", wires = new[] { ribbon } },
            };
            wc.decoyWires = decoys;
            wc.safeWires = new[] { blue };
            log.AppendLine("Sequence: red -> yellow+yellow (timer stops) -> white ribbon. Decoys: orange C4 wires. Safe: blue.");

            // 4) Countdown on the bomb's LED display.
            BDSBombTimer timer = BuildTimer(all, log);
            if (timer != null)
            {
                timer.cutter = wc;
                wc.timer = timer;
                EditorUtility.SetDirty(timer);
            }

            EditorUtility.SetDirty(wc);
            Scene scene = wc.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine("Scene saved.");
            Write(log);
            Debug.Log("[BDS Manual Sequence] " + log);
            return true;
        }
        catch (System.Exception e)
        {
            Write(log.AppendLine("ERROR: " + e));
            Debug.LogError("[BDS Manual Sequence] " + e);
            return false;
        }
    }

    private static BDSBombTimer BuildTimer(Transform[] all, System.Text.StringBuilder log)
    {
        Transform display = all.FirstOrDefault(t => t.name == "Bomb.019" && t.GetComponent<MeshFilter>() != null);
        if (display == null) { log.AppendLine("LED display (Bomb.019) not found - no timer added."); return null; }

        Transform old = display.parent.Find(TimerName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // Display box in world space: find its vertical (thin) axis and its long axis.
        Mesh m = display.GetComponent<MeshFilter>().sharedMesh;
        Vector3 c = display.TransformPoint(m.bounds.center);
        Vector3[] axes =
        {
            display.TransformVector(new Vector3(m.bounds.size.x, 0, 0)),
            display.TransformVector(new Vector3(0, m.bounds.size.y, 0)),
            display.TransformVector(new Vector3(0, 0, m.bounds.size.z)),
        };
        int up = Enumerable.Range(0, 3).OrderByDescending(i => Mathf.Abs(Vector3.Dot(axes[i].normalized, Vector3.up))).First();
        var flat = Enumerable.Range(0, 3).Where(i => i != up).OrderByDescending(i => axes[i].magnitude).ToArray();
        Vector3 upV = axes[up] * Mathf.Sign(Vector3.Dot(axes[up], Vector3.up));
        Vector3 longV = axes[flat[0]], shortV = axes[flat[1]];
        Vector3 top = c + upV * 0.5f;

        // Letters' tops point away from the player, so the digits read the right way up from where they stand.
        OVRCameraRig rig = Object.FindObjectOfType<OVRCameraRig>(true);
        Vector3 toPlayer = rig != null ? Vector3.ProjectOnPlane(rig.transform.position - top, Vector3.up) : Vector3.back;
        Vector3 letterUp = Vector3.ProjectOnPlane(shortV, Vector3.up).normalized;
        if (Vector3.Dot(letterUp, toPlayer) > 0) letterUp = -letterUp;
        Quaternion rot = Quaternion.LookRotation(Vector3.down, letterUp); // faces up, readable from above

        var root = new GameObject(TimerName);
        Undo.RegisterCreatedObjectUndo(root, "Create bomb timer");
        root.transform.SetParent(display.parent, false);
        root.transform.SetPositionAndRotation(top, rot);
        Vector3 ps = display.parent.lossyScale;
        root.transform.localScale = new Vector3(1f / ps.x, 1f / ps.y, 1f / ps.z); // 1 unit = 1 metre below here

        float w = longV.magnitude, h = shortV.magnitude;

        GameObject back = GameObject.CreatePrimitive(PrimitiveType.Quad);
        back.name = "Backing";
        Object.DestroyImmediate(back.GetComponent<Collider>());
        back.transform.SetParent(root.transform, false);
        back.transform.localPosition = new Vector3(0, 0, -0.0012f);
        back.transform.localScale = new Vector3(w * 0.97f, h * 0.92f, 1f);
        back.GetComponent<MeshRenderer>().sharedMaterial = UnlitMaterial("BDS_TimerBacking", new Color(0.03f, 0.02f, 0.02f));
        back.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var textGo = new GameObject("Digits", typeof(RectTransform));
        textGo.transform.SetParent(root.transform, false);
        textGo.transform.localPosition = new Vector3(0, 0, -0.0022f);
        var tmp = textGo.AddComponent<TextMeshPro>();
        tmp.rectTransform.sizeDelta = new Vector2(w * 0.9f, h * 0.8f);
        tmp.text = "05:00";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.01f;
        tmp.fontSizeMax = 5f;
        tmp.enableWordWrapping = false;
        tmp.color = new Color(1f, 0.12f, 0.08f);
        tmp.raycastTarget = false;
        tmp.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        BDSBombTimer timer = root.AddComponent<BDSBombTimer>();
        timer.duration = 300f;
        timer.display = tmp;
        log.AppendLine($"Timer added on the LED display at {top:F3} ({w * 100f:F1} x {h * 100f:F1} cm), 5:00 countdown.");
        return timer;
    }

    private static void SetMaterial(GameObject go, Material mat)
    {
        var r = go.GetComponent<MeshRenderer>();
        Undo.RecordObject(r, "Recolour wire");
        var mats = r.sharedMaterials;
        for (int i = 0; i < mats.Length; i++) mats[i] = mat;
        r.sharedMaterials = mats;
        EditorUtility.SetDirty(r);
    }

    private static Material LitMaterial(string name, Color color)
    {
        string path = $"{MatFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", 0.45f);
        mat.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static Material UnlitMaterial(string name, Color color)
    {
        string path = $"{MatFolder}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void Write(System.Text.StringBuilder sb)
    {
        try { File.WriteAllText(LogPath, sb.ToString()); } catch { }
    }
}
#endif
