namespace GnssProxyDemo;

/// <summary>
/// The RealSubject participant. Models the two properties of physical
/// receiver hardware that are material to this design: acquisition takes a
/// non-trivial amount of time, and every activation consumes energy.
///
/// The instrumentation lives here rather than in the proxy because these
/// counters must record what the hardware actually did, not what the proxy
/// believes it did.
/// </summary>
public sealed class SimulatedGnssReceiver : IGnssReceiver
{
    private readonly TimeSpan _acquisitionDelay;
    private readonly double _energyPerActivation;
    private readonly Random _random;
    private readonly Lock _counterGuard = new();

    /// <summary>
    /// When true, every activation powers the radio, consumes energy, and
    /// then fails to obtain a fix. Used to exercise the stale-fallback path.
    /// </summary>
    public bool FailAcquisition { get; set; }

    /// <summary>Number of times the receiver has been powered on.</summary>
    public int ActivationCount { get; private set; }

    /// <summary>Cumulative energy attributable to those activations, in millijoules.</summary>
    public double EnergyConsumed { get; private set; }

    public SimulatedGnssReceiver(
        TimeSpan acquisitionDelay,
        double energyPerActivation,
        bool failAcquisition = false,
        int randomSeed = 20260727)
    {
        if (acquisitionDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(acquisitionDelay), "Acquisition delay cannot be negative.");
        }
        if (energyPerActivation < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(energyPerActivation), "Energy per activation cannot be negative.");
        }

        _acquisitionDelay = acquisitionDelay;
        _energyPerActivation = energyPerActivation;
        _random = new Random(randomSeed);
        FailAcquisition = failAcquisition;
    }

    public PositionFix GetFix()
    {
        // The cost is incurred the moment the radio powers on, before it is
        // known whether a fix will be obtained.
        lock (_counterGuard)
        {
            ActivationCount++;
            EnergyConsumed += _energyPerActivation;
        }

        if (_acquisitionDelay > TimeSpan.Zero)
        {
            Thread.Sleep(_acquisitionDelay);
        }

        if (FailAcquisition)
        {
            throw new GnssAcquisitionException();
        }

        double latitude, longitude;
        lock (_counterGuard)
        {
            // A small random walk around the University of Lagos, so that
            // successive fresh fixes are visibly distinct from cached ones.
            latitude = 6.5158 + (_random.NextDouble() - 0.5) * 0.002;
            longitude = 3.3898 + (_random.NextDouble() + 0.5) * 0.002;
        }

        return new PositionFix(latitude, longitude, DateTime.UtcNow);
    }

    /// <summary>Clears the instrumentation between experimental runs.</summary>
    public void Reset()
    {
        lock (_counterGuard)
        {
            ActivationCount = 0;
            EnergyConsumed = 0.0;
        }
    }
}
