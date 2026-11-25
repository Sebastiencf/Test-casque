using System.Collections;
using UnityEngine;

namespace NangTani
{
    /// <summary>
    /// Nang Tani. Elle apparait dans la brume (fondu depuis la couleur du brouillard),
    /// s'approche lentement, puis se dissout. Greybox : silhouette verte, visage plonge dans l'ombre.
    /// Le vert de la robe doit rester la SEULE couleur saturee de l'ecran (CDD, Section 3).
    /// </summary>
    public class SpiritController : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [Tooltip("Tous les rendus de la silhouette (robe, tete...).")]
        public Renderer[] renderers;

        public Light glow;
        public float glowIntensity = 1.1f;

        [Tooltip("Couleur de depart/arrivee du fondu (proche du brouillard).")]
        public Color hiddenColor = new Color(0.06f, 0.11f, 0.13f, 1f);

        Color[] _baseColors;
        MaterialPropertyBlock _block;
        bool _ready;

        void EnsureReady()
        {
            if (_ready) return;
            _block = new MaterialPropertyBlock();
            _baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var m = renderers[i].sharedMaterial;
                _baseColors[i] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
            }
            _ready = true;
        }

        public void SetVisible(float k)
        {
            EnsureReady();
            k = Mathf.Clamp01(k);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(_block);
                _block.SetColor(BaseColorId, Color.Lerp(hiddenColor, _baseColors[i], k));
                renderers[i].SetPropertyBlock(_block);
            }
            if (glow != null) glow.intensity = glowIntensity * k;
        }

        /// <summary>Cache completement Nang Tani.</summary>
        public void Hide()
        {
            EnsureReady();
            SetVisible(0f);
            gameObject.SetActive(false);
        }

        /// <summary>L'affiche immediatement, entierement visible.</summary>
        public void ShowInstant()
        {
            gameObject.SetActive(true);
            SetVisible(1f);
        }

        public void PlaceAt(Vector3 position, Vector3 lookAt)
        {
            position.y = 0f;
            transform.position = position;
            FacePoint(lookAt);
        }

        public void FacePoint(Vector3 worldPoint)
        {
            Vector3 d = worldPoint - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }

        public IEnumerator Appear(float seconds)
        {
            gameObject.SetActive(true);
            SetVisible(0f);
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                SetVisible(Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            SetVisible(1f);
        }

        public IEnumerator WalkTo(Vector3 target, float speed)
        {
            target.y = transform.position.y;
            while ((target - transform.position).sqrMagnitude > 0.01f)
            {
                FacePoint(target);
                transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
                yield return null;
            }
        }

        /// <summary>Elle se dissout dans la brume.</summary>
        public IEnumerator Dissolve(float seconds)
        {
            Vector3 start = transform.position;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = t / seconds;
                SetVisible(1f - Mathf.SmoothStep(0f, 1f, k));
                transform.position = start + Vector3.up * (0.25f * k);
                yield return null;
            }
            transform.position = start;
            Hide();
        }
    }
}
