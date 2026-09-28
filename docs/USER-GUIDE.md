# Give up Nation — 0.3.1

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

This mod adds no campaign save fields. Ownership changes use native game state and will be saved normally. Disabling or removing the mod removes its UI but does not undo an already confirmed surrender. Existing/new campaigns are intended to work; save/load remains unverified.

Requires Terra Invicta and Unity Mod Manager 0.32.4 or later, with the Terra Invicta profile using `Mods/Enabled` and `ModInfo.json`. Compiled against the locally inspected installation; compatibility with other game builds is unverified.

**Build status:** v0.3.0 compiled with zero warnings/errors and 184 passing offline checks: 13 confirmation/action, 17 probe-tooltip data/startup, and 154 prospecting-filter checks. The user confirmed that the installed v0.3.0 filters work in-game. Exhaustive filter combinations, live status transitions, and enable/disable lifecycle checks remain unverified. The user confirmed the v0.2.1 date tooltip in-game; the user also confirmed v0.2.2 days remaining works. The v0.2.3 inline formatting awaits in-game verification. After the 0.1.1 popup-initialization fix, the user reported that the UI works in-game. Executive-release side effects and save/load still need verification. The game installation remains read-only except for explicitly authorized installation/update of this mod; the build tool never deploys to it.

Created using https://github.com/Laurentiu-Andronache/ti-mod-template

Version 0.2.1 fixes the v0.2.0 startup exception by attaching probe tooltips after game loading completes, without patching the probe list refresh method. A full restart is required after updating from the failed version.

Version 0.2.2 adds a `Days remaining` line using the scheduled arrival minus `TITimeState.Now()` via `TIDateTime.DifferenceInDays`. Partial days round up; due or overdue arrivals show zero. The existing hover/half-second refresh recalculates this from campaign time. This added label is English; the original two lines retain native localization.

Version 0.2.3 places the remaining time after the date, for example `Survey Completion: 04 September 2008 (225.5 days)`. It shows one decimal place instead of rounding up to whole days. The `days` suffix is English.
