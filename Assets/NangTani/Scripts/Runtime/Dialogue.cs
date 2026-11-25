namespace NangTani
{
    /// <summary>
    /// Repliques PROVISOIRES (sous-titres en anglais), adaptees de Story.md.
    /// Quand les voix seront enregistrees, ces textes serviront de sous-titres / de reference.
    /// Pour changer une replique : modifier la chaine ici, rien d'autre.
    /// (Si un accent s'affiche comme un carre dans le jeu, c'est que la police TextMeshPro
    /// n'a pas le glyphe : on changera de police, pas le texte.)
    /// </summary>
    public static class Dialogue
    {
        // ---- Etat 01 : village ----
        public const string MingLegend =
            "They say Nang Tani, the spirit of the banana trees, appears at night as a beautiful woman dressed in green.";

        public const string MingWarning =
            "Never harm a sacred banana tree without asking the spirit's permission.";

        public const string RamScoff =
            "A spirit in a banana tree? Come on... watch this.";

        // ---- Etat 02 : bascule ----
        public const string MingFindRam =
            "It's getting dark, and Ram isn't back... Let's go find him.";

        // ---- Etat 02/03 : clairiere ----
        public const string SpiritLine =
            "That leaf was part of me...";

        public const string MingWarning2 =
            "Don't look into her eyes...";

        // ---- Etat 03 : climax ----
        public const string PocketHint =
            "In your pocket: a few flowers and an incense stick, picked up at the foot of the sacred tree.";

        // TEMPORAIRE : indication de la gachette (a remplacer par un indice diegetique)
        public const string TapHint =
            "( Trigger: offer the flowers, the incense and the leaf )";

        // ---- Resolution ----
        public const string PlayerApology =
            "Forgive us. We should never have taken it.";

        public const string RamApology =
            "I'm sorry... I didn't know. I didn't believe it.";

        public const string Narration =
            "The three friends walk back to the village without a word.";

        public const string EndTitle =
            "NANG TANI";

        // ---- Introduction (fond noir) : une page par ligne du tableau ----
        public const string IntroTitle =
            "<size=190%>NANG TANI</size>\n\n<color=#8FB3C8>A story inspired by Thai folklore</color>";

        public static readonly string[] IntroPages =
        {
            "Three friends set out to discover the traditional villages of Thailand.\n\n" +
            "You are one of them. With you: Ram and Ming.",

            "One evening, you reach a small village at the edge of the forest.\n\n" +
            "The villagers are preparing a ritual around an old banana tree wrapped in colored cloth.",

            "They are honoring Nang Tani, a female spirit who is said to live in certain banana trees.",

            "Night is falling. You sit down with your friends, close to the ritual.\n\n" +
            "Listen. Look around you."
        };

        public const string IntroReady = "Are you ready?";
        public const string IntroSkipHint = "Trigger: next";
        public const string IntroContinue = "CONTINUE";
        public const string IntroContinueHint = "Look at the button, or pull the trigger";
    }
}
