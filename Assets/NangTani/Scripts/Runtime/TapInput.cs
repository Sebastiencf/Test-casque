using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NangTani
{
    /// <summary>
    /// L'unique action explicite de l'experience (CDD, Section 5) : un tap de gachette.
    /// Fonctionne avec la gachette de n'importe quelle manette XR (Quest 2 / 3), et en test sans casque
    /// avec Espace ou le clic gauche.
    /// </summary>
    public class TapInput : MonoBehaviour
    {
        InputAction _tap;
        bool _armed;
        bool _pressed;

        public bool Tapped { get; private set; }
        public bool TimedOut { get; private set; }

        /// <summary>Nombre d'appuis depuis le debut (pour surveiller un appui sans attendre dans Wait).</summary>
        public int PressCount { get; private set; }

        void Awake()
        {
            _tap = new InputAction("NangTani_Tap", InputActionType.Button);
            _tap.AddBinding("<XRController>{RightHand}/{TriggerButton}");
            _tap.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
            _tap.AddBinding("<XRController>{RightHand}/triggerPressed");
            _tap.AddBinding("<XRController>{LeftHand}/triggerPressed");
            _tap.AddBinding("<Keyboard>/space");
            _tap.AddBinding("<Mouse>/leftButton");
            _tap.AddBinding("<Gamepad>/buttonSouth");
            _tap.performed += OnPerformed;
        }

        void OnEnable() { _tap.Enable(); }
        void OnDisable() { _tap.Disable(); }

        void OnDestroy()
        {
            if (_tap == null) return;
            _tap.performed -= OnPerformed;
            _tap.Dispose();
        }

        void OnPerformed(InputAction.CallbackContext ctx)
        {
            PressCount++;
            if (_armed) _pressed = true;
        }

        /// <summary>Attend un tap (timeout &lt;= 0 : indefiniment). Teste ensuite Tapped / TimedOut.</summary>
        public IEnumerator Wait(float timeoutSeconds)
        {
            Tapped = false;
            TimedOut = false;
            _pressed = false;
            _armed = true;
            float start = Time.time;

            while (!_pressed)
            {
                if (timeoutSeconds > 0f && Time.time - start >= timeoutSeconds)
                {
                    TimedOut = true;
                    break;
                }
                yield return null;
            }

            _armed = false;
            Tapped = _pressed;
        }
    }
}
