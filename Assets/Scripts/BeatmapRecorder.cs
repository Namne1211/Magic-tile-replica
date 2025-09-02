#if UNITY_EDITOR
using UnityEditor;
#endif

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class BeatmapRecorder : MonoBehaviour {
    [Header("Audio")]
    public AudioSource audioSource;
    public float scheduleDelay = 0.10f;

    [Header("Beatmap Meta")]
    public string songId = "song01";
    public float bpm = 160f;
    public string audioFile = "song01";
    public float globalOffset = 0.06f;
    public float spawnLeadTime = 1.5f;
    public int lanes = 4;

    [Header("Quantization")]
    public int quantizeSubdiv = 0;

    [Header("Hold/Tap")]
    public float minHoldSeconds = 0.18f;

    private readonly List<NoteData> _notes = new List<NoteData>();
    private readonly bool[] _isDown = new bool[8];
    private readonly float[] _downTime = new float[8];
    private readonly KeyCode[] _keys = new KeyCode[] { KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F };

    private double _dspStart;
    private bool _started;

    void Reset() { audioSource = GetComponent<AudioSource>(); }

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

    void StopSong() {
        if (!_started) return;
        audioSource.Stop();
        _started = false;
        Debug.Log("Stopped.");
    }

    void PressLane(int lane) {
        if (lane < 0 || lane >= lanes) return;
        if (_isDown[lane]) return;
        _isDown[lane] = true;
        _downTime[lane] = (float)(AudioSettings.dspTime - _dspStart);
    }

    void ReleaseLane(int lane) {
        if (lane < 0 || lane >= lanes) return;
        if (!_isDown[lane]) return;
        float start = _downTime[lane];
        float end = (float)(AudioSettings.dspTime - _dspStart);
        _isDown[lane] = false;
        CommitNote(lane, start, end);
    }

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

    void SaveBeatmap() {
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
