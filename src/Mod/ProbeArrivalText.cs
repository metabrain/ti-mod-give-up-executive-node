using System;
using PavonisInteractive.TerraInvicta;

namespace GiveUpNation
{
    internal static class ProbeArrivalText
    {
        // Resolve the scheduled event on demand. Do not recompute flight time from
        // today's launch costs or retain a date from another (reused) list row.
        internal static string Build(TIFactionState faction, TISpaceBodyState body)
        {
            if (faction == null || body == null) return null;
            var arrival = faction.ProspectorArrival(body);
            if (arrival == null) return null;
            return Loc.T("UI.Space.ProspectorEnRoute") + "\n"
                + Loc.T("UI.Space.ProbeArrival", new object[] { arrival.ToCustomDateString() })
                + " (" + Math.Max(0, arrival.DifferenceInDays(TITimeState.Now())).ToString("0.0") + " days)";
        }
    }
}
