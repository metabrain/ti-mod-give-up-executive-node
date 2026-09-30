using PavonisInteractive.TerraInvicta;

namespace GiveUpNation
{
    internal static class ProspectingState
    {
        internal static ProspectingStatus GetStatus(TIFactionState faction, TISpaceBodyState body)
        {
            // Completed knowledge wins even if an old survey operation remains.
            if (faction.Prospected(body)) return ProspectingStatus.Prospected;
            if (faction.ProspectorEnRoute(body)) return ProspectingStatus.Prospecting;
            if (faction.FleetSurveyingPlanet(body)) return ProspectingStatus.Prospecting;
            return faction.CanProspectWithProbe(body, false) ? ProspectingStatus.Prospectable : ProspectingStatus.Unprospected;
        }
    }
}
