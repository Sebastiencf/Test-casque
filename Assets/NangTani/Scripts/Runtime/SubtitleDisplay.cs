using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace NangTani
{
    /// <summary>
    /// Sous-titres provisoires (les voix definitives remplaceront ces repliques).
    /// Le texte flotte devant le joueur et le suit avec un leger retard (pas colle a la tete : confort VR).
    /// </summary>
    public class SubtitleDisplay : MonoBehaviour
    {
        public TextMeshPro text;
        public float distance = 1.7f;
        public float verticalOffset = -0.32f;
        public float followSpeed = 3f;

        Coroutine _routine;

        void Awake()
        {
            if (text != null)
            {
                text.text = "";
                text.alpha = 0f;
            }
        }

        // Dans Start (et non Awake) : le composant TextMeshPro est alors forcement initialise.
        void Start()
        {
            if (text != null) DrawOnTop(text);
        }

        /// <summary>Affiche le texte par-dessus le decor et par-dessus le quad de fondu au noir.</summary>
        public static void DrawOnTop(TextMeshPro label)
        {
            var material = label.fontMaterial; // copie propre a ce texte
            material.renderQueue = (int)RenderQueue.Overlay + 100;
            material.SetFloat("unity_GUIZTestMode", (float)CompareFunction.Always);
        }

        void LateUpdate()
        {
            var cam = ViewUtil.Cam;
            if (cam == null) return;

            Vector3 target = cam.transform.position + cam.transform.forward * distance + Vector3.up * verticalOffset;
            float k = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, target, k);

            Vector3 away = transform.position - cam.transform.position;
            if (away.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        public void Show(string line, float holdSeconds)
        {
            if (text == null) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(Run(line, holdSeconds));
        }

        public void Hide()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            if (text != null)
            {
                text.text = "";
                text.alpha = 0f;
            }
        }

        IEnumerator Run(string line, float holdSeconds)
        {
            text.text = line;
            yield return Fade(0f, 1f, 0.3f);
            yield return new WaitForSeconds(holdSeconds);
            yield return Fade(1f, 0f, 0.5f);
            text.text = "";
            _routine = null;
        }

        IEnumerator Fade(float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                text.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            text.alpha = to;
        }
    }
}
