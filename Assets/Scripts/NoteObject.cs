using UnityEngine;

public class NoteObject : MonoBehaviour {
    public NoteData data;
    SongConductor _conductor;
    float _hitY;
    float _speed;
    SpriteRenderer _head;
    SpriteRenderer _body;
    float _bodyBaseHeight;

    public void Init(SongConductor c, float hitY, float speed, NoteData note) {
        _conductor = c;
        _hitY = hitY;
        _speed = speed;
        data = note;

        if (_head == null) _head = GetComponent<SpriteRenderer>();

        if (data.duration > 0f) {
            if (_body == null) {
                var go = new GameObject("Body");
                go.transform.SetParent(transform, false);
                _body = go.AddComponent<SpriteRenderer>();
                _body.sprite = _head ? _head.sprite : null;
                _body.color = _head ? new Color(_head.color.r, _head.color.g, _head.color.b, 1f) : new Color(0f, 0f, 0f, 0.6f);
                if (_head) {
                    _body.sortingLayerID = _head.sortingLayerID;
                    _body.sortingOrder = _head.sortingOrder - 1;
                }
                _body.transform.localScale = Vector3.one;
            }
            _body.enabled = true;
            _bodyBaseHeight = (_body.sprite ? (_body.sprite.rect.height / _body.sprite.pixelsPerUnit) : 1f);
            if (_bodyBaseHeight < 1e-4f) _bodyBaseHeight = 1f;
        } else {
            if (_body) _body.enabled = false;
        }

        float yNow = _hitY + (data.time - _conductor.SongTime) * _speed;
        var p = transform.position;
        transform.position = new Vector3(p.x, yNow, p.z);
        UpdateBody();
    }

    void Update() {
        if (_conductor == null) return;
        float t = _conductor.SongTime;
        float y = _hitY + (data.time - t) * _speed;
        var p = transform.position;
        transform.position = new Vector3(p.x, y, p.z);
        UpdateBody();
    }

    void UpdateBody() {
        if (_body == null || data.duration <= 0f) return;

        float t = _conductor.SongTime;
        float headY = transform.position.y;
        float tailY = _hitY + (data.time + data.duration - t) * _speed;
        float midY = 0.5f * (headY + tailY);
        float hWorld = Mathf.Abs(tailY - headY);

        const float EPS = 1e-4f;
        float parentScaleY = Mathf.Max(transform.lossyScale.y, EPS);
        float baseH = Mathf.Max(_bodyBaseHeight, EPS);

        float sY = hWorld / (baseH * parentScaleY);
        if (!float.IsFinite(sY)) sY = 0.001f;
        sY = Mathf.Clamp(sY, 0.001f, 1000f);

        _body.transform.localScale = new Vector3(1f, sY, 1f);
        var bp = _body.transform.position;
        _body.transform.position = new Vector3(transform.position.x, midY, bp.z);
    }
}
