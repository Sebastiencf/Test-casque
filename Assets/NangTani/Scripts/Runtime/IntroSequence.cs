using System.Collections;
using TMPro;
using UnityEngine;

namespace NangTani
{
    /// <summary>
    /// Introduction sur fond noir : le titre puis quelques pages qui posent l'histoire, sur une musique
    /// tres discrete. Les pages defilent seules (la gachette passe a la suivante). A la fin, un bouton
    /// "CONTINUE" s'active en le regardant (declencheur implicite du CDD) ou d'un tap de gachette.
    /// Les textes sont dans Dialogue.cs. Ajoute automatiquement par ExperienceDirector.
    /// </summary>
    public class IntroSequence : MonoBehaviour
    {
        [Header("Musique (dans un dossier Resources)")]
        public string musicResource = "Audio/M3_musique_resolution_pardon";
        [Range(0f, 1f)] public float musicVolume = 0.12f;

        [Header("Rythme de lecture")]
        public float titleSeconds = 4f;
        [Tooltip("Duree minimale d'une page, en secondes.")]
        public float minPageSeconds = 6f;
        [Tooltip("Vitesse de lecture laissee au joueur, en caracteres par seconde.")]
        public float charactersPerSecond = 13f;

        [Header("Bouton Continue")]
        public float gazeSeconds = 1.5f;
        public float gazeAngle = 7f;

        const float Distance = 2.2f;

        static readonly Color TextColor = new Color(0.79f, 0.84f, 0.87f);   // blanc lune (CDD)
        static readonly Color HintColor = new Color(0.45f, 0.55f, 0.65f);
        static readonly Color Highlight = new Color(0.91f, 0.64f, 0.24f);   // ambre (CDD)

        Transform _panel;
        TextMeshPro _body, _button, _hint;
        AudioSource _music;
        float _musicTarget;
        float _musicRate;

        public IEnumerator Play(TapInput tap, TMP_FontAsset font)
        {
            Cancel();
            Build(font);
            StartMusic();

            // Deux images pour que le suivi du casque soit en place avant de poser le texte.
            yield return null;
            yield return null;
            Recenter(true);

            yield return Page(tap, Dialogue.IntroTitle, titleSeconds);
            _hint.text = Dialogue.IntroSkipHint;
            foreach (var page in Dialogue.IntroPages)
                yield return Page(tap, page, Mathf.Max(minPageSeconds, page.Length / charactersPerSecond));

            yield return ContinueButton(tap);

            _musicTarget = 0f;                 // la musique s'efface pendant que le village apparait
            _musicRate = musicVolume / 2.5f;
            Destroy(_panel.gameObject);
            _panel = null;
        }

        /// <summary>Interrompt tout (changement d'etape pendant l'introduction).</summary>
        public void Cancel()
        {
            StopAllCoroutines();
            if (_panel != null) Destroy(_panel.gameObject);
            _panel = null;
            if (_music != null) _music.Stop();
            _musicTarget = 0f;
        }

        // ------------------------------------------------------------------

        void Build(TMP_FontAsset font)
        {
            _panel = new GameObject("Introduction").transform;
            _body = Label("Texte", font, 1.0f, TextColor, 0.1f, new Vector2(2.1f, 1.1f));
            _button = Label("Bouton_Continue", font, 1.25f, TextColor, -0.55f, new Vector2(1.4f, 0.3f));
            _hint = Label("Indication", font, 0.55f, HintColor, -0.82f, new Vector2(2.1f, 0.15f));
        }

        TextMeshPro Label(string name, TMP_FontAsset font, float size, Color color, float height, Vector2 box)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_panel, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            var label = go.AddComponent<TextMeshPro>();
            if (font != null) label.font = font;
            label.rectTransform.sizeDelta = box;
            label.fontSize = size;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.color = color;
            label.text = "";
            SubtitleDisplay.DrawOnTop(label);
            return label;
        }

