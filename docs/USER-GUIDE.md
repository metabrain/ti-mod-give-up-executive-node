# Give up Nation — 0.3.14

Select one of your fleets (or a ship in it), then open **Intel → Prospecting** or **Solar System**. The separate **Travel (days | km/s)** column stays blank until you click its header. That click queues the destinations; one body is calculated per frame and its values appear immediately. Sites on the same body share a result. The list sorts by shortest travel time once calculation finishes; subsequent header clicks reverse the sort using cached results. Unavailable routes stay at the bottom. Opening the page, scrolling and filtering do not start calculations.

The fleet/status line above the table shows progress. Click it to cancel or clear results, then click the Travel column to calculate again. Closing or switching tabs, changing the selected fleet, loading a campaign or disabling the mod cancels remaining work and clears the old results. Advancing time or changing fuel/ships does not automatically recalculate. Each route uses the game time and fleet performance when that body is calculated; pause the game if you want consistent departure conditions across the list.

Figures use the fastest route found by the native solver within the fleet's current delta-v, to the body's lowest orbit. Days include any wait before departure. They describe one outbound journey, not a round trip or a particular station/base. `No route found` means this solver search found no affordable route, not proof that no trajectory is possible. Fleets already in transfer show `Fleet in transfer`. No transfer orders are issued. A single native route calculation can still cause a short hitch, but the whole list is no longer calculated in a single frame. Destination count and elapsed time are written to the mod log. Live v0.3.14 layout, sorting and performance await testing.

The third red line, `Q 0.000`, shows raw global alien quietness to three decimals: 1 is maximum quietness; 0 satisfies the game's "go loud" condition. It uses the native AI calculation and shares the hate counters' two-second refresh. Quietness is not specific to your faction or a guarantee against attacks.

Hover over the red alien-hate number to see the latest 12 recorded changes, including campaign date, the game's reason, requested change, and actual before/after values. Up to 100 changes are retained and included in saves made with v0.3.12 enabled. Loading restores that save's history after the campaign is ready; loading an older save rolls back the history too. Saves without history start empty. Switching player faction or disabling the mod clears the current in-memory history. Recording begins once the campaign is ready; unrecorded past events and changes to the calculated minimum are not reconstructed. The tooltip refreshes when hovered again. Generic game reasons such as "Effect" are shown as supplied.

The history is an optional `Local.GiveUpNation` section inside the normal save, including compressed saves. Missing, unsupported, or malformed history is ignored without rejecting the game save. Loading without the mod ignores the section; saving again without the mod discards it. This compatibility has been checked with the installed serializer and synthetic saves; live campaign save/load testing is still pending for v0.3.12.

Version 0.3.9 reduces recurring UI discovery and repeated prospecting checks. The red alien-hate value and `(>XX)` minimum retain their two-second refresh; they clear while a campaign is loading. Disabling the mod removes its readout without removing the game's HUD.

Adds **Give up Nation** beside **Abandon Nation** in the nation's Policies tab. The existing Auto-Renew Abandon checkbox remains on the row below.

The button is enabled when your faction owns that nation's executive control point. Pressing it opens a final confirmation. Confirming makes only the executive point unowned; your other points remain yours. This is an ownership loss, not a temporary crackdown. Regaining the executive requires normal gameplay.

Cancel makes no changes. Changing the selected nation, losing ownership, leaving the Policies tab, or closing the nation panel invalidates an open confirmation. Alien nations are excluded because the game's ownership-change operation does not permit their points to become unowned.

In **Intel → Solar System**, hover the in-flight probe icon to see its scheduled survey completion date. The native launch-cost and faster-replacement-probe tooltips remain unchanged. The date uses the game's localization and calendar formatting, and disappears when no probe event is scheduled.

The Solar System screen also has three status checkboxes on a second filter row:

- **Filter Prospected**: survey completed for your faction.
- **Filter Unprospected**: survey incomplete, with no probe en route or fleet surveying.
- **Filter Prospectable**: survey incomplete and idle, with the game's native probe-target eligibility satisfied.
- **Filter Prospecting**: survey incomplete, with your probe en route or your fleet actively surveying.

Check multiple boxes to include those statuses together. Check none to show all statuses. For example, Unprospected + Prospectable + Prospecting shows everything not yet completed. Location, faction, and text filters still apply. Status changes are checked once a second while the screen is open; the three added selections reset when the current faction changes or the mod is disabled. They are not saved in campaigns. The three added labels are English.

These are display filters. **Probe All** retains its native target selection and may include bodies hidden by your filters.

Travel snapshots and filter selections are not saved. The optional hate-history section is described above. Ownership changes use native game state and will be saved normally. Disabling or removing the mod removes its UI but does not undo an already confirmed surrender.

