using System;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using UnityEngine;

namespace GiveUpNation
{
    internal static class ProspectingFiltersRuntime
    {
        private static readonly ProbeScanScheduler scheduler = new ProbeScanScheduler();
        private static Harmony harmony;
        private static bool failed;

        internal static void Update(float elapsed)
        {
            scheduler.Tick(Main.Enabled && !failed, elapsed, () => GameControl.loadcycle100
                && GameControl.control != null && GameControl.control.activePlayer != null, Discover, Main.Log);
        }

        private static void Discover()
        {
            try
            {
                foreach (var controller in UnityEngine.Object.FindObjectsOfType<IntelScreenController>())
                {
                    if (controller.filterProspected == null || !controller.filterProspected.gameObject.activeInHierarchy
                        || controller.spacebodyModels == null || controller.spacebodyModels.Count == 0) continue;
                    if (harmony == null)
                    {
                        harmony = new Harmony("Local.GiveUpNation.ProspectingFilters");
                        harmony.Patch(AccessTools.Method(typeof(IntelScreenController), "UpdateSpaceBodiesListVisibility"),
                            transpiler: new HarmonyMethod(typeof(ProspectingFilterPatch), "Transpiler"));
                    }
                    var ui = controller.GetComponent<ProspectingFiltersUi>();
                    if (ui == null)
                    {
                        ui = controller.gameObject.AddComponent<ProspectingFiltersUi>();
                        ui.Initialize(controller);
                    }
                    ui.Tick();
                }
            }
            catch (Exception exception)
            {
                failed = true;
                Cleanup();
                Main.Log(new InvalidOperationException("Prospecting filters disabled for this enabled session.", exception));
            }
        }

        internal static void Reset()
        {
            Cleanup();
            failed = false;
            scheduler.Reset();
        }

        private static void Cleanup()
        {
            // Nothing touches Intel controller types during initial mod startup.
            if (harmony == null) return;
            harmony.UnpatchAll(harmony.Id);
            harmony = null;
            foreach (var ui in Resources.FindObjectsOfTypeAll<ProspectingFiltersUi>())
            {
                try { ui.Cleanup(); }
                catch (Exception exception) { Main.Log(exception); }
            }
        }
    }
}
