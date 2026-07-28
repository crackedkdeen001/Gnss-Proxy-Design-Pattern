namespace GnssProxyDemo;

/// <summary>
/// The Client participant. Depends only on <see cref="IGnssReceiver"/> and is
/// therefore indifferent to whether it holds a receiver or a proxy. The
/// method <see cref="PollForPositions"/> is written once and executed against
/// both, which is the substitutability claim of the design.
/// </summary>
internal static class Program
{
    // Simulated time is compressed so that a ten minute deployment completes
    // in a few seconds. One simulated second corresponds to five real
    // milliseconds. All quoted intervals below are in simulated time.
    private const int TimeScale = 200;

    private static readonly TimeSpan TimeToFirstFix = Scaled(10);      // 10 s, per [5]
    private static readonly TimeSpan PollInterval = Scaled(30);        // client polls every 30 s
    private static readonly TimeSpan ValidityWindow = Scaled(120);     // fix good for 2 min
    private const int PollCount = 20;                                  // 10 minutes of operation

    // A receiver drawing 25 mW [1] for a 10 s acquisition [5] expends
    // approximately 250 mJ per activation.
    private const double EnergyPerActivationMilliJoules = 250.0;

    private static TimeSpan Scaled(double simulatedSeconds) =>
        TimeSpan.FromMilliseconds(simulatedSeconds * 1000.0 / TimeScale);

    private static void Main()
    {
        Rule();
        Console.WriteLine("  GNSS CACHING PROXY: COMPARATIVE EVALUATION");
        Rule();
        Console.WriteLine("  Simulated receiver draw          25 mW");
        Console.WriteLine($"  Time to first fix                10 s  ({EnergyPerActivationMilliJoules:F0} mJ per activation)");
        Console.WriteLine("  Client poll interval             30 s");
        Console.WriteLine("  Proxy validity window            120 s");
        Console.WriteLine($"  Requests issued                  {PollCount}  (10 minutes of operation)");
        Console.WriteLine($"  Time compression                 {TimeScale}x");
        Console.WriteLine();

        var baseline = RunExperiment(useProxy: false);
        var proxied = RunExperiment(useProxy: true);

        ReportComparison(baseline, proxied);
        ReportWindowSweep();
        ReportStaleFallback();
        ReportCoalescing();
    }

    /// <summary>
    /// The client. Identical in both configurations: it calls GetFix on an
    /// IGnssReceiver and knows nothing of power management.
    /// </summary>
    private static List<PositionFix> PollForPositions(IGnssReceiver source, int count, TimeSpan interval)
    {
        var fixes = new List<PositionFix>(count);
        for (var i = 0; i < count; i++)
        {
            fixes.Add(source.GetFix());
            if (i < count - 1)
            {
                Thread.Sleep(interval);
            }
        }
        return fixes;
    }

    private static Result RunExperiment(bool useProxy)
    {
        var receiver = new SimulatedGnssReceiver(TimeToFirstFix, EnergyPerActivationMilliJoules);
        IGnssReceiver source = useProxy
            ? new CachingGnssProxy(receiver, ValidityWindow)
            : receiver;

        var fixes = PollForPositions(source, PollCount, PollInterval);

        var proxy = source as CachingGnssProxy;
        return new Result(
            Label: useProxy ? "With proxy" : "Direct access",
            Requests: fixes.Count,
            Activations: receiver.ActivationCount,
            EnergyMilliJoules: receiver.EnergyConsumed,
            Hits: proxy?.CacheHits ?? 0);
    }

