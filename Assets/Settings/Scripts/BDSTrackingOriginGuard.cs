using System.Reflection;
using UnityEngine;

/// <summary>
/// Stops OVRManager from re-applying the tracking origin every frame under Unity OpenXR.
///
/// OVRManager.Update() does: if (trackingOriginType != _trackingOriginType) trackingOriginType = _trackingOriginType;
/// Under OpenXR the getter returns what the runtime reports (OVRPlugin.GetTrackingOriginType()), which never
/// equals the stored field, so the setter runs every frame. Each call recreates the OpenXR app space
/// (OnAppSpaceChange spam, thousands per minute) which costs frame time and can make the view jitter.
///
/// This guard runs before OVRManager: it applies the wanted origin once, then copies the value the runtime
/// reports into OVRManager's stored field so the comparison matches and no further re-apply happens.
/// It re-applies only when the runtime's reported value changes (e.g. after a session restart).
/// Created automatically at startup; nothing needs to be added to the scene.
/// </summary>
[DefaultExecutionOrder(-200)] // OVRManager runs at -100
public class BDSTrackingOriginGuard : MonoBehaviour
{
    private static readonly FieldInfo OriginField =
        typeof(OVRManager).GetField("_trackingOriginType", BindingFlags.Instance | BindingFlags.NonPublic);

    private OVRManager.TrackingOrigin wanted = OVRManager.TrackingOrigin.Stage;
    private bool haveWanted;
    private bool applied;
    private OVRManager.TrackingOrigin lastReported;
    private int resyncCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        if (FindObjectOfType<BDSTrackingOriginGuard>() != null) return;
        GameObject go = new GameObject("[BDS] Tracking Origin Guard");
        go.hideFlags = HideFlags.HideInHierarchy;
        DontDestroyOnLoad(go);
        go.AddComponent<BDSTrackingOriginGuard>();
    }

    private void Update()
    {
        OVRManager mgr = OVRManager.instance;
        if (mgr == null || OriginField == null) return;
        if (!OVRManager.isHmdPresent || !OVRPlugin.UnityOpenXR.Enabled) return;

        if (!haveWanted)
        {
            // Whatever the scene asked for (Stage after the BDS fix); this is applied once.
            wanted = (OVRManager.TrackingOrigin)OriginField.GetValue(mgr);
            haveWanted = true;
        }

        OVRManager.TrackingOrigin reported = mgr.trackingOriginType; // runtime value under OpenXR

        if (!applied || reported != lastReported)
        {
            if (resyncCount < 20)
            {
                mgr.trackingOriginType = wanted; // one real apply (floor origin, no recentering for Stage)
                reported = mgr.trackingOriginType;
                resyncCount++;
                Debug.Log($"[BDS Tracking Origin Guard] applied {wanted}; runtime reports {reported}");
            }
            applied = true;
            lastReported = reported;
        }

        // Make OVRManager's own check (getter == field) pass, so it doesn't re-apply this frame.
        if ((OVRManager.TrackingOrigin)OriginField.GetValue(mgr) != reported)
            OriginField.SetValue(mgr, reported);
    }
}
