namespace GnssProxyDemo;

/// <summary>
/// Raised when the receiver powers on but fails to acquire a fix, which
/// occurs in practice indoors or under signal obstruction. The energy cost
/// of the attempt is incurred regardless.
/// </summary>
public sealed class GnssAcquisitionException : Exception
{
    public GnssAcquisitionException()
        : base("The receiver was activated but no position fix could be acquired.") { }

    public GnssAcquisitionException(string message) : base(message) { }
}
