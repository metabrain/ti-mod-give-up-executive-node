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
        private static ProspectingFiltersUi ownedUi;

        internal static void Update(float elapsed)
        {
            scheduler.Tick(Main.Enabled && !failed, elapsed, () => GameControl.loadcycle100
                && GameControl.control != null && GameControl.control.activePlayer != null, Discover, Main.Log);
        }

        private static void Discover()
        {
            try
            {
                var controller = IntelUiCache.OpenController();
                if (controller != null)
                {
                    if (controller.filterProspected == null || !controller.filterProspected.gameObject.activeInHierarchy
                        || controller.spacebodyModels == null || controller.spacebodyModels.Count == 0) return;
                    if (harmony == null)
                    {
                        harmony = new Harmony("Local.GiveUpNation.ProspectingFilters");
                        harmony.Patch(AccessTools.Method(typeof(IntelScreenController), "UpdateSpaceBodiesListVisibility"),
                            transpiler: new HarmonyMethod(typeof(ProspectingFilterPatch), "Transpiler"));
                    }
                    var ui = controller.GetComponent<ProspectingFiltersUi>();
                    if (ownedUi != null && ownedUi != ui) ownedUi.Cleanup();
                    if (ui == null)
                    {
                        ui = controller.gameObject.AddComponent<ProspectingFiltersUi>();
                        ownedUi = ui;
                        ui.Initialize(controller);
                    }
                    ownedUi = ui;
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
            if (ownedUi != null)
            {
                try { ownedUi.Cleanup(); }
                catch (Exception exception) { Main.Log(exception); }
            }
            ownedUi = null;
        }
    }
}
