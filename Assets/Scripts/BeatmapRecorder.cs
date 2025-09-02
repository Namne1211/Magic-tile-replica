#if UNITY_EDITOR
using UnityEditor;
#endif

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Live beatmap recorder:
/// - Space to start scheduled playback (sample-accurate with dsp delay)
/// - A/S/D/F to start a note on lanes 0..3; release to end (creates hold if long enough)
/// - Enter to quantize (optional) and save as JSON in Resources/Beatmaps (Editor) or persistentDataPath (Build)
/// </summary>
public class BeatmapRecorder : MonoBehaviour {
    [Header("Audio")]
    [Tooltip("AudioSource to play the song. Assign an AudioClip in the inspector or at runtime.")]
    public AudioSource audioSource;
    [Tooltip("Seconds to schedule ahead of AudioSettings.dspTime to ensure accurate playback.")]
    public float scheduleDelay = 0.10f;

    [Header("Beatmap Meta")]
    [Tooltip("Unique ID for this song/beatmap (also the filename when saved).")]
    public string songId = "song01";
    [Tooltip("Song tempo in beats per minute.")]
    public float bpm = 160f;
    [Tooltip("Name of the audio file in Resources/Audio (without extension).")]
    public string audioFile = "song01";
    [Tooltip("Global offset in seconds to apply to all notes (positive = later).")]
    public float globalOffset = 0.06f;
    [Tooltip("Seconds before a note's hit time to spawn it.")]
    public float spawnLeadTime = 1.5f;
    [Tooltip("Number of lanes (max 8, limited by key bindings).")]
    public int lanes = 4;

    [Header("Quantization")]
    [Tooltip("If >0 and bpm>0, quantize note start/end times to this many subdivisions per beat.")]
    public int quantizeSubdiv = 0;

    [Header("Hold/Tap")]
    [Tooltip("Minimum hold duration in seconds to register as a hold; shorter = tap.")]
    public float minHoldSeconds = 0.18f;

    private readonly List<NoteData> _notes = new List<NoteData>();
    private readonly bool[] _isDown = new bool[8];
    private readonly float[] _downTime = new float[8];
    private readonly KeyCode[] _keys = new KeyCode[] { KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F };

    private double _dspStart;
    private bool _started;

    /// <summary>
    /// Unity callback to reset references on component add or Reset action in Inspector.
    /// Ensures audioSource is bound to this GameObject if present.
    /// </summary>
    void Reset() { audioSource = GetComponent<AudioSource>(); }

    /// <summary>
    /// Main input loop:
    /// - Space to start, Backspace to stop
    /// - For each lane key: on key down, mark press; on key up, commit a note
    /// - Enter to save the beatmap to disk
    /// </summary>
    void Update() {
        if (Input.GetKeyDown(KeyCode.Space)) StartSong();
        if (Input.GetKeyDown(KeyCode.Backspace)) StopSong();

        if (!_started) return;

        for (int lane = 0; lane < Mathf.Min(lanes, _keys.Length); lane++) {
            if (Input.GetKeyDown(_keys[lane])) PressLane(lane);
            if (Input.GetKeyUp(_keys[lane])) ReleaseLane(lane);
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) SaveBeatmap();
    }

    /// <summary>
    /// Schedules the AudioSource to play via the DSP clock (sample-accurate),
    /// clears previous note state, and begins recording input.
    /// </summary>
    void StartSong() {
        if (!audioSource || !audioSource.clip) { Debug.LogError("Assign an AudioSource with a clip."); return; }
        Array.Clear(_isDown, 0, _isDown.Length);
        Array.Clear(_downTime, 0, _downTime.Length);
        _notes.Clear();
        _dspStart = AudioSettings.dspTime + scheduleDelay;
        audioSource.Stop();
        audioSource.PlayScheduled(_dspStart);
        _started = true;
        Debug.Log($"Started scheduled at dsp={_dspStart:F3}. Press A/S/D/F to start notes, release to end. Enter to save.");
    }

    /// <summary>
    /// Stops playback and ends the recording session.
    /// </summary>
    void StopSong() {
        if (!_started) return;
        audioSource.Stop();
        _started = false;
        Debug.Log("Stopped.");
    }

