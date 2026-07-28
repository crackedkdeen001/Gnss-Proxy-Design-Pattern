using System;
using System.Collections.Generic;

namespace Verify;

public sealed class AssertionException : Exception
{
    public AssertionException(string message) : base(message) { }
}

public static class Check
{
    public static void Equal<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new AssertionException($"{what}: expected <{expected}>, got <{actual}>");
        }
    }

    public static void EqualDouble(double expected, double actual, string what, double tol = 1e-6)
    {
        if (Math.Abs(expected - actual) > tol)
        {
            throw new AssertionException($"{what}: expected <{expected}>, got <{actual}>");
        }
    }

    public static void True(bool condition, string what)
    {
        if (!condition) throw new AssertionException($"{what}: expected true, got false");
    }

    public static void False(bool condition, string what)
    {
        if (condition) throw new AssertionException($"{what}: expected false, got true");
    }

    public static void InRange(double value, double lo, double hi, string what)
    {
        if (value < lo || value > hi)
        {
            throw new AssertionException($"{what}: <{value}> not within [{lo}, {hi}]");
        }
    }

    public static void Throws<TException>(Action action, string what) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        catch (Exception other)
        {
            throw new AssertionException(
                $"{what}: expected {typeof(TException).Name}, got {other.GetType().Name}");
        }
        throw new AssertionException($"{what}: expected {typeof(TException).Name}, nothing was thrown");
    }
}

public sealed class Runner
{
    private readonly List<(string Name, Action Body)> _tests = new();
    private int _passed;
    private readonly List<string> _failures = new();

    public void Add(string name, Action body) => _tests.Add((name, body));

    public int Run()
    {
        Console.WriteLine($"Running {_tests.Count} verification tests");
        Console.WriteLine(new string('-', 72));

        foreach (var (name, body) in _tests)
        {
            try
            {
                body();
                _passed++;
                Console.WriteLine($"  PASS  {name}");
            }
            catch (Exception ex)
            {
                var detail = ex is AssertionException ? ex.Message : $"{ex.GetType().Name}: {ex.Message}";
                _failures.Add($"{name}\n          {detail}");
                Console.WriteLine($"  FAIL  {name}");
                Console.WriteLine($"          {detail}");
            }
        }

        Console.WriteLine(new string('-', 72));
        Console.WriteLine($"{_passed} passed, {_failures.Count} failed, {_tests.Count} total");
        return _failures.Count == 0 ? 0 : 1;
    }
}
