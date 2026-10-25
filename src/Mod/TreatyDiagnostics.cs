using System;
using System.Linq;
using System.Text;
using PavonisInteractive.TerraInvicta;
using PavonisInteractive.TerraInvicta.Tasks;
namespace GiveUpNation
{
    internal static class TreatyDiagnostics
    {
        internal static string Build(TIFactionState ai, TIFactionState other, TradeOffer.TreatyType treaty)
        {
            if (ai == null || other == null) return string.Empty;
            bool eligible = ai.CanTradeTreaty(other, treaty);
            bool willing = AIEvaluators.GetWillingnessToTradeTreaty(ai, other, treaty) > 0;
            var b = new StringBuilder("\n\nTreaty conditions (AI perspective)\n");
            b.Append(eligible && willing ? "Available to negotiate" : "Unavailable").Append("\n");
            void Check(bool pass, string text) => b.Append(pass ? "[OK] " : "[BLOCKED] ").Append(text).Append('\n');
            void Note(string text) => b.Append(text).Append('\n');
            string assessment = ai.selfAssessement.ToString();
            bool ally = ai.permanentAlly(other);
            bool hunting = ai.currentlyCapturingHydra || ai.currentlyHuntingHydraToKill;
            if (treaty == TradeOffer.TreatyType.NAP)
            {
                Check(!other.permanentAlly(ai), "Not already permanent allies");
                Check(!ai.HasNAP(other), "No existing non-aggression pact");
                Check(!ai.AI_AtWarWithFaction(other) && !other.AI_AtWarWithFaction(ai), "Neither side considers itself at war with the other");
                Check(ai.mostPowerfulHumanEnemy != other, "You are not the AI's strongest human enemy");
                Check(other.mostPowerfulHumanEnemy != ai, "AI is not your faction's strongest human enemy");
                Check((!ai.IsAlienFaction || other.veryProAlien) && (!other.IsAlienFaction || ai.veryProAlien), "Alien treaties require a strongly pro-alien human partner");
                Check(ally || other.GetObjectivesByTypeAndStatus(ObjectiveType.Victory, ObjectiveStatus.Unlocked).Count == 0, "Your victory objective is not unlocked (permanent ally exception)");
                float jealousy = AIDailyFactionPlanner.JealousyAndDeescalation(ai, other, false, false);
                Check(jealousy <= 0, "Strategic jealousy: " + jealousy.ToString("0.###") + "; must be <= 0");
                Note("\nStrategic willingness" + (eligible ? "" : " (only evaluated after eligibility passes)"));
                float distance = Math.Abs(ai.ideologyCoordinates.x - other.ideologyCoordinates.x);
                Check(distance <= 2 || ai.malleable, "Ideological X-distance: " + distance.ToString("0.##") + "; maximum 2 unless malleable");
                Check(!((!ai.antiAlien && other.IsAlienFaction) || (ai.IsAlienFaction && other.antiAlien)), "Native alien/alignment willingness rule");
                Check(!other.IsAlienFaction || !hunting, "Not actively capturing/killing Hydra when seeking an alien pact");
                bool special = ally || other.permanentAlly(ai);
                if (ai.IsAlienFaction)
                {
                    bool attacks = other.currentlyCapturingHydra || other.currentlyHuntingHydraToKill || (other.factionAssassinations.ContainsKey(ai) && other.factionAssassinations[ai] > 0);
                    if (!special) Check(!attacks, "Human partner is not hunting/capturing Hydra and has no recorded assassinations against aliens");
                    special |= other.isAlienAppeaser && (other.selfAssessement > GameStateManager.AlienProxy().selfAssessement || other.selfAssessement >= ai.selfAssessement || other.unlockedVictoryObjective);
                }
                else special |= ai.isAlienAppeaser && other.IsAlienFaction;
                Check(special || TreatyNeeds.Nap(assessment), "AI situation: " + assessment + "; normally requires Losing/LosingBig" + (special ? " (special alliance/alien exception applies)" : ""));
            }
            else if (treaty == TradeOffer.TreatyType.Truce)
            {
                Check(!other.permanentAlly(ai), "Not already permanent allies");
                Check(ai.FindGoals(GoalType.WarOnFaction, ai, other).Count > 0 || other.FindGoals(GoalType.WarOnFaction, other, ai).Count > 0, "At least one side has a war goal against the other");
                Note("\nStrategic willingness" + (eligible ? "" : " (only evaluated after eligibility passes)"));
                float jealousy = AIDailyFactionPlanner.JealousyAndDeescalation(ai, other, false, false);
                bool beaten = AIEvaluators.HumanFactionTooBeatDownToContinue(ai, other);
                Check(!ai.player.isAI || jealousy <= 0 || beaten, "Strategic jealousy: " + jealousy.ToString("0.###") + "; must be <= 0 or beaten-down exception (" + (beaten ? "yes" : "no") + ")");
                Check(!((!ai.proAlien && other.IsAlienFaction) || (ai.IsAlienFaction && other.proAlien)), "Native alien/alignment truce rule");
                Check(!other.IsAlienFaction || !hunting, "Not actively capturing/killing Hydra when seeking alien peace");
                bool victory = other.IsActiveHumanFaction && other.primaryHab != null && other.unlockedVictoryObjective && ((ai.IsActiveHumanFaction && other.proAlien) || (ai.veryProAlien && !other.proAlien));
                Check(!victory, "No opposing late-victory alignment restriction");
                Check(TreatyNeeds.Truce(assessment, ai.enemyWarFactions.Count, ai.mostPowerfulHumanEnemy == other), "AI situation: " + assessment + "; needs Losing/LosingBig, or Even with >2 war enemies and you as strongest human enemy");
                Note("War enemies: " + ai.enemyWarFactions.Count + "; you are strongest human enemy: " + (ai.mostPowerfulHumanEnemy == other ? "yes" : "no"));
            }
            else if (treaty == TradeOffer.TreatyType.Intel)
            {
                Check(!ai.intelSharingFactions.Contains(other), "No existing intelligence-sharing agreement");
                float hate = ai.GetFactionHate(other), limit = TemplateManager.global.factionHateConflictThreshold;
                Check(hate < limit, "AI hate: " + hate.ToString("0.##") + "; must be < " + limit.ToString("0.##"));
                Check(!ai.turnedCouncilors.Any(x => x.faction == other), "AI has no turned councilor inside your faction");
                Check(ai.HasNAP(other, false) || ally, "Existing active NAP or permanent alliance");
                Check(!other.IsAlienFaction || ai.veryProAlien, "Alien partner requires strongly pro-alien alignment");
                Note("\nStrategic willingness" + (eligible ? "" : " (only evaluated after eligibility passes)"));
                Check(ai.malleable || (ai.proAlien && other.proAlien) || (ai.antiAlien && other.antiAlien), "Compatible pro/anti-alien alignment, or malleable AI");
                if (!ally)
                {
                    bool loyaltyGate = (ai.IsAlienProxy && !other.IsAlienFaction && ai.intelSharingFactions.Contains(GameStateManager.AlienFaction())) || (ai.IsAlienFaction && !other.IsAlienProxy);
                    if (loyaltyGate) Check(!other.councilors.Any(x => ai.GetViewofCouncilor(x).GetAttribute(CouncilorAttribute.Loyalty) < 20f), "AI estimates all your councilors' loyalty at >= 20 (alien intelligence security rule)");
                    Check(!(ai.proAlien && other.intelSharingFactions.Any(x => x.veryAntiAlien)), "You do not share intel with strongly anti-alien factions opposed to this AI");
                    Check(!(ai.antiAlien && other.intelSharingFactions.Any(x => x.veryProAlien)), "You do not share intel with strongly pro-alien factions opposed to this AI");
                    Check(!(other.proAlien && ai.intelSharingFactions.Any(x => x.veryAntiAlien)), "AI's intelligence partners do not conflict with your pro-alien alignment");
                    Check(!(other.antiAlien && ai.intelSharingFactions.Any(x => x.veryProAlien)), "AI's intelligence partners do not conflict with your anti-alien alignment");
                }
                float distance = TINationState.GetIdeologicalDistance(ai, other);
                bool close = !ally && distance <= 1.12f;
                bool needs = TreatyNeeds.Intel(assessment, ai.turnedCouncilors.Count == 0, ai.AI_AtWarWithOtherFactions(), ai.currentlyDetectingHydra, close, other.selfAssessement <= ai.selfAssessement, other.selfAssessement < ai.selfAssessement);
                Check(needs, "Strategic need to share intelligence");
                Note("AI situation: " + assessment + "; your situation: " + other.selfAssessement + "; AI turned councilors: " + ai.turnedCouncilors.Count);
                Note("At war: " + ai.AI_AtWarWithOtherFactions() + "; detecting Hydra: " + ai.currentlyDetectingHydra + "; ideological distance: " + distance.ToString("0.###") + " (close <= 1.12)");
                Note("Need passes if LosingBig; or Losing with no spies and war/Hydra detection; or Even with no spies and Hydra detection; or helping a close faction doing no better (strictly worse if AI is winning). Permanent allies skip the close-neighbor branch in this build.");
            }
            return b.ToString();
        }
    }
}
