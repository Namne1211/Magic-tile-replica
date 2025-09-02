using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles note queueing, input timing, tap/hold judgement, and lane-specific auto updates.
/// </summary>
public class LaneController : MonoBehaviour {

    [Header("Lane Settings")]
    [Tooltip("Index of this lane (0-based).")]
    public int laneIndex;
    [Tooltip("Transform that defines the x-position of this lane.")]
    public Transform laneAnchor;

    [Header("Timing Windows (seconds)")]
    [Tooltip("Max time difference for an Excellent hit.")]
    public float excellentWindow = 0.040f;
    [Tooltip("Max time difference for a Great hit.")]
    public float greatWindow = 0.090f;
    [Tooltip("Max time difference for a Good hit.")]
    public float goodWindow = 0.140f;
    [Tooltip("Time after which a note is considered missed.")]
    public float missForgive = 0.180f;
    [Tooltip("Fraction of hold duration required for an Excellent hold.")]
    public float holdExcellent = 0.98f;
    [Tooltip("Fraction of hold duration required for a Great hold.")]
    public float holdGreat = 0.70f;
    [Tooltip("Fraction of hold duration required for a Good hold.")]
    public float holdGood = 0.40f;

    float _bottomY;
    SongConductor _c;
    GameManager _gm;
    readonly Queue<NoteObject> _q = new Queue<NoteObject>();

    bool _holdActive;
    bool _pressing;
    NoteObject _holdNote;
    float _holdEndTime;
    float _heldTime;
    float _lastSongTime;

    NoteObject _latePenalized;

    /// <summary>
    /// Initializes the lane with references to the GameManager and SongConductor,
    /// sets the world Y threshold for game over, and seeds last-song-time.
    /// </summary>
    public void Init(GameManager gm, SongConductor c, float bottomYWorld) {
        _gm = gm; _c = c; _bottomY = bottomYWorld;
        _lastSongTime = _c != null ? _c.SongTime : 0f;
        _latePenalized = null;
    }

    /// <summary>
    /// Snaps the incoming note to this lane's X position and enqueues it for processing.
    /// </summary>
    public void Enqueue(NoteObject n) {
        var p = n.transform.position;
        n.transform.position = new Vector3(laneAnchor.position.x, p.y, p.z);
        _q.Enqueue(n);
    }

    /// <summary>
    /// Handles press/tap start logic:
    /// - If a hold is active, marks that we are pressing.
    /// - For tap notes, judges based on timing windows and despawns on hit.
    /// - For hold notes, begins tracking held time if within forgiveness.
    /// </summary>
    public void PressDown() {
        if (_holdActive) { _pressing = true; return; }
        if (_q.Count == 0) return;

        var n = _q.Peek();
        float dt = _c.SongTime - n.data.time;

        if (dt < -goodWindow) return;

        if (n.data.duration > 0f) {
            if (dt > missForgive) { if (_latePenalized != n) { _gm.RegisterMiss(); _latePenalized = n; } return; }
            _holdActive = true;
            _pressing = true;
            _holdNote = n;
            _holdEndTime = n.data.time + n.data.duration;
            _heldTime = 0f;
            _lastSongTime = _c.SongTime;
        } else {
            float adt = Mathf.Abs(dt);
            if (adt <= excellentWindow) { _gm.RegisterHit(ScoreTier.Excellent); DequeueIfLateRef(n); _gm.Despawn(n); }
            else if (adt <= greatWindow) { _gm.RegisterHit(ScoreTier.Great); DequeueIfLateRef(n); _gm.Despawn(n); }
            else if (adt <= goodWindow) { _gm.RegisterHit(ScoreTier.Good); DequeueIfLateRef(n); _gm.Despawn(n); }
            else if (dt > missForgive) { if (_latePenalized != n) { _gm.RegisterMiss(); _latePenalized = n; } }
        }
    }

    /// <summary>
    /// Handles release for hold notes: stops accumulating held time and evaluates the hold.
    /// </summary>
    public void PressUp() {
        if (!_holdActive) return;
        _pressing = false;
        EvaluateHoldAndEnd();
    }

    /// <summary>
    /// Per-frame lane logic:
    /// - If holding, accumulates held time while pressed and auto-finishes at hold end.
    /// - If idle, checks head-of-queue note for game-over threshold and late miss penalties.
    /// </summary>
    public void UpdateAuto() {
        float t = _c.SongTime;

        if (_holdActive) {
            float dt = Mathf.Max(0f, t - _lastSongTime);

            if (_pressing) _heldTime += dt;
            if (t >= _holdEndTime) EvaluateHoldAndEnd();

            _lastSongTime = t;
            return;
        }

        if (_q.Count == 0) { _lastSongTime = t; return; }

        var head = _q.Peek();

        if (head.transform.position.y < _bottomY) {
            _gm.TriggerGameOver();
            while (_q.Count > 0) { _gm.Despawn(_q.Dequeue()); }
            return;
        }

        float dth = t - head.data.time;
        if (dth > missForgive)
        {
            if (_latePenalized != head) { _gm.RegisterMiss(); _latePenalized = head; }
        }

        _lastSongTime = t;
    }

    /// <summary>
    /// Computes the fraction of required hold time achieved and registers
    /// the corresponding judgement; then dequeues and cleans up the hold note.
    /// </summary>
    void EvaluateHoldAndEnd() {
        if (!_holdNote) { _holdActive = false; return; }
        float required = Mathf.Max(_holdNote.data.duration, 1e-4f);
        float frac = Mathf.Clamp01(_heldTime / required);

        if (frac >= holdExcellent) _gm.RegisterHit(ScoreTier.Excellent);
        else if (frac >= holdGreat) _gm.RegisterHit(ScoreTier.Great);
        else if (frac >= holdGood) _gm.RegisterHit(ScoreTier.Good);
        else _gm.RegisterMiss();

        _q.Dequeue();
        if (_latePenalized == _holdNote) _latePenalized = null;

        _gm.Despawn(_holdNote);
        
        _holdActive = false;
        _pressing = false;
        _holdNote = null;
        _heldTime = 0f;
    }

    /// <summary>
    /// Safely dequeues the provided note if it is at the head, and clears late-penalty tracking for it.
    /// </summary>
    void DequeueIfLateRef(NoteObject n) {
        if (_q.Count > 0 && _q.Peek() == n) _q.Dequeue();
        if (_latePenalized == n) _latePenalized = null;
    }
}
