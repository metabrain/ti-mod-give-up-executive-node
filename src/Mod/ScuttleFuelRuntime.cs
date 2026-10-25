using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
namespace GiveUpNation
{
    internal static class ScuttleFuelRuntime
    {
        private const string Owner = "Local.GiveUpNation.ScuttleFuel";
        private static Harmony harmony;
        private static bool failed, reported;
        private static OperationCanvasController preview;
        internal static void Update(float elapsed)
        {
            if (!Main.Enabled || failed || harmony != null || !GameControl.loadcycle100) return;
            try
            {
                harmony = new Harmony(Owner);
                harmony.Patch(AccessTools.Method(typeof(TISpaceShipState), nameof(TISpaceShipState.ScuttleCost)),
                    postfix: new HarmonyMethod(typeof(ScuttleFuelRuntime), nameof(Refund)));
                harmony.Patch(AccessTools.Method(typeof(TIOperationTemplate), "GetDescription"),
                    postfix: new HarmonyMethod(typeof(ScuttleFuelRuntime), nameof(Description)));
                Patch(typeof(ScuttleShipsOperation), "ScuttleShipsFromFleet", nameof(Schedule), true);
                Patch(typeof(ScuttleShipsOperation), "ExecuteOperation", nameof(Complete), true);
                Patch(typeof(ScuttleShipsOperation), "GetOperationTiming", nameof(Timing));
                Patch(typeof(ScuttleShipsOperation), "IsBlockingOperation", nameof(Blocking));
                Patch(typeof(OperationCanvasController), "UpdateSplitFleetPanel", nameof(Preview));
                Patch(typeof(SplitFleetShipListItemController), "SetListItem", nameof(Row));
                foreach (var method in new[] { "CanCancel", "CancelUponCombat", "CancelUponDepartHab" })
                    Patch(typeof(TISpaceFleetOperationTemplate), method, nameof(ScuttleFlag));
            }
            catch (Exception e) { Reset(); failed = true; Main.Log(e); }
        }
        private static void Patch(Type type, string method, string callback, bool prefix = false)
        {
            var hook = new HarmonyMethod(typeof(ScuttleFuelRuntime), callback);
            harmony.Patch(AccessTools.Method(type, method), prefix: prefix ? hook : null, postfix: prefix ? null : hook);
        }
        private static bool Schedule(TISpaceFleetState fleet, List<TISpaceShipState> shipsToScuttle)
        {
            if (!Main.Enabled) return true;
            try { return ScuttleDelay.Schedule(fleet, shipsToScuttle); }
            catch (Exception e) { Main.Log(e); return false; } // Never fall back to immediate destruction after a queue error.
        }
        private static bool Complete(TIGameState actorState, TIGameState target)
        {
            if (Main.Enabled) try { ScuttleDelay.Complete(actorState, target); } catch (Exception e) { Main.Log(e); }
            return false;
        }
        private static void Timing(ref OperationTiming __result)
        {
            __result = OperationTiming.DelayedExecutionOfInstantEffect;
        }
        private static void Blocking(ref bool __result) { __result = true; }
        private static void ScuttleFlag(TISpaceFleetOperationTemplate __instance, ref bool __result)
        {
            if (__instance is ScuttleShipsOperation) __result = true;
        }
        private static void Refund(TISpaceShipState __instance, ref TIResourcesCost __result)
        {
            if (!Main.Enabled || failed) return;
            try
            {
                var result = new TIResourcesCost(ScuttleFuelRefund.Calculate(__instance, __result));
                result.SetCompletionTime_Days(ScuttleDelay.Duration(__instance));
                __result = result;
            }
            catch (Exception e) { if (!reported) { reported = true; Main.Log(e); } }
        }
        private static void Description(TIOperationTemplate __instance, ref string __result)
        {
            if (!Main.Enabled || failed || __instance.GetType().Name != "ScuttleShipsOperation") return;
            __result = "Scuttle ships from this fleet. At your own shipyard, recover "
                + (TemplateManager.global.scuttleRefund * 100).ToString("0.#")
                + "% of construction resources and 100% of the resources represented by remaining propellant. "
                + "Ships remain docked while fuel unloads at the station refuelling rate; selected ships unload sequentially. "
                + "Cancel the fleet operation to keep the ship and fuel. Recovery happens only on completion. "
                + "Elsewhere, no propellant is recovered. Native crew evacuation and Boost costs still apply.";
        }
        private static string TimeText(float days)
        {
            return days >= 1 ? days.ToString("0.##") + " days" : (days * 24).ToString("0.##") + " hours";
        }
        private static void Preview(OperationCanvasController __instance, List<TISpaceShipState> ___newFleetShips)
        {
            if (!Main.Enabled || !(__instance.SelectedOperation is ScuttleShipsOperation)) return;
            try
            {
                preview = __instance;
                float days = ___newFleetShips.Sum(ScuttleDelay.Duration);
                __instance.newFleetText.SetText(Loc.T("UI.Operations.ShipsToScuttle")
                    + (days > 0 ? " (" + TimeText(days) + ")" : ""));
            }
            catch (Exception e) { if (!reported) { reported = true; Main.Log(e); } }
        }
        private static void Row(SplitFleetShipListItemController __instance, TISpaceShipState ship, bool includeScuttleCost)
        {
            if (!Main.Enabled || !includeScuttleCost) return;
            try
            {
                float days = ScuttleDelay.Duration(ship);
                if (days <= 0) return;
                // Native resource formatting has an hours conversion bug when costs accompany the time.
                var cost = ship.ScuttleCost();
                __instance.scuttleCost.SetText(cost.GetString("N1", false, false, false) + " / " + TimeText(days));
            }
            catch (Exception e) { if (!reported) { reported = true; Main.Log(e); } }
        }
        internal static void Reset()
        {
            if (harmony != null && GameControl.loadcycle100)
            {
                // Cancel while our timing hook is still installed, so native cancellation removes time events.
                foreach (var fleet in GameStateManager.GetAllGameStates<TISpaceFleetState>())
                    foreach (var pending in fleet.CurrentOperations().Where(o => o.operation is ScuttleShipsOperation))
                        try { fleet.CancelOperation(pending); } catch (Exception e) { Main.Log(e); }
            }
            if (preview != null && preview.SelectedOperation is ScuttleShipsOperation)
                preview.newFleetText.SetText(Loc.T("UI.Operations.ShipsToScuttle"));
            preview = null;
            harmony?.UnpatchAll(Owner); harmony = null; failed = reported = false;
        }
    }
}
