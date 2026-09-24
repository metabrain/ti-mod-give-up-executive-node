# Additional Policies-tab button

Investigation date: 24 September 2026. Scope: identify how to add a button beside **Abandon Nation**, without installing a mod or launching the game.

This is the initial research record. The feature and prefab layout have since been implemented; see [implementation and validation notes](implementation.md) for current findings and remaining runtime checks.

## Evidence from the installed game

Read-only inspection of `G:\SteamLibrary\steamapps\common\Terra Invicta\TerraInvicta_Data\Managed\Assembly-CSharp.dll` using Python `dnfile` and `dncil` (installed under `/tmp`). SHA-256:

```text
4a4b9aae4154e444e9727204205d2d42ae8ed9e1c5f92cdc1280074a259d8350
```

This identifies the inspected assembly; the game build number was not independently established. No game files were modified. No game code was executed.

The relevant class is `PavonisInteractive.TerraInvicta.NationInfoController`.

| Member | Verified role |
| --- | --- |
| `disableControlPointsButton` | Public `UnityEngine.UI.Button` reference |
| `disableControlPointsButtonText` | Public `TMPro.TMP_Text` reference |
| `disbaleControlPointsTip` | Public `ModelShark.TooltipTrigger` reference; spelling is from the game |
| `policiesTabController` | Public `TabbedPaneController` reference |
| `nation` | Property for the currently displayed nation |
| `Initialize()` | Sets the original button label and tooltip |
| `AssignControlPoints()` | Sets the original button's `interactable` using `nation.CanDisableControlPoints(activePlayer)` |
| `UpdatePoliciesPanel()` | Rebuilds policy entries and calls `policiesTabController.SetSize(412f, 0f, 0f, optionCount)` |
| `UpdateNationPanel()` | The parameterless overload calls `UpdatePoliciesPanel()` and updates the active tab size |
| `ShowNationPanel(TIRegionState)` | Updates the selected nation and refreshes the panel when selection changes |
| `OpenSelfDisablePanel()` | Opens the existing abandon confirmation panel |
| `ConfirmSelfDisableControlPoints()` | Dispatches a `SelfDisableControlPoints` action through `Player.StartAction`, refreshes control points, and closes the confirmation |

`StreamingAssets/Localization/en/UINation.en:417` maps `UI.Nation.DisableControlPointsButton` to `Abandon Nation`.

Initialization does not construct this button or register its click callback in the inspected method. The controller holds an existing reference, consistent with prefab/scene wiring. The serialized callback and hierarchy have not yet been inspected.

## Proposed implementation

Use a C# Harmony code mod. A native JSON override alone does not provide the UI insertion identified here.

1. Postfix `NationInfoController.Initialize()` to create one uniquely named clone of `disableControlPointsButton.gameObject` under the same parent. Guard against duplicate creation per controller instance. Keep the original controller field pointing at the original button.
2. Replace the clone's `onClick` with a fresh `Button.ButtonClickedEvent`, then register the new action. Merely removing runtime listeners is insufficient protection against copied serialized listeners. Inspect any other copied event components before enabling it.
3. Set the cloned text and tooltip to the new action. Reuse the existing visual styling, with no new asset bundle required for this approach.
4. Position the clone beside the original. Inspect the parent layout first: use sibling ordering if a layout group controls positions; otherwise adjust `RectTransform` anchors, offsets, and widths deliberately. A clone initially shares the original geometry and can overlap it. Do not assume the `412f` tab-size argument is spare horizontal space.
5. Postfix `AssignControlPoints()` to refresh the new button's eligibility. Add a refresh on panel display/update if the action depends on state not covered by control-point updates. Read `controller.nation` at click time and recheck eligibility; do not capture the initially displayed nation.
6. Give the new action its own handler and, if appropriate, confirmation. The existing abandon action acts on the nation's control points and must not be reused blindly for an executive-only feature.
7. On mod disable/unload, remove the injected UI and restore any original layout properties changed by the mod. Unpatching alone does not destroy cloned objects. If enabling after initialization is supported, inject into the existing controller too.

Candidate hooks are verified to exist in this assembly, but the injection itself has not been built or tested. Resolve overloads explicitly where applicable.

## Remaining decisions and checks

- Define the button label, click behavior, eligibility, and confirmation text. The repository name suggests giving up the executive control point, but that behavior has not been specified or investigated here.
- Inspect the serialized UI hierarchy, persistent callbacks, and available horizontal room. Exact coordinates remain unknown.
- Build against installed assemblies as read-only references, with all outputs outside the game directory. Do not run the template's deployment/setup or runtime-test commands against this installation.
- Later, in an authorized test installation, check nation switching, repeated tab opening, control changes, disabled state, multiple resolutions/UI scales, controller recreation, mod toggling, and that the new button never invokes Abandon Nation.

## External reference

The community [UI modding example](https://github.com/TROYTRON/ti-mods/blob/main/tutorials/IntroToUI.md) demonstrates Harmony postfixes, cloning existing UI objects, and adjusting their parent layout. It supports the general approach; the nation-specific members above were identified directly from the local assembly.
