namespace GnssProxyDemo;

/// <summary>
/// The Subject participant of the proxy pattern. Declares the single
/// operation common to the real receiver and to any surrogate standing in
/// for it. Clients depend on this type alone and are therefore indifferent
/// to which implementation they are supplied with.
/// </summary>
public interface IGnssReceiver
{
    /// <summary>Obtains a position fix.</summary>
    /// <exception cref="GnssAcquisitionException">
    /// Thrown when a fix cannot be acquired, for example under signal obstruction.
    /// </exception>
    PositionFix GetFix();
}
