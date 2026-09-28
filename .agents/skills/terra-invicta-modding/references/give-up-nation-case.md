# Worked case: Give up Nation

This case records inspected evidence from September 2026, not a permanent Terra Invicta API guarantee. The installed game build/DLC combination was not independently established.

Assembly-CSharp SHA-256: `4a4b9aae4154e444e9727204205d2d42ae8ed9e1c5f92cdc1280074a259d8350`.

## Discovery and gameplay semantics

Searching `UINation.en` for “Abandon Nation” found `UI.Nation.DisableControlPointsButton`. The controller is `PavonisInteractive.TerraInvicta.NationInfoController`:

| Member | Inspected role |
| --- | --- |
| `disableControlPointsButton` | Public Unity UI Button |
| `disableControlPointsButtonText` | Public TMP_Text |
| `disbaleControlPointsTip` | Public TooltipTrigger; actual misspelling |
| `Initialize()` | Sets labels/tooltips; candidate insertion postfix |
| `AssignControlPoints()` | Refreshes original abandon eligibility |
| `nation`, `activePlayer` | Current selection and faction |
| `confirmDisableControlPointPanel` and associated text fields | Existing popup to clone |
| `CloseAnySecondaryPanels(exceptPanel, allowGeneric)` | Secondary-window dismissal, including Escape integration |

The original abandon path is `ConfirmSelfDisableControlPoints` → `Player.StartAction(SelfDisableControlPoints)` → `TINationState.SelfDisableControlPoints(faction)` → voluntary `ResolveCrackdownEffect` on every owned point. It retains ownership. This is unsuitable for an executive-only ownership release.

`TIControlPoint.SetFaction(null, false)` supports an unowned point, but the higher-level operation provides more bookkeeping. The **public index overload** of `TINationState.ChangeControlPointOwner(index, cause, faction)` was used with a null faction. The point-object overload was private. The native method handles defense/crackdown, priorities, type/name, faction holdings, income cache invalidation, ownership events, and executive history. Native government-change/coup paths also pass null.

`RemoveControlPointFromNation()` removes the actual point from the nation and game state; it is not an ownership-release operation.

The feature checks extant ordinary nation, non-null executive, and executive faction == active player. Alien nations are excluded because the native change-owner method substitutes AlienFaction. It snapshots the original executive and revalidates at confirmation and execution, preventing replacement-target and double-execution errors. `ControlPointChangeCause.None` was selected because no voluntary-surrender member existed and alien transfer causes expected a receiving faction. Its downstream side effects remain a runtime-test concern.

## Layout and failure recovery

The serialized `ui` bundle showed the original 218×30 abandon button and auto-renew toggle at the bottom of `PolicyTabPane`. The parent had no LayoutGroup. `Policy Container` reserved 43 px below its list. The implementation placed two half-width buttons at y=40..70, preserved the auto-renew row, and reserved 35 additional pixels beneath the policy list. It restored original rect values on cleanup.

Version 0.1.0 compiled successfully and passed 13 action/confirmation checks against stand-ins, but the button did not appear. UMM marked the mod Active while logging `NullReferenceException` in UI initialization. The popup was deliberately inactive; default `GetComponentInParent<Button>()` returned null for the confirmation text's inactive button ancestors. Cleanup then removed the injected button, making the symptom look like a positioning issue.

Version 0.1.1 used `GetComponentInParent<Button>(true)` for both buttons, validated results, rebuilt, backed up the owned installed files, and updated only this mod with the game closed. The user subsequently reported that it worked. This supports user-reported UI success, not independently observed executive-release/save-load correctness.

The reusable lessons are to inspect logs before guessing layout, test Unity lifecycle separately from pure action logic, include inactive ancestors during inactive-popup wiring, replace persistent click events, and validate the exact entity again before a confirmed mutation.
