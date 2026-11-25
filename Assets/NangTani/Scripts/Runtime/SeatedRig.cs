using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;

namespace NangTani
{
    /// <summary>
    /// Joueur assis sur une ancre fixe (CDD, Section 4) :
    ///  - hauteur des yeux imposee (1,1 - 1,2 m), identique pour tout le monde (mode "Device") ;
    ///  - deplacement / teleportation du template desactives ;
    ///  - MoveTo(ancre) pour passer d'un lieu a l'autre (toujours pendant un fondu au noir) ;
    ///  - hors casque (editeur) : clic droit + souris pour regarder autour de soi.
    /// A poser sur la racine du XR Origin.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class SeatedRig : MonoBehaviour
    {
        public static Camera ViewCamera { get; private set; }

        public XROrigin origin;

        [Tooltip("Hauteur des yeux au-dessus du sol (CDD : 1,1 a 1,2 m).")]
        [Min(0.5f)] public float eyeHeight = 1.15f;

        public bool disableLocomotion = true;

        [Header("Test sans casque (editeur)")]
        public bool desktopFallback = true;
        public float mouseSensitivity = 0.12f;

        float _yaw;
        float _pitch;
        Transform _pitchPivot;

        bool IsDesktop => desktopFallback && !XRSettings.isDeviceActive;

        void Awake()
        {
            if (origin == null) origin = GetComponent<XROrigin>();
            if (origin == null) origin = FindFirstObjectByType<XROrigin>();
            if (origin == null)
            {
                Debug.LogError("[NangTani] SeatedRig : aucun XROrigin trouve.", this);
                enabled = false;
                return;
            }

            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
            origin.CameraYOffset = eyeHeight;

            ViewCamera = origin.Camera;
            _pitchPivot = origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : null;
            _yaw = origin.transform.eulerAngles.y;

            if (disableLocomotion) DisableLocomotion();
        }

        void OnDestroy()
        {
            if (ViewCamera == (origin != null ? origin.Camera : null)) ViewCamera = null;
        }

        void DisableLocomotion()
        {
            foreach (var provider in origin.GetComponentsInChildren<LocomotionProvider>(true))
                provider.enabled = false;

            foreach (var t in origin.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name.IndexOf("Teleport", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    t.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Place le joueur sur l'ancre : la tete (horizontalement) a la position de l'ancre,
        /// regard initial oriente comme l'ancre, yeux a <see cref="eyeHeight"/> au-dessus du sol.
        /// L'ancre se pose AU SOL (y = hauteur du sol).
        /// </summary>
        public void MoveTo(Transform anchor)
        {
            if (origin == null || anchor == null) return;

            if (_pitchPivot != null && IsDesktop)
            {
                _pitchPivot.localRotation = Quaternion.identity;
                _pitch = 0f;
            }

            Transform cam = origin.Camera.transform;

            // Cap actuel de la camera par rapport au rig : on le compense pour que le joueur
            // regarde dans la direction de l'ancre quelle que soit l'orientation reelle de sa tete.
            Vector3 fwd = origin.transform.InverseTransformDirection(cam.forward);
            fwd.y = 0f;
            float camYaw = fwd.sqrMagnitude > 1e-4f ? Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg : 0f;
            float yaw = anchor.eulerAngles.y - camYaw;
            origin.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            Vector3 delta = anchor.position - cam.position;
            delta.y = 0f;
            Vector3 p = origin.transform.position + delta;
            p.y = anchor.position.y;
            origin.transform.position = p;

            _yaw = yaw;
        }

        void Update()
        {
            if (origin == null || !IsDesktop) return;

            var mouse = Mouse.current;
            if (mouse == null || !mouse.rightButton.isPressed) return;

            Vector2 d = mouse.delta.ReadValue() * mouseSensitivity;
            _yaw += d.x;
            _pitch = Mathf.Clamp(_pitch - d.y, -80f, 80f);

            origin.transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_pitchPivot != null) _pitchPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }
    }
}
