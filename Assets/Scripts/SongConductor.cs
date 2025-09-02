using UnityEngine;

[RequireComponent(typeof(AudioSource))]
/// <summary>
/// Sample-accurate song timing and playback control using PlayScheduled,
/// exposing a stable <see cref="SongTime"/> for gameplay sync.
/// </summary>
public class SongConductor : MonoBehaviour {
    [Header("Audio Source")]
    [Tooltip("AudioSource to play the song.")]
    public AudioSource audioSource;

    double _dspStart;
    bool _started;

    /// <summary>
    /// Current song time in seconds since the scheduled start.
    /// Returns 0 while not started.
    /// </summary>
    public float SongTime => _started ? (float)(AudioSettings.dspTime - _dspStart) : 0f;

    /// <summary>
    /// Ensures an AudioSource is present and disables play-on-awake to avoid unscheduled playback.
    /// </summary>
    void Awake() {
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    /// <summary>
    /// Schedules the provided clip to start at the audio DSP clock
    /// with a small safety delay plus an optional global offset.
    /// </summary>
    /// <param name="clip">Audio clip to play.</param>
    /// <param name="globalOffsetSeconds">Additional start delay (positive = later start).</param>
    public void StartSong(AudioClip clip, float globalOffsetSeconds = 0f) {
        audioSource.clip = clip;
        _dspStart = AudioSettings.dspTime + 0.10 + globalOffsetSeconds;
        audioSource.PlayScheduled(_dspStart);
        _started = true;
    }

    /// <summary>
    /// Stops playback immediately and resets the started flag.
    /// </summary>
    public void StopSong() {
        if (audioSource && audioSource.isPlaying) audioSource.Stop();
        _started = false;
    }
}
