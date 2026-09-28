# Prospecting filters — issue #2

Request: https://github.com/metabrain/ti-mod-give-up-executive-node/issues/2

Version 0.3.0 adds Filter Unprospected and Filter Prospecting to Intel → Solar System. The original Filter Prospected is retained. Selected statuses are ORed together; no selection includes all statuses. Existing location, faction, and name/resource/description filters continue to narrow the result. These controls change only list visibility, not surveys, probe launches, or saves.

## Installed-game evidence

Inspected `Assembly-CSharp.dll` SHA-256: `4a4b9aae4154e444e9727204205d2d42ae8ed9e1c5f92cdc1280074a259d8350`. This is the same assembly as the probe-tooltip investigation; the hash alone does not establish a game version or DLC set.

- `IntelScreenController.UpdateSpaceBodiesListVisibility` resets each model's `showInList`, excludes non-prospected bodies when `filterProspected.isOn`, then applies faction, location, and text filters.
- `UpdateSpaceBodySort` calls visibility filtering before `UpdateSpaceBodyListModelData`, which submits models to the virtualized list adapter. Filtering visible GameObjects afterward would miss hidden and recycled rows.
- `UpdateProspectedFilter` plays the native toggle sound and invokes the normal sort/refresh path. New checkbox callbacks use that method.
- `TIFactionState.Prospected(TISpaceBodyState)` tests faction intel >= 1. `ProspectorEnRoute` tests intel >= 0.1 and not already prospected. `FleetSurveyingPlanet` examines that faction's current fleet operations for `SurveyPlanetFromFleetOperation` targeting the body. Completed knowledge takes priority over any remaining survey operation.
- `SetProbeAllButton` gets targets from `LaunchAllProbeOperation`, independently of list visibility. This feature does not alter that operation.

Read-only serialized UI inspection used UnityPy 1.25.0. All followed pointers were local to the same serialized file (`m_FileID == 0`). The native toggle is in a 34-unit-high top-anchored Upper Container, alongside three right-anchored search/dropdown controls totaling approximately 762 units. Its label is the root TextMeshProUGUI referenced by `filterProspectedText`, with a ContentSizeFitter and 24/8-unit text margins. Its child Label is disabled. The toggle has no ToggleGroup; its persistent callback targets `UpdateProspectedFilter`. Other scripts are UITextStyle and UIToggleFeedback, with no tutorial component on the cloned subtree. The Lower Container stretches beneath the filter bar and contains the list adapter.

## Implementation

`ProspectingFiltersRuntime` uses the existing throttled scheduler after game readiness and discovers initialized, active Intel screens. It installs a separate Harmony patch only then, avoiding startup patching of Intel types. The patch class has no automatic HarmonyPatch attribute. A failure disables this optional feature for the enabled session and cleans up its controls/patch, independently of the nation button and tooltip.

The transpiler replaces exactly the native toggle getter and the body-specific `Prospected` call inside the visibility method with controller-aware helpers. It preserves native branch targets and the remaining filters. It requires exactly one matching gate and predicate in the expected order; an unexpected method shape rejects the patch. Controllers without a ready attached UI and a disabled mod retain native behavior.

`ProspectingFiltersUi` clones the native toggle twice under an inactive staging parent, replaces the entire `onValueChanged` event, and clears group membership and selection before activation. The original toggle and callback remain intact. The container grows by 34 units, existing mid-anchored controls are compensated to retain their position, and the list's top moves down by 34 units while its bottom stays fixed. All three status controls sit together on the new row; their spacing follows actual/preferred label widths. The layout is checked before mutation, saved, and restored on cleanup.

While the Solar System screen is active and any status is selected, a once-per-second scan compares statuses for all models, including hidden bodies. A change reruns visibility and updates the adapter without changing sort selection. Current-player changes clear the additional selections and cached statuses. Reopening the same screen preserves the selections. Disabling removes owned UI/listeners, restores geometry and native filtering, and clears transient state. No save fields are introduced.

## Build and offline validation

Tools: .NET SDK 8.0.425, dnfile 0.18.0, dncil 1.0.2, UnityPy 1.25.0. Added OSA.Core as a read-only build reference to access the native adapter's transform; it is not packaged.

```bash
DOTNET_CLI_HOME=/tmp/ti-dotnet-home NUGET_PACKAGES=/tmp/ti-nuget \
python3 tools/build.py --dotnet /tmp/ti-dotnet/dotnet \
  --game-dir '/mnt/g/SteamLibrary/steamapps/common/Terra Invicta'
```

The Release build passed with zero warnings/errors. All 184 checks passed: 13 executive-action, 17 probe-tooltip/scheduler, and 154 new filter checks. The filter suite links the production status mapping and transpiler against game stand-ins and the installed Harmony library. It emits and executes rewritten fixture IL across all eight selections and all eight underlying status-flag combinations, checks native filter exclusions, launch/completion/fleet cancellation/player changes, disabled/unattached/partial-UI fallback, and rejection of missing or duplicate patch sites. It does not execute the real game method or Unity UI.

The archive is `artifacts/Local.GiveUpNation-0.3.0.zip`, containing only ModInfo.json, Local.GiveUpNation.dll, README.md, LICENSE, and THIRD_PARTY_NOTICES.md under the mod folder. The wrapper checks its allowlist and CRC and writes assembly/mod/package hashes to `artifacts/build-evidence.json`. The package was subsequently installed at the user’s request after they confirmed the game was closed. All five installed files were verified against the package; the previous v0.2.3 files were backed up under `artifacts/backups/20260928T203345.953763Z/Local.GiveUpNation`. Unrelated files were preserved. The user then reported that the new filters work in-game. This is user-reported confirmation, not an observed execution of every acceptance case below.

## Remaining runtime acceptance checks

- Open the Solar System tab and check the second row at normal and high UI scales; check labels, search/dropdowns, list header, scrolling, and tooltip alignment.
- Exercise all eight checkbox combinations with examples of completed, untouched, probe-underway, and fleet-only surveying bodies; combine with faction, location, text, and sort controls.
- Launch a probe, complete a survey, cancel a fleet survey, and confirm visible/hidden rows move into or out of the selected statuses within the next scan.
- Close/reopen, switch tabs, load another campaign, and disable/re-enable; verify no duplicate controls, stale faction data, or geometry changes remain after disabling.
- Confirm the nation button, probe arrival tooltip, native Filter Prospected, and Probe All retain their behavior. Probe All is independent of displayed rows.

New labels are English. Static prefab inspection and offline tests do not prove final runtime layout, Unity lifecycle, interaction with other UI mods, or compatibility with another game assembly.
