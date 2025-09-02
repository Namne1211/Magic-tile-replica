using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SimplePool : MonoBehaviour {
    public GameObject prefab;
    public int prewarm = 64;
    public float fadeDuration = 0.15f;

    readonly Stack<GameObject> _stack = new Stack<GameObject>();
    readonly Dictionary<GameObject, Coroutine> _fade = new Dictionary<GameObject, Coroutine>();

    void Awake() {
        for (int i = 0; i < prewarm; i++) {
            var go = Instantiate(prefab, transform);
            go.SetActive(false);
            _stack.Push(go);
        }
    }

    public GameObject Get() {
        GameObject go = _stack.Count > 0 ? _stack.Pop() : Instantiate(prefab, transform);
        if (_fade.TryGetValue(go, out var co)) { StopCoroutine(co); _fade.Remove(go); }
        go.SetActive(true);
        var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++) {
            var c = srs[i].color;
            if (!float.IsFinite(c.a) || c.a < 1e-3f) srs[i].color = new Color(c.r, c.g, c.b, 1f);
        }
        return go;
    }

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

    IEnumerator FadeAndPool(GameObject go) {
        var srs = go.GetComponentsInChildren<SpriteRenderer>(true);
        var originals = new Color[srs.Length];
        for (int i = 0; i < srs.Length; i++) originals[i] = srs[i].color;

        float t = 0f;
        while (t < fadeDuration && go) {
            t += Time.deltaTime;
            float k = 1f - Mathf.Clamp01(t / fadeDuration);
            for (int i = 0; i < srs.Length; i++) {
                var oc = originals[i];
                srs[i].color = new Color(oc.r, oc.g, oc.b, k * oc.a);
            }
            yield return null;
        }

        if (go) {
            for (int i = 0; i < srs.Length; i++) srs[i].color = originals[i] * new Color(1f, 1f, 1f, 0f);
            go.SetActive(false);
            for (int i = 0; i < srs.Length; i++) srs[i].color = originals[i];
            _stack.Push(go);
        }

        _fade.Remove(go);
    }
}
