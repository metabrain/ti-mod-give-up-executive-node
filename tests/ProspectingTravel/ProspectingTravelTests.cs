using System;
using System.Globalization;
using GiveUpNation;

internal static class Tests
{
    private static int checks;
    private static void Check(bool condition, string name)
    {
        if (!condition) throw new Exception(name);
        checks++;
    }
    private static void Main()
    {
        var best = TravelEstimate.Faster(null, 100, 5, 10);
        best = TravelEstimate.Faster(best, 30, 9, 10);
        Check(best.Days == 30 && best.DeltaV == 9, "Time and delta-v come from the same fastest candidate");
        Check(ReferenceEquals(best, TravelEstimate.Faster(best, 10, 11, 10)), "Reject faster but unaffordable candidate");
        best = TravelEstimate.Faster(best, 30, 7, 10);
        Check(best.DeltaV == 7, "Equal arrival chooses lower delta-v");
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, -1 })
        {
            Check(ReferenceEquals(best, TravelEstimate.Faster(best, invalid, 1, 10)), "Invalid duration rejected");
            Check(ReferenceEquals(best, TravelEstimate.Faster(best, 1, invalid, 10)), "Invalid delta-v rejected");
        }
        var zero = TravelEstimate.Faster(null, 0, 0, 0);
        Check(zero != null && zero.Days == 0, "Already at destination is a valid zero-cost result");
        Check(TravelEstimate.Faster(null, 5, 50, 10) == null, "No affordable route stays unavailable");
        Check(new TravelEstimate("No route found").Text == "No route found", "Failure is not presented as zero delta-v");
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
        Check(new TravelEstimate(12.34, 2.456).Text == "12.3 d | 2.46 km/s", "Units and precision");

        var snapshot = new TravelSnapshot<object, int>();
        var fleet = new object(); int calls = 0;
        Func<int, TravelEstimate> solve = body => { calls++; return new TravelEstimate(body, body / 10.0); };
        Check(snapshot.Get(1) == null && calls == 0, "Blank column access before header click performs no calculation");
        Check(TravelEstimate.SortKey(new TravelEstimate(10, 2), true) < TravelEstimate.SortKey(new TravelEstimate(20, 3), true), "Ascending travel sorts earliest first");
        Check(TravelEstimate.SortKey(new TravelEstimate(10, 2), false) > TravelEstimate.SortKey(new TravelEstimate(20, 3), false), "Descending travel sorts latest first");
        foreach (bool asc in new[] { true, false })
        {
            Check(TravelEstimate.SortKey(null, asc) == double.PositiveInfinity, "Uncalculated rows sort last");
            Check(TravelEstimate.SortKey(new TravelEstimate("No route found"), asc) == double.PositiveInfinity, "Unavailable rows sort last in both directions");
        }
        Check(!snapshot.Step(solve) && calls == 0, "Idle frames cannot start calculation");
        snapshot.Start(fleet, new[] { 1, 2, 2, 3 });
        Check(calls == 0 && snapshot.Total == 3 && snapshot.IsRunning, "Header click queues unique bodies without blocking on solving");
        Check(snapshot.Step(solve) && calls == 1 && snapshot.Count == 1, "One frame computes exactly one destination");
        Check(snapshot.Get(1) != null && snapshot.Get(2) == null, "Completed cell visible while pending cells remain blank");
        snapshot.Step(solve); snapshot.Step(solve);
        Check(calls == 3 && snapshot.Count == 3 && !snapshot.IsRunning, "All unique destinations completed over separate frames");
        for (int i = 0; i < 150; i++)
        {
            snapshot.Get(3);
            snapshot.Step(solve);
        }
        Check(calls == 3 && snapshot.Get(1) != null, "Scrolling and filtering do not repeat calculations or drop hidden bodies");
        Check(snapshot.Get(99) == null, "Recycled unknown row never displays prior body's result");
        snapshot.Clear();
        Check(snapshot.Get(1) == null && !snapshot.IsFor(fleet), "Closing or refreshing invalidates results");
        Check(!snapshot.Step(solve) && calls == 3, "Reopening remains blank until another request");
        snapshot.Start(fleet, new[] { 1, 2, 3 });
        snapshot.Step(solve);
        snapshot.Clear();
        Check(!snapshot.Step(solve) && calls == 4 && snapshot.Count == 0, "Closing mid-calculation cancels all pending destinations");
        var other = new object();
        snapshot.Start(fleet, new[] { 1, 2, 3 }); snapshot.Step(solve);
        snapshot.Start(other, new[] { 9 }); snapshot.Step(solve);
        Check(calls == 6 && snapshot.Get(1) == null && snapshot.Get(9) != null && snapshot.IsFor(other), "Replacing fleet discards old results and pending work");
        snapshot.Start(null, new[] { 1 });
        Check(!snapshot.Step(solve) && calls == 6, "No fleet means no solver calls");
        bool thrown = false;
        snapshot.Start(fleet, new[] { 1, 2 }); snapshot.Step(solve);
        try { snapshot.Step(b => throw new Exception()); }
        catch { thrown = true; }
        Check(thrown && !snapshot.IsFor(fleet) && snapshot.Count == 0 && !snapshot.IsRunning, "Unexpected failure cancels pending work and clears partial results");
        snapshot.Start(fleet, new[] { 1 }); snapshot.Step(_ => new TravelEstimate("Calculation failed"));
        Check(snapshot.Get(1).Status == "Calculation failed", "Per-destination errors can be cached without retry storms");
        Console.WriteLine(checks + " prospecting-travel checks passed (route selection and snapshot lifecycle; no live solver).");
    }
}
