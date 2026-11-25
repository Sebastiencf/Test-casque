using UnityEngine;

namespace NangTani
{
    /// <summary>
    /// Sons PROVISOIRES generes par code (vent, insectes, murmure), pour pouvoir tester le rythme de
    /// l'experience avant d'avoir les vrais enregistrements. Des qu'un vrai AudioClip est assigne a une
    /// AudioSource, ces sons ne sont plus utilises.
    /// </summary>
    public static class AudioPlaceholders
    {
        const int Rate = 44100;

        /// <summary>Vent : bruit filtre, avec des rafales lentes. Boucle.</summary>
        public static AudioClip Wind(float seconds = 12f)
        {
            int n = Mathf.RoundToInt(seconds * Rate);
            var data = new float[n];
            var rng = new System.Random(11);
            float lp1 = 0f, lp2 = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)Rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float cutoff = 0.012f + 0.014f * (Mathf.Sin(t * 0.6f) * 0.5f + 0.5f);
                lp1 += (noise - lp1) * cutoff;
                lp2 += (lp1 - lp2) * 0.12f;
                float gust = 0.55f + 0.45f * Mathf.Sin(t * 0.45f + 1f);
                data[i] = Mathf.Clamp(lp2 * 11f * gust, -1f, 1f);
            }

            ApplyLoopEnvelope(data, 1.2f);
            return MakeClip("NT_Placeholder_Wind", data);
        }

        /// <summary>Grillons : trois "voix" de trilles aigus. Boucle.</summary>
        public static AudioClip Crickets(float seconds = 6f)
        {
            int n = Mathf.RoundToInt(seconds * Rate);
            var data = new float[n];

            for (int voice = 0; voice < 3; voice++)
            {
                float freq = 4100f + voice * 330f;
                float period = 0.85f + voice * 0.23f;
                float offset = voice * 0.31f;

                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)Rate;
                    float phase = Mathf.Repeat(t + offset, period);
                    float burst = phase < 0.28f ? Mathf.Sin(phase / 0.28f * Mathf.PI) : 0f;
                    float pulse = Mathf.Max(0f, Mathf.Sin(t * 2f * Mathf.PI * 26f));
                    data[i] += Mathf.Sin(2f * Mathf.PI * freq * t) * burst * pulse * 0.07f;
                }
            }

            ApplyLoopEnvelope(data, 0.4f);
            return MakeClip("NT_Placeholder_Crickets", data);
        }

        /// <summary>Murmure chuchote, sans paroles : bruit passe dans deux formants, rythme de syllabes.</summary>
        public static AudioClip Murmur(float seconds = 4f)
        {
            int n = Mathf.RoundToInt(seconds * Rate);
            var data = new float[n];
            var rng = new System.Random(23);

            var f1 = new BandPass(650f);
            var f2 = new BandPass(1700f);

            // Enveloppe : une suite de "syllabes" douces
            var env = new float[n];
            float t0 = 0.15f;
            while (t0 < seconds - 0.4f)
            {
                float len = 0.14f + (float)rng.NextDouble() * 0.16f;
                int start = Mathf.RoundToInt(t0 * Rate);
                int count = Mathf.RoundToInt(len * Rate);
                for (int k = 0; k < count && start + k < n; k++)
                    env[start + k] = Mathf.Max(env[start + k], Mathf.Sin(k / (float)count * Mathf.PI));
                t0 += len + 0.04f + (float)rng.NextDouble() * 0.12f;
            }

            for (int i = 0; i < n; i++)
            {
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float v = f1.Process(noise) * 1.0f + f2.Process(noise) * 0.6f;
                data[i] = Mathf.Clamp(v * env[i] * 0.9f, -1f, 1f);
            }

            return MakeClip("NT_Placeholder_Murmur", data);
        }

        // ------------------------------------------------------------------

        static AudioClip MakeClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static void ApplyLoopEnvelope(float[] data, float fadeSeconds)
        {
            int fade = Mathf.Min(data.Length / 2, Mathf.RoundToInt(fadeSeconds * Rate));
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] *= k;
                data[data.Length - 1 - i] *= k;
            }
        }

        // Filtre passe-bande (variable d'etat)
        sealed class BandPass
        {
            readonly float _f;
            const float Q = 0.18f;
            float _low, _band;

            public BandPass(float centerHz)
            {
                _f = 2f * Mathf.Sin(Mathf.PI * centerHz / Rate);
            }

            public float Process(float input)
            {
                _low += _f * _band;
                float high = input - _low - Q * _band;
                _band += _f * high;
                return _band;
            }
        }
    }
}
