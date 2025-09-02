using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SongConductor : MonoBehaviour {
    public AudioSource audioSource;
    double _dspStart;
    bool _started;
    public float SongTime => _started ? (float)(AudioSettings.dspTime - _dspStart) : 0f;
    void Awake() {
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }
    public void StartSong(AudioClip clip, float globalOffsetSeconds = 0f) {
        audioSource.clip = clip;
        _dspStart = AudioSettings.dspTime + 0.10 + globalOffsetSeconds;
        audioSource.PlayScheduled(_dspStart);
        _started = true;
    }
    public void StopSong() {
        if (audioSource && audioSource.isPlaying) audioSource.Stop();
        _started = false;
    }
}
