using UnityEngine;
using UnityEngine.InputSystem;

namespace DashAndCollect
{
    /// <summary>
    /// Production IInputProvider. Reads the primary action input from keyboard, mouse,
    /// and touchscreen via the Unity Input System.
    ///
    /// Supported bindings:
    ///   Keyboard : Space
    ///   Mouse    : left button
    ///   Touch    : primary touch press
    ///
    /// JumpPressed is true only on the frame each source first transitions to pressed.
    /// Unity Input System tracks wasPressedThisFrame per-device at the global input state
    /// level — querying the property is safe from any Update() call on the same frame.
    ///
    /// Add this component to a GameObject in the scene and assign it to PlayerController
    /// via PlayerController.Initialize(gm, unityInputProvider) or the inspector shim.
    /// </summary>
    public sealed class UnityInputProvider : MonoBehaviour, IInputProvider
    {
        /// <inheritdoc/>
        public bool JumpPressed =>
            (Keyboard.current?.spaceKey.wasPressedThisFrame   ?? false) ||
            (Mouse.current?.leftButton.wasPressedThisFrame    ?? false) ||
            (Touchscreen.current?.primaryTouch.press.wasPressedThisFrame ?? false);
    }
}
