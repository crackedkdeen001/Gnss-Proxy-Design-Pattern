namespace GnssProxyDemo;

/// <summary>
/// The Proxy participant. Implements the same interface as the receiver it
/// controls, and encapsulates the entire power management policy: the
/// decision of whether a request reaches the hardware at all.
///
/// Every member constituting that policy is private. No other component in
/// the system is aware that the receiver is duty cycled, and none can alter
/// the policy.
/// </summary>
public sealed class CachingGnssProxy : IGnssReceiver
{
    private readonly IGnssReceiver _receiver;
    private readonly TimeSpan _validityWindow;
    private readonly Lock _gate = new();

    private PositionFix? _cachedFix;

    /// <summary>Requests satisfied from the cached fix.</summary>
    public int CacheHits { get; private set; }

    /// <summary>Requests that required the receiver to be activated.</summary>
    public int CacheMisses { get; private set; }

    /// <summary>
    /// Requests served from an expired fix because acquisition failed.
    /// Counted separately from hits, since the receiver was activated.
    /// </summary>
    public int StaleFallbacks { get; private set; }

    /// <param name="receiver">
    /// The receiver whose access is controlled. Typed as the interface, not
    /// the concrete class, so that a driver for physical hardware may be
    /// substituted without alteration to this class.
    /// </param>
    /// <param name="validityWindow">
    /// The maximum age at which a retained fix is still considered adequate.
    /// A longer window saves more energy at the cost of greater staleness;
    /// this tradeoff is examined in Section 5.2.
    /// </param>
    public CachingGnssProxy(IGnssReceiver receiver, TimeSpan validityWindow)
    {
        if (validityWindow < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(validityWindow), "Validity window cannot be negative.");
        }

        _receiver = receiver ?? throw new ArgumentNullException(nameof(receiver));
        _validityWindow = validityWindow;
    }

    public PositionFix GetFix()
    {
        // The lock is held across the whole operation. This is what coalesces
        // concurrent requests onto a single activation: a caller arriving
        // during an acquisition blocks, and on entering finds a fix that has
        // just been stored and is necessarily within the window. The cost is
        // that such a caller waits for the full acquisition delay; see 3.2.4.
        lock (_gate)
        {
            if (IsCachedFixValid())
            {
                CacheHits++;
                return _cachedFix!;
            }

            CacheMisses++;

            try
            {
                var fresh = _receiver.GetFix();
                _cachedFix = fresh;
                return fresh;
            }
            catch (GnssAcquisitionException)
            {
                // Acquisition failed. If a previous fix is held, serve it
                // marked stale rather than failing outright or reactivating
                // repeatedly. Analogous to serving a stale HTTP response when
                // the origin server is unreachable.
                if (_cachedFix is null)
                {
                    throw;
                }

                StaleFallbacks++;

                // The stored fix retains its original timestamp. Refreshing it
                // here would silently make the fix valid again and the
                // receiver would never be retried.
                return _cachedFix.AsStale();
            }
        }
    }

    private bool IsCachedFixValid()
    {
        if (_cachedFix is null)
        {
            return false;
        }

        var age = DateTime.UtcNow - _cachedFix.AcquiredAt;
        return age < _validityWindow;
    }
}