        void StartMusic()
        {
            if (_music == null)
            {
                _music = gameObject.AddComponent<AudioSource>();
                _music.playOnAwake = false;
                _music.loop = true;
                _music.spatialBlend = 0f;
            }
            _music.clip = Resources.Load<AudioClip>(musicResource);
            if (_music.clip == null)
            {
                Debug.LogWarning("[NangTani] Musique d'introduction introuvable : Resources/" + musicResource);
                return;
            }
            _music.volume = 0f;
            _musicTarget = musicVolume;
            _musicRate = musicVolume / 3f;
            _music.Play();
        }

        void Update()
        {
            if (_music == null || !_music.isPlaying) return;
            _music.volume = Mathf.MoveTowards(_music.volume, _musicTarget, _musicRate * Time.unscaledDeltaTime);
            if (_musicTarget <= 0f && _music.volume <= 0f) _music.Stop();
        }

        void LateUpdate()
        {
            if (_panel != null) Recenter(false);
        }

        /// <summary>Le texte reste fixe dans l'espace ; il ne revient devant le joueur que s'il s'en detourne.</summary>
        void Recenter(bool instantly)
        {
            var cam = ViewUtil.Cam;
            if (cam == null) return;

            Vector3 forward = cam.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) return;
            forward.Normalize();

            Vector3 current = _panel.position - cam.transform.position;
            float heightGap = Mathf.Abs(current.y);
            current.y = 0f;
            // 0,2 m : le suivi du casque peut changer la hauteur de la tete juste apres le lancement.
            if (!instantly && Vector3.Angle(forward, current) < 55f && heightGap < 0.2f) return;

            Vector3 target = cam.transform.position + forward * Distance;
            _panel.position = instantly ? target : Vector3.Lerp(_panel.position, target, 2f * Time.deltaTime);
            Vector3 away = _panel.position - cam.transform.position;
            away.y = 0f;
            _panel.rotation = Quaternion.LookRotation(away, Vector3.up);
        }

        IEnumerator Page(TapInput tap, string text, float seconds)
        {
            _body.text = text;
            yield return Fade(_body, 0f, 1f, 0.8f);

            int presses = tap != null ? tap.PressCount : 0;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                if (tap != null && tap.PressCount != presses) break;
                yield return null;
            }
            yield return Fade(_body, 1f, 0f, 0.5f);
        }

        IEnumerator ContinueButton(TapInput tap)
        {
            // Le bouton est place nettement sous le regard : il ne s'active pas tout seul.
            _body.text = Dialogue.IntroReady;
            _body.alpha = 1f;
            _button.text = Dialogue.IntroContinue;
            _hint.text = Dialogue.IntroContinueHint;
            yield return Fade(_button, 0f, 1f, 0.8f);   // un appui trop rapide ne saute pas le bouton

            int presses = tap != null ? tap.PressCount : 0;
            float gaze = 0f;
            while (gaze < 1f)
            {
                if (tap != null && tap.PressCount != presses) break;

                var cam = ViewUtil.Cam;
                bool looking = cam != null && Vector3.Angle(cam.transform.forward,
                    _button.transform.position - cam.transform.position) <= gazeAngle;
                gaze = Mathf.Clamp01(gaze + (looking ? 1f : -2f) * Time.deltaTime / gazeSeconds);

                // Le bouton s'allume et grossit tant qu'on le regarde.
                _button.color = Color.Lerp(TextColor, Highlight, gaze);
                _button.transform.localScale = Vector3.one * (1f + 0.25f * gaze);
                yield return null;
            }

            _button.color = Highlight;
            _hint.text = "";
            _body.text = "";
            yield return Fade(_button, 1f, 0f, 0.6f);
        }

        static IEnumerator Fade(TextMeshPro label, float from, float to, float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                label.alpha = Mathf.Lerp(from, to, t / seconds);
                yield return null;
            }
            label.alpha = to;
        }
    }
}
