namespace DashAndCollect.Tests
{
    /// <summary>
    /// Test double for IInputProvider. JumpPressed is a settable property so
    /// test methods can simulate frame-accurate press events without a player loop.
    ///
    /// Usage:
    ///   var input = new TestInputProvider { JumpPressed = true };
    ///   pc.Initialize(gm, input);
    ///   // ... assert behavior ...
    ///   input.JumpPressed = false;
    /// </summary>
    public sealed class TestInputProvider : DashAndCollect.IInputProvider
    {
        public bool JumpPressed { get; set; }
    }
}
