namespace NangTani
{
    /// <summary>
    /// "Gaze Dwell" du CDD : fixation soutenue (1,5 - 2 s) sur un accessoire focal,
    /// ici l'arbre sacre. A poser sur l'objet regarde.
    /// </summary>
    public class GazeDwellTarget : ViewTrigger
    {
        void Reset()
        {
            requiredSeconds = 2f;
            maxAngleDegrees = 9f;
        }
    }
}
