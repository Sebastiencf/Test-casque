using UnityEngine;

namespace NangTani
{
    /// <summary>
    /// Un "etat d'ambiance" complet : brouillard, lumiere de lune, desaturation, sons de fond.
    /// L'ambiance de l'experience est une succession de fondus entre ces etats
    /// (CDD : calme curieux -> malaise -> silence surnaturel -> soulagement).
    /// Pour retoucher le rendu, modifier les valeurs ci-dessous.
    /// </summary>
    [System.Serializable]
    public struct AtmosphereState
    {
        public float fogDensity;      // brouillard exponentiel^2 : 0.03 = leger, 0.07 = epais
        public Color fogColor;
        public Color ambient;         // lumiere ambiante plate
        public Color moonColor;
        public float moonIntensity;
        public float moonPitch;       // hauteur de la lune : plus bas = ombres plus longues
        public float saturation;      // -100..100 (post-process)
        public float vignette;        // 0..1
        public float grain;           // 0..1
        public float windVolume;      // 0..1
        public float cricketVolume;   // 0..1

        public static AtmosphereState Lerp(AtmosphereState a, AtmosphereState b, float t)
        {
            return new AtmosphereState
            {
                fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t),
                fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
                ambient = Color.Lerp(a.ambient, b.ambient, t),
                moonColor = Color.Lerp(a.moonColor, b.moonColor, t),
                moonIntensity = Mathf.Lerp(a.moonIntensity, b.moonIntensity, t),
                moonPitch = Mathf.Lerp(a.moonPitch, b.moonPitch, t),
                saturation = Mathf.Lerp(a.saturation, b.saturation, t),
                vignette = Mathf.Lerp(a.vignette, b.vignette, t),
                grain = Mathf.Lerp(a.grain, b.grain, t),
                windVolume = Mathf.Lerp(a.windVolume, b.windVolume, t),
                cricketVolume = Mathf.Lerp(a.cricketVolume, b.cricketVolume, t)
            };
        }

        /// <summary>Etat 01 : village sous la lune, calme et curieux.</summary>
        public static AtmosphereState Calm => new AtmosphereState
        {
            fogDensity = 0.030f,
            fogColor = Palette.NightBlue,
            ambient = new Color(0.07f, 0.11f, 0.17f),
            moonColor = Palette.MoonWhite,
            moonIntensity = 0.9f,
            moonPitch = 50f,
            saturation = 0f,
            vignette = 0.30f,
            grain = 0.25f,
            windVolume = 0.25f,
            cricketVolume = 0.6f
        };

        /// <summary>Le regard se pose sur l'arbre sacre : la lumiere change a peine.</summary>
        public static AtmosphereState Listening
        {
            get
            {
                var s = Calm;
                s.fogDensity = 0.034f;
                s.moonIntensity = 0.82f;
                s.vignette = 0.34f;
                s.windVolume = 0.2f;
                return s;
            }
        }

        /// <summary>Le vent se leve, quelque chose cloche.</summary>
        public static AtmosphereState Unease => new AtmosphereState
        {
            fogDensity = 0.045f,
            fogColor = new Color(0.043f, 0.12f, 0.15f),
            ambient = new Color(0.05f, 0.09f, 0.12f),
            moonColor = new Color(0.70f, 0.80f, 0.84f),
            moonIntensity = 0.7f,
            moonPitch = 38f,
            saturation = -20f,
            vignette = 0.38f,
            grain = 0.38f,
            windVolume = 0.8f,
            cricketVolume = 0.35f
        };

        /// <summary>Le chemin dans le noir : insectes, feuilles, malaise latent.</summary>
        public static AtmosphereState Path
        {
            get
            {
                var s = Unease;
                s.windVolume = 0.3f;
                s.cricketVolume = 0.55f;
                return s;
            }
        }

        /// <summary>Silence total : le vent est tombe, les insectes se sont tus, tout est froid.</summary>
        public static AtmosphereState Silence => new AtmosphereState
        {
            fogDensity = 0.065f,
            fogColor = new Color(0.06f, 0.11f, 0.13f),
            ambient = new Color(0.04f, 0.07f, 0.09f),
            moonColor = new Color(0.75f, 0.84f, 0.85f),
            moonIntensity = 0.55f,
            moonPitch = 24f,
            saturation = -50f,
            vignette = 0.5f,
            grain = 0.5f,
            windVolume = 0f,
            cricketVolume = 0f
        };

        /// <summary>Etat 03 : ombres tres longues, brume epaisse, couleurs delavees.</summary>
        public static AtmosphereState Supernatural => new AtmosphereState
        {
            fogDensity = 0.075f,
            fogColor = new Color(0.07f, 0.12f, 0.13f),
            ambient = new Color(0.035f, 0.06f, 0.075f),
            moonColor = new Color(0.75f, 0.84f, 0.85f),
            moonIntensity = 0.5f,
            moonPitch = 11f,
            saturation = -60f,
            vignette = 0.55f,
            grain = 0.55f,
            windVolume = 0f,
            cricketVolume = 0f
        };

        /// <summary>Retour a une lune douce et a l'ambre ; le vent revient, tres doucement.</summary>
        public static AtmosphereState Relief => new AtmosphereState
        {
            fogDensity = 0.036f,
            fogColor = new Color(0.07f, 0.10f, 0.14f),
            ambient = new Color(0.09f, 0.11f, 0.15f),
            moonColor = new Color(0.84f, 0.85f, 0.85f),
            moonIntensity = 0.85f,
            moonPitch = 45f,
            saturation = -5f,
            vignette = 0.28f,
            grain = 0.3f,
            windVolume = 0.25f,
            cricketVolume = 0.5f
        };
    }
}
