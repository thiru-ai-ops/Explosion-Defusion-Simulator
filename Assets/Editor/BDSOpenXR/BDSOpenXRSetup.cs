using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

/// <summary>
/// Switches the project from the legacy Oculus XR Plugin to Unity OpenXR + Meta XR feature
/// (the setup Meta XR SDK v201 expects). Runs once automatically; re-run from
/// Tools > BDS > Switch XR to OpenXR (Meta).
/// Writes a report to Logs/BDS_OpenXRSetup.txt.
/// </summary>
[InitializeOnLoad]
public static class BDSOpenXRSetup
{
    private const string DoneMarker = "ProjectSettings/BDS_OpenXRSetup.done";
    private const string OculusLoader = "Unity.XR.Oculus.OculusLoader";
    private const string OpenXRLoaderType = "UnityEngine.XR.OpenXR.OpenXRLoader";

    static BDSOpenXRSetup()
    {
        if (File.Exists(DoneMarker)) return;
        // Wait until the editor and package metadata are fully ready.
        int frames = 0;
        void Tick()
        {
            if (++frames < 60 || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= Tick;
            Run();
        }
        EditorApplication.update += Tick;
    }

    [MenuItem("Tools/BDS/Switch XR to OpenXR (Meta)")]
    public static void Run()
    {
        var log = new StringBuilder($"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss} BDS OpenXR setup\n");
        bool ok = true;
        try
        {
            foreach (var group in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                var general = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(group);
                if (general == null || general.AssignedSettings == null)
                {
                    log.AppendLine($"! {group}: no XR General Settings found");
                    ok = false;
                    continue;
                }
                var mgr = general.AssignedSettings;
                bool removed = XRPackageMetadataStore.RemoveLoader(mgr, OculusLoader, group);
                bool added = mgr.activeLoaders.Any(l => l != null && l.GetType().FullName == OpenXRLoaderType)
                             || XRPackageMetadataStore.AssignLoader(mgr, OpenXRLoaderType, group);
                general.InitManagerOnStart = true;
                EditorUtility.SetDirty(general);
                EditorUtility.SetDirty(mgr);
                log.AppendLine($"{group}: Oculus loader removed={removed}, OpenXR loader active={added}, loaders now: " +
                               string.Join(", ", mgr.activeLoaders.Where(l => l != null).Select(l => l.GetType().Name)));

                var oxr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (oxr == null)
                {
                    log.AppendLine($"! {group}: OpenXR settings not found");
                    ok = false;
                    continue;
                }
                foreach (var f in oxr.GetFeatures())
                {
                    string t = f.GetType().Name;
                    bool want =
                        t == "MetaXRFeature" ||                       // Meta XR Core SDK (OVRPlugin on OpenXR)
                        t == "OculusTouchControllerProfile" ||        // Touch controllers
                        (group == BuildTargetGroup.Android && t == "MetaQuestFeature"); // Quest builds
                    if (want && !f.enabled)
                    {
                        f.enabled = true;
                        log.AppendLine($"  + enabled {t}");
                    }
                }
                log.AppendLine($"  {group} enabled features: " +
                               string.Join(", ", oxr.GetFeatures().Where(f => f.enabled).Select(f => f.GetType().Name)));
                bool hasMeta = oxr.GetFeatures().Any(f => f.enabled && f.GetType().Name == "MetaXRFeature");
                if (!hasMeta) { log.AppendLine($"! {group}: MetaXRFeature not available/enabled"); ok = false; }
                EditorUtility.SetDirty(oxr);
            }
            AssetDatabase.SaveAssets();
        }
        catch (Exception e)
        {
            ok = false;
            log.AppendLine("! Exception: " + e);
        }

        log.AppendLine(ok ? "RESULT: OK" : "RESULT: needs attention");
        Directory.CreateDirectory("Logs");
        File.AppendAllText("Logs/BDS_OpenXRSetup.txt", log.ToString());
        if (ok) File.WriteAllText(DoneMarker, DateTime.Now.ToString("s"));
        Debug.Log("[BDS OpenXR Setup]\n" + log);
    }
}
