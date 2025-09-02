using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data structure for a single note in a beatmap.
/// </summary>
[Serializable]
public class NoteData {
    public int lane;
    public float time;
    public float duration = 0f;
}
/// <summary>
/// Data structure for a beatmap, including metadata and a list of notes.
/// </summary>
[Serializable]
public class Beatmap
{
    public string songId = "song01";
    public float bpm = 120f;
    public string audioFile = "song01";
    public float globalOffset = 0f;
    public float spawnLeadTime = 1.5f;
    public int lanes = 4;
    public List<NoteData> notes = new List<NoteData>();
}

/// <summary>
/// Loads a Beatmap (JSON) and associated AudioClip from Resources.
/// </summary>
public static class BeatmapLoader {
    public static Beatmap Load(string id) {
        var ta = Resources.Load<TextAsset>($"Beatmaps/{id}");
        if (!ta) return null;
        return JsonUtility.FromJson<Beatmap>(ta.text);
    }
    public static AudioClip LoadAudio(string name) {
        return Resources.Load<AudioClip>($"Audio/{name}");
    }
}
