#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Writes Logs/BDS_XR_Diagnostics.txt: which GPU the editor renders on, and the XR state a few
/// seconds after entering Play mode. Used to diagnose "project won't play in the headset" over Link.
/// Safe to delete.
/// </summary>
[InitializeOnLoad]
public static class BDSXRDiagnostics
{
    private static readonly string FilePath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "BDS_XR_Diagnostics.txt");
    private static readonly string ConsolePath = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "BDS_Console.txt");
    private static double _checkAt = -1;
    private static readonly object _lock = new object();
    // Keep the mirror small: each distinct message is written at most MaxRepeats times, and the file stops at MaxBytes.
    private const int MaxRepeats = 20;
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly System.Collections.Generic.Dictionary<string, int> _seen = new System.Collections.Generic.Dictionary<string, int>();
    private static long _written;
    private static bool _capNoted;

    static BDSXRDiagnostics()
    {
        // Mirror every console message to a file so it can be read without the Editor UI.
        Application.logMessageReceivedThreaded += (msg, stack, type) =>
        {
            if (type == LogType.Log && !msg.Contains("XR") && !msg.Contains("OVR") && !msg.Contains("BDS") && !msg.Contains("Oculus")) return;
            try
            {
                lock (_lock)
                {
                    // Messages that differ only by a number (e.g. "OnAppSpaceChange: 7814") count as the same message.
                    string key = System.Text.RegularExpressions.Regex.Replace(msg ?? "", @"\d+", "#");
                    _seen.TryGetValue(key, out int n);
                    _seen[key] = n + 1;
                    if (n == MaxRepeats)
                    {
                        Append($"[{DateTime.Now:HH:mm:ss}] (message repeated {MaxRepeats}+ times, further copies not mirrored): {Short(msg)}\n");
                        return;
                    }
                    if (n > MaxRepeats) return;
                    string first = stack != null && (type == LogType.Error || type == LogType.Exception)
                        ? "\n    " + stack.Split('\n')[0] : "";
                    Append($"[{DateTime.Now:HH:mm:ss}] {type}: {msg}{first}\n");
                }
            }
            catch { }
        };
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.ExitingEditMode)
            {
                lock (_lock)
                {
                    _seen.Clear();
                    _written = 0;
                    _capNoted = false;
                    try { File.WriteAllText(ConsolePath, $"=== Play pressed {DateTime.Now:HH:mm:ss}, Error Pause on: {ErrorPauseOn()}\n"); } catch { }
                }
            }
        };
        EditorApplication.delayCall += () => Write("Editor loaded", true);
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode) _checkAt = EditorApplication.timeSinceStartup + 6;
        };
        EditorApplication.update += () =>
        {
            if (_checkAt > 0 && EditorApplication.timeSinceStartup > _checkAt)
            {
                _checkAt = -1;
                Write("6s after entering Play mode", false);
            }
        };
    }

    private static bool ErrorPauseOn() => BDSPlayModeGuard.IsErrorPauseOn();

    private static string Short(string s) => s == null ? "" : (s.Length > 160 ? s.Substring(0, 160) + "..." : s);

    // Caller holds _lock.
    private static void Append(string line)
    {
        if (_written >= MaxBytes)
        {
            if (!_capNoted)
            {
                _capNoted = true;
                File.AppendAllText(ConsolePath, $"[{DateTime.Now:HH:mm:ss}] (BDS_Console.txt reached {MaxBytes / (1024 * 1024)} MB; mirroring stopped until next Play)\n");
            }
            return;
        }
        File.AppendAllText(ConsolePath, line);
        _written += line.Length;
    }

    private static void Write(string when, bool overwrite)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"=== {DateTime.Now:yyyy-MM-dd HH:mm:ss}  {when}");
            sb.AppendLine($"GPU: {SystemInfo.graphicsDeviceName}");
            sb.AppendLine($"GPU vendor: {SystemInfo.graphicsDeviceVendor} (id {SystemInfo.graphicsDeviceVendorID:X4}), device id {SystemInfo.graphicsDeviceID:X4}");
            sb.AppendLine($"Graphics API: {SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceVersion}");
            sb.AppendLine($"VRAM MB: {SystemInfo.graphicsMemorySize}");
            sb.AppendLine($"CPU: {SystemInfo.processorType}, RAM MB: {SystemInfo.systemMemorySize}");
            sb.AppendLine($"OS: {SystemInfo.operatingSystem}");
            sb.AppendLine($"Battery: {SystemInfo.batteryStatus} {SystemInfo.batteryLevel}");
            sb.AppendLine($"Build target: {EditorUserBuildSettings.activeBuildTarget}");
            sb.AppendLine($"Paused: {EditorApplication.isPaused}, Error Pause: {ErrorPauseOn()}");
            sb.AppendLine($"XR_RUNTIME_JSON env: {Environment.GetEnvironmentVariable("XR_RUNTIME_JSON") ?? "(not set)"}");
            if (EditorApplication.isPlaying)
            {
                sb.AppendLine($"XR loaded device: '{XRSettings.loadedDeviceName}', enabled {XRSettings.enabled}, device active {XRSettings.isDeviceActive}");
                sb.AppendLine($"XR eye texture: {XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight}");
                try { sb.AppendLine($"OVRManager.isHmdPresent: {OVRManager.isHmdPresent}, OVRPlugin.initialized: {OVRPlugin.initialized}, userPresent: {OVRPlugin.userPresent}"); }
                catch (Exception e) { sb.AppendLine("OVR query failed: " + e.Message); }
            }
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            if (overwrite) File.WriteAllText(FilePath, sb.ToString()); else File.AppendAllText(FilePath, sb.ToString());
            Debug.Log("[BDS XR Diagnostics]\n" + sb);
        }
        catch (Exception e) { Debug.LogWarning("[BDS XR Diagnostics] " + e.Message); }
    }
}
#endif
