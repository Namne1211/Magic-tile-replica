using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
/// <summary>
/// Judgement tiers for scoring.
/// </summary>
public enum ScoreTier { Good, Great, Excellent }

/// <summary>
/// Central gameplay orchestrator: spawns notes, routes input to lanes, scoring/UI updates,
/// lightweight VFX triggers, and game-over handling.
/// </summary>
public class GameManager : MonoBehaviour {
    [Header("Core References")]
    [Tooltip("Song timing provider used for precise beat-time and audio scheduling.")]
    public SongConductor conductor;
    [Tooltip("Pool responsible for spawning and recycling note GameObjects.")]
    public SimplePool notePool;
    [Tooltip("World-space Y position where notes should be judged/hit.")]
    public Transform hitLine;
    [Tooltip("World-space Y position that triggers Game Over when a note passes below it.")]
    public Transform bottomLine;
    [Tooltip("Ordered list of lane controllers. Size must match Beatmap.lanes.")]
    public List<LaneController> lanes = new List<LaneController>();

    [Header("UI References")]
    [Tooltip("Score text UI.")]
    public TextMeshProUGUI scoreTMP;
    [Tooltip("Combo text UI.")]
    public TextMeshProUGUI comboTMP;
    [Tooltip("Accuracy percentage UI.")]
    public TextMeshProUGUI accuracyTMP;
    [Tooltip("Judgement text UI (GOOD/GREAT/EXCELLENT).")]
    public TextMeshProUGUI judgementTMP;

    [Header("Judgement Visuals")]
    [Tooltip("Seconds the judgement text takes to fade out after showing.")]
    [Min(0f)] public float judgementFadeTime = 0.20f;
    [Tooltip("Display color for GOOD judgement.")]
    public Color goodColor = new Color(1f, 0.9f, 0.25f);
    [Tooltip("Display color for GREAT judgement.")]
    public Color greatColor = new Color(1f, 0.55f, 0.15f);
    [Tooltip("Display color for EXCELLENT judgement.")]
    public Color excellentColor = new Color(0.25f, 0.95f, 0.35f);

    [Header("Game Over")]
    [Tooltip("Panel shown when the game ends.")]
    public GameObject gameOverPanel;

    [Header("Beatmap & Travel")]
    [Tooltip("Beatmap identifier file name in Resources/Beatmaps without extension.")]
    public string beatmapId = "song01";
    [Tooltip("Note vertical travel speed in world units per second.")]
    [Min(0.01f)] public float travelSpeed = 7f;
    [Tooltip("Y-position below which pooled notes are force-despawned (safety net).")]
    public float despawnY = -12f;

    [Header("Scoring")]
    [Tooltip("Base score value used to calculate per-judgement points.")]
    [Min(1)] public int baseScorePerNote = 50;

    [Header("VFX")]
    [Tooltip("One-shot particles for judgement pop.")]
    public ParticleSystem judgementVFX;
    [Tooltip("World position for judgement VFX.")]
    public Transform judgementVFXPoint;
    [Tooltip("One-shot particles when combo increases.")]
    public ParticleSystem comboVFX;
    [Tooltip("World position for combo VFX.")]
    public Transform comboVFXPoint;
    [Tooltip("One-shot particles when tapping a lane.")]
    public ParticleSystem clickVFXPrefab;
    [Tooltip("Looping particles while holding a lane.")]
    public ParticleSystem holdLoopVFXPrefab;

    [Header("UI Pop Animation")]
    [Tooltip("Target scale multiplier at pop peak.")]
    [Range(1f, 2f)] public float uiPopScale = 1.15f;
    [Tooltip("Seconds to scale up to peak.")]
    [Min(0f)] public float uiPopUpTime = 0.06f;
    [Tooltip("Seconds to scale back to base.")]
    [Min(0f)] public float uiPopDownTime = 0.10f;

    [Header("End Conditions")]
    [Tooltip("Automatically trigger Game Over when the song finishes.")]
    public bool gameOverOnSongEnd = true;
    [Tooltip("Extra seconds to avoid early cutoff due to scheduling jitter.")]
    [Min(0f)] public float songEndGrace = 0.05f;

    Beatmap _map;
    AudioClip _clip;
    int _spawnIndex;
    float _hitY, _bottomY;
    int _score, _combo, _hitNotes, _totalNotes;
    bool _started;
    int _mouseHeldLane = -1;
    Dictionary<int,int> _touchLane = new Dictionary<int,int>();

