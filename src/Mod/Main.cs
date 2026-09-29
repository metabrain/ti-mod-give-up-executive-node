using System;
using System.Reflection;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using UnityEngine;
using UnityModManagerNet;

namespace GiveUpNation
{
    // Lifecycle adapted from the ti-mod-template UMM starter; see third-party notices.
    public static class Main
    {
        internal static bool Enabled;
        private static Harmony harmony;
        private static UnityModManager.ModEntry entry;
        private static float alienReadoutScanRemaining;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            entry = modEntry;
            harmony = new Harmony(entry.Info.Id);
            entry.OnToggle = Toggle;
            entry.OnUnload = e => Toggle(e, false);
            entry.OnUpdate = (e, elapsed) =>
            {
                ProbeArrivalRuntime.Update(elapsed);
                ProspectingFiltersRuntime.Update(elapsed);
                alienReadoutScanRemaining -= elapsed;
                if (alienReadoutScanRemaining <= 0)
                {
                    alienReadoutScanRemaining = 1f;
                    foreach (var controller in Resources.FindObjectsOfTypeAll<GeneralControlsController>())
                    {
                        if (!controller.gameObject.scene.IsValid()) continue;
                        AlienHateDebugReadout.Attach(controller);
                    }
                    foreach (var readout in Resources.FindObjectsOfTypeAll<AlienHateDebugReadout>()) readout.UpdateValue();
                }
            };
            return true;
        }

        private static bool Toggle(UnityModManager.ModEntry modEntry, bool enabled)
        {
            if (Enabled == enabled) return true;
            try
            {
                if (enabled)
                {
                    harmony.PatchAll(Assembly.GetExecutingAssembly());
                    Enabled = true;
                    ProbeArrivalRuntime.Reset();
                    ProspectingFiltersRuntime.Reset();
                    alienReadoutScanRemaining = 0;
                    foreach (var controller in Resources.FindObjectsOfTypeAll<NationInfoController>())
                        if (controller.gameObject.scene.IsValid()) Attach(controller);

                }
                else
                {
                    Enabled = false;
                    ProbeArrivalRuntime.Reset();
                    ProspectingFiltersRuntime.Reset();
                    alienReadoutScanRemaining = 0;
                    foreach (var ui in Resources.FindObjectsOfTypeAll<GiveUpNationUi>()) ui.Cleanup();
                    foreach (var tooltip in Resources.FindObjectsOfTypeAll<ProbeArrivalTooltip>()) tooltip.Cleanup();
                    foreach (var readout in Resources.FindObjectsOfTypeAll<AlienHateDebugReadout>()) readout.Cleanup();
                    harmony.UnpatchAll(entry.Info.Id);
                }
                return true;
            }
            catch (Exception exception)
            {
                Enabled = false;
                ProbeArrivalRuntime.Reset();
                ProspectingFiltersRuntime.Reset();
                alienReadoutScanRemaining = 0;
                harmony.UnpatchAll(entry.Info.Id);
                foreach (var ui in Resources.FindObjectsOfTypeAll<GiveUpNationUi>()) ui.Cleanup();
                foreach (var tooltip in Resources.FindObjectsOfTypeAll<ProbeArrivalTooltip>()) tooltip.Cleanup();
                foreach (var readout in Resources.FindObjectsOfTypeAll<AlienHateDebugReadout>()) readout.Cleanup();
                Log(exception);
                return false;
            }
        }

        internal static void Log(Exception exception) { entry.Logger.Error(exception.ToString()); }
        internal static void Attach(NationInfoController controller)
        {
            GiveUpNationUi ui = null;
            try
            {
                if (!Enabled || controller == null || controller.disableControlPointsButton == null
                    || controller.GetComponent<GiveUpNationUi>() != null) return;
                ui = controller.gameObject.AddComponent<GiveUpNationUi>();
                ui.Initialize(controller);
            }
            catch (Exception exception) { Log(exception); if (ui != null) ui.Cleanup(); }
        }
    }

    [HarmonyPatch(typeof(NationInfoController), nameof(NationInfoController.Initialize))]
    internal static class InitializePatch
    {
        private static void Postfix(NationInfoController __instance) { Main.Attach(__instance); }
    }

    // Integrate with the game's Escape/secondary-panel lifecycle.
    [HarmonyPatch(typeof(NationInfoController), nameof(NationInfoController.CloseAnySecondaryPanels))]
    internal static class ClosePanelsPatch
    {
        private static void Postfix(NationInfoController __instance, GameObject exceptPanel, ref bool __result)
        {
            try
            {
                var ui = __instance.GetComponent<GiveUpNationUi>();
                if (ui != null && ui.CloseUnless(exceptPanel)) __result = true;
            }
            catch (Exception exception) { Main.Log(exception); }
        }
    }
}
