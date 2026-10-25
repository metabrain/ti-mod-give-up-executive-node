using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GiveUpNation
{
    internal static class ProspectingTravelRuntime
    {
        private static readonly ProbeScanScheduler scheduler = new ProbeScanScheduler();
        private static readonly TravelSnapshot<TISpaceFleetState, TISpaceBodyState> snapshot =
            new TravelSnapshot<TISpaceFleetState, TISpaceBodyState>();
        private static readonly List<IntelSpaceBodyListItemController> rows = new List<IntelSpaceBodyListItemController>();
        private static readonly List<IntelHabSiteListItemController> siteRows = new List<IntelHabSiteListItemController>();
        private static readonly HashSet<ProspectingTravelRow> ownedRows = new HashSet<ProspectingTravelRow>();
        private static Harmony harmony;
        private static IntelScreenController controller;
        private static Canvas intelCanvas;
        private static TabbedPaneController displayedTab;
        private static TIFactionState player;
        private static TISpaceFleetState selectedFleet;
        private static TMP_Text header;
        private static ProspectingTravelRow columnHeader;
        private static bool ascending = true;
        private static Stopwatch calculationTimer;
        private static string calculationDate;
        private static int calculationErrors;
        private static bool wasOpen, failed;

        internal static void Update(float elapsed)
        {
            if (!Main.Enabled || failed) return;
            try
            {
                if (!GameControl.loadcycle100 || GameControl.control == null || GameControl.control.activePlayer == null)
                {
                    ClearCampaign();
                    return;
                }
                var currentPlayer = GameControl.control.activePlayer;
                if (!ReferenceEquals(player, currentPlayer)) { ClearCampaign(); player = currentPlayer; }
                // Retain the last explicitly selected player fleet when opening Intel deselects it.
                var selected = GeneralControlsController.UISelectedAssetState;
                var fleet = selected as TISpaceFleetState ?? (selected as TISpaceShipState)?.fleet;
                if (fleet != null && fleet.faction == player && !fleet.deleted && !fleet.archived)
                    selectedFleet = fleet;
                if (selectedFleet != null && (selectedFleet.deleted || selectedFleet.archived || selectedFleet.faction != player))
                { selectedFleet = null; snapshot.Clear(); }
                bool open = IsOpen();
                if (!open && wasOpen) snapshot.Clear();
                wasOpen = open;
                if (snapshot.IsRunning)
                {
                    if (!snapshot.IsFor(selectedFleet) || OpenTab() != displayedTab) snapshot.Clear();
                    else StepCalculation();
                }
                scheduler.Tick(true, elapsed, () => true, Discover, Main.Log);
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static TabbedPaneController OpenTab()
        {
            if (controller == null || !controller.gameObject.activeInHierarchy
                || (intelCanvas != null && !intelCanvas.isActiveAndEnabled)) return null;
            if (controller.habSiteTab != null && controller.habSiteTab.IsSelected
                && controller.habSite_filterNameInputField.gameObject.activeInHierarchy) return controller.habSiteTab;
            if (controller.spaceBodyTab != null && controller.spaceBodyTab.IsSelected
                && controller.filterProspected.gameObject.activeInHierarchy) return controller.spaceBodyTab;
            return null;
        }

        private static bool IsOpen() => OpenTab() != null;

        private static void Discover()
        {
            try
            {
                var current = IntelUiCache.FindController();
                if (current == null) return;
                if (controller != current)
                {
                    RemoveHeader();
                    snapshot.Clear();
                    controller = current;
                    intelCanvas = current.GetComponent<Canvas>();
                }
                var tab = OpenTab();
                if (tab == null) return;
                if (displayedTab != tab)
                {
                    RemoveHeader(); snapshot.Clear(); displayedTab = tab;
                }
                bool sites = tab == controller.habSiteTab;
                if (sites ? controller.habSiteModels == null || controller.habSiteModels.Count == 0
                    : controller.spacebodyModels == null || controller.spacebodyModels.Count == 0) return;
                if (harmony == null)
                {
                    harmony = new Harmony("Local.GiveUpNation.ProspectingTravel");
                    harmony.Patch(AccessTools.Method(typeof(IntelSpaceBodyListItemController), "Initialize"),
                        postfix: new HarmonyMethod(typeof(ProspectingTravelRuntime), nameof(RowRefreshed)));
                    harmony.Patch(AccessTools.Method(typeof(IntelSpaceBodyListItemController), "Refresh"),
                        postfix: new HarmonyMethod(typeof(ProspectingTravelRuntime), nameof(RowRefreshed)));
                    harmony.Patch(AccessTools.Method(typeof(IntelHabSiteListItemController), "Initialize"),
                        postfix: new HarmonyMethod(typeof(ProspectingTravelRuntime), nameof(SiteRefreshed)));
                    harmony.Patch(AccessTools.Method(typeof(IntelHabSiteListItemController), "Refresh"),
                        postfix: new HarmonyMethod(typeof(ProspectingTravelRuntime), nameof(SiteRefreshed)));
                    harmony.Patch(AccessTools.Method(typeof(IntelScreenController), "UpdateSpaceBodiesListVisibility"),
                        postfix: new HarmonyMethod(typeof(ProspectingTravelRuntime), nameof(BodyVisibilityChanged)));
                    harmony.Patch(AccessTools.Method(typeof(IntelScreenController), "UpdateHabSitesListVisibility"),
                        postfix: new HarmonyMethod(typeof(ProspectingTravelRuntime), nameof(SiteVisibilityChanged)));
                    harmony.Patch(AccessTools.Method(typeof(TabbedPaneController), "Show"),
                        postfix: new HarmonyMethod(typeof(ProspectingTravelRuntime), nameof(TabShown)));
                }
                EnsureHeader();
                if (selectedFleet == null)
                    header.text = "Travel: select one of your fleets, then open Prospecting";
                else if (!snapshot.IsFor(selectedFleet))
                {
                    header.text = "Travel: " + selectedFleet.displayName + " | click the Travel column to calculate and sort";
                    columnHeader.SetText("Travel (days | km/s)");
                }
                RefreshVisibleRows(sites);
                wasOpen = true;
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void RefreshVisibleRows(bool sites)
        {
            if (sites)
            {
                controller.habSiteListAdapter.GetComponentsInChildren(false, siteRows);
                for (int i = 0; i < siteRows.Count && !failed; i++) SiteRefreshed(siteRows[i]);
                siteRows.Clear();
            }
            else
            {
                controller.spacebodyListAdapter.GetComponentsInChildren(false, rows);
                for (int i = 0; i < rows.Count && !failed; i++) RowRefreshed(rows[i]);
                rows.Clear();
            }
        }

        private static void SortRequested()
        {
            if (!Main.Enabled || failed || !GameControl.loadcycle100 || OpenTab() != displayedTab) return;
            try
            {
                if (selectedFleet == null || selectedFleet.deleted || selectedFleet.archived || selectedFleet.faction != player)
                { header.text = "Travel: select one of your fleets first"; return; }
                InvalidateChangedDestinations();
                if (snapshot.IsRunning) return;
                if (!snapshot.IsFor(selectedFleet)) { StartCalculation(); return; }
                ascending = !ascending;
                ApplySort();
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void ApplySort()
        {
            if (displayedTab == controller.habSiteTab)
            {
                var sorted = controller.habSiteModels.OrderBy(m => SortKey(m.IntelScreenHabSiteListItemData.habSiteState?.parentBody)).ToList();
                controller.habSiteModels.Clear(); controller.habSiteModels.AddRange(sorted);
                controller.UpdateHabSiteListModelData();
            }
            else
            {
                var sorted = controller.spacebodyModels.OrderBy(m => SortKey(m.IntelScreenSpacebodyListItemData.spacebodyState)).ToList();
                controller.spacebodyModels.Clear(); controller.spacebodyModels.AddRange(sorted);
                controller.UpdateSpaceBodyListModelData();
            }
            columnHeader.SetText("Travel " + (ascending ? "^" : "v") + " (days | km/s)");
            scheduler.Reset();
        }

        private static double SortKey(TISpaceBodyState body) =>
            TravelEstimate.SortKey(body == null ? null : snapshot.Get(body), ascending);

        private static TISpaceBodyState[] VisibleDestinations() => displayedTab == controller.habSiteTab
            ? TravelDestinations.Select(controller.habSiteModels,
                m => m.IntelScreenHabSiteListItemData != null && m.IntelScreenHabSiteListItemData.showInList,
                m => m.IntelScreenHabSiteListItemData.habSiteState?.parentBody)
            : TravelDestinations.Select(controller.spacebodyModels,
                m => m.IntelScreenSpacebodyListItemData != null && m.IntelScreenSpacebodyListItemData.showInList,
                m => m.IntelScreenSpacebodyListItemData.spacebodyState);

        private static void BodyVisibilityChanged(IntelScreenController __instance) => VisibilityChanged(__instance, false);
        private static void SiteVisibilityChanged(IntelScreenController __instance) => VisibilityChanged(__instance, true);
        private static void VisibilityChanged(IntelScreenController current, bool sites)
        {
            if (!Main.Enabled || failed || controller != current || header == null
                || displayedTab != (sites ? current.habSiteTab : current.spaceBodyTab)) return;
            try { InvalidateChangedDestinations(); }
            catch (Exception exception) { Fail(exception); }
        }

        private static void InvalidateChangedDestinations()
        {
            if (!snapshot.IsFor(selectedFleet) || snapshot.MatchesDestinations(VisibleDestinations())) return;
            snapshot.Clear();
            calculationTimer?.Stop();
            header.text = "Travel: filters changed | click the Travel column to calculate";
            columnHeader.SetText("Travel (days | km/s)");
            RefreshVisibleRows(displayedTab == controller.habSiteTab);
            scheduler.Reset();
        }

        private static void StartCalculation()
        {
            var fleet = selectedFleet;
            calculationDate = TITimeState.Now().ToCustomDateString();
            calculationTimer = Stopwatch.StartNew();
            calculationErrors = 0;
            ascending = true;
            var bodies = VisibleDestinations();
            Main.Info("Travel calculation started for " + (displayedTab == controller.habSiteTab ? "Prospecting" : "Solar System")
                + "; " + fleet.displayName + ".");
            snapshot.Start(fleet, bodies);
            if (snapshot.Total == 0)
            {
                header.text = "Travel: no destinations available";
                return;
            }
            header.text = "Travel: " + fleet.displayName + " | calculating 0/" + snapshot.Total + " | click here to cancel";
            columnHeader.SetText("Travel (calculating)");
            RefreshVisibleRows(displayedTab == controller.habSiteTab);
        }

        private static void StepCalculation()
        {
            snapshot.Step(CalculateBody);
            header.text = "Travel: " + selectedFleet.displayName + " | calculating " + snapshot.Count + "/" + snapshot.Total
                + " | click here to cancel";
            RefreshVisibleRows(displayedTab == controller.habSiteTab);
            if (failed) return;
            if (snapshot.IsRunning) return;
            calculationTimer.Stop();
            header.text = "Travel: " + selectedFleet.displayName + " | started " + calculationDate + " | click here to clear";
            Main.Info("Prospecting travel: " + snapshot.Count + " destinations in "
                + calculationTimer.Elapsed.TotalSeconds.ToString("0.000") + " s across frames; " + calculationErrors + " errors.");
            ApplySort();
        }

        private static TravelEstimate CalculateBody(TISpaceBodyState body)
        {
            var fleet = selectedFleet;
            // The campaign can advance between frames. Pair each arrival with its own departure time.
            double seconds = TITimeState.Now().ToJulianDateInSeconds();
            if (fleet.inTransfer) return new TravelEstimate("Fleet in transfer");
            if (body.orbits == null || body.orbits.Count == 0) return new TravelEstimate("No destination orbit");
            try
            {
                TravelEstimate best = null;
                double minimum;
                // Native body targeting resolves to its lowest orbit. Only calculate; never assign a transfer.
                MasterTransferPlanner.RequestTrajectories(fleet, body, 20, trajectories =>
                {
                    foreach (var route in trajectories)
                    {
                        if (route == null || route.arrivalTime == null) continue;
                        best = TravelEstimate.Faster(best,
                            (route.arrivalTime.ToJulianDateInSeconds() - seconds) / 86400.0,
                            route.DV_kps, fleet.currentDeltaV_mps / 1000.0);
                    }
                }, out minimum, false, false, 1.0);
                return best ?? new TravelEstimate("No route found");
            }
            catch (Exception exception)
            {
                if (calculationErrors++ == 0) Main.Log(new InvalidOperationException("Prospecting route calculation failed for " + body.displayName, exception));
                return new TravelEstimate("Calculation failed");
            }
        }

        private static void EnsureHeader()
        {
            if (header != null) return;
            bool sites = displayedTab == controller.habSiteTab;
            var parent = sites
                ? controller.habSite_filterNameInputField.transform.parent.parent as RectTransform
                : controller.filterProspected.transform.parent as RectTransform;
            // The existing filter feature frees the upper-left row for this readout.
            var filters = controller.GetComponent<ProspectingFiltersUi>();
            if ((!sites && (filters == null || !filters.Ready)) || parent == null)
                throw new InvalidOperationException("Prospecting travel requires the initialized filter bar.");
            var go = new GameObject("GiveUpNation.ProspectingTravelHeader", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(Button));
            go.transform.SetParent(parent, false);
            go.layer = controller.filterProspected.gameObject.layer;
            header = go.GetComponent<TextMeshProUGUI>();
            header.font = controller.filterProspectedText.font;
            header.fontSize = 13;
            header.color = new Color(.65f, .85f, 1f);
            header.alignment = TextAlignmentOptions.MidlineLeft;
            header.textWrappingMode = TextWrappingModes.NoWrap;
            header.overflowMode = TextOverflowModes.Ellipsis;
            header.richText = false;
            var rect = header.rectTransform;
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0, .5f);
            rect.anchoredPosition = new Vector2(8, -17);
            rect.sizeDelta = new Vector2(-780, 30);
            var button = go.GetComponent<Button>();
            button.targetGraphic = header;
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => { snapshot.Clear(); scheduler.Reset(); });
            columnHeader = controller.gameObject.AddComponent<ProspectingTravelRow>();
            columnHeader.Initialize(sites ? controller.habSiteHeaderDescription : controller.spacebodyHeaderMiningDescription,
                sites ? null : controller.spacebodyHeaderDescription, SortRequested);
            Main.Info("Travel header attached to " + (sites ? "Prospecting" : "Solar System") + ".");
        }

        private static void TabShown(TabbedPaneController __instance)
        {
            if (controller != null && (controller.spaceBodyTab == __instance || controller.habSiteTab == __instance))
            { snapshot.Clear(); scheduler.Reset(); }
        }

        private static void RowRefreshed(IntelSpaceBodyListItemController __instance)
        {
            if (__instance != null) RefreshRow(__instance.gameObject, __instance.description2, __instance.spaceBody, __instance.description);
        }

        private static void SiteRefreshed(IntelHabSiteListItemController __instance)
        {
            if (__instance != null) RefreshRow(__instance.gameObject, __instance.siteDescription, __instance.site?.parentBody);
        }

        private static void RefreshRow(GameObject rowObject, TMP_Text description, TISpaceBodyState body, TMP_Text preceding = null)
        {
            if (!Main.Enabled || failed || header == null || rowObject == null || description == null
                || rowObject.GetComponentInParent<IntelScreenController>() != controller) return;
            try
            {
                var estimate = body == null || !snapshot.IsFor(selectedFleet) ? null : snapshot.Get(body);
                var ui = rowObject.GetComponent<ProspectingTravelRow>();
                if (ui == null)
                {
                    ui = rowObject.AddComponent<ProspectingTravelRow>();
                    ownedRows.Add(ui);
                    ui.Initialize(description, preceding);
                }
                if (ui != null) ui.SetText(estimate?.Text);
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void ClearCampaign()
        {
            if (player == null && selectedFleet == null && !wasOpen) return;
            snapshot.Clear(); selectedFleet = null; player = null; wasOpen = false;
            if (header != null) header.text = string.Empty;
            foreach (var row in ownedRows) if (row != null) row.SetText(null);
            scheduler.Reset();
            ownedRows.RemoveWhere(row => row == null);
        }

        private static void RemoveHeader()
        {
            if (columnHeader != null) columnHeader.Cleanup();
            columnHeader = null;
            if (header != null) { header.gameObject.SetActive(false); UnityEngine.Object.Destroy(header.gameObject); }
            header = null;
        }

        private static void Fail(Exception exception)
        {
            Reset(); failed = true;
            Main.Log(new InvalidOperationException("Prospecting travel disabled for this enabled session.", exception));
        }

        internal static void Reset()
        {
            if (harmony != null) harmony.UnpatchAll(harmony.Id);
            harmony = null;
            RemoveHeader();
            foreach (var row in ownedRows) if (row != null) row.Cleanup();
            ownedRows.Clear(); rows.Clear(); siteRows.Clear(); displayedTab = null;
            controller = null; intelCanvas = null; player = null; selectedFleet = null; wasOpen = failed = false;
            snapshot.Clear(); scheduler.Reset();
            calculationTimer = null; calculationDate = null;
        }
    }
}
