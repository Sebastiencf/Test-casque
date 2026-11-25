namespace NangTani
{
    /// <summary>
    /// Repliques PROVISOIRES (sous-titres du greybox), tirees de Story.md.
    /// Quand les voix seront enregistrees, ces textes serviront de sous-titres / de reference.
    /// Pour changer une replique : modifier la chaine ici, rien d'autre.
    /// (Si un accent s'affiche comme un carre dans le jeu, c'est que la police TextMeshPro
    /// n'a pas le glyphe : on changera de police, pas le texte.)
    /// </summary>
    public static class Dialogue
    {
        // ---- Etat 01 : village ----
        public const string MingLegend =
            "On raconte que Nang Tani, l'esprit des bananiers, apparaît surtout la nuit, " +
            "sous les traits d'une belle femme tout en vert.";

        public const string MingWarning =
            "Il ne faut jamais abîmer un bananier sacré sans demander la permission à l'esprit.";

        // ---- Etat 02 : bascule ----
        public const string MingFindRam =
            "La nuit tombe, et Ram n'est pas revenu... Allons le chercher.";

        // ---- Etat 02/03 : clairiere ----
        public const string SpiritLine =
            "Cette feuille faisait partie de moi...";

        public const string MingWarning2 =
            "Ne la regarde pas dans les yeux...";

        // ---- Etat 03 : climax ----
        public const string PocketHint =
            "Dans ta poche : quelques fleurs et un bâtonnet d'encens, pris au pied de l'arbre sacré.";

        // TEMPORAIRE : indication de la gachette (a remplacer par un indice diegetique)
        public const string TapHint =
            "( Gâchette : offrir les fleurs, l'encens et la feuille )";

        // ---- Resolution ----
        public const string PlayerApology =
            "Pardonnez-nous. Nous n'aurions pas dû la prendre.";

        public const string RamApology =
            "Pardon... Je ne savais pas. Je n'y croyais pas.";

        public const string Narration =
            "Les trois amis regagnent le village sans dire un mot.";

        public const string EndTitle =
            "NANG TANI - version greybox";
    }
}
