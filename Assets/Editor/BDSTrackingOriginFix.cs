#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Meta XR SDK v201 + Unity OpenXR: with OVRManager's tracking origin on "Floor Level", the runtime reports a
/// different origin back, so OVRManager re-applies it every frame. That recreates the tracking space every frame
/// (thousands of "OnAppSpaceChange" logs, wasted frame time). "Stage" is also floor-based and stays stable.
/// Runs once for MAP2; re-run from Tools > BDS > Set Tracking Origin To Stage.
/// </summary>
[InitializeOnLoad]
public static class BDSTrackingOriginFix
{
    private const string DoneMarker = "ProjectSettings/BDS_TrackingOrigin.done";

    static BDSTrackingOriginFix()
    {
        if (File.Exists(DoneMarker)) return;
        int frames = 0;
        void Tick()
        {
            if (++frames < 120 || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= Tick;
            if (EditorSceneManager.GetActiveScene().name == "MAP2") Apply();
        }
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/BDS/Set Tracking Origin To Stage")]
    public static void Apply()
    {
        var manager = Object.FindObjectOfType<OVRManager>(true);
        if (manager == null) { Debug.LogWarning("[BDS Tracking Origin] No OVRManager in the open scene."); return; }
        var so = new SerializedObject(manager);
        var prop = so.FindProperty("_trackingOriginType");
        if (prop == null) { Debug.LogWarning("[BDS Tracking Origin] _trackingOriginType not found."); return; }
        int before = prop.intValue;
        prop.intValue = (int)OVRManager.TrackingOrigin.Stage;
        so.ApplyModifiedProperties();
        var scene = manager.gameObject.scene;
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText(DoneMarker, System.DateTime.Now.ToString("s"));
        Debug.Log($"[BDS Tracking Origin] OVRManager tracking origin {(OVRManager.TrackingOrigin)before} -> Stage on '{manager.name}'. Scene saved.");
    }
}
#endif
