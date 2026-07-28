# GNSS Caching Proxy

C# demonstration of the proxy pattern applied to GNSS receiver power management.

## Layout

    src/                        the deliverable
      IGnssReceiver.cs          Subject      - the shared interface
      PositionFix.cs            the value type returned by GetFix
      GnssAcquisitionException.cs
      SimulatedGnssReceiver.cs  RealSubject  - models delay, energy, failure
      CachingGnssProxy.cs       Proxy        - the validity policy lives here
      Program.cs                Client       - comparative evaluation harness

    verification/               NOT part of the submission
      Harness.cs                minimal assertion library, no dependencies
      Program.cs                16 tests covering the design

    program_output.txt          captured output of a full run

## Build and run

    dotnet new console -n GnssProxyDemo
    # replace the generated Program.cs and add the other five files
    dotnet run

Target framework: net9.0 or later.

## Framework requirement

CachingGnssProxy and SimulatedGnssReceiver declare their lock fields as
System.Threading.Lock, which requires .NET 9 and C# 13.

To target .NET 8 or earlier, change these two lines:

    private readonly Lock _gate = new();            // CachingGnssProxy.cs
    private readonly Lock _counterGuard = new();    // SimulatedGnssReceiver.cs

to:

    private readonly object _gate = new();
    private readonly object _counterGuard = new();

Nothing else changes. The lock statement itself is identical in both cases.

Never assign a Lock to a variable or field typed as object. The lock
statement silently reverts to the older monitor path if you do. The compiler
warns about this; do not suppress the warning.
