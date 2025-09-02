using UnityEngine;
using System.Collections;
/// <summary>
/// Scales a target transform in time with the beat of the song,
///  using either a SongConductor or an AudioSource as the timing source.
/// </summary>
public class BackgroundBeatAnimator : MonoBehaviour
{
    [Header("Core Timing Sources")]
    [Tooltip("Song timing provider; uses SongConductor.SongTime when assigned.")]
    public SongConductor conductor;
    [Tooltip("Fallback audio time source if no conductor is provided.")]
    public AudioSource audioSource;

    [Header("Beat Settings")]
    [Tooltip("Song tempo in beats per minute.")]
    [Min(1f)] public float bpm = 120f;
    [Tooltip("Pulses per beat.")]
    [Min(1f)] public float subdivision = 1f;
    [Tooltip("Shift the pulse in beats. Positive = later; negative = earlier.")]
    public float beatOffsetBeats = 0f;

    [Header("Animation Target")]
    [Tooltip("Transform to scale on each pulse; defaults to this.transform.")]
    public Transform target;
    [Tooltip("Scale multiplier at pulse peak (1 = no change).")]
    [Range(1f, 2f)] public float popScale = 1.12f;
    [Tooltip("Seconds to scale up to the peak.")]
    [Min(0f)] public float popUpTime = 0.06f;
    [Tooltip("Seconds to scale back down to base.")]
    [Min(0f)] public float popDownTime = 0.10f;

    Vector3 _baseScale;
    float _secPerTick;
    float _nextTick;
    Coroutine _pulseCo;

    /// <summary>
    /// Initializes base scale/target, computes tick timing from BPM & subdivision,
    /// and schedules the time for the next pulse based on current song time and beat offset.
    /// </summary>
    void OnEnable()
    {
        if (!target) target = transform;
        _baseScale = target.localScale == Vector3.zero ? Vector3.one : target.localScale;

        RecomputeTiming();

        float now = SongTime();
        float start = Mathf.Max(0f, now);
        float tick0 = Mathf.Ceil((start / _secPerTick) - 1e-4f) * _secPerTick;

        _nextTick = tick0 + beatOffsetBeats * (60f / Mathf.Max(1e-4f, bpm));
        if (_nextTick <= now) _nextTick += _secPerTick;
    }

    /// <summary>
    /// When values change in the Inspector during play, recompute tick duration.
    /// </summary>
    void OnValidate()
    {
        if (!Application.isPlaying) return;
        RecomputeTiming();
    }

    /// <summary>
    /// Converts BPM and subdivision into seconds-per-tick for the pulse clock.
    /// </summary>
    void RecomputeTiming()
    {
        float beat = 60f / Mathf.Max(1e-4f, bpm);
        _secPerTick = beat / subdivision;
    }

    /// <summary>
    /// Drives the pulse: if the current time has passed the scheduled next tick,
    /// fire a tick and schedule the following one (handles frame skips).
    /// </summary>
    void Update()
    {
        float now = SongTime();
        while (now >= _nextTick)
        {
            OnTick();
            _nextTick += _secPerTick;
        }
    }

    /// <summary>
    /// Returns the authoritative song time: SongConductor if present, otherwise AudioSource.time,
    /// else falls back to Time.timeSinceLevelLoad.
    /// </summary>
    float SongTime()
    {
        if (conductor) return conductor.SongTime;
        if (audioSource) return audioSource.time;
        return Time.timeSinceLevelLoad;
    }

    /// <summary>
    /// Starts a new pulse animation, cancelling any currently running pulse.
    /// </summary>
    void OnTick()
    {
        if (_pulseCo != null) StopCoroutine(_pulseCo);
        _pulseCo = StartCoroutine(Pulse());
    }

    /// <summary>
    /// Animates the target scale: scales up toward popScale, then back down to base scale.
    /// </summary>
    IEnumerator Pulse()
    {
        Vector3 up = _baseScale * Mathf.Max(1f, popScale);

        float t = 0f;
        while (t < popUpTime)
        {
            t += Time.deltaTime;
            float k = popUpTime <= 0f ? 1f : Mathf.Clamp01(t / popUpTime);
            target.localScale = Vector3.Lerp(_baseScale, up, k);
            yield return null;
        }

        t = 0f;
        while (t < popDownTime)
        {
            t += Time.deltaTime;
            float k = popDownTime <= 0f ? 1f : Mathf.Clamp01(t / popDownTime);
            target.localScale = Vector3.Lerp(up, _baseScale, k);
            yield return null;
        }
        
        target.localScale = _baseScale;
    }
}
