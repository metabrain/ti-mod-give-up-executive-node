using System;
using System.Collections.Generic;
using System.Globalization;

namespace GiveUpNation
{
    internal sealed class TravelEstimate
    {
        internal readonly double Days, DeltaV;
        internal readonly string Status;
        internal TravelEstimate(string status) { Status = status; }
        internal TravelEstimate(double days, double deltaV) { Days = days; DeltaV = deltaV; }
        internal static bool Valid(double days, double deltaV) =>
            !double.IsNaN(days) && !double.IsInfinity(days) && days >= 0
            && !double.IsNaN(deltaV) && !double.IsInfinity(deltaV) && deltaV >= 0;
        internal static TravelEstimate Faster(TravelEstimate best, double days, double deltaV, double budget)
        {
            if (!Valid(days, deltaV) || deltaV > budget + 0.000001) return best;
            return best == null || days < best.Days || (days == best.Days && deltaV < best.DeltaV)
                ? new TravelEstimate(days, deltaV) : best;
        }
        internal string Text => Status ?? Days.ToString("0.#", CultureInfo.CurrentCulture)
            + " d | " + DeltaV.ToString("0.##", CultureInfo.CurrentCulture) + " km/s";
        internal static double SortKey(TravelEstimate estimate, bool ascending) =>
            estimate == null || estimate.Status != null ? double.PositiveInfinity
                : ascending ? estimate.Days : -estimate.Days;
    }

    // UI redraws and filtering must never perform route calculations.
    internal sealed class TravelSnapshot<TFleet, TBody> where TFleet : class
    {
        private TFleet fleet;
        private readonly Dictionary<TBody, TravelEstimate> values = new Dictionary<TBody, TravelEstimate>();
        private readonly Queue<TBody> pending = new Queue<TBody>();
        internal int Count => values.Count;
        internal int Total { get; private set; }
        internal bool IsRunning => fleet != null && pending.Count > 0;
        internal bool IsFor(TFleet candidate) => candidate != null && ReferenceEquals(fleet, candidate);
        internal void Clear() { fleet = null; values.Clear(); pending.Clear(); Total = 0; }
        internal TravelEstimate Get(TBody body) => values.TryGetValue(body, out var result) ? result : null;
        internal void Start(TFleet candidate, IEnumerable<TBody> bodies)
        {
            Clear();
            if (candidate == null) return;
            var unique = new HashSet<TBody>();
            foreach (var body in bodies) if (unique.Add(body)) pending.Enqueue(body);
            Total = pending.Count;
            fleet = candidate;
        }

        // Called once per Unity frame, on the main thread. Native solving is synchronous.
        internal bool Step(Func<TBody, TravelEstimate> calculate)
        {
            if (!IsRunning) return false;
            try
            {
                var body = pending.Dequeue();
                values.Add(body, calculate(body));
                return true;
            }
            catch { Clear(); throw; }
        }
    }
}
