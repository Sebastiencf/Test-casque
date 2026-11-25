namespace NangTani
{
    /// <summary>
    /// "Orientation / Head Rotation" du CDD : le joueur tourne naturellement la tete vers un son
    /// hors-axe (la voix de Nang Tani venant de la foret). A poser sur la source sonore.
    /// Le cone est large (le son est spatialise, on ne cherche pas la precision).
    /// </summary>
    public class HeadTurnTrigger : ViewTrigger
    {
        void Reset()
        {
            requiredSeconds = 0.35f;
            maxAngleDegrees = 28f;
        }
    }
}
