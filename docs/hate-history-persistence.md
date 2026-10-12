# Hate history in saves — v0.3.12

The save contains an optional `Local.GiveUpNation` object with `version: 1`, the owning faction's stable game-state ID as `factionId`, and up to 100 `hateHistory` entries in oldest-first order. Each entry contains `date`, `cause`, `requested`, `before`, and `after`. All values are plain JSON; there are no mod CLR type names or new game-state objects. Hate, quietness, and the hate floor remain calculated by the game and are not restored from this section.

## Installed-game evidence

`GameStateManager.SaveAllGameStates` constructs `SaveStructure`, calls `StringSerializationAPI.Serialize`, then writes the result through `fsJsonPrinter.PrettyJson`. Compression wraps the same output in a GZip stream. `SaveStructure.Load` reads plain text or decompresses `.gz`, then calls `StringSerializationAPI.Deserialize`. `GameStateManager.LoadAllGameStates` installs the resulting native state only on success. `SolarSystemBootstrap.LoadGame` clears old state before dispatching the load to a worker thread.

`SaveStructure` has `currentID` and `gamestates` fields, not a dedicated mod-data field. The embedded FullSerializer exposes `fsObjectProcessor` and `fsSerializer.AddProcessor`/`RemoveProcessor`. Its reflected converter reads known writable properties; unknown root fields are ignored and are not retained on a subsequent unmodified save. The private `StringSerializationAPI._serializer` is accessed once at activation. Its initializer only creates the serializer, and `GameStateManager`'s initializer constructs its ID/dictionaries; neither inspected initializer accesses AssetCache.

## Lifecycle and failure handling

- Register the processor before the first campaign load; scope it specifically to `SaveStructure` and an actual save/load operation. Preview deserialization cannot restore history.
- Capture an independent history snapshot at the start of native saving. Add the fully built extension only after native serialization succeeds. A history error leaves native fields intact and is logged; native saving still controls its own success/failure.
- Stage parsed history in a thread-local load frame. The finalizer commits it only if the native load returns true without an exception. The finalizer returns void so it does not replace the game's result or swallow its exception.
- Hand the snapshot across threads under the history lock. Restore only when campaign readiness and the active player's faction ID match. Until then, hate recording is suspended during the native load. Missing/rejected data restores an empty history, never the previous campaign's history.
- Clear old state on a new load/campaign. Epochs reject completion from a superseded load, campaign clear, or mod disable/re-enable. Per-call hate capture generations reject stale before/after pairs.
- Schema parsing is all-or-nothing. Reject unsupported versions, missing/wrong field types, more than 100 entries, oversized strings, NaN/infinity, and numbers that overflow a float. Persisted text is bounded (512 characters for causes, 128 for dates/IDs). Additional unknown fields within schema 1 are tolerated.
- Disable/unload removes only this mod's patches and processor and clears runtime history. A later save without the mod drops the unknown history section. No companion files or campaign-save edits occur during installation.

## Offline verification

The completed v0.3.12 verification run passed all 663 standard offline checks (13 action, 17 probe, 581 prospecting/history, 52 persistence). The same 52 persistence checks also passed under Windows .NET Framework with real Harmony fixture detours. Release builds had zero warnings/errors. Installed `Assembly-CSharp.dll` SHA-256: `4a4b9aae4154e444e9727204205d2d42ae8ed9e1c5f92cdc1280074a259d8350`. The built mod DLL SHA-256 is `dce0f3b5d3a5af1521f3b5e012e6610e8eb5ef397c53f3e034bb6d726c3d2c0c`.

The standard build runs `tests/HatePersistence` on .NET 8 against the installed game's **real FullSerializer, StringSerializationAPI, SaveStructure and SaveStructure.Load**. The test data are disposable synthetic save structures with empty native game-state dictionaries; no user saves are accessed. Tests compare native fields before/after injection and load plain and gzip files with the processor present and removed. They cover ordering, buffer wrap, float round trips, culture/Unicode, absent/corrupt/future data, preview isolation, and failed operations. Separate history tests exercise deferred faction binding, continued recording, old-save rollback, and pending-state clearing with game stand-ins.

The same persistence project also targets .NET Framework 4.8. In that run the game's bundled Harmony actually detours safe fixture save/load methods using the production prefix/finalizer functions, including exceptions and a worker-thread load. Build and run that lane on Windows:

```text
dotnet build tests/HatePersistence/HatePersistence.Tests.csproj -f net48 -c Release -p:TerraInvictaDir="<game directory>"
tests\HatePersistence\bin\Release\net48\HatePersistence.Tests.exe
```

The .NET 8 lane invokes the hook lifecycle directly: bundled Harmony's older MonoMod implementation is incompatible with that runtime. Attempting to register patches on the actual Unity-dependent game methods under desktop .NET Framework also fails with `ECall methods must be packaged into a system module`; those methods need Unity's runtime. This is not evidence of a live-game failure, but neither is real game-method registration claimed as offline-verified. Startup/load timing and interactions with other mods still require the live checks below.

Game assemblies copied into ignored test output are not included in the release ZIP. The build records the installed game assembly, mod DLL and package SHA-256 in `artifacts/build-evidence.json`.

## Remaining live acceptance

Use a disposable campaign/save slot. Confirm mod activation with no history-persistence errors in UMM's log. Record a few hate changes, save A, record more changes, save B, and reload A; only A's events should return. Load B and verify its additional events. Repeat after a full game restart and with both compression settings. Test a pre-v0.3.12 save and a new campaign (empty history), then a copied save with this mod disabled (normal game loading). Preserve the original test save when checking a re-save without the mod, since that intentionally removes the optional history. Check that manual, quick, autosave, combat and exit saves still succeed; their inspected callers all use `SaveAllGameStates`.
