using System.Collections;
using UnityEngine;

namespace NangTani
{
    /// <summary>
    /// Personnage de la scene (Ram, Ming) : assis / debout, marche, tremble, salue.
    /// Fonctionne pour les silhouettes du greybox comme pour les futurs modeles
    /// (il suffit de placer le modele dans l'objet "Visual").
    /// </summary>
    public class CharacterActor : MonoBehaviour
    {
        [Tooltip("Enfant qui contient le corps. Mis a l'echelle quand le personnage s'assoit.")]
        public Transform visual;

        [Tooltip("Echelle verticale quand il est assis (greybox : corps ecrase).")]
        public float sittingScale = 0.5f;

        public float groundY = 0f;
        public float trembleAmount = 0.012f;

        public bool Trembling { get; set; }

        Vector3 _visualBasePos;
        Quaternion _visualBaseRot;
        float _seed;
        bool _wasTrembling;

        void Awake()
        {
            if (visual == null && transform.childCount > 0) visual = transform.GetChild(0);
            if (visual != null)
            {
                _visualBasePos = visual.localPosition;
                _visualBaseRot = visual.localRotation;
            }
            _seed = Random.value * 50f;
        }

        /// <summary>Place le personnage sur un repere (position + orientation).</summary>
        public void Place(Transform spot)
        {
            transform.SetPositionAndRotation(spot.position, spot.rotation);
        }

        public void SitDown()
        {
            SetVisualHeight(sittingScale);
        }

        public void StandUp()
        {
            SetVisualHeight(1f);
            Vector3 p = transform.position;
            p.y = groundY;
            transform.position = p;
        }

        void SetVisualHeight(float scaleY)
        {
            if (visual == null) return;
            Vector3 s = visual.localScale;
            s.y = scaleY;
            visual.localScale = s;
        }

        public void FaceTowards(Vector3 worldPoint)
        {
            Vector3 d = worldPoint - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(d, Vector3.up);
        }

        public IEnumerator MoveTo(Vector3 target, float speed)
        {
            target.y = transform.position.y;
            while ((target - transform.position).sqrMagnitude > 0.01f)
            {
                Vector3 dir = target - transform.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 1e-4f)
                {
                    Quaternion want = Quaternion.LookRotation(dir, Vector3.up);
                    transform.rotation = Quaternion.Slerp(transform.rotation, want, 6f * Time.deltaTime);
                }
                transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
                yield return null;
            }
        }

        /// <summary>Salut profond (Ram devant l'arbre sacre).</summary>
        public IEnumerator Bow(float seconds)
        {
            if (visual == null) yield break;
            float half = seconds * 0.5f;
            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                float a = Mathf.SmoothStep(0f, 38f, t / half);
                visual.localRotation = Quaternion.Euler(a, 0f, 0f) * _visualBaseRot;
                yield return null;
            }
            for (float t = 0f; t < half; t += Time.deltaTime)
            {
                float a = Mathf.SmoothStep(38f, 0f, t / half);
                visual.localRotation = Quaternion.Euler(a, 0f, 0f) * _visualBaseRot;
                yield return null;
            }
            visual.localRotation = _visualBaseRot;
        }

        void LateUpdate()
        {
            if (visual == null) return;

            if (Trembling)
            {
                float nx = (Mathf.PerlinNoise(Time.time * 18f + _seed, 0f) - 0.5f) * 2f;
                float nz = (Mathf.PerlinNoise(0f, Time.time * 17f + _seed) - 0.5f) * 2f;
                visual.localPosition = _visualBasePos + new Vector3(nx, 0f, nz) * trembleAmount;
                _wasTrembling = true;
            }
            else if (_wasTrembling)
            {
                visual.localPosition = _visualBasePos;
                _wasTrembling = false;
            }
        }
    }
}
