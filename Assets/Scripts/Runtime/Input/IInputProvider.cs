namespace DashAndCollect
{
    /// <summary>
    /// Abstracts per-frame player input over a single-button action (tap / click / space).
    ///
    /// Implementations:
    ///   UnityInputProvider — production; reads Space key, left mouse button, and touch.
    ///   TestInputProvider  — test double; exposes a settable JumpPressed property.
    ///
    /// Injected into PlayerController.Initialize() so the player loop has no direct
    /// dependency on UnityEngine.InputSystem (TDD §8).
    /// </summary>
    public interface IInputProvider
    {
        /// <summary>
        /// True on the frame the primary action input (tap / click / space) was first pressed.
        /// Must return false on all subsequent frames until the button is released and re-pressed.
        /// </summary>
        bool JumpPressed { get; }
    }
}
