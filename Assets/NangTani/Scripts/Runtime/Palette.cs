using UnityEngine;

namespace NangTani
{
    /// <summary>
    /// Palette du CDD (Section 3). Toutes les couleurs de l'experience partent d'ici :
    /// pour changer l'ambiance, on modifie ce fichier.
    /// </summary>
    public static class Palette
    {
        public static readonly Color NightBlue   = Hex("#0B1A2B"); // bleu nuit (ombres)
        public static readonly Color ForestGreen = Hex("#1F3B2D"); // vert foret sombre
        public static readonly Color MoonWhite   = Hex("#C9D6DF"); // blanc lune
        public static readonly Color SpiritGreen = Hex("#5FAE7B"); // robe de Nang Tani (seul vert sature)
        public static readonly Color CandleWarm  = Hex("#FFB04A"); // flammes, bougies
        public static readonly Color Amber       = Hex("#E8A33D"); // ambre du retour au calme

        static Color Hex(string html)
        {
            ColorUtility.TryParseHtmlString(html, out var c);
            return c;
        }
    }
}
