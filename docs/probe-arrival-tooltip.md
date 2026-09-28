# In-flight probe arrival tooltip — issue #1

Request: https://github.com/metabrain/ti-mod-give-up-executive-node/issues/1

## Installed-code evidence

The screenshot targets the in-flight probe status icon in **Intel → Solar System**. `IntelSpaceBodyListItemController.Refresh()` displays `prospectedIcon` with the `prospectingUnderway` sprite while hiding `orderProspecting` when a probe is en route and no faster replacement is available. That branch does not assign an arrival tooltip to the status icon. The native `prospectTooltip` is used for launching and replacing probes.

`TIFactionState.ProspectorArrival(spaceBody)` checks whether a probe is en route, looks up the scheduled LaunchProbeOperation completion event, and falls back to LaunchOverrideProbeOperation. Its return value can be null. The native replacement-probe tooltip uses this method and `TIDateTime.ToCustomDateString()`.

The mod reuses `UI.Space.ProspectorEnRoute` and `UI.Space.ProbeArrival` (English: “Survey Completion: {0}”). It does not recalculate travel time or change operations/saves.

Inspected Assembly-CSharp SHA-256: `4a4b9aae4154e444e9727204205d2d42ae8ed9e1c5f92cdc1280074a259d8350`.

## Implementation

UMM OnUpdate waits for `GameControl.loadcycle100`, a current player, and TooltipManager before discovering initialized active rows once per second. Each row gets one owned component; the probe feature no longer patches any game method. Already-created rows are discovered through the same runtime path. A transparent, raycastable child of `prospectedIcon` hosts a new `TooltipTrigger` using the native tooltip style and basic appearance settings. Transparent mesh culling is disabled for this hover surface so it remains a raycast target. The original launch button, click events, tooltip fields/delegates, and icon settings are untouched.

The surface is active only while the icon is visible, the launch/replacement button is hidden, and the current player has a scheduled probe arrival for that row's current body. It reads the current body/player at hover time instead of capturing a recycled row's original data. A half-second check on each active row clear outdated data and update an open tooltip after schedule changes. The surface is disabled with its row and removed on mod disable/cleanup. Initialization failures are logged once per affected component.

This ships in the existing Local.GiveUpNation package as v0.2.2; the existing executive-release feature remains available.

## Validation

Release compilation uses installed game assemblies as read-only references. Nine offline checks link the production text provider against game stand-ins: null player/body, no scheduled event, scheduled-date formatting, rescheduling, row reuse, faction switching, completed survey, and localization. The original 13 executive-action checks still run. These checks do not validate actual Unity pointer events, serialized prefab geometry, the game's scheduler, or other mods.

The v0.2.0 Release build completed with zero warnings/errors. All 22 offline checks passed; the archive passed its allowlist and CRC checks. These were offline results before the subsequent installation and startup failure described below.

Runtime checks still required:

- Hover a launched probe in the Solar System list; compare the date to its space-body panel.
- Check launch-cost and faster-replacement-probe tooltips and buttons still behave normally.
- Complete a survey and check the old arrival tooltip disappears; verify ship surveying without a probe does not invent a date.
- Scroll, filter, reopen the list, load another campaign, and toggle the mod; check no stale dates/duplicate hover targets.
- Check the game language's labels/date format and relevant UI scales.

No game installation files are written by the build or offline checks.

## Startup fix in v0.2.1

The user's v0.2.0 UMM log reported a Harmony patching exception for `IntelSpaceBodyListItemController.Refresh`, with an inner `AssetCacheManager` type-initialization exception. Startup patching forced asset initialization before the loader was ready, and the shared activation catch disabled both features.

The probe `Refresh` patch has been removed entirely. Only the previously working nation-controller patches remain in startup PatchAll. Probe discovery now runs through a guarded UMM update callback after readiness, and discovery errors are isolated/logged once per enabled session rather than escaping into mod activation. Visible row components continue checking eligibility even before a hover surface exists, covering later probe launches and recycled rows.

Seven additional offline scheduler checks cover disabled/startup gating, throttling, failure isolation/recovery, log suppression, campaign loading transitions, and re-enable. The user subsequently confirmed successful startup and pointer-hover behavior in v0.2.1; their issue screenshot shows the native-styled arrival tooltip. A failed static constructor stays failed for the process lifetime, so testing the fix requires a full game restart.

Version 0.2.2 adds a `Days remaining` line using the scheduled arrival minus `TITimeState.Now()` via `TIDateTime.DifferenceInDays`. Partial days round up; due or overdue arrivals show zero. The existing hover/half-second refresh recalculates this from campaign time. This added label is English; the original two lines retain native localization.

The v0.2.2 build passed all 30 offline checks (13 executive-action and 17 probe data/startup checks), with zero compiler warnings/errors. Its five mod files were installed and verified after backing up v0.2.1. User runtime testing of the days-remaining line is underway.
