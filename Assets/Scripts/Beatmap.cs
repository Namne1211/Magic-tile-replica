using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class NoteData {
    public int lane;
    public float time;
    public float duration = 0f;
}

[Serializable]
public class Beatmap {
    public string songId = "song01";
    public float bpm = 120f;
    public string audioFile = "song01";
    public float globalOffset = 0f;
    public float spawnLeadTime = 1.5f;
    public int lanes = 4;
    public List<NoteData> notes = new List<NoteData>();
}

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
