namespace DashAndCollect
{
    /// <summary>
    /// Authoritative run-lifecycle states owned by GameManager.
    /// Transitions: Idle -> Running -> Dead -> Running (repeat).
    /// No Paused state in v1 scope (TDD §4.1).
    /// </summary>
    public enum RunState
    {
        Idle,
        Running,
        Dead
    }
}
