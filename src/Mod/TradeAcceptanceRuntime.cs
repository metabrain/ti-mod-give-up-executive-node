using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using UnityEngine;
namespace GiveUpNation
{
    internal static class TradeAcceptanceRuntime
    {
        private const string Owner = "Local.GiveUpNation.TradeAcceptance";
        private static Harmony harmony;
        private static bool failed;
        private static MethodInfo categoryScore, distrust, tableEmpty;
        private static FieldInfo playerOffer, aiOffer, aiInitiated, testing;
        private static object[] categories;
        private static object net;
        private static readonly HashSet<TradeAcceptanceUi> views = new HashSet<TradeAcceptanceUi>();
        internal static void Update(float elapsed)
        {
            if (!Main.Enabled || failed || harmony != null || !GameControl.loadcycle100) return;
            try
            {
                var category = AccessTools.Inner(typeof(TradeAI), "CategoryType");
                var score = AccessTools.Inner(typeof(TradeAI), "CategoryScoreType");
                categories = new[] { "Resources", "Orgs", "Projects", "Habs", "Diplomatic" }.Select(x => Enum.Parse(category, x)).ToArray();
                net = Enum.Parse(score, "Net");
                categoryScore = AccessTools.Method(typeof(TradeAI), "GetCategoryScore", new[] { typeof(TradeOffer.TradeAgreement), category, typeof(TIFactionState), typeof(TIFactionState), score });
                distrust = AccessTools.Method(typeof(TradeAI), "GetDistrust");
                tableEmpty = AccessTools.Method(typeof(DiplomacyController), "IsTableEmpty");
                playerOffer = AccessTools.Field(typeof(DiplomacyController), "playerTradeOffer");
                aiOffer = AccessTools.Field(typeof(DiplomacyController), "aiTradeOffer");
                aiInitiated = AccessTools.Field(typeof(DiplomacyController), "isThisAnAIOffer");
                testing = AccessTools.Field(typeof(DiplomacyController), "testingAITrade");
                if (new MemberInfo[] { categoryScore, distrust, tableEmpty, playerOffer, aiOffer, aiInitiated, testing }.Any(x => x == null)) throw new MissingMemberException("Trade evaluation API changed.");
                harmony = new Harmony(Owner);
                var postfix = new HarmonyMethod(typeof(TradeAcceptanceRuntime), nameof(Refresh));
                harmony.Patch(AccessTools.Method(typeof(DiplomacyController), "EvaluateTrade"), postfix: postfix);
                harmony.Patch(AccessTools.Method(typeof(DiplomacyController), "Setup"), postfix: postfix);
                harmony.Patch(AccessTools.Method(typeof(DiplomacyController), "LoadBankValues"),
                    prefix: new HarmonyMethod(typeof(TreatyTooltips), nameof(TreatyTooltips.BeforeBankRefresh)),
                    postfix: new HarmonyMethod(typeof(TreatyTooltips), nameof(TreatyTooltips.Attach)));
                harmony.Patch(AccessTools.Method(typeof(DiplomacyController), "Setup"),
                    postfix: new HarmonyMethod(typeof(TreatyTooltips), nameof(TreatyTooltips.Attach)));
                foreach (var c in Resources.FindObjectsOfTypeAll<DiplomacyController>())
                    if (c.gameObject.scene.IsValid() && c.gameObject.activeInHierarchy) { Refresh(c); TreatyTooltips.Attach(c); }
            }
            catch (Exception e) { Fail(e); }
        }
        private static void Refresh(DiplomacyController __instance)
        {
            if (!Main.Enabled || failed || !GameControl.loadcycle100) return;
            try
            {
                var c = __instance;
                var player = GameControl.control?.activePlayer;
                var ai = c.tradingFaction;
                var p = playerOffer.GetValue(c) as TradeOffer;
                var a = aiOffer.GetValue(c) as TradeOffer;
                if (player == null || ai == null || p == null || a == null) return;
                bool empty = (bool)tableEmpty.Invoke(c, null);
                var ui = c.GetComponent<TradeAcceptanceUi>();
                if (ui == null) { ui = c.gameObject.AddComponent<TradeAcceptanceUi>(); views.Add(ui); ui.Initialize(c); }
                if (empty) { ui.Show("AI benefit: —\nYour benefit: —", "", "Add items to see their valuation."); return; }
                TradeOffer.TradeAgreement agreement = (A: p, B: a);
                var aiScores = Scores(agreement, ai, player);
                var playerScores = Scores(agreement, player, ai);
                float aiScore = TradeAcceptanceText.Score(aiScores), playerScore = TradeAcceptanceText.Score(playerScores);
                var blockers = Blockers(a, p, ai);
                string blocker = blockers.Count == 0 ? null : string.Join("\n", blockers.Select((text, index) => (index + 1) + ". " + text));
                bool offered = !c.touchedAIOffer && ((bool)aiInitiated.GetValue(c) || (bool)testing.GetValue(c));
                string result = TradeAcceptanceText.Result(false, offered, blocker);
                var tip = new StringBuilder("Internal valuation scores, not money or acceptance probability.\nNegative category totals count three times against each faction's benefit.\n\nCategory: AI / You (before negative weighting)\n");
                for (int i = 0; i < categories.Length; i++) tip.Append(categories[i]).Append(": ").Append(TradeAcceptanceText.Number(aiScores[i])).Append(" / ").Append(TradeAcceptanceText.Number(playerScores[i])).Append('\n');
                tip.Append("\nDistrust (AI / You): ").Append(((float)distrust.Invoke(null, new object[] { ai, player })).ToString("0.000")).Append(" / ").Append(((float)distrust.Invoke(null, new object[] { player, ai })).ToString("0.000"));
                tip.Append("\n\n").Append(result);
                tip.Append("\n\nHard restrictions").Append(offered ? " (bypassed for an untouched AI offer)" : "").Append(":\n");
                if (blockers.Count == 0) tip.Append("None violated.\n");
                for (int i = 0; i < blockers.Count; i++) tip.Append(i + 1).Append(". ").Append(blockers[i]).Append('\n');
                tip.Append("\nRules: AI may give at most one hab when receiving none, otherwise no more habs than it receives; at most two more orgs than it receives.\nFor each resource whose org/hab income decreases: current and resulting monthly income must be positive, and at least 60% must remain. Mission Control instead requires a resulting balance of at least zero.\nThese income checks concern transferred orgs/habs, not one-off resource payments.");
                ui.Show("AI benefit: " + TradeAcceptanceText.Number(aiScore) + "\nYour benefit: " + TradeAcceptanceText.Number(playerScore), result, tip.ToString());
            }
            catch (Exception e) { Fail(e); }
        }
        private static float[] Scores(TradeOffer.TradeAgreement agreement, TIFactionState scorer, TIFactionState other) => categories.Select(x => (float)categoryScore.Invoke(null, new object[] { agreement, x, scorer, other, net })).ToArray();
        private static List<string> Blockers(TradeOffer given, TradeOffer received, TIFactionState ai)
        {
            var blockers = new List<string>();
            string result = TradeAcceptanceText.CountBlocker(given.habs.Count, received.habs.Count, 0, 0);
            if (result != null) blockers.Add(result);
            result = TradeAcceptanceText.CountBlocker(0, 0, given.orgs.Count, received.orgs.Count);
            if (result != null) blockers.Add(result);
            foreach (var resource in TIResourcesCost.tradeableResources.Concat(new[] { FactionResource.MissionControl }))
            {
                float change = received.orgs.Sum(x => x.GetMonthlyIncome(resource)) - given.orgs.Sum(x => x.GetMonthlyIncome(resource));
                change += received.habs.Sum(x => x.GetNetCurrentMonthlyIncome(x.faction, resource, false, true)) - given.habs.Sum(x => x.GetNetCurrentMonthlyIncome(x.faction, resource, false, true));
                if (change >= 0) continue;
                bool mc = resource == FactionResource.MissionControl;
                result = TradeAcceptanceText.IncomeBlocker(resource.ToString(), change, mc ? ai.MissionControlBalance : ai.GetMonthlyIncome(resource, true), mc);
                if (result != null) blockers.Add(result);
            }
            return blockers;
        }
        private static void Fail(Exception e) { Reset(); failed = true; Main.Log(new InvalidOperationException("Trade breakdown disabled; native trading is unaffected.", e)); }
        internal static void Forget(TradeAcceptanceUi ui) { views.Remove(ui); }
        internal static void Reset()
        {
            harmony?.UnpatchAll(Owner); harmony = null;
            foreach (var ui in views.ToArray()) if (ui != null) ui.Cleanup();
            views.Clear(); TreatyTooltips.ResetAll(); failed = false;
        }
    }
}
