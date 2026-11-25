using UnityEngine;

namespace NangTani
{
    /// <summary>Acces a la camera du joueur, sans dependre du tag MainCamera.</summary>
    public static class ViewUtil
    {
        public static Camera Cam
        {
            get
            {
                var c = SeatedRig.ViewCamera;
                return c != null ? c : Camera.main;
            }
        }
    }
}
