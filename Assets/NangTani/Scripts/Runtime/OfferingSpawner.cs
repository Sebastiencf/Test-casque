using System.Collections;
using UnityEngine;

namespace NangTani
{
    /// <summary>
    /// Les offrandes deposees par le joueur au tap de gachette : fleurs, encens, feuille.
    /// Apparition douce, fumee d'encens qui monte droit, petite lueur ambree.
    /// </summary>
    public class OfferingSpawner : MonoBehaviour
    {
        public GameObject offeringsRoot;
        public ParticleSystem smoke;
        public Light emberLight;
        public float emberIntensity = 0.9f;

        public bool Revealed { get; private set; }

        public void Hide()
        {
            Revealed = false;
            if (offeringsRoot != null) offeringsRoot.SetActive(false);
            if (smoke != null) smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (emberLight != null) emberLight.intensity = 0f;
        }

        public IEnumerator Reveal(float seconds)
        {
            if (offeringsRoot == null) yield break;

            Transform t = offeringsRoot.transform;
            Vector3 basePos = t.position;
            Vector3 baseScale = t.localScale;

            offeringsRoot.SetActive(true);
            Revealed = true;
            if (smoke != null) smoke.Play();

            for (float e = 0f; e < seconds; e += Time.deltaTime)
            {
                float k = e / seconds;
                float ease = 1f - Mathf.Pow(1f - k, 3f);
                t.position = basePos + Vector3.up * (0.35f * (1f - ease));
                t.localScale = baseScale * Mathf.Lerp(0.6f, 1f, ease);
                if (emberLight != null) emberLight.intensity = emberIntensity * ease;
                yield return null;
            }

            t.position = basePos;
            t.localScale = baseScale;
            if (emberLight != null) emberLight.intensity = emberIntensity;
        }
    }
}
