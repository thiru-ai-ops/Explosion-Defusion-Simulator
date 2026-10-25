using TMPro;
using UnityEngine;

/// <summary>
/// Countdown shown on the bomb's LED display (tablet manual, step 6: "If timer stops, cut the wire connected
/// to the timer"). It starts when the scene starts, stops when the detonator wires are cut
/// (<see cref="WireCutter.WireStep.stopsTimer"/>), and detonates the bomb if it reaches 00:00.
/// </summary>
public class BDSBombTimer : MonoBehaviour
{
    [Tooltip("Countdown length in seconds.")]
    public float duration = 300f;
    [Tooltip("Text on the bomb's LED display.")]
    public TMP_Text display;
    [Tooltip("Cutter that is told when the time runs out.")]
    public WireCutter cutter;
    public Color runningColor = new Color(1f, 0.12f, 0.08f);
    public Color stoppedColor = new Color(1f, 0.12f, 0.08f);
    [Tooltip("Below this many seconds the display flashes.")]
    public float warningSeconds = 30f;

    private float _remaining;
    private bool _running;
    private bool _expired;

    public bool IsRunning => _running;
    public bool IsExpired => _expired;
    public float Remaining => _remaining;
    public string Display => Format(_remaining, true);

    private void Awake() => ResetTimer();

    /// <summary>Restarts the countdown from <see cref="duration"/>.</summary>
    public void ResetTimer()
    {
        _remaining = Mathf.Max(1f, duration);
        _running = true;
        _expired = false;
        Refresh();
    }

    /// <summary>Sets the time left (used by the self test to fast-forward the countdown).</summary>
    public void SetRemaining(float seconds)
    {
        _remaining = Mathf.Max(0.01f, seconds);
        Refresh();
    }

    /// <summary>Freezes the countdown (the display keeps showing the time it stopped at).</summary>
    public void Stop()
    {
        if (!_running) return;
        _running = false;
        Refresh();
    }

    private void Update()
    {
        if (!_running) return;
        _remaining -= Time.deltaTime;
        if (_remaining <= 0f)
        {
            _remaining = 0f;
            _running = false;
            _expired = true;
            Refresh();
            if (cutter != null) cutter.OnTimerExpired();
            return;
        }
        Refresh();
    }

    private void Refresh()
    {
        if (display == null) return;
        // The colon blinks while counting; a frozen timer shows a steady colon.
        bool colon = !_running || (_remaining % 1f) > 0.5f;
        display.text = Format(_remaining, colon);
        Color c = _running ? runningColor : stoppedColor;
        if (_running && _remaining < warningSeconds && (_remaining % 0.5f) < 0.25f) c.a = 0.35f;
        display.color = c;
    }

    private static string Format(float seconds, bool colon)
    {
        int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        // A hidden colon keeps its width, so the digits don't jump while it blinks.
        return $"{s / 60:00}{(colon ? ":" : "<alpha=#00>:<alpha=#FF>")}{s % 60:00}";
    }
}
