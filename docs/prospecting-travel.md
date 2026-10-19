# Prospecting travel column — v0.3.14

## Installed-game evidence

The originally inspected Assembly-CSharp SHA-256 was `4a4b9aae4154e444e9727204205d2d42ae8ed9e1c5f92cdc1280074a259d8350`; the current build fingerprint is recorded in `artifacts/build-evidence.json`.

`MasterTransferPlanner.RequestTrajectories` performs synchronous calculation and calls back with candidate trajectories. Body targeting resolves to the orbit with minimum `semiMajorAxis_km`. `Trajectory.arrivalTime` and `DV_kps` provide paired results. The quick time estimator does not supply matching delta-v. This feature never assigns a transfer.

The installed English localization explicitly distinguishes `UI.Intel.SpaceBodies=Solar System` from `UI.Intel.HabSiteTabText=Prospecting`. Version 0.3.13 targeted only Solar System. The user confirmed that its readout appeared after a long delay; the log measured 372 destinations in 22.643 seconds, with zero errors. That is not a performance measurement of v0.3.14.

Both virtualized lists are supported now: `spacebodyModels` / `IntelSpaceBodyListItemController` and `habSiteModels` / `IntelHabSiteListItemController`. Prospecting targets each site's `parentBody`, deduplicating bodies so multiple sites share one calculation. Delayed owned postfixes on each row's Initialize/Refresh perform cache lookups only. Updating the adapters submits the models in their current order, allowing travel sorting without changing game state.

## UI and lifecycle

UnityPy 1.25.0 inspection found 28-unit rows. The Solar System description and mining-profile columns are 250 and 255 units wide; each gives 80 units to the new 160-unit Travel column. The Prospecting site-description column gives 160 of its 358 units. Header buttons and row cells receive the same width/position changes, leaving downstream resource columns fixed. Cleanup restores original rectangles, wrapping and overflow. New header buttons have only owned click callbacks. The fleet/progress line occupies unused space at the left of the filter bar; the right 780 units remain available for native controls.

Opening either tab creates a blank Travel column and does no route solving. Clicking its header queues distinct destinations. Each UMM update performs at most one destination calculation on Unity's main thread, publishing that result immediately and updating visible rows. The display shows completed/total counts. On completion the list sorts ascending by travel days; later clicks reverse the cached sort. Missing/unavailable routes sort last in either direction. Sorting waits until completion to keep rows from jumping while values appear. Native column sorting remains available.

The native solver is synchronous: one expensive route can still delay a frame. No background thread accesses Unity/game objects. Closing/switching the tab, changing fleet, loading another campaign, clearing results or disabling the mod cancels the remaining queue. Clicking the fleet/progress line cancels or clears; another Travel-header click is required to restart. Campaign/faction identity guards prevent stale results crossing saves. The last selected player fleet is retained when opening Intel deselects it; selected ships resolve to their fleet.

## Route semantics

For each body the solver requests 20 candidates, with placeholders and early-success stopping disabled and sample multiplier 1. Choose the earliest arrival within current fleet delta-v, breaking ties by lower delta-v. Reject non-finite/negative candidates. The displayed days and delta-v belong to the same route; days include pre-departure waiting. Each body's duration is measured against that calculation's game time, since the campaign may advance between frames. Results are the fastest found route to lowest orbit, not guaranteed global optima, station rendezvous, landings or round trips. A transferring fleet is reported unavailable in this version.

Individual solver exceptions become cached error results, with the first exception logged once per run. Unexpected queue failures clear partial state. The total elapsed time includes the frames between calculations. No orders, fuel mutations, new save data or automatic recalculation loops are introduced by the travel feature.

## Verification and remaining checks

Offline tests cover paired route choice, affordability, invalid numbers, zero-cost routes, sorting directions, unavailable-last ordering, idle/blank access without solver calls, one result per step, deduplication, incremental publication, cancellation, fleet replacement, completed-cache reuse and failure cleanup. Release/package verification uses the repository build wrapper; its evidence file records hashes. Tests do not execute the live Unity solver or UI.

Live acceptance: confirm a separate column in both tabs without automatic calculation; click Travel and watch partial results/progress; compare a few entries with native transfers to the same orbit; verify final ascending and cached descending sorting, scrolling/filtering, cancellation and fleet/save changes. Check UI scaling, column/header alignment, and disable/re-enable restoration. The package also includes previously committed hate-history persistence, whose live acceptance is separate.
