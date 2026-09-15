using UnityEngine;
using UnityEngine.InputSystem;

namespace HoldMyBeer.Player
{
    /// <summary>
    /// Reads the Input System devices directly. Deliberately asset-free so the
    /// prefab has nothing to wire; swap in an InputActionAsset implementation once
    /// rebinding matters.
    /// </summary>
    public sealed class KeyboardMousePlayerInputSource : IPlayerInputSource
    {
        private readonly float _lookSensitivity;

        public KeyboardMousePlayerInputSource(float lookSensitivity) => _lookSensitivity = lookSensitivity;

        public Vector2 Move
        {
            get
            {
                var keyboard = Keyboard.current;
                if (keyboard == null)
                {
                    return Vector2.zero;
                }

                var move = new Vector2(
                    (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                    (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));

                return move.sqrMagnitude > 1f ? move.normalized : move;
            }
        }

        // Mouse delta is already per-frame, so it must NOT be multiplied by deltaTime.
        public Vector2 Look =>
            Mouse.current == null ? Vector2.zero : Mouse.current.delta.ReadValue() * (_lookSensitivity * 0.02f);

        public bool JumpPressedThisFrame => Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;

        public bool SprintHeld => Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;

        public bool InteractPressedThisFrame =>
            (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);

        public bool InteractHeld =>
            (Mouse.current != null && Mouse.current.leftButton.isPressed) ||
            (Keyboard.current != null && Keyboard.current.eKey.isPressed);

        public bool InteractReleasedThisFrame =>
            (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame) ||
            (Keyboard.current != null && Keyboard.current.eKey.wasReleasedThisFrame);
    }
}
