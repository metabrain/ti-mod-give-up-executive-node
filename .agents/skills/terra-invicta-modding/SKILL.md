---
name: terra-invicta-modding
description: Investigate, implement, build, package, and troubleshoot Terra Invicta native or C# Harmony/Unity Mod Manager mods using installed-game evidence. Use for new mod features, UI controls, game-state actions, and compatibility fixes.
---

# Terra Invicta modding

Deliver a reproducible mod and distinguish compiled, offline-tested, observed in-game, and user-reported results. Use the installed game's data, assembly metadata/IL, and serialized UI as the source of truth. Historical API names are leads, not contracts.

## Start with scope and authorization

- Read repo guidance and Git status. Identify the game directory, OS/shell, current implementation, and the requested behavior. Define eligibility, confirmation, exact state change, failure cases, and intended save compatibility before changing gameplay.
- Treat game inspection as read-only. Keep tools, extracted information, build output, and backups in the workspace's ignored local directories or temporary storage. Never redistribute game assemblies or extracted proprietary assets/source.
- Preserve session authorization: an explicit request to install this mod authorizes that scoped installation, not arbitrary game changes. Do not infer installation, game launch, save mutation, publication, or GitHub push from a request to build. Do not re-request authorization already given, except where the execution environment requires escalation.
- Prefer native JSON/localization for exposed data changes; use C# Harmony/UMM for UI or runtime behavior; use hybrid only when both are needed.

## Workflow and reference routing

1. **Inspect the current build.** Read [inspection.md](references/inspection.md) for assembly fingerprints, localization searches, metadata/IL, asset-bundle hierarchy inspection, and temporary tooling. Trace callers, overload visibility, state bookkeeping, and event updates before selecting a method.
2. **Choose the UI lifecycle and action.** For UI or state-changing work read [ui-and-actions.md](references/ui-and-actions.md). Preserve copied visuals while replacing copied callbacks; validate confirmed actions against current state.
3. **Build and package locally.** Read [build-and-delivery.md](references/build-and-delivery.md). Use read-only installed references, deterministic local outputs, an explicit package allowlist, and tests of meaningful behavior. Inspect third-party scaffold setup before running it: it may deploy to the game.
4. **Validate only within authorized scope.** Offline tests do not prove Unity lifecycle, rendering, game events, or save/load. If runtime testing is unauthorized, finish the source/build/package and report precisely what remains untested. If authorized, exercise the actual button/handler on a disposable save and collect the first relevant log failure.
5. **Install or update when requested.** Follow the narrow deployment procedure in the build reference. Confirm the game is closed before replacing a loaded DLL; back up only the owned mod files, preserve unrelated files, and verify installed bytes. Stop on unexpected local changes or ambiguous destination instead of overwriting broadly.
6. **Retain the evidence.** Record assembly fingerprint, tool versions, source dependencies, build command, tests, package contents, and remaining limitations. Commit/push when requested, excluding proprietary and generated content; verify the push and final Git status.

For the concrete executive-release example and the real initialization failure that shaped this workflow, read [give-up-nation-case.md](references/give-up-nation-case.md). Do not make executive-release rules, folder paths, or that example's layout universal requirements.
