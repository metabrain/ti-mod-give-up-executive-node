# Give up Nation implementation

## Agreed behavior

A Policies-tab button labelled **Give up Nation** releases only the player-owned executive control point after final confirmation. Other points are unchanged. No cost, temporary crackdown, or new campaign state is introduced. Ordinary human nations are supported; the game's alien-nation override prevents releasing alien executive points.

## Implementation and provenance

The net48 project and UMM lifecycle are adapted from `Laurentiu-Andronache/ti-mod-template`, commit `394dd0ca817d9a9709a13f3bb6fc9607d143cadc`. We use a narrow local build/package wrapper instead of that template's setup/deployment tooling to respect the strict read-only installation rule and allow building under WSL. See the included license and third-party notices.

Installed `Assembly-CSharp.dll` SHA-256: `4a4b9aae4154e444e9727204205d2d42ae8ed9e1c5f92cdc1280074a259d8350`. Installed UMM assembly version: 0.32.4.0. Actual game build/DLC combination has not been independently established.

`NationInfoController.Initialize` is postfixed to attach the UI. Enabling in an existing campaign also checks loaded scene controllers. The original button is cloned, all copied onClick listeners are replaced, and the cloned tutorial target is removed. The original button callback is untouched. A separate clone of the existing abandon confirmation panel receives new text and new confirm/cancel events.

Read-only inspection of the `ui` asset bundle verified the original button and auto-abandon toggle are separate, bottom-anchored children of `PolicyTabPane`, with no parent layout group. Each is 218 by 30 pixels. The policy list occupies `Policy Container`, which reserves 43 pixels below it. Both action buttons now use half-width anchors at y=40..70; the checkbox stays on the original row. The policy container reserves an additional 35 pixels. Original rect properties are restored on cleanup.

A transient `ReleaseRequest` captures the selected nation, faction, and executive point. Confirm and action execution validate ownership again, reject a replaced executive, and prevent duplicate execution. The public index overload of `TINationState.ChangeControlPointOwner(index, cause, faction)` is called with a null faction. The point overload is private in this installed build.

`ControlPointChangeCause.None` is used because the inspected enum has no voluntary-surrender member. This avoids falsely recording a coup/trade and avoids the Enthrall/Terrorize branches that require a receiving faction. The native method clears defense/crackdown, restores initial priorities, updates faction holdings and income caches, emits ownership events, and records `lastExecutiveChange`. Consequences of the neutral cause still require runtime validation.

The inspected `Player.StartAction` directly calls `Execute`. The mod follows that action entry point with a transient action, not a new serialized game-state type. The method's normal ownership bookkeeping is retained; the control point itself is never deleted.

## Offline validation and limitations

Version 0.1.0 loaded in the user's game but failed during UI initialization. The UMM log reported a null reference in `GiveUpNationUi.Initialize`; IL inspection located the failing confirm-button lookup. The cloned dialog is inactive during setup, so the default `GetComponentInParent<Button>()` skips its inactive button ancestors. Version 0.1.1 explicitly includes inactive ancestors and validates both references before binding listeners. After installing 0.1.1 and restarting, the user reported that it works. This is user-reported UI validation; executive-release side effects and save/load have not been independently verified.

- Release build references the installed game/Unity/UMM/Harmony assemblies, all with `Private=false`.
- Thirteen executable checks compile the production `ReleaseRequest.cs` against small game stand-ins. They cover eligibility, no mutation before confirmation, executive-only targeting, cancellation, stale selections, faction changes, lost ownership, replaced executive, nulls, alien/extinct nations, duplicate execution, and execution-time revalidation. These do not validate Unity or the game's internal side effects.
- The ZIP is verified against an explicit five-file allowlist and CRCs. It contains only the mod DLL, metadata, user guide, and notices/license.
- Build/package hashes are recorded in ignored `artifacts/build-evidence.json`.

## Remaining runtime acceptance checklist

1. Verify both buttons, checkbox, scroll area, and confirmation fit at supported resolutions/UI scales; keyboard Escape and Cancel close without mutation.
2. Check player-owned, rival-owned, unowned, and cracked-down executives. Owning only non-executive points must not enable the button.
3. Confirm and verify only the executive becomes unowned. Check faction CP cap/income, nation executive UI, priorities, attached armies, diplomacy, and events/logs.
4. Open the popup, then switch nation/faction, lose or replace the executive, close the panel, or change tabs; ensure a stale confirmation cannot execute.
5. Verify Abandon Nation and Auto-Renew Abandon retain their original behavior.
6. Reopen screens repeatedly; enable/disable/re-enable the mod; start a second campaign; ensure no duplicates or retained old references.
7. Save/load after a release, then reload with the mod absent. Confirm normal game ownership state persists without requiring custom mod types.

Do not run these checks against the protected installation without a change in authorization. Building and packaging do not launch or modify the game.