    /// <summary>
    /// Marks the start time of a lane press at the current DSP-relative song time.
    /// Ignores repeats if the lane is already held.
    /// </summary>
    /// <param name="lane">Lane index (0-based).</param>
    void PressLane(int lane) {
        if (lane < 0 || lane >= lanes) return;
        if (_isDown[lane]) return;
        _isDown[lane] = true;
        _downTime[lane] = (float)(AudioSettings.dspTime - _dspStart);
    }

    /// <summary>
    /// Completes a lane hold by recording the end time and committing a note
    /// (tap if short, hold if longer than minHoldSeconds).
    /// </summary>
    /// <param name="lane">Lane index (0-based).</param>
    void ReleaseLane(int lane) {
        if (lane < 0 || lane >= lanes) return;
        if (!_isDown[lane]) return;
        float start = _downTime[lane];
        float end = (float)(AudioSettings.dspTime - _dspStart);
        _isDown[lane] = false;
        CommitNote(lane, start, end);
    }

    /// <summary>
    /// Creates a NoteData from a press/release pair, optionally quantizes start/end,
    /// applies the min-hold threshold, and stores it in the working list.
    /// </summary>
    /// <param name="lane">Lane index.</param>
    /// <param name="start">Press time (seconds since scheduled start).</param>
    /// <param name="end">Release time (seconds since scheduled start).</param>
    void CommitNote(int lane, float start, float end) {
        if (end < start) end = start;
        float duration = end - start;

        if (quantizeSubdiv > 0 && bpm > 0f) {
            float beat = 60f / bpm;
            float step = beat / quantizeSubdiv;
            float qs = Mathf.Round(start / step) * step;
            float qe = Mathf.Round(end   / step) * step;
            if (qe < qs) qe = qs;
            start = Mathf.Max(0f, qs);
            duration = Mathf.Max(0f, qe - qs);
        }

        if (duration < minHoldSeconds) duration = 0f;

        _notes.Add(new NoteData {
            lane = Mathf.Clamp(lane, 0, lanes - 1),
            time = start,
            duration = duration
        });

        Debug.Log(duration > 0f
            ? $"Hold lane {lane} {start:0.000}s → {start + duration:0.000}s (dur {duration:0.000}s)"
            : $"Tap  lane {lane} @ {start:0.000}s");
    }

    /// <summary>
    /// Finalizes any still-held lanes, sorts notes by time, builds a Beatmap,
    /// and writes JSON to Assets/Resources/Beatmaps (Editor) or persistentDataPath (Build).
    /// </summary>
    void SaveBeatmap() {
        // Auto-release any keys still down so their notes are captured
        for (int lane = 0; lane < lanes; lane++) {
            if (_isDown[lane]) {
                float now = (float)(AudioSettings.dspTime - _dspStart);
                ReleaseLane(lane);
            }
        }

        var notes = new List<NoteData>(_notes);
        notes.Sort((a, b) => a.time.CompareTo(b.time));

        var map = new Beatmap {
            songId = songId,
            bpm = bpm,
            audioFile = audioFile,
            globalOffset = globalOffset,
            spawnLeadTime = spawnLeadTime,
            lanes = lanes,
            notes = notes
        };

        string json = JsonUtility.ToJson(map, true);

#if UNITY_EDITOR
        string resourcesDir = Path.Combine(Application.dataPath, "Resources");
        string beatmapsDir  = Path.Combine(resourcesDir, "Beatmaps");
        if (!Directory.Exists(beatmapsDir)) Directory.CreateDirectory(beatmapsDir);
        string path = Path.Combine(beatmapsDir, $"{songId}.json");
        File.WriteAllText(path, json);
        AssetDatabase.Refresh();
        Debug.Log($"Saved to Assets/Resources/Beatmaps/{songId}.json and refreshed. Load with Resources.Load<TextAsset>(\"Beatmaps/{songId}\").");
#else
        string dir = Application.persistentDataPath;
        string path = Path.Combine(dir, $"{songId}.json");
        File.WriteAllText(path, json);
        Debug.Log($"Saved beatmap to: {path}");
#endif
    }
}
