using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lightweight object pool with optional fade-out (and scale-down for normal tiles) when returning items.
/// Long notes (NoteObject with duration > 0) only fade, they do not scale down.
/// </summary>
public class SimplePool : MonoBehaviour {
    [Header("Pool Settings")]
    [Tooltip("Prefab to instantiate and pool.")]
    public GameObject prefab;
    [Tooltip("Number of instances to pre-instantiate and keep in the pool.")]
    public int prewarm = 64;
    [Tooltip("Seconds to fade out when returning to pool. Set to 0 for instant.")]
    public float fadeDuration = 0.15f;

    readonly Stack<GameObject> _stack = new Stack<GameObject>();
    readonly Dictionary<GameObject, Coroutine> _fade = new Dictionary<GameObject, Coroutine>();
    readonly Dictionary<GameObject, Vector3> _origScale = new Dictionary<GameObject, Vector3>();

    /// <summary>
    /// Prewarms the pool by instantiating the requested number of objects and deactivating them.
    /// Also caches each instance's original local scale for later restoration.
    /// </summary>
    void Awake() {
        for (int i = 0; i < prewarm; i++) {
            var go = Instantiate(prefab, transform);
            if (!_origScale.ContainsKey(go)) _origScale[go] = go.transform.localScale;
            go.SetActive(false);
            _stack.Push(go);
        }
    }

    /// <summary>
    /// Retrieves an object from the pool (or instantiates a new one if empty),
    /// cancels any pending fade, resets scale and sprite alphas, and activates it.
    /// </summary>
    public GameObject Get() {
        GameObject go = _stack.Count > 0 ? _stack.Pop() : Instantiate(prefab, transform);

        if (!_origScale.ContainsKey(go)) _origScale[go] = go.transform.localScale;
        if (_fade.TryGetValue(go, out var co)) { StopCoroutine(co); _fade.Remove(go); }

        go.transform.localScale = _origScale[go];
        go.SetActive(true);

        var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++) {
            var c = srs[i].color;
            if (!float.IsFinite(c.a) || c.a < 1e-3f) srs[i].color = new Color(c.r, c.g, c.b, 1f);
        }
        return go;
    }

    /// <summary>
    /// Returns an object to the pool. If fadeDuration > 0, starts a fade/scale coroutine;
    /// otherwise deactivates immediately and pushes back to the stack.
    /// </summary>
    public void Return(GameObject go) {
        if (!go) return;

        if (fadeDuration <= 0f) {
            go.SetActive(false);
            _stack.Push(go);
            return;
        }

        if (_fade.TryGetValue(go, out var co)) StopCoroutine(co);
        _fade[go] = StartCoroutine(FadeAndPool(go));
    }

    /// <summary>
    /// Coroutine that fades all SpriteRenderers to alpha 0 over fadeDuration.
    /// For normal tiles, also scales the object down; long notes only fade.
    /// When finished, deactivates the object, restores original colors & scale, and returns it to the stack.
    /// </summary>
    IEnumerator FadeAndPool(GameObject go) {
        var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
        var originals = new Color[srs.Length];
        for (int i = 0; i < srs.Length; i++) originals[i] = srs[i].color;

        Vector3 origScale = _origScale.ContainsKey(go) ? _origScale[go] : go.transform.localScale;

        bool isLong = false;
        var note = go.GetComponent<NoteObject>();
        if (note != null && note.data != null && note.data.duration > 0f) isLong = true;

        float t = 0f;
        while (t < fadeDuration && go) {
            t += Time.deltaTime;
            float f = Mathf.Clamp01(t / fadeDuration);
            float k = 1f - f;

            for (int i = 0; i < srs.Length; i++) {
                var oc = originals[i];
                srs[i].color = new Color(oc.r, oc.g, oc.b, k * oc.a);
            }

            if (!isLong) {
                float ks = Mathf.Max(k, 0.001f);
                go.transform.localScale = new Vector3(origScale.x * ks, origScale.y * ks, origScale.z);
            }
            yield return null;
        }

        if (go) {
            // Force alpha 0 before pooling
            for (int i = 0; i < srs.Length; i++) {
                var oc = originals[i];
                srs[i].color = new Color(oc.r, oc.g, oc.b, 0f);
            }

            go.SetActive(false);

            // Restore colors & scale for next Get()
            for (int i = 0; i < srs.Length; i++) srs[i].color = originals[i];
            go.transform.localScale = origScale;

            _stack.Push(go);
        }

        _fade.Remove(go);
    }
}
