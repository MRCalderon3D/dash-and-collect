using System.Runtime.CompilerServices;

// Allow DashAndCollect.Tests.Runtime to access internal members of DashAndCollect.Runtime.
// Required for unit-testing internal logic (e.g. PlayerController.ProcessDash) without
// exposing it as part of the public API (TDD §11).
[assembly: InternalsVisibleTo("DashAndCollect.Tests.Runtime")]
