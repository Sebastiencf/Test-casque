using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace NangTani
{
    /// <summary>
    /// Pilote l'ambiance : brouillard, lune (couleur, intensite, hauteur = longueur des ombres),
    /// desaturation / vignette / grain (post-process URP), volume du vent et des insectes, bougies.
    /// Les fondus entre etats (AtmosphereState) se font par BlendTo().
    /// </summary>
    public class AtmosphereController : MonoBehaviour
    {
        public Light moon;
        public float moonYaw = -30f;

        [Tooltip("Multiplie la couleur de la lune de chaque etat (blanc = inchange).")]
        public Color moonTint = Color.white;
        [Tooltip("Multiplie la lumiere ambiante de chaque etat (blanc = inchange).")]
        public Color ambientTint = Color.white;
        [Tooltip("Multiplie l'intensite de la lune de chaque etat.")]
        public float moonBoost = 1f;
        public AudioSource wind;
        public AudioSource crickets;

        [Tooltip("Desactiver sur Quest 2 si le post-process coute trop cher.")]
        public bool usePostProcessing = true;

        AtmosphereState _current;
        ColorAdjustments _color;
        Vignette _vignette;
        FilmGrain _grain;
        Coroutine _blend;

        public AtmosphereState Current => _current;

        void Awake()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.ambientMode = AmbientMode.Flat;

            if (usePostProcessing) BuildVolume();
            SetupLoop(wind, () => AudioPlaceholders.Wind());
            SetupLoop(crickets, () => AudioPlaceholders.Crickets());

            SetInstant(AtmosphereState.Calm);
        }

        static void SetupLoop(AudioSource source, System.Func<AudioClip> makePlaceholder)
        {
            if (source == null) return;
            if (source.clip == null) source.clip = makePlaceholder();
            source.loop = true;
            source.playOnAwake = false;
            if (!source.isPlaying) source.Play();
        }

        void BuildVolume()
        {
            var volume = new GameObject("NT Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _color = profile.Add<ColorAdjustments>(true);
            _vignette = profile.Add<Vignette>(true);
            _grain = profile.Add<FilmGrain>(true);
            _grain.type.Override(FilmGrainLookup.Thin2);
            volume.profile = profile;

            var cam = ViewUtil.Cam;
            if (cam != null)
            {
                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data != null) data.renderPostProcessing = true;
            }
        }

        // ---- Etat -----------------------------------------------------------

        public void SetInstant(AtmosphereState state)
        {
            StopBlend();
            _current = state;
            Apply(_current);
        }

        /// <summary>Fondu vers un etat. Annule le fondu precedent. Peut etre attendu avec yield return.</summary>
        public Coroutine BlendTo(AtmosphereState target, float seconds)
        {
            StopBlend();
            _blend = StartCoroutine(BlendRoutine(target, seconds));
            return _blend;
        }

        IEnumerator BlendRoutine(AtmosphereState target, float seconds)
        {
            AtmosphereState from = _current;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / seconds);
                _current = AtmosphereState.Lerp(from, target, k);
                Apply(_current);
                yield return null;
            }
            _current = target;
            Apply(_current);
            _blend = null;
        }

        void StopBlend()
        {
            if (_blend != null) StopCoroutine(_blend);
            _blend = null;
        }

        /// <summary>Annule tout ce que ce composant est en train de faire (utilise quand on saute d'etape).</summary>
        public void CancelAll()
        {
            StopAllCoroutines();
            _blend = null;
        }

        void Apply(AtmosphereState s)
        {
            RenderSettings.fogDensity = s.fogDensity;
            RenderSettings.fogColor = s.fogColor;
            RenderSettings.ambientLight = s.ambient * ambientTint;

            if (moon != null)
            {
                moon.color = s.moonColor * moonTint;
                moon.intensity = s.moonIntensity * moonBoost;
                moon.transform.rotation = Quaternion.Euler(s.moonPitch, moonYaw, 0f);
            }

            if (_color != null) _color.saturation.Override(s.saturation);
            if (_vignette != null) _vignette.intensity.Override(s.vignette);
            if (_grain != null) _grain.intensity.Override(s.grain);

            if (wind != null) wind.volume = s.windVolume;
            if (crickets != null) crickets.volume = s.cricketVolume;

            var cam = ViewUtil.Cam;
            if (cam != null) cam.backgroundColor = s.fogColor;
        }

        // ---- Vent / silence -------------------------------------------------

        /// <summary>Le vent monte en rafale (avant de "couper" net).</summary>
        public Coroutine WindGust(float seconds)
        {
            StopBlend();
            _blend = StartCoroutine(GustRoutine(seconds));
            return _blend;
        }

        IEnumerator GustRoutine(float seconds)
        {
            float from = _current.windVolume;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                _current.windVolume = Mathf.Lerp(from, 1f, Mathf.SmoothStep(0f, 1f, t / seconds));
                Apply(_current);
                yield return null;
            }
            _current.windVolume = 1f;
            Apply(_current);
            _blend = null;
        }

        /// <summary>Le vent et les insectes s'arretent net.</summary>
        public void CutSilence()
        {
            StopBlend();
            _current.windVolume = 0f;
            _current.cricketVolume = 0f;
            Apply(_current);
        }

        // ---- Bougies --------------------------------------------------------

        public void SetCandlesInstant(float level)
        {
            foreach (var c in new List<CandleFlame>(CandleFlame.Candles)) c.SetLevel(level);
        }

        /// <summary>Eteint les bougies, une par une, sur <paramref name="spreadSeconds"/> secondes.</summary>
        public Coroutine SnuffCandles(float spreadSeconds)
        {
            return StartCoroutine(CandlesRoutine(0f, spreadSeconds, 0.35f));
        }

        /// <summary>Rallume les bougies progressivement.</summary>
        public Coroutine RelightCandles(float spreadSeconds)
        {
            return StartCoroutine(CandlesRoutine(1f, spreadSeconds, 1.5f));
        }

        IEnumerator CandlesRoutine(float target, float spreadSeconds, float flameFade)
        {
            foreach (var c in new List<CandleFlame>(CandleFlame.Candles))
                StartCoroutine(DelayedFade(c, Random.value * spreadSeconds, target, flameFade));
            yield return new WaitForSeconds(spreadSeconds + flameFade);
        }

        IEnumerator DelayedFade(CandleFlame candle, float delay, float target, float seconds)
        {
            yield return new WaitForSeconds(delay);
            if (candle != null) yield return StartCoroutine(candle.FadeTo(target, seconds));
        }
    }
}
