#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps VR Play mode from freezing on the headset's loading screen.
///
/// Meta's plugin logs a harmless error ("Unable to set TrackingOrigin Floor...") in the first frame,
/// before the headset session is running. With the Console's "Error Pause" switched on, that error
/// pauses the Editor, no frames reach the headset, and Link shows the loading dots forever.
///
/// This guard switches Error Pause off when the project opens, and if the Editor is paused during the
/// first seconds of Play mode it resumes it. Pausing manually later in Play still works normally.
/// </summary>
[InitializeOnLoad]
public static class BDSPlayModeGuard
{
    private const int ErrorPauseFlag = 1 << 2; // UnityEditor.ConsoleWindow.ConsoleFlags.ErrorPause
    private static double _guardUntil = -1;

    static BDSPlayModeGuard()
    {
        EditorApplication.delayCall += DisableErrorPause;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.update += Update;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            DisableErrorPause();
            if (EditorApplication.isPaused)
            {
                // The Pause button was left on before pressing Play.
                EditorApplication.isPaused = false;
                Log("Pause button was on before Play - turned it off so the headset gets frames.");
            }
        }
        else if (state == PlayModeStateChange.EnteredPlayMode)
        {
            _guardUntil = EditorApplication.timeSinceStartup + 5.0;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            _guardUntil = -1;
        }
    }

    private static void Update()
    {
        if (_guardUntil < 0) return;
        if (EditorApplication.timeSinceStartup > _guardUntil) { _guardUntil = -1; return; }
        if (EditorApplication.isPlaying && EditorApplication.isPaused)
        {
            EditorApplication.isPaused = false;
            Log("Editor got paused while VR was starting (startup error + Error Pause) - resumed it.");
        }
    }

    private static void DisableErrorPause()
    {
        try
        {
            Type logEntries = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries");
            if (logEntries == null) return;
            PropertyInfo flagsProp = logEntries.GetProperty("consoleFlags", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodInfo setFlag = logEntries.GetMethod("SetConsoleFlag", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (flagsProp == null || setFlag == null) return;
            int flags = (int)flagsProp.GetValue(null);
            if ((flags & ErrorPauseFlag) != 0)
            {
                setFlag.Invoke(null, new object[] { ErrorPauseFlag, false });
                Log("Console 'Error Pause' was ON - switched it OFF (it froze VR Play mode on startup errors).");
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BDS PlayMode Guard] Could not read console flags: " + e.Message);
        }
    }

    public static bool IsErrorPauseOn()
    {
        try
        {
            Type logEntries = typeof(EditorWindow).Assembly.GetType("UnityEditor.LogEntries");
            PropertyInfo flagsProp = logEntries?.GetProperty("consoleFlags", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return flagsProp != null && (((int)flagsProp.GetValue(null)) & ErrorPauseFlag) != 0;
        }
        catch { return false; }
    }

    private static void Log(string msg)
    {
        Debug.Log("[BDS PlayMode Guard] " + msg);
        try
        {
            Directory.CreateDirectory("Logs");
            File.AppendAllText("Logs/BDS_PlayModeGuard.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {msg}\n");
        }
        catch { }
    }
}
#endif