    private static void ReportComparison(Result baseline, Result proxied)
    {
        Rule();
        Console.WriteLine("  TABLE 4.1  Receiver activations and energy over an identical request sequence");
        Rule();
        Console.WriteLine($"  {"Configuration",-16}{"Requests",10}{"Activations",13}{"Energy (mJ)",14}{"From cache",13}");
        foreach (var r in new[] { baseline, proxied })
        {
            Console.WriteLine($"  {r.Label,-16}{r.Requests,10}{r.Activations,13}{r.EnergyMilliJoules,14:F0}{r.Hits,13}");
        }
        Console.WriteLine();

        var activationReduction = 100.0 * (baseline.Activations - proxied.Activations) / baseline.Activations;
        var energyReduction = 100.0 * (baseline.EnergyMilliJoules - proxied.EnergyMilliJoules) / baseline.EnergyMilliJoules;
        var factor = (double)baseline.Activations / proxied.Activations;

        Console.WriteLine($"  Activations avoided              {baseline.Activations - proxied.Activations} of {baseline.Activations}");
        Console.WriteLine($"  Reduction in activations         {activationReduction:F1} %");
        Console.WriteLine($"  Reduction in energy consumed     {energyReduction:F1} %");
        Console.WriteLine($"  Improvement factor               {factor:F2}x");
        Console.WriteLine();
        Console.WriteLine("  The client code executed in both configurations is identical. The");
        Console.WriteLine("  difference is attributable solely to the presence of the proxy.");
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates the tradeoff of Section 5.2: a longer window saves more
    /// energy but permits an older position to be returned. Acquisition delay
    /// is set to zero here so the sweep completes quickly.
    /// </summary>
    private static void ReportWindowSweep()
    {
        Rule();
        Console.WriteLine("  TABLE 4.2  Effect of validity window length on activations and staleness");
        Rule();
        Console.WriteLine($"  {"Window (s)",12}{"Activations",13}{"Energy (mJ)",14}{"Max staleness (s)",20}");

        foreach (var windowSeconds in new[] { 0, 30, 60, 120, 300, 600 })
        {
            var receiver = new SimulatedGnssReceiver(TimeSpan.Zero, EnergyPerActivationMilliJoules);
            var proxy = new CachingGnssProxy(receiver, Scaled(windowSeconds));

            var maxStaleness = TimeSpan.Zero;
            for (var i = 0; i < PollCount; i++)
            {
                var fix = proxy.GetFix();
                var age = DateTime.UtcNow - fix.AcquiredAt;
                if (age > maxStaleness) maxStaleness = age;
                if (i < PollCount - 1) Thread.Sleep(PollInterval);
            }

            var staleSeconds = maxStaleness.TotalMilliseconds * TimeScale / 1000.0;
            Console.WriteLine($"  {windowSeconds,12}{receiver.ActivationCount,13}{receiver.EnergyConsumed,14:F0}{staleSeconds,20:F1}");
        }

        Console.WriteLine();
        Console.WriteLine("  A window of zero disables caching and reproduces direct access. Longer");
        Console.WriteLine("  windows reduce activations monotonically and increase maximum position");
        Console.WriteLine("  age correspondingly. The appropriate value is application dependent.");
        Console.WriteLine();
    }

    private static void ReportStaleFallback()
    {
        Rule();
        Console.WriteLine("  4.3  Behaviour when acquisition fails");
        Rule();

        var receiver = new SimulatedGnssReceiver(TimeSpan.Zero, EnergyPerActivationMilliJoules);
        var proxy = new CachingGnssProxy(receiver, Scaled(30));

        var good = proxy.GetFix();
        Console.WriteLine($"  Fix obtained under clear sky     {good}");

        receiver.FailAcquisition = true;
        Thread.Sleep(Scaled(60));

        var degraded = proxy.GetFix();
        Console.WriteLine($"  Signal obstructed, fix returned  {degraded}");
        Console.WriteLine();
        Console.WriteLine($"  Stale fallbacks served           {proxy.StaleFallbacks}");
        Console.WriteLine("  The last known position is returned marked stale rather than failing");
        Console.WriteLine("  outright. The stored timestamp is not refreshed, so the receiver is");
        Console.WriteLine("  retried on the next request instead of the fix appearing valid again.");
        Console.WriteLine();
    }

    private static void ReportCoalescing()
    {
        Rule();
        Console.WriteLine("  4.4  Concurrent requests");
        Rule();

        var receiver = new SimulatedGnssReceiver(TimeToFirstFix, EnergyPerActivationMilliJoules);
        var proxy = new CachingGnssProxy(receiver, ValidityWindow);

        const int threads = 20;
        using var barrier = new Barrier(threads);
        Parallel.For(0, threads, _ =>
        {
            barrier.SignalAndWait();
            proxy.GetFix();
        });

        Console.WriteLine($"  Concurrent requests issued       {threads}");
        Console.WriteLine($"  Receiver activations             {receiver.ActivationCount}");
        Console.WriteLine($"  Energy consumed (mJ)             {receiver.EnergyConsumed:F0}");
        Console.WriteLine();
        Console.WriteLine("  Requests arriving during an acquisition are coalesced onto it rather");
        Console.WriteLine("  than each triggering a separate activation.");
        Rule();
    }

    private static void Rule() => Console.WriteLine(new string('=', 78));

    private readonly record struct Result(
        string Label,
        int Requests,
        int Activations,
        double EnergyMilliJoules,
        int Hits);
}
