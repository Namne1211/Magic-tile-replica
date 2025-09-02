using UnityEngine;

/// <summary>
/// Renders and positions a note in world space. For tap notes, it follows a single point.
/// For long notes, it stretches the sprite vertically to span from head to tail time
/// and keeps the object centered between the two.
/// </summary>
public class NoteObject : MonoBehaviour {
    [HideInInspector]
    public NoteData data;

    SongConductor _conductor;
    float _hitY;
    float _speed;
    SpriteRenderer _sr;
    float _baseSpriteHeight;
    Vector3 _origLocalScale;

    /// <summary>
    /// Initializes the note with timing, travel parameters, caches sprite/scale info,
    /// snaps it to the correct Y based on current song time, and applies initial shape.
    /// </summary>
    /// <param name="c">Song time source.</param>
    /// <param name="hitY">World Y of the hit/judge line.</param>
    /// <param name="speed">Travel speed in world units per second.</param>
    /// <param name="note">Beatmap data for this note.</param>
    public void Init(SongConductor c, float hitY, float speed, NoteData note) {
        _conductor = c;
        _hitY = hitY;
        _speed = speed;
        data = note;

        if (_sr == null) _sr = GetComponent<SpriteRenderer>();

        _origLocalScale = transform.localScale;
        _baseSpriteHeight = (_sr && _sr.sprite) ? (_sr.sprite.rect.height / _sr.sprite.pixelsPerUnit) : 1f;
        if (_baseSpriteHeight < 1e-4f) _baseSpriteHeight = 1f;

        float yNow = _hitY + (data.time - _conductor.SongTime) * _speed;
        var p = transform.position;
        transform.position = new Vector3(p.x, yNow, p.z);

        UpdateShape();
    }

    /// <summary>
    /// Per-frame update: re-evaluates the note's position/scale from current song time.
    /// </summary>
    void Update() {
        if (_conductor == null) return;
        UpdateShape();
    }

    /// <summary>
    /// Computes the note's world Y (and scale if long note) from the song time.
    /// Tap notes: keep original local scale and move along Y.
    /// Long notes: stretch along Y so the sprite spans from head to tail and stay centered.
    /// </summary>
    void UpdateShape() {
        float t = _conductor.SongTime;

        if (data.duration > 0f) {
            float headY = _hitY + (data.time - t) * _speed;
            float tailY = _hitY + (data.time + data.duration - t) * _speed;
            float midY  = 0.5f * (headY + tailY);
            float hWorld = Mathf.Abs(tailY - headY);

            float parentScaleY = transform.parent ? transform.parent.lossyScale.y : 1f;
            const float EPS = 1e-4f;
            parentScaleY = Mathf.Max(parentScaleY, EPS);

            float localY = Mathf.Max(hWorld / (Mathf.Max(_baseSpriteHeight, EPS) * parentScaleY), 0.001f);

            transform.localScale = new Vector3(_origLocalScale.x, localY, _origLocalScale.z);
            var p = transform.position;
            transform.position = new Vector3(p.x, midY, p.z);
        } else {
            float y = _hitY + (data.time - t) * _speed;
            var p = transform.position;
            transform.position = new Vector3(p.x, y, p.z);
            transform.localScale = _origLocalScale;
        }
    }
}
