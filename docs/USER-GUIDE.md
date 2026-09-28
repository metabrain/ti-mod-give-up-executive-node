# Give up Nation — 0.2.2

Adds **Give up Nation** beside **Abandon Nation** in the nation's Policies tab. The existing Auto-Renew Abandon checkbox remains on the row below.

The button is enabled when your faction owns that nation's executive control point. Pressing it opens a final confirmation. Confirming makes only the executive point unowned; your other points remain yours. This is an ownership loss, not a temporary crackdown. Regaining the executive requires normal gameplay.

Cancel makes no changes. Changing the selected nation, losing ownership, leaving the Policies tab, or closing the nation panel invalidates an open confirmation. Alien nations are excluded because the game's ownership-change operation does not permit their points to become unowned.

In **Intel → Solar System**, hover the in-flight probe icon to see its scheduled survey completion date. The native launch-cost and faster-replacement-probe tooltips remain unchanged. The date uses the game's localization and calendar formatting, and disappears when no probe event is scheduled.

This mod adds no campaign save fields. Ownership changes use native game state and will be saved normally. Disabling or removing the mod removes its UI but does not undo an already confirmed surrender. Existing/new campaigns are intended to work; save/load remains unverified.

Requires Terra Invicta and Unity Mod Manager 0.32.4 or later, with the Terra Invicta profile using `Mods/Enabled` and `ModInfo.json`. Compiled against the locally inspected installation; compatibility with other game builds is unverified.

**Build status:** compiled with 13 passing offline confirmation/action checks and 17 probe-tooltip data/startup checks. The user confirmed the v0.2.1 date tooltip in-game; v0.2.2 days-remaining testing is underway. After the 0.1.1 popup-initialization fix, the user reported that the UI works in-game. Executive-release side effects and save/load still need verification. The game installation remains read-only except for explicitly authorized installation/update of this mod; the build tool never deploys to it.

Created using https://github.com/Laurentiu-Andronache/ti-mod-template

Version 0.2.1 fixes the v0.2.0 startup exception by attaching probe tooltips after game loading completes, without patching the probe list refresh method. A full restart is required after updating from the failed version.

Version 0.2.2 adds a `Days remaining` line using the scheduled arrival minus `TITimeState.Now()` via `TIDateTime.DifferenceInDays`. Partial days round up; due or overdue arrivals show zero. The existing hover/half-second refresh recalculates this from campaign time. This added label is English; the original two lines retain native localization.
