using System.Collections;
using UnityEngine;

namespace NangTani
{
    /// <summary>
    /// Declencheur implicite base sur la direction du regard (CDD, Section 5).
    /// Le joueur n'appuie sur rien : il suffit que sa tete soit orientee vers le point vise
    /// pendant <see cref="requiredSeconds"/>. Si le joueur regarde ailleurs, la jauge redescend
    /// (deux fois plus vite qu'elle ne monte).
    /// </summary>
    public abstract class ViewTrigger : MonoBehaviour
    {
        [Tooltip("Point vise. Si vide : la position de cet objet.")]
        public Transform aimPoint;

        [Tooltip("Duree (s) pendant laquelle le regard doit rester dans le cone.")]
        [Min(0.05f)] public float requiredSeconds = 1f;

        [Tooltip("Demi-angle du cone de regard, en degres.")]
        [Range(1f, 90f)] public float maxAngleDegrees = 10f;

        public bool Completed { get; private set; }
        public bool TimedOut { get; private set; }
        public float Progress { get; private set; }

        float _held;

        public Vector3 AimPosition => aimPoint != null ? aimPoint.position : transform.position;

        /// <summary>
        /// Coroutine : attend que le declencheur soit complete, ou que le delai soit ecoule
        /// (timeout &lt;= 0 : attend indefiniment). Teste ensuite <see cref="Completed"/> / <see cref="TimedOut"/>.
        /// </summary>
        public IEnumerator Wait(float timeoutSeconds)
        {
            Completed = false;
            TimedOut = false;
            _held = 0f;
            Progress = 0f;
            float startTime = Time.time;

            while (!Completed)
            {
                Tick();
                if (Completed) break;
                if (timeoutSeconds > 0f && Time.time - startTime >= timeoutSeconds)
                {
                    TimedOut = true;
                    break;
                }
                yield return null;
            }
        }

        void Tick()
        {
            var cam = ViewUtil.Cam;
            if (cam == null) return;

            Vector3 toTarget = AimPosition - cam.transform.position;
            float angle = Vector3.Angle(cam.transform.forward, toTarget);

            if (angle <= maxAngleDegrees) _held += Time.deltaTime;
            else _held = Mathf.Max(0f, _held - Time.deltaTime * 2f);

            Progress = Mathf.Clamp01(_held / requiredSeconds);
            if (_held >= requiredSeconds) Completed = true;
        }
    }
}
