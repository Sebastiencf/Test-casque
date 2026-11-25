using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NangTani
{
    /// <summary>
    /// Bougie : flamme qui vacille, avec un niveau 0..1 (0 = eteinte).
    /// Toutes les bougies actives sont listees dans <see cref="Candles"/> pour que
    /// l'atmosphere puisse les eteindre / rallumer d'un coup.
    /// </summary>
    public class CandleFlame : MonoBehaviour
    {
        static readonly List<CandleFlame> All = new List<CandleFlame>();
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public static IReadOnlyList<CandleFlame> Candles => All;

        public Light flameLight;
        public Renderer flameRenderer;
        public float baseIntensity = 2f;
        [Range(0f, 1f)] public float flicker = 0.25f;
        public Color flameColor = new Color(1f, 0.69f, 0.29f, 1f);

        float _level = 1f;
        float _seed;
        Vector3 _flameScale = Vector3.one;
        MaterialPropertyBlock _block;

        public float Level => _level;

        void Awake()
        {
            _seed = Random.value * 100f;
            _block = new MaterialPropertyBlock();
            if (flameRenderer != null) _flameScale = flameRenderer.transform.localScale;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public void SetLevel(float level)
        {
            _level = Mathf.Clamp01(level);
            Apply();
        }

        void Update()
        {
            if (_level > 0.001f) Apply();
        }

        void Apply()
        {
            float n = Mathf.PerlinNoise(Time.time * 6f + _seed, _seed);
            float f = 1f - flicker + 2f * flicker * n;
            bool on = _level > 0.01f;

            if (flameLight != null)
            {
                flameLight.enabled = on;
                flameLight.intensity = baseIntensity * _level * f;
            }

            if (flameRenderer != null)
            {
                flameRenderer.enabled = on;
                if (_block == null) _block = new MaterialPropertyBlock();
                Color c = flameColor * (0.6f + 0.4f * f);
                c.a = 1f;
                _block.SetColor(BaseColorId, c);
                flameRenderer.SetPropertyBlock(_block);
                flameRenderer.transform.localScale = _flameScale * Mathf.Max(0.01f, _level);
            }
        }

        public IEnumerator FadeTo(float target, float seconds)
        {
            float from = _level;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                SetLevel(Mathf.Lerp(from, target, t / seconds));
                yield return null;
            }
            SetLevel(target);
        }
    }
}