Requires Terra Invicta and Unity Mod Manager 0.32.4 or later, with the Terra Invicta profile using `Mods/Enabled` and `ModInfo.json`. Compiled against the locally inspected installation; compatibility with other game builds is unverified.

**Build status:** v0.3.0 compiled with zero warnings/errors and 184 passing offline checks: 13 confirmation/action, 17 probe-tooltip data/startup, and 154 prospecting-filter checks. The user confirmed that the installed v0.3.0 filters work in-game. Exhaustive filter combinations, live status transitions, and enable/disable lifecycle checks remain unverified. The user confirmed the v0.2.1 date tooltip in-game; the user also confirmed v0.2.2 days remaining works. The v0.2.3 inline formatting awaits in-game verification. After the 0.1.1 popup-initialization fix, the user reported that the UI works in-game. Executive-release side effects and save/load still need verification. The game installation remains read-only except for explicitly authorized installation/update of this mod; the build tool never deploys to it.

Created using https://github.com/Laurentiu-Andronache/ti-mod-template

Version 0.2.1 fixes the v0.2.0 startup exception by attaching probe tooltips after game loading completes, without patching the probe list refresh method. A full restart is required after updating from the failed version.

Version 0.2.2 adds a `Days remaining` line using the scheduled arrival minus `TITimeState.Now()` via `TIDateTime.DifferenceInDays`. Partial days round up; due or overdue arrivals show zero. The existing hover/half-second refresh recalculates this from campaign time. This added label is English; the original two lines retain native localization.

Version 0.2.3 places the remaining time after the date, for example `Survey Completion: 04 September 2008 (225.5 days)`. It shows one decimal place instead of rounding up to whole days. The `days` suffix is English.

### Trade acceptance breakdown (0.3.19)

The negotiation screen now shows AI benefit, your benefit, and any hard restrictions beneath the AI response. Hover anywhere over **the breakdown or the leader’s response** for category valuations, distrust, and a numbered list of every violated hard restriction with values and limits. The tooltip also lists the full set of hard rules, even when none are violated. These are internal scores, not money or acceptance probabilities. Hard restrictions explain hab/org limits, income loss, and Mission Control deficits. Untouched AI offers retain the game's special acceptance rule.

The breakdown refreshes when the game evaluates the offer, with no periodic scoring. It does not change trade decisions or saves. The AI offer list is shortened to make room; disabling the mod restores it. Build and offline checks passed; live layout, hover, reopening, and disable/re-enable still require in-game testing.

Violated trade restrictions also appear as a numbered list below the benefits. The area expands for multiple restrictions; scroll over it when the list exceeds the available space. The same list remains in the tooltip.

When no hard restrictions are violated, the display collapses to the two benefit lines and returns the unused space to the offer list.

Travel calculations respect all active filters in Solar System and Prospecting. Changing the matching destinations clears the current calculation; click Travel again to calculate the new filtered list.

### Treaty hover conditions (0.3.21)

Hover over non-aggression pacts, truces, intelligence sharing or intelligence exchange in either trade bank or the offer table to see native descriptions plus eligibility and strategic-willingness checks. Informational rows remain available when a treaty is unavailable, including when eligibility passes but willingness fails. Checks show hate limits, strategic jealousy, faction-war goals/enemies, ideology, victory conditions, spies, intelligence partners and AI self-assessment. These expose internal information for debugging; treaty decisions are unchanged. Diagnostics are calculated on hover, with no periodic scoring. Disable/unload restores owned tooltip delegates and informational-row visibility. Live hover, layout and reopening tests remain necessary.

### Scuttle propellant recovery (0.3.22)

Scuttling at your own shipyard now recovers 100% of the resources represented by remaining propellant, alongside the native construction refund (normally25%). It uses exactly the native shipyard eligibility, including its inactive-yard allowance; refuelling-only facilities and foreign stations do not qualify. Burned propellant is not refunded. Native resource conversion applies current faction modifiers and the ship's fuel mix. Preview and execution share the same calculation, and the scuttle action description reflects the refund. No new save data is added. Live preview/payment validation is pending.

### Scuttle unloading delay (0.3.23)

At an eligible own shipyard, ships with remaining propellant now wait for unloading before scuttling and paying the refund. Duration scales with fuel remaining, station resupply capacity, and officer resupply modifiers. Selected ships unload sequentially, with a time estimate in the selection panel; the fleet is occupied until the pending operations finish or are canceled. Empty ships and scuttling elsewhere remain immediate. Canceling preserves the ship and its fuel, with no refund. Pending orders use native saved fleet operations; disabling the mod cancels currently loaded orders. Loading a pending order without the mod lets it expire harmlessly. Restart after installation. Live timing, cancellation, and save/reload still require in-game testing.
