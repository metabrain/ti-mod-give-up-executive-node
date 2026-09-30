using ModelShark;
using PavonisInteractive.TerraInvicta;
using UnityEngine;
using System.Collections.Generic;

namespace GiveUpNation
{
    internal static class ProbeArrivalRuntime
    {
        private static readonly ProbeScanScheduler scheduler = new ProbeScanScheduler();
        private static readonly List<IntelSpaceBodyListItemController> rows = new List<IntelSpaceBodyListItemController>();

        internal static void Reset() { scheduler.Reset(); rows.Clear(); IntelUiCache.Reset(); }

        internal static void Update(float elapsed)
        {
            scheduler.Tick(Main.Enabled, elapsed, Ready, DiscoverRows, Main.Log);
        }

        private static bool Ready()
        {
            // Reading the readiness flag does not force AssetCacheManager's static
            // constructor. Do not reference that type or patch row.Refresh here.
            return GameControl.loadcycle100 && GameControl.control != null
                && GameControl.control.activePlayer != null && TooltipManager.Instance != null;
        }

        private static void DiscoverRows()
        {
            // Only active scene objects, not serialized prefabs. Initialized rows
            // have a body and native tooltip style; new rows are found next tick.
            var controller = IntelUiCache.OpenController();
            if (controller == null || controller.spacebodyListAdapter == null) return;
            controller.spacebodyListAdapter.GetComponentsInChildren(false, rows);
            foreach (var row in rows)
            {
                if (row.spaceBody == null || row.prospectTooltip == null
                    || row.prospectTooltip.tooltipStyle == null) continue;
                if (row.GetComponent<ProbeArrivalTooltip>() == null) ProbeArrivalTooltip.Attach(row);
            }
            rows.Clear();
        }
    }
}
