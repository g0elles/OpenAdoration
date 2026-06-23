namespace OpenAdoration.WPF.Services;

/// <summary>
/// Remembers the last-selected Bible version across navigation. Singleton so a transient
/// <c>BibleViewModel</c> can restore the operator's choice without a static field leaking
/// state across DI scopes and test runs (M3).
/// </summary>
public sealed class BibleNavigationState
{
    public int? LastVersionId { get; set; }
}
