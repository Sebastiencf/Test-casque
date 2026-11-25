using System.Collections;
using UnityEngine;

namespace NangTani
{
    /// <summary>
    /// Fondu au noir : un quad colle devant la camera (materiau URP/Unlit transparent).
    /// Les changements d'ancre se font toujours sous fondu (pas de deplacement visible : confort VR).
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public Renderer overlay;
        public Color color = Color.black;

        MaterialPropertyBlock _block;

        public float Alpha { get; private set; } = 1f;

        void Awake()
        {
            _block = new MaterialPropertyBlock();
            SetAlpha(Alpha);
        }

        public void SetAlpha(float alpha)
        {
            Alpha = Mathf.Clamp01(alpha);
            if (overlay == null) return;
            if (_block == null) _block = new MaterialPropertyBlock();

            Color c = color;
            c.a = Alpha;
            overlay.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, c);
            overlay.SetPropertyBlock(_block);
            overlay.enabled = Alpha > 0.002f;
        }

        public IEnumerator FadeTo(float target, float seconds)
        {
            float from = Alpha;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                SetAlpha(Mathf.Lerp(from, target, Mathf.SmoothStep(0f, 1f, t / seconds)));
                yield return null;
            }
            SetAlpha(target);
        }
    }
}
