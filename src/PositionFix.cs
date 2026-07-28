namespace GnssProxyDemo;

/// <summary>
/// The result of a positioning request. Immutable: a fix, once acquired,
/// describes a moment that has passed and must never be altered.
/// </summary>
/// <param name="Latitude">Estimated latitude in decimal degrees.</param>
/// <param name="Longitude">Estimated longitude in decimal degrees.</param>
/// <param name="AcquiredAt">
/// The instant at which the receiver produced this fix. This is the key on
/// which the entire validity policy operates; see Section 3.1.3.
/// </param>
/// <param name="IsStale">
/// True when the fix is served beyond its validity window because a fresh
/// acquisition could not be completed.
/// </param>
public sealed record PositionFix(
    double Latitude,
    double Longitude,
    DateTime AcquiredAt,
    bool IsStale = false)
{
    /// <summary>Returns a copy of this fix marked as stale.</summary>
    public PositionFix AsStale() => this with { IsStale = true };

    public override string ToString() =>
        $"({Latitude,9:F5}, {Longitude,9:F5}) acquired {AcquiredAt:HH:mm:ss.fff}{(IsStale ? " [STALE]" : "")}";
}
