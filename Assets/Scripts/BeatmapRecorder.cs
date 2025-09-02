#if UNITY_EDITOR
using UnityEditor;
#endif

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable] public class NoteData { public int lane; public float time; public float duration = 0f; }
[Serializable] public class Beatmap {
    public string songId = "song01";
    public float bpm = 120f;
    public string audioFile = "song01";
    public float globalOffset = 0f;
    public float spawnLeadTime = 1.5f;
    public int lanes = 4;
    public List<NoteData> notes = new List<NoteData>();
}

public class BeatmapRecorder : MonoBehaviour {
    [Header("Audio")]
    public AudioSource audioSource;
    [Tooltip("Start the song this many seconds after you press Space, for stable scheduling.")]
    public float scheduleDelay = 0.10f;

    [Header("Beatmap Meta")]
    public string songId = "song01";
    public float bpm = 160f;
    public string audioFile = "song01";
    public float globalOffset = 0.06f;
    public float spawnLeadTime = 1.5f;
    public int lanes = 4;

    [Header("Quantization")]
    [Tooltip("0 = no quantize; e.g., 8 for eighths, 16 for sixteenths")]
    public int quantizeSubdiv = 0;

    private readonly List<NoteData> _rawNotes = new List<NoteData>();
    private double _dspStart;
    private bool _started;

    void Reset() {
        audioSource = GetComponent<AudioSource>();
    }

    void Update() {
        // Start / stop playback
        if (Input.GetKeyDown(KeyCode.Space)) StartSong();
        if (Input.GetKeyDown(KeyCode.Backspace)) StopSong();

        if (!_started) return;

        // Record taps: A/S/D/F => lanes 0..3
        if (Input.GetKeyDown(KeyCode.A)) AddNote(0);
        if (Input.GetKeyDown(KeyCode.S)) AddNote(1);
        if (Input.GetKeyDown(KeyCode.D)) AddNote(2);
        if (Input.GetKeyDown(KeyCode.F)) AddNote(3);

        // Save JSON
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) {
            SaveBeatmap();
        }
    }

    void StartSong() {
        if (!audioSource || !audioSource.clip) {
            Debug.LogError("Assign an AudioSource with a clip.");
            return;
        }
        _rawNotes.Clear();
        _dspStart = AudioSettings.dspTime + scheduleDelay;
        audioSource.Stop();
        audioSource.PlayScheduled(_dspStart);
        _started = true;
        Debug.Log($"Started scheduled at dsp={_dspStart:F3}. Tap A/S/D/F to record. Press Enter to save.");
    }

    void StopSong() {
        if (!_started) return;
        audioSource.Stop();
        _started = false;
        Debug.Log("Stopped.");
    }

    void AddNote(int lane) {
        double now = AudioSettings.dspTime;
        float t = (float)(now - _dspStart); // seconds since song start
        _rawNotes.Add(new NoteData { lane = Mathf.Clamp(lane, 0, lanes - 1), time = t, duration = 0f });
        Debug.Log($"Note lane {lane} @ {t:0.000}s");
    }

    void SaveBeatmap() {
        // Copy + sort
        var notes = new List<NoteData>(_rawNotes);
        notes.Sort((a,b) => a.time.CompareTo(b.time));

        // Optional quantize
        if (quantizeSubdiv > 0 && bpm > 0f) {
            float beat = 60f / bpm;
            float step = beat / quantizeSubdiv;
            for (int i = 0; i < notes.Count; i++) {
                float q = Mathf.Round(notes[i].time / step) * step;
                notes[i].time = Mathf.Max(0f, q);
            }
        }

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
        // Save directly into the Unity project (Editor only)
        string resourcesDir = System.IO.Path.Combine(Application.dataPath, "Resources");
        string beatmapsDir  = System.IO.Path.Combine(resourcesDir, "Beatmaps");
        if (!System.IO.Directory.Exists(beatmapsDir))
            System.IO.Directory.CreateDirectory(beatmapsDir);

        string path = System.IO.Path.Combine(beatmapsDir, $"{songId}.json");
        System.IO.File.WriteAllText(path, json);
        AssetDatabase.Refresh(); // make Unity import it as a TextAsset

        Debug.Log($"<b>Saved</b> beatmap to <i>Assets/Resources/Beatmaps/{songId}.json</i> " +
                "and refreshed the AssetDatabase. " +
                "Load with Resources.Load<TextAsset>(\"Beatmaps/" + songId + "\").");
    #else
        // Fallback for builds (cannot write into Assets at runtime)
        string dir = Application.persistentDataPath;
        string path = System.IO.Path.Combine(dir, $"{songId}.json");
        System.IO.File.WriteAllText(path, json);
        Debug.Log($"Saved beatmap to: {path}\n" +
                "In builds, copy this file into your project's Assets/Resources/Beatmaps/ folder.");
    #endif
    }

}
