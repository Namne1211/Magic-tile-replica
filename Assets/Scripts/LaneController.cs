using System.Collections.Generic;
using UnityEngine;

public class LaneController : MonoBehaviour {
    public int laneIndex;
    public Transform laneAnchor;
    public float excellentWindow = 0.040f;
    public float greatWindow = 0.090f;
    public float goodWindow = 0.140f;
    public float missForgive = 0.180f;
    public float holdExcellent = 0.98f;
    public float holdGreat = 0.70f;
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

    public void Init(GameManager gm, SongConductor c, float bottomYWorld) {
        _gm = gm; _c = c; _bottomY = bottomYWorld;
        _lastSongTime = _c != null ? _c.SongTime : 0f;
    }

    public void Enqueue(NoteObject n) {
        var p = n.transform.position;
        n.transform.position = new Vector3(laneAnchor.position.x, p.y, p.z);
        _q.Enqueue(n);
    }

    public void PressDown() {
        if (_holdActive) { _pressing = true; return; }
        if (_q.Count == 0) return;
        var n = _q.Peek();
        float dt = _c.SongTime - n.data.time;
        if (dt < -goodWindow) return;

        if (n.data.duration > 0f) {
            if (dt > missForgive) {
                _gm.RegisterMiss();
                _q.Dequeue(); _gm.Despawn(n);
                return;
            }
            _holdActive = true;
            _pressing = true;
            _holdNote = n;
            _holdEndTime = n.data.time + n.data.duration;
            _heldTime = 0f;
            _lastSongTime = _c.SongTime;
        } else {
            float adt = Mathf.Abs(dt);
            if (adt <= excellentWindow) { _gm.RegisterHit(ScoreTier.Excellent); _q.Dequeue(); _gm.Despawn(n); }
            else if (adt <= greatWindow) { _gm.RegisterHit(ScoreTier.Great); _q.Dequeue(); _gm.Despawn(n); }
            else if (adt <= goodWindow) { _gm.RegisterHit(ScoreTier.Good); _q.Dequeue(); _gm.Despawn(n); }
            else if (dt > missForgive) { _gm.RegisterMiss(); _q.Dequeue(); _gm.Despawn(n); }
        }
    }

    public void PressUp() {
        if (!_holdActive) return;
        _pressing = false;
        EvaluateHoldAndEnd();
    }

    public void UpdateAuto() {
        float t = _c.SongTime;

        if (_holdActive) {
            float dt = Mathf.Max(0f, t - _lastSongTime);
            if (_pressing) _heldTime += dt;
            if (t >= _holdEndTime) {
                EvaluateHoldAndEnd();
            }
            _lastSongTime = t;
            return;
        }

        if (_q.Count == 0) return;

        var head = _q.Peek();

        if (head.transform.position.y < _bottomY) {
            _gm.TriggerGameOver();
            while (_q.Count > 0) { _gm.Despawn(_q.Dequeue()); }
            return;
        }

        float dth = t - head.data.time;
        if (dth > missForgive) {
            _gm.RegisterMiss();
            _q.Dequeue();
            _gm.Despawn(head);
        }

        _lastSongTime = t;
    }

    void EvaluateHoldAndEnd() {
        if (!_holdNote) { _holdActive = false; return; }
        float required = Mathf.Max(_holdNote.data.duration, 1e-4f);
        float frac = Mathf.Clamp01(_heldTime / required);

        if (frac >= holdExcellent) _gm.RegisterHit(ScoreTier.Excellent);
        else if (frac >= holdGreat) _gm.RegisterHit(ScoreTier.Great);
        else if (frac >= holdGood) _gm.RegisterHit(ScoreTier.Good);
        else _gm.RegisterMiss();

        _q.Dequeue();
        _gm.Despawn(_holdNote);
        _holdActive = false;
        _pressing = false;
        _holdNote = null;
        _heldTime = 0f;
    }
}