    int _prevScore = -1;
    int _prevCombo = -1;
    string _prevAcc = null;

    readonly Dictionary<Transform, Coroutine> _popCo = new Dictionary<Transform, Coroutine>();
    readonly Dictionary<Transform, Vector3> _baseScale = new Dictionary<Transform, Vector3>();

    ParticleSystem[] _holdLoopPS;
    int[] _holdPressCount;

    /// <summary>
    /// One-time UI state prep: hide game-over, clear judgement, and cache base scales used by the pop animation.
    /// </summary>
    void Awake() {
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (judgementTMP) {
            judgementTMP.alpha = 0f;
            CacheScale(judgementTMP.rectTransform);
        }
        if (scoreTMP) CacheScale(scoreTMP.rectTransform);
        if (comboTMP) CacheScale(comboTMP.rectTransform);
        if (accuracyTMP) CacheScale(accuracyTMP.rectTransform);
    }

    /// <summary>
    /// Loads beatmap/audio, validates lanes, initializes lane controllers, sets up hold-loop VFX,
    /// and starts playback via the SongConductor.
    /// </summary>
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

        SetupHoldLoopVFX();

        conductor.StartSong(_clip, _map.globalOffset);
        _started = true;
    }

    /// <summary>
    /// Main frame loop: spawns upcoming notes, advances lane state, processes input, and refreshes UI.
    /// </summary>
    void Update()
    {
        if (!_started) return;

        bool flowControl = SongRunning();
        if (!flowControl)
        {
            return;
        }

        SpawnNotes();
        foreach (var lane in lanes) lane.UpdateAuto();
        HandleInput();
        UpdateUI();
    }

    private bool SongRunning()
    {
        if (gameOverOnSongEnd && _started && _clip && conductor)
        {
            float songTime = conductor.SongTime;

            // Prefer time-based check; also fall back to AudioSource state as a safety
            if (songTime >= (_clip.length - songEndGrace) ||
                (conductor.audioSource && !conductor.audioSource.isPlaying && songTime > 0f))
            {
                TriggerGameOver();
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Spawns notes ahead of time based on spawnLeadTime and queues them into the correct lane.
    /// </summary>
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

    /// <summary>
    /// Maps mouse and touch input to lane presses/releases.
    /// Also triggers click VFX on press and manages the hold-loop VFX lifecycle per lane.
    /// </summary>
    void HandleInput() {
        if (Input.GetMouseButtonDown(0)) {
            int lane = LaneFromScreenX(Input.mousePosition.x);
            if (lane >= 0) {
                lanes[lane].PressDown();
                _mouseHeldLane = lane;
                PlayClickAtLane(lane);
                StartHoldLoopAtLane(lane);
            }
        }
        if (Input.GetMouseButtonUp(0)) {
            if (_mouseHeldLane >= 0) {
                lanes[_mouseHeldLane].PressUp();
                StopHoldLoopAtLane(_mouseHeldLane);
                _mouseHeldLane = -1;
            }
        }

        for (int i = 0; i < Input.touchCount; i++) {
            var t = Input.GetTouch(i);
            if (t.phase == TouchPhase.Began) {
                int lane = LaneFromScreenX(t.position.x);
                
                if (lane >= 0)
                {
                    lanes[lane].PressDown();
                    _touchLane[t.fingerId] = lane;
                    PlayClickAtLane(lane);
                    StartHoldLoopAtLane(lane);
                }
            } else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) {
                if (_touchLane.TryGetValue(t.fingerId, out int lane))
                {
                    lanes[lane].PressUp();
                    StopHoldLoopAtLane(lane);
                    _touchLane.Remove(t.fingerId);
                }
            }
        }
    }

    /// <summary>
    /// Converts a screen X position (mouse/touch) into a lane index spanning the full screen width.
    /// </summary>
    int LaneFromScreenX(float x) {
        float w = Screen.width;
        int n = lanes.Count;
        return Mathf.Clamp(Mathf.FloorToInt(x / (w / n)), 0, n - 1);
    }

    /// <summary>
    /// Applies scoring/combo on a successful hit, shows judgement text and VFX.
    /// </summary>
    public void RegisterHit(ScoreTier tier) {
        _hitNotes++;

        if (tier == ScoreTier.Great || tier == ScoreTier.Excellent) _combo++;
        else _combo = 0;

        _score += ScoreFor(tier);
        ShowJudgement(tier);
    }

    /// <summary>
    /// Handles a miss: resets combo and shows a fading judgement (no label).
    /// </summary>
    public void RegisterMiss() {
        _combo = 0;
        ShowJudgement(null);
    }

    /// <summary>
    /// Returns the numeric score for a given judgement tier using baseScorePerNote.
    /// </summary>
    int ScoreFor(ScoreTier tier) {
        switch (tier) {
            case ScoreTier.Excellent: return baseScorePerNote * 2;
            case ScoreTier.Great:     return (int)(baseScorePerNote * 1.5f);
            case ScoreTier.Good:      return  baseScorePerNote;
        }
        return 0;
    }

    /// <summary>
    /// Displays judgement text with color, plays pop + VFX, then starts a fade-out.
    /// If tier is null, quickly fades out any current text.
    /// </summary>
    void ShowJudgement(ScoreTier? tier) {
        if (!judgementTMP) return;

        StopAllCoroutines();

        if (tier == null) { StartCoroutine(FadeOutJudgement(0.1f)); return; }

        string label = tier == ScoreTier.Excellent ? "EXCELLENT" : tier == ScoreTier.Great ? "GREAT" : "GOOD";
        Color c = (tier == ScoreTier.Excellent) ? excellentColor
               : (tier == ScoreTier.Great)     ? greatColor
               :                                 goodColor;

        judgementTMP.text = label;
        judgementTMP.color = c;
        judgementTMP.alpha = 1f;

        Pop(judgementTMP ? judgementTMP.rectTransform : null);
        PlayVFX(judgementVFX, judgementVFXPoint);
        StartCoroutine(FadeOutJudgement(judgementFadeTime));
    }

    /// <summary>
    /// Coroutine to fade the judgement text alpha to zero after a short hold delay.
    /// </summary>
    IEnumerator FadeOutJudgement(float holdSeconds) {
        yield return new WaitForSeconds(holdSeconds);
        float a = judgementTMP.alpha;
        float dur = 0.20f;
        float t = 0f;
        
        while (t < dur)
        {
            t += Time.deltaTime;
            judgementTMP.alpha = Mathf.Lerp(a, 0f, t / dur);
            yield return null;
        }

        judgementTMP.alpha = 0f;
    }

    /// <summary>
    /// Stops gameplay and shows the game-over UI.
    /// </summary>
    public void TriggerGameOver() {
        _started = false;
        conductor.StopSong();

        if (gameOverPanel) gameOverPanel.SetActive(true);
    }

    /// <summary>
    /// Writes score/combo/accuracy to UI and plays small pop animations;
    /// combo VFX triggers when combo increases.
    /// </summary>
    void UpdateUI() {
        if (scoreTMP) {
            if (_score != _prevScore)
            {
                scoreTMP.text = $"{_score}";
                Pop(scoreTMP.rectTransform);
                _prevScore = _score;
            }
        }
        if (comboTMP) {
            string comboText = _combo > 0 ? $"x{_combo}" : "";
            if (comboTMP.text != comboText)
            {
                int oldCombo = _prevCombo;
                comboTMP.text = comboText;
                Pop(comboTMP.rectTransform);
                if (_combo > 0 && _combo > oldCombo) PlayVFX(comboVFX, comboVFXPoint);
                _prevCombo = _combo;
            }
        }
        if (accuracyTMP) {
            float acc = (_totalNotes > 0) ? (100f * _hitNotes / _totalNotes) : 100f;
            string accStr = $"{acc:0.0}%";
            if (_prevAcc != accStr)
            {
                accuracyTMP.text = accStr;
                Pop(accuracyTMP.rectTransform);
                _prevAcc = accStr;
            }
        }
    }

    /// <summary>
    /// Returns a note object to the pool through the SimplePool.
    /// </summary>
    public void Despawn(NoteObject n) {
        if (!n) return;
        notePool.Return(n.gameObject);
    }

    /// <summary>
    /// Records the base localScale of a transform (used by Pop animation).
    /// </summary>
    void CacheScale(Transform t) {
        if (!t) return;
        if (!_baseScale.ContainsKey(t)) _baseScale[t] = t.localScale;
    }

    /// <summary>
    /// Starts (or restarts) a pop-scale animation on the given transform.
    /// </summary>
    void Pop(Transform t) {
        if (!t) return;
        CacheScale(t);
        if (_popCo.TryGetValue(t, out var co)) StopCoroutine(co);
        _popCo[t] = StartCoroutine(PopRoutine(t));
    }

    /// <summary>
    /// Coroutine: scales a transform up to uiPopScale, then back to its cached base scale.
    /// </summary>
    IEnumerator PopRoutine(Transform t) {
        Vector3 baseS = _baseScale[t];
        Vector3 upS = baseS * uiPopScale;
        float t1 = 0f;
        
        while (t1 < uiPopUpTime)
        {
            t1 += Time.deltaTime;
            float k = Mathf.Clamp01(t1 / uiPopUpTime);
            t.localScale = Vector3.Lerp(baseS, upS, k);
            yield return null;
        }

        float t2 = 0f;
        
        while (t2 < uiPopDownTime)
        {
            t2 += Time.deltaTime;
            float k = Mathf.Clamp01(t2 / uiPopDownTime);
            t.localScale = Vector3.Lerp(upS, baseS, k);
            yield return null;
        }

        t.localScale = baseS;
        _popCo.Remove(t);
    }

    /// <summary>
    /// Places and plays a ParticleSystem (one-shot) at a given point.
    /// </summary>
    void PlayVFX(ParticleSystem ps, Transform point) {
        if (!ps) return;
        if (point) ps.transform.position = point.position;

        ps.Clear(true);
        ps.Play(true);
    }

    /// <summary>
    /// Instantiates and configures a looping ParticleSystem per lane for hold visuals.
    /// </summary>
    void SetupHoldLoopVFX() {
        int n = lanes.Count;
        _holdLoopPS = new ParticleSystem[n];
        _holdPressCount = new int[n];

        if (!holdLoopVFXPrefab) return;
        
        for (int i = 0; i < n; i++)
        {
            var inst = Instantiate(holdLoopVFXPrefab, transform);
            var pos = new Vector3(lanes[i].laneAnchor.position.x, _hitY, 0f);

            inst.transform.position = pos;
            var main = inst.main;
            main.loop = true;

            inst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            _holdLoopPS[i] = inst;
            _holdPressCount[i] = 0;
        }
    }

    /// <summary>
    /// Spawns and plays a one-shot click ParticleSystem at the lane hit line.
    /// </summary>
    void PlayClickAtLane(int lane) {
        if (!clickVFXPrefab || lane < 0 || lane >= lanes.Count) return;

        var ps = Instantiate(clickVFXPrefab, transform);
        var pos = new Vector3(lanes[lane].laneAnchor.position.x, _hitY, 0f);

        ps.transform.position = pos;

        var main = ps.main;
        main.loop = false;

#if UNITY_2022_2_OR_NEWER
        main.stopAction = ParticleSystemStopAction.Destroy;
#endif

        ps.Play(true);
        StartCoroutine(DestroyAfter(ps, main.duration + main.startLifetime.constantMax + 0.2f));
    }

    /// <summary>
    /// Increments the hold-press counter for a lane and starts its loop VFX if not already playing.
    /// </summary>
    void StartHoldLoopAtLane(int lane) {
        if (_holdLoopPS == null || lane < 0 || lane >= _holdLoopPS.Length) return;

        var ps = _holdLoopPS[lane];
        if (!ps) return;

        _holdPressCount[lane]++;
        
        if (!ps.isPlaying)
        {
            ps.Clear(true);
            ps.Play(true);
        }
    }

    /// <summary>
    /// Decrements the hold-press counter for a lane and stops its loop VFX when the counter reaches zero.
    /// </summary>
    void StopHoldLoopAtLane(int lane) {
        if (_holdLoopPS == null || lane < 0 || lane >= _holdLoopPS.Length) return;

        var ps = _holdLoopPS[lane];
        if (!ps) return;

        _holdPressCount[lane] = Mathf.Max(0, _holdPressCount[lane] - 1);
        
        if (_holdPressCount[lane] == 0)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    /// <summary>
    /// Utility coroutine to destroy a particle system after a delay (duration + lifetime).
    /// </summary>
    IEnumerator DestroyAfter(ParticleSystem ps, float time) {
        if (!ps) yield break;
        yield return new WaitForSeconds(time);
        if (ps) Destroy(ps.gameObject);
    }
}
