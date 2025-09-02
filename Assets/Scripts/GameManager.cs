using System.Collections.Generic;
using UnityEngine;
using TMPro;

public enum ScoreTier { Good, Great, Excellent }

public class GameManager : MonoBehaviour {
    public SongConductor conductor;
    public SimplePool notePool;
    public Transform hitLine;
    public Transform bottomLine;
    public List<LaneController> lanes = new List<LaneController>();
    public TextMeshProUGUI scoreTMP;
    public TextMeshProUGUI comboTMP;
    public TextMeshProUGUI accuracyTMP;
    public TextMeshProUGUI judgementTMP;
    public GameObject gameOverPanel;
    public string beatmapId = "song01";
    public float travelSpeed = 7f;
    public float despawnY = -12f;

    Beatmap _map;
    AudioClip _clip;
    int _spawnIndex;
    float _hitY, _bottomY;
    int _score, _combo, _hitNotes, _totalNotes;
    bool _started;
    int _mouseHeldLane = -1;
    Dictionary<int,int> _touchLane = new Dictionary<int,int>();

    void Awake() {
        Application.targetFrameRate = 120;
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (judgementTMP) judgementTMP.alpha = 0f;
    }

    void Start() {
        _map = BeatmapLoader.Load(beatmapId);
        if (_map == null) { enabled = false; return; }
        _clip = BeatmapLoader.LoadAudio(_map.audioFile);
        if (!_clip) { enabled = false; return; }
        if (lanes == null || lanes.Count != _map.lanes) { enabled = false; return; }

        _hitY = hitLine ? hitLine.position.y : 0f;
        _bottomY = bottomLine ? bottomLine.position.y : -6f;

        _map.notes.Sort((a,b) => a.time.CompareTo(b.time));
        _totalNotes = _map.notes.Count;

        foreach (var lane in lanes) lane.Init(this, conductor, _bottomY);

        conductor.StartSong(_clip, _map.globalOffset);
        _started = true;
    }

    void Update() {
        if (!_started) return;
        SpawnNotes();
        foreach (var lane in lanes) lane.UpdateAuto();
        HandleInput();
        UpdateUI();
    }

    void SpawnNotes() {
        float t = conductor.SongTime;
        while (_spawnIndex < _map.notes.Count && (_map.notes[_spawnIndex].time - t) <= _map.spawnLeadTime) {
            var nd = _map.notes[_spawnIndex];
            var go = notePool.Get();
            var note = go.GetComponent<NoteObject>();
            if (!note) note = go.AddComponent<NoteObject>();
            go.transform.position = new Vector3(lanes[nd.lane].laneAnchor.position.x, _hitY, 0f);
            note.Init(conductor, _hitY, travelSpeed, nd);
            lanes[nd.lane].Enqueue(note);
            _spawnIndex++;
        }
    }

    void HandleInput() {
        if (Input.GetMouseButtonDown(0)) {
            int lane = LaneFromScreenX(Input.mousePosition.x);
            if (lane >= 0) { lanes[lane].PressDown(); _mouseHeldLane = lane; }
        }
        if (Input.GetMouseButtonUp(0)) {
            if (_mouseHeldLane >= 0) { lanes[_mouseHeldLane].PressUp(); _mouseHeldLane = -1; }
        }
        for (int i = 0; i < Input.touchCount; i++) {
            var t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Began) {
                int lane = LaneFromScreenX(t.position.x);
                if (lane >= 0) { lanes[lane].PressDown(); _touchLane[t.fingerId] = lane; }
            } else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) {
                if (_touchLane.TryGetValue(t.fingerId, out int lane)) {
                    lanes[lane].PressUp();
                    _touchLane.Remove(t.fingerId);
                }
            }
        }
    }

    int LaneFromScreenX(float x) {
        float w = Screen.width;
        int n = lanes.Count;
        return Mathf.Clamp(Mathf.FloorToInt(x / (w / n)), 0, n - 1);
    }

    public void RegisterHit(ScoreTier tier) {
        _hitNotes++;
        if (tier == ScoreTier.Great || tier == ScoreTier.Excellent) _combo++;
        _score += ScoreFor(tier);
        ShowJudgement(tier);
    }

    public void RegisterMiss() {
        _combo = 0;
        ShowJudgement(null);
    }

    int ScoreFor(ScoreTier tier) {
        switch (tier) {
            case ScoreTier.Excellent: return 110;
            case ScoreTier.Great:     return  80;
            case ScoreTier.Good:      return  50;
        }
        return 0;
    }

    void ShowJudgement(ScoreTier? tier) {
        if (!judgementTMP) return;
        StopAllCoroutines();
        if (tier == null) { StartCoroutine(FadeOutJudgement(0.1f)); return; }
        string label = tier == ScoreTier.Excellent ? "EXCELLENT" : tier == ScoreTier.Great ? "GREAT" : "GOOD";
        Color c = (tier == ScoreTier.Excellent) ? new Color(0.25f, 0.95f, 0.35f)
               : (tier == ScoreTier.Great)     ? new Color(1.0f, 0.55f, 0.15f)
               :                                 new Color(1.0f, 0.90f, 0.25f);
        judgementTMP.text = label;
        judgementTMP.color = c;
        judgementTMP.alpha = 1f;
        StartCoroutine(FadeOutJudgement(0.35f));
    }

    System.Collections.IEnumerator FadeOutJudgement(float holdSeconds) {
        yield return new WaitForSeconds(holdSeconds);
        float a = judgementTMP.alpha;
        float dur = 0.20f;
        float t = 0f;
        while (t < dur) {
            t += Time.deltaTime;
            judgementTMP.alpha = Mathf.Lerp(a, 0f, t / dur);
            yield return null;
        }
        judgementTMP.alpha = 0f;
    }

    public void TriggerGameOver() {
        _started = false;
        conductor.StopSong();
        if (gameOverPanel) gameOverPanel.SetActive(true);
    }

    void UpdateUI() {
        if (scoreTMP) scoreTMP.text = $"{_score}";
        if (comboTMP) comboTMP.text = _combo > 0 ? $"x{_combo}" : "";
        if (accuracyTMP) {
            float acc = (_totalNotes > 0) ? (100f * _hitNotes / _totalNotes) : 100f;
            accuracyTMP.text = $"{acc:0.0}%";
        }
    }

    public void Despawn(NoteObject n) {
        if (!n) return;
        notePool.Return(n.gameObject);
    }
}
