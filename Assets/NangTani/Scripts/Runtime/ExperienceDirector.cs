using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NangTani
{
    /// <summary>
    /// Chef d'orchestre de l'experience : enchaine les etapes du CDD (Section 5) et de Story.md.
    ///
    ///   0 Village    - Etat 01 : calme, regard sur l'arbre sacre, legende de Ming, Ram prend la feuille
    ///   1 Shift      - Etat 02 : le vent se leve, la voix, tourner la tete, bougies eteintes, silence, silhouette verte
    ///   2 Clearing   - (noir : le chemin) clairiere du temple, Nang Tani approche de Ram fige
    ///   3 Climax     - Etat 03 : ombres etirees, un seul tap de gachette
    ///   4 Resolution - offrandes, excuses, Nang Tani se dissout, retour de la lune douce
    ///   5 Epilogue   - retour au village, Ram s'incline, tissu vert sur l'arbre
    ///
    /// Les durees (WaitForSeconds) sont des valeurs de depart : on les ajustera au test pour viser 3 a 5 minutes.
    /// Chaque declencheur implicite a un delai de securite : si le joueur ne fait rien, l'experience avance quand meme.
    /// </summary>
    public class ExperienceDirector : MonoBehaviour
    {
        public enum Stage { Village = 0, Shift = 1, Clearing = 2, Climax = 3, Resolution = 4, Epilogue = 5 }

        [Header("Systemes")]
        public SeatedRig rig;
        public ScreenFader fader;
        public SubtitleDisplay subtitles;
        public AtmosphereController atmosphere;
        public TapInput tapInput;

        [Header("Ancres du joueur (poser AU SOL)")]
        public Transform anchorVillage;
        public Transform anchorClearing;

        [Header("Declencheurs implicites")]
        public GazeDwellTarget sacredTreeGaze;
        public HeadTurnTrigger voiceTurn;

        [Header("Personnages et accessoires")]
        public CharacterActor ram;
        public CharacterActor ming;
        public SpiritController spirit;
        public OfferingSpawner offerings;
        public GameObject ramLeafInHand;
        public GameObject leafOnTree;
        public GameObject leafOnGround;
        public GameObject leafAtSacredTree;
        public GameObject greenClothOnTree;

        [Header("Reperes de la scene")]
        public Transform ramVillageSeat;
        public Transform mingVillageSeat;
        public Transform ramLeafPickup;
        public Transform ramVillageExit;
        public Transform voiceVillagePoint;
        public Transform ramClearingSeat;
        public Transform mingClearingSeat;
        public Transform spiritSpawn;
        public Transform spiritStop;
        public Transform ramEpilogueSpot;
        public Transform mingEpilogueSpot;

        [Header("Voix de Nang Tani (spatialisee)")]
        public AudioSource voiceSource;

        [Header("Test / debug (editeur)")]
        public Stage startStage = Stage.Village;
        public bool debugKeys = true;
        public bool showEditorHelp = true;
        [Range(1f, 8f)] public float editorSpeed = 1f;
        public bool restartAtEnd = false;

        public Stage CurrentStage { get; private set; }

        bool _voiceLoop;

        // ------------------------------------------------------------------
        //  Cycle de vie
        // ------------------------------------------------------------------

        void Start()
        {
            if (Application.isEditor) Time.timeScale = editorSpeed;

            if (voiceSource != null)
            {
                voiceSource.spatialBlend = 1f;
                voiceSource.loop = false;
                voiceSource.playOnAwake = false;
                if (voiceSource.clip == null) voiceSource.clip = AudioPlaceholders.Murmur();
            }

            StartCoroutine(Run(startStage));
        }

        void OnDestroy()
        {
            if (Application.isEditor) Time.timeScale = 1f;
        }

        void Update()
        {
            if (!debugKeys) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.digit1Key.wasPressedThisFrame) Jump(Stage.Village);
            else if (kb.digit2Key.wasPressedThisFrame) Jump(Stage.Shift);
            else if (kb.digit3Key.wasPressedThisFrame) Jump(Stage.Clearing);
            else if (kb.digit4Key.wasPressedThisFrame) Jump(Stage.Climax);
            else if (kb.digit5Key.wasPressedThisFrame) Jump(Stage.Resolution);
            else if (kb.digit6Key.wasPressedThisFrame) Jump(Stage.Epilogue);
            else if (kb.rKey.wasPressedThisFrame) Jump(Stage.Village);
        }

        void OnGUI()
        {
            if (!Application.isEditor || !showEditorHelp) return;
            GUI.Label(new Rect(10f, 10f, 900f, 24f),
                "NANG TANI  |  etape : " + CurrentStage +
                "  |  touches 1-6 : aller a l'etape  |  R : recommencer  |  clic droit + souris : regarder  |  Espace : gachette");
        }

        /// <summary>Saute a une etape (remet le monde dans l'etat de debut de cette etape).</summary>
        public void Jump(Stage stage)
        {
            StopAllCoroutines();
            if (atmosphere != null) atmosphere.CancelAll();
            if (subtitles != null) subtitles.Hide();
            _voiceLoop = false;
            if (voiceSource != null) voiceSource.Stop();
            StartCoroutine(Run(stage));
        }

        IEnumerator Run(Stage first)
        {
            PrepareStage(first);
            for (int s = (int)first; s <= (int)Stage.Epilogue; s++)
            {
                CurrentStage = (Stage)s;
                yield return StartCoroutine(RunStage(CurrentStage));
            }
            yield return StartCoroutine(Ending());
        }

        IEnumerator RunStage(Stage stage)
        {
            switch (stage)
            {
                case Stage.Village: return VillageRoutine();
                case Stage.Shift: return ShiftRoutine();
                case Stage.Clearing: return ClearingRoutine();
                case Stage.Climax: return ClimaxRoutine();
                case Stage.Resolution: return ResolutionRoutine();
                default: return EpilogueRoutine();
            }
        }

        // ------------------------------------------------------------------
        //  Mise en place instantanee du monde au debut d'une etape
        // ------------------------------------------------------------------

        void PrepareStage(Stage stage)
        {
            CurrentStage = stage;

            spirit.Hide();
            offerings.Hide();
            ram.Trembling = false;
            ming.Trembling = false;
            ram.gameObject.SetActive(true);
            ming.gameObject.SetActive(true);
            ramLeafInHand.SetActive(false);
            leafOnTree.SetActive(true);
            leafOnGround.SetActive(false);
            leafAtSacredTree.SetActive(false);
            greenClothOnTree.SetActive(false);

            bool startsBlack = stage == Stage.Village || stage == Stage.Clearing || stage == Stage.Epilogue;
            fader.SetAlpha(startsBlack ? 1f : 0f);

            switch (stage)
            {
                case Stage.Village:
                    atmosphere.SetInstant(AtmosphereState.Calm);
                    atmosphere.SetCandlesInstant(1f);
                    rig.MoveTo(anchorVillage);
                    Seat(ram, ramVillageSeat);
                    Seat(ming, mingVillageSeat);
                    break;

                case Stage.Shift:
                    atmosphere.SetInstant(AtmosphereState.Listening);
                    atmosphere.SetCandlesInstant(1f);
                    rig.MoveTo(anchorVillage);
                    leafOnTree.SetActive(false);
                    Seat(ming, mingVillageSeat);
                    ram.gameObject.SetActive(false);
                    break;

                case Stage.Clearing:
                    atmosphere.SetInstant(AtmosphereState.Silence);
                    atmosphere.SetCandlesInstant(0f);
                    rig.MoveTo(anchorVillage);
                    leafOnTree.SetActive(false);
                    Seat(ming, mingVillageSeat);
                    ram.gameObject.SetActive(false);
                    break;

                case Stage.Climax:
                    atmosphere.SetInstant(AtmosphereState.Silence);
                    atmosphere.SetCandlesInstant(0f);
                    leafOnTree.SetActive(false);
                    rig.MoveTo(anchorClearing);
                    SetupClearingCast();
                    spirit.PlaceAt(spiritStop.position, ramClearingSeat.position);
                    spirit.ShowInstant();
                    break;

                case Stage.Resolution:
                    atmosphere.SetInstant(AtmosphereState.Supernatural);
                    atmosphere.SetCandlesInstant(0f);
                    leafOnTree.SetActive(false);
                    rig.MoveTo(anchorClearing);
                    SetupClearingCast();
                    ming.Trembling = true;
                    spirit.PlaceAt(spiritStop.position, ramClearingSeat.position);
                    spirit.ShowInstant();
                    break;

                case Stage.Epilogue:
                    atmosphere.SetInstant(AtmosphereState.Relief);
                    atmosphere.SetCandlesInstant(0f);
                    leafOnTree.SetActive(false);
                    break;
            }
        }

        static void Seat(CharacterActor actor, Transform spot)
        {
            actor.gameObject.SetActive(true);
            actor.Place(spot);
            actor.SitDown();
        }

        void SetupClearingCast()
        {
            Seat(ming, mingClearingSeat);
            ming.FaceTowards(spiritSpawn.position);
            Seat(ram, ramClearingSeat);
            ram.FaceTowards(spiritSpawn.position);
            leafOnGround.SetActive(true);
        }

        IEnumerator Line(string speaker, string text, float seconds)
        {
            string shown = string.IsNullOrEmpty(speaker)
                ? text
                : "<size=65%><color=#8FB3C8>" + speaker + "</color></size>\n" + text;
            subtitles.Show(shown, seconds);
            yield return new WaitForSeconds(seconds + 0.9f);
        }

        // ------------------------------------------------------------------
        //  Etape 0 : Village (Etat 01 : calme curieux)
        // ------------------------------------------------------------------

        IEnumerator VillageRoutine()
        {
            yield return StartCoroutine(fader.FadeTo(0f, 3.5f));

            // Le joueur regarde autour de lui : villageois, rituel, lune.
            yield return new WaitForSeconds(6f);

            // Regard soutenu sur l'arbre sacre (CDD : 2 s). Sans action du joueur : on continue apres 30 s.
            yield return StartCoroutine(sacredTreeGaze.Wait(30f));
            atmosphere.BlendTo(AtmosphereState.Listening, 3f);

            // La legende de Ming et son avertissement : les regles du monde.
            yield return StartCoroutine(Line("Ming", Dialogue.MingLegend, 8f));
            yield return StartCoroutine(Line("Ming", Dialogue.MingWarning, 7f));

            // Ram ne croit pas a ces histoires : il se leve, va au bananier pres du temple, arrache une feuille.
            yield return new WaitForSeconds(1.5f);
            ram.StandUp();
            yield return StartCoroutine(ram.MoveTo(ramLeafPickup.position, 1.2f));
            yield return new WaitForSeconds(1.2f);
            leafOnTree.SetActive(false);
            ramLeafInHand.SetActive(true);
            yield return new WaitForSeconds(1f);
        }

        // ------------------------------------------------------------------
        //  Etape 1 : Shift (Etat 02 : malaise, voix, silence, silhouette verte)
        // ------------------------------------------------------------------

        IEnumerator ShiftRoutine()
        {
            // "A ce moment precis, le vent se leve etrangement."
            atmosphere.BlendTo(AtmosphereState.Unease, 7f);
            StartCoroutine(RamLeaves());
            yield return new WaitForSeconds(2f);

            // Une voix feminine, tres faible, vient de la foret (hors axe) : le joueur tourne la tete.
            StartCoroutine(VoiceLoop());
            yield return StartCoroutine(voiceTurn.Wait(40f));

            // Declenchement : bougies eteintes, le vent monte puis tombe net, silence, froid.
            atmosphere.SnuffCandles(1.6f);
            yield return atmosphere.WindGust(1.4f);
            _voiceLoop = false;
            voiceSource.Stop();
            atmosphere.CutSilence();
            atmosphere.BlendTo(AtmosphereState.Silence, 6f);
            yield return new WaitForSeconds(1.5f);

            // Silhouette verte entre les arbres, un court instant.
            spirit.PlaceAt(voiceVillagePoint.position, anchorVillage.position);
            yield return StartCoroutine(spirit.Appear(3.5f));
            yield return new WaitForSeconds(3.5f);
            yield return StartCoroutine(spirit.Dissolve(3f));
            yield return new WaitForSeconds(1f);

            yield return StartCoroutine(Line("Ming", Dialogue.MingFindRam, 5f));
            yield return StartCoroutine(fader.FadeTo(1f, 2.5f));
        }

        IEnumerator RamLeaves()
        {
            if (!ram.gameObject.activeSelf) yield break;
            yield return StartCoroutine(ram.MoveTo(ramVillageExit.position, 1.4f));
            ram.gameObject.SetActive(false);
        }

        IEnumerator VoiceLoop()
        {
            _voiceLoop = true;
            voiceSource.transform.position = voiceVillagePoint.position;
            float startTime = Time.time;

            while (_voiceLoop)
            {
                // La voix se rapproche (plus forte) si le joueur tarde a se retourner.
                voiceSource.volume = Mathf.Lerp(0.25f, 1f, (Time.time - startTime) / 30f);
                if (!voiceSource.isPlaying) voiceSource.Play();
                yield return null;
            }
        }

        // ------------------------------------------------------------------
        //  Etape 2 : Clairiere du temple
        // ------------------------------------------------------------------

        IEnumerator ClearingRoutine()
        {
            // Ecran noir : on marche dans la nuit (insectes, bruissement de feuilles).
            atmosphere.SetInstant(AtmosphereState.Path);
            yield return new WaitForSeconds(0.6f);

            rig.MoveTo(anchorClearing);
            SetupClearingCast();
            spirit.Hide();
            ramLeafInHand.SetActive(false);
            yield return new WaitForSeconds(5f);
            yield return StartCoroutine(fader.FadeTo(0f, 3f));

            // On decouvre Ram assis sur la feuille, pres du temple abandonne.
            yield return new WaitForSeconds(6f);

            // Silence : une femme apparait entre les arbres.
            atmosphere.CutSilence();
            atmosphere.BlendTo(AtmosphereState.Silence, 4f);
            spirit.PlaceAt(spiritSpawn.position, ramClearingSeat.position);
            yield return StartCoroutine(spirit.Appear(4f));

            // Elle s'approche lentement de Ram, qui tente de se lever mais reste fige.
            ram.Trembling = true;
            yield return StartCoroutine(spirit.WalkTo(spiritStop.position, 0.7f));
            ram.Trembling = false;
            yield return new WaitForSeconds(1.2f);
            spirit.FacePoint(ramClearingSeat.position);

            yield return StartCoroutine(Line("Nang Tani", Dialogue.SpiritLine, 5f));
        }

        // ------------------------------------------------------------------
        //  Etape 3 : Climax (Etat 03 : ombres etirees, un seul tap)
        // ------------------------------------------------------------------

        IEnumerator ClimaxRoutine()
        {
            atmosphere.BlendTo(AtmosphereState.Supernatural, 9f);
            ming.Trembling = true;
            yield return new WaitForSeconds(2f);
            yield return StartCoroutine(Line("Ming", Dialogue.MingWarning2, 4f));

            // Le joueur s'attarde sur la scene (attenuation passive).
            yield return new WaitForSeconds(8f);
            yield return StartCoroutine(Line("", Dialogue.PocketHint, 5f));

            // L'unique action explicite : un tap de gachette. Sans tap, l'offrande se fait au bout de 50 s.
            subtitles.Show(Dialogue.TapHint, 55f);
            yield return StartCoroutine(tapInput.Wait(50f));
            subtitles.Hide();
        }

        // ------------------------------------------------------------------
        //  Etape 4 : Resolution
        // ------------------------------------------------------------------

        IEnumerator ResolutionRoutine()
        {
            leafOnGround.SetActive(false);
            yield return StartCoroutine(offerings.Reveal(1.3f));
            yield return new WaitForSeconds(1.5f);

            yield return StartCoroutine(Line("", Dialogue.PlayerApology, 4f));
            yield return new WaitForSeconds(3f); // long silence
            yield return StartCoroutine(Line("Ram", Dialogue.RamApology, 4.5f));
            yield return new WaitForSeconds(3f); // son regard se radoucit

            ming.Trembling = false;
            atmosphere.BlendTo(AtmosphereState.Relief, 10f); // les ombres reprennent leur place
            yield return new WaitForSeconds(4f);
            yield return StartCoroutine(spirit.Dissolve(6f));
            yield return new WaitForSeconds(3f);

            yield return StartCoroutine(Line("", Dialogue.Narration, 4f));
            yield return StartCoroutine(fader.FadeTo(1f, 3f));
        }

        // ------------------------------------------------------------------
        //  Etape 5 : Epilogue (retour au village)
        // ------------------------------------------------------------------

        IEnumerator EpilogueRoutine()
        {
            atmosphere.SetInstant(AtmosphereState.Relief);
            atmosphere.SetCandlesInstant(0f);
            spirit.Hide();
            offerings.Hide();
            leafOnGround.SetActive(false);
            ramLeafInHand.SetActive(false);
            leafOnTree.SetActive(false);

            rig.MoveTo(anchorVillage);
            ming.gameObject.SetActive(true);
            ram.gameObject.SetActive(true);
            ming.Trembling = false;
            ming.Place(mingEpilogueSpot);
            ming.StandUp();
            ram.Place(ramEpilogueSpot);
            ram.StandUp();

            // Ming remet la feuille au pied de l'arbre sacre.
            leafAtSacredTree.SetActive(true);
            yield return new WaitForSeconds(1f);
            yield return StartCoroutine(fader.FadeTo(0f, 3f));

            atmosphere.RelightCandles(5f); // l'ambre revient
            yield return new WaitForSeconds(4f);

            // Ram ne plaisante plus : il s'incline, puis attache un tissu vert au tronc.
            yield return StartCoroutine(ram.Bow(5f));
            greenClothOnTree.SetActive(true);
            yield return new WaitForSeconds(6f);
        }

        IEnumerator Ending()
        {
            yield return StartCoroutine(fader.FadeTo(1f, 3f));
            subtitles.Show(Dialogue.EndTitle, 8f);
            yield return new WaitForSeconds(11f);
            if (restartAtEnd) Jump(Stage.Village);
        }
    }
}
