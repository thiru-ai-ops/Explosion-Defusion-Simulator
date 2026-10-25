#if UNITY_EDITOR
using System.IO;
using System.Linq;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Builds the two mission-end panels in the open scene (MAP2) and hooks them to the WireCutter:
///   * DefusedPanel  - "BOMB DEFUSED!"  + Play Again
///   * ExplodedPanel - "BOMB EXPLODED!" + Retry
/// Both restart the scene. Ray-clickable with the Interaction SDK, plus A / X on the controllers.
/// Menu: Tools > BDS > Create End-Game Panels  (runs automatically once for MAP2).
/// </summary>
[InitializeOnLoad]
public static class BDSEndGameUIBuilder
{
    private const string DoneMarker = "ProjectSettings/BDS_EndGameUI.done";
    private const float Width = 900f, Height = 560f, Scale = 0.001f; // 0.9 m x 0.56 m

    static BDSEndGameUIBuilder()
    {
        if (File.Exists(DoneMarker)) return;
        int frames = 0;
        void Tick()
        {
            if (++frames < 90 || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= Tick;
            if (EditorSceneManager.GetActiveScene().name == "MAP2") Build();
        }
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/BDS/Create End-Game Panels")]
    public static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var cutter = Object.FindObjectOfType<WireCutter>(true);
        if (cutter == null)
        {
            Debug.LogError("[BDS EndGame UI] No WireCutter in the open scene - open MAP2 first.");
            return;
        }

        // Rebuild cleanly if it already exists.
        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "EndGameUI");
        if (old != null) Undo.DestroyObjectImmediate(old);

        var root = new GameObject("EndGameUI");
        Undo.RegisterCreatedObjectUndo(root, "End game UI");

        var defused = CreatePanel(root.transform, "DefusedPanel", EndGamePanel.Outcome.Defused,
            "BOMB DEFUSED!", new Color(0.25f, 0.9f, 0.35f),
            "Congratulations!\nYou cut the wires in the right order\nand saved everyone.",
            "PLAY AGAIN", new Color(0.13f, 0.55f, 0.22f));

        var exploded = CreatePanel(root.transform, "ExplodedPanel", EndGamePanel.Outcome.Exploded,
            "BOMB EXPLODED!", new Color(1f, 0.32f, 0.22f),
            "Wrong wire - mission failed.\nCheck the manual on the tablet\nand try again.",
            "RETRY", new Color(0.75f, 0.2f, 0.12f));

        Undo.RecordObject(cutter, "Assign end panels");
        cutter.missionPassedCanvas = defused;
        cutter.missionFailedCanvas = exploded;
        EditorUtility.SetDirty(cutter);

        defused.SetActive(false);
        exploded.SetActive(false);

        // Retry needs the scene in Build Settings for builds on the headset.
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == scene.path))
        {
            scenes.Add(new EditorBuildSettingsScene(scene.path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[BDS EndGame UI] Added {scene.path} to Build Settings.");
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText(DoneMarker, System.DateTime.Now.ToString("s"));
        Debug.Log("[BDS EndGame UI] Created DefusedPanel (Play Again) and ExplodedPanel (Retry) and linked them to the WireCutter. Scene saved.");
    }

    private static GameObject CreatePanel(Transform parent, string name, EndGamePanel.Outcome outcome,
        string title, Color titleColor, string message, string buttonText, Color buttonColor)
    {
        // Canvas root
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        var rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(Width, Height);
        rt.localScale = Vector3.one * Scale;
        rt.localPosition = new Vector3(0, 1.4f, 0);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 3;
        go.AddComponent<GraphicRaycaster>();
        var panel = go.AddComponent<EndGamePanel>();
        panel.outcome = outcome;

        // Background + frame
        var frame = MakeImage(go.transform, "Frame", titleColor, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        frame.raycastTarget = false;
        var bg = MakeImage(go.transform, "Background", new Color(0.06f, 0.07f, 0.09f, 0.95f), Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -8));
        bg.raycastTarget = false;

        // Texts
        MakeText(go.transform, "Title", title, 78, titleColor, FontStyles.Bold, new Vector2(0, 175), new Vector2(Width - 60, 110));
        MakeText(go.transform, "Message", message, 36, Color.white, FontStyles.Normal, new Vector2(0, 30), new Vector2(Width - 80, 170));
        MakeText(go.transform, "Hint", "Point at the button and pull the trigger  -  or press A / X", 24,
            new Color(0.75f, 0.75f, 0.75f), FontStyles.Italic, new Vector2(0, -235), new Vector2(Width - 60, 40));

        // Button
        var btnGo = new GameObject("Button", typeof(RectTransform));
        btnGo.transform.SetParent(go.transform, false);
        btnGo.layer = go.layer;
        var brt = (RectTransform)btnGo.transform;
        brt.anchoredPosition = new Vector2(0, -130);
        brt.sizeDelta = new Vector2(420, 110);
        var bimg = btnGo.AddComponent<Image>();
        bimg.color = buttonColor;
        var button = btnGo.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        button.colors = colors;
        button.targetGraphic = bimg;
        UnityEventTools.AddVoidPersistentListener(button.onClick, new UnityAction(panel.OnButtonPressed));
        var label = MakeText(btnGo.transform, "Label", buttonText, 50, Color.white, FontStyles.Bold, Vector2.zero, new Vector2(420, 110));
        label.raycastTarget = false;

        // Interaction SDK: ray-clickable canvas
        var isdk = new GameObject("ISDK_RayInteraction", typeof(RectTransform));
        isdk.transform.SetParent(go.transform, false);
        var irt = (RectTransform)isdk.transform;
        irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one; irt.sizeDelta = Vector2.zero;
        var pointable = isdk.AddComponent<PointableCanvas>();
        SetRef(pointable, "_canvas", canvas);

        var surfaceGo = new GameObject("Surface");
        surfaceGo.transform.SetParent(isdk.transform, false);
        var plane = surfaceGo.AddComponent<PlaneSurface>();
        SetBool(plane, "_doubleSided", true);
        var clipper = surfaceGo.AddComponent<BoundsClipper>();
        var so = new SerializedObject(clipper);
        so.FindProperty("_position").vector3Value = Vector3.zero;
        so.FindProperty("_size").vector3Value = new Vector3(Width, Height, 0.01f);
        so.ApplyModifiedPropertiesWithoutUndo();
        var clipped = surfaceGo.AddComponent<ClippedPlaneSurface>();
        var cso = new SerializedObject(clipped);
        cso.FindProperty("_planeSurface").objectReferenceValue = plane;
        var clippers = cso.FindProperty("_clippers");
        clippers.arraySize = 1;
        clippers.GetArrayElementAtIndex(0).objectReferenceValue = clipper;
        cso.ApplyModifiedPropertiesWithoutUndo();

        var ray = isdk.AddComponent<RayInteractable>();
        SetRef(ray, "_pointableElement", pointable);
        SetRef(ray, "_surface", clipped);
        SetRef(ray, "_selectSurface", plane);

        return go;
    }

    private static Image MakeImage(Transform parent, string name, Color color, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = offMin; rt.offsetMax = offMax;
        var img = go.AddComponent<Image>();
        img.color = color;
        return img;
    }

    private static TextMeshProUGUI MakeText(Transform parent, string name, string text, float size, Color color, FontStyles style, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.AddComponent<TextMeshProUGUI>();
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        return t;
    }

    private static void SetRef(Object target, string prop, Object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(prop);
        if (p == null) { Debug.LogWarning($"[BDS EndGame UI] {target.GetType().Name}.{prop} not found"); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string prop, bool value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(prop);
        if (p != null) { p.boolValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
#endif
