namespace GiveUpNation
{
    internal enum ProspectingStatus { Unprospected, Prospectable, Prospecting, Prospected }

    internal static class ProspectingSelection
    {
        internal static ProspectingStatus Classify(bool completed, bool probeEnRoute, bool fleetSurveying, bool prospectable)
        {
            if (completed) return ProspectingStatus.Prospected;
            if (probeEnRoute || fleetSurveying) return ProspectingStatus.Prospecting;
            return prospectable ? ProspectingStatus.Prospectable : ProspectingStatus.Unprospected;
        }

        internal static bool Matches(ProspectingStatus status, bool completed, bool unprospected, bool prospectable, bool underway)
        {
            if (!completed && !unprospected && !prospectable && !underway) return true;
            switch (status)
            {
                case ProspectingStatus.Prospected: return completed;
                case ProspectingStatus.Prospecting: return underway;
                case ProspectingStatus.Prospectable: return prospectable;
                default: return unprospected;
            }
        }
    }
}
