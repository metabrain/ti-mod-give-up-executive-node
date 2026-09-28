namespace GiveUpNation
{
    internal enum ProspectingStatus { Unprospected, Prospecting, Prospected }

    internal static class ProspectingSelection
    {
        internal static ProspectingStatus Classify(bool completed, bool probeEnRoute, bool fleetSurveying)
        {
            if (completed) return ProspectingStatus.Prospected;
            return probeEnRoute || fleetSurveying ? ProspectingStatus.Prospecting : ProspectingStatus.Unprospected;
        }

        internal static bool Matches(ProspectingStatus status, bool completed, bool unprospected, bool underway)
        {
            if (!completed && !unprospected && !underway) return true;
            switch (status)
            {
                case ProspectingStatus.Prospected: return completed;
                case ProspectingStatus.Prospecting: return underway;
                default: return unprospected;
            }
        }
    }
}
