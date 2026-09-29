# Terra Invicta mod workspace

Research snapshot: 24 September 2026.

## Local game installation: strictly read-only

The local Terra Invicta installation is at `G:\SteamLibrary\steamapps\common\Terra Invicta` (WSL path: `/mnt/g/SteamLibrary/steamapps/common/Terra Invicta`). Its files may be inspected to support development, but the entire installation must remain **strictly read-only**. Do not edit, delete, replace, or create files there, install or deploy mods there, or run tooling that writes there. Keep generated files and development outputs in this repository or a temporary directory.

## Give up Nation

This repository now implements **Give up Nation**: a button beside **Abandon Nation** in the Policies tab, enabled only when the player owns the executive control point. A confirmation popup explains that only the executive will become unowned; the player's other control points remain theirs.

Version 0.2.3 also adds the scheduled survey completion date and days remaining when hovering an in-flight probe icon in **Intel → Solar System** ([issue #1](https://github.com/metabrain/ti-mod-give-up-executive-node/issues/1)). The user confirmed the date tooltip in-game on v0.2.1, with a screenshot in the issue. The user also confirmed v0.2.2 days remaining works in-game. Version 0.2.3 displays the remaining time inline after the date, for example `(225.5 days)`.

Version 0.3.0 adds **Filter Unprospected** and **Filter Prospecting** alongside the existing **Filter Prospected** in a second filter row ([issue #2](https://github.com/metabrain/ti-mod-give-up-executive-node/issues/2)). Selected statuses combine inclusively; no status selected shows all bodies. Unprospected excludes surveys underway, and Prospecting includes probes and fleet surveys belonging to the current player. See [implementation and verification notes](docs/prospecting-filters.md). This feature passed the offline checks, and the user confirmed that the installed v0.3.0 filters work in-game.

Version 0.3.1 adds **Filter Prospectable**, matching the game's native `CanProspectWithProbe(body, false)` target eligibility. It separates valid, currently idle probe targets from bodies that are unprospected but unavailable for probing.

Version 0.3.2 adds a red raw alien-hate debug number to the left of the native alien-threat segments. It reads the alien faction's stored `GetFactionHate(activePlayer)` value directly, rather than the player's assessed estimate.

See the [user guide](docs/USER-GUIDE.md) and [implementation and validation notes](docs/implementation.md). This is a C# Harmony/Unity Mod Manager mod, built against the installed game assemblies as read-only references. The user reported that the UI works in-game with v0.1.1; executive-release side effects and save/load remain unverified.

Build, run offline checks, and create a ZIP using Python 3.11+ and the .NET 8 SDK:

```powershell
python tools/build.py --game-dir 'G:\SteamLibrary\steamapps\common\Terra Invicta'
```

Under WSL:

```bash
python3 tools/build.py --game-dir '/mnt/g/SteamLibrary/steamapps/common/Terra Invicta'
```

Use `--dotnet /path/to/dotnet` for a non-PATH SDK, and `--no-restore` after dependencies have been restored. Outputs go to `src/Mod/bin`, `tests/**/bin`, `.local`, and `artifacts`. The package is `artifacts/Local.GiveUpNation-0.3.1.zip`. The build tool has no deployment or game-launch operation.

## Modding research reference

The research below predates this implementation. Template setup, deployment, and runtime-test commands shown here are reference material and must **not** be run against the protected installation. This project uses `tools/build.py` instead.

Choose the smallest extension mechanism that can deliver a feature:

| Requirement | Mod type | Player dependency |
|---|---|---|
| Change or add static game data, text, projects, orgs, traits, nations, etc. | Native JSON/localization | Terra Invicta's built-in mod loader |
| Change game logic, runtime behavior, or most UI | C# code using Harmony | Unity Mod Manager (UMM) |
| Both kinds of change | Hybrid | UMM for the code portion |
| New models, textures, portraits, or audio | Native/hybrid plus authored assets | Tooling depends on asset type |

Start with native JSON whenever the desired behavior is represented by a template. It is simpler to install and less likely to break when game code changes. Do not use a code patch merely to change a value already exposed in a template.

## Recommended development route

Use [Laurentiu-Andronache/ti-mod-template](https://github.com/Laurentiu-Andronache/ti-mod-template) as the project base. It is a newer, tested scaffold built around the community handbook in [TROYTRON/ti-mods](https://github.com/TROYTRON/ti-mods). At the time of this review its compatibility record covers Terra Invicta 1.0.53a with Dark Skies, Unity 2020.3.49f1, UMM 0.33, a .NET Framework 4.8 mod target, and Windows x64.

Prerequisites:

- Windows x64
- Terra Invicta installed through Steam
- Git and Python 3.11+
- Unity Mod Manager configured for Terra Invicta (needed for code mods and the template's live test tooling)
- Unity 2020.3.49f1 only if authoring asset bundles

Bootstrap in PowerShell with the game closed:

```powershell
git clone https://github.com/Laurentiu-Andronache/ti-mod-template.git terra-invicta-mod
cd terra-invicta-mod
./ti.ps1 setup
./ti.ps1 init -Id YourName.YourMod -Name 'Your Mod' -Kind Native -Author 'Your Name'
./ti.ps1 doctor
```

Use `-Kind Code` for C#/Harmony behavior or `-Kind Hybrid` for code plus native content. The scaffold stores native files in `content/`, code in `src/Mod/`, local game inspection and test evidence in `.local/`, and release packages in `artifacts/`.

The normal loop is:

```powershell
./ti.ps1 inspect
./ti.ps1 build
./ti.ps1 test -Offline
./ti.ps1 test -Recipe native-smoke  # or a feature-specific recipe
./ti.ps1 package
```

The template intentionally does not include proprietary game DLLs or extracted game content. It compiles against the player's installed assemblies and does not package those dependencies.

## How native mods work

The base data is under:

```text
Terra Invicta/TerraInvicta_Data/StreamingAssets/Templates/
```

Each JSON filename corresponds to a game template class, for example `TIProjectTemplate.json`. A mod normally supplies a sparse array of records. An existing record is matched by `dataName`; only supplied fields are changed. A new `dataName` adds a record.

Example layout for a manually authored native mod:

```text
Terra Invicta/Mods/Enabled/YourName.YourMod/
├── ModInfo.json
├── TIProjectTemplate.json
└── Localization/
    └── en/
        └── YourName.YourMod.en
```

Minimal metadata:

```json
{
  "Title": "YourName.YourMod",
  "Author": "Your Name",
  "Description": "What the mod changes.",
  "LoadOrder": 0
}
```

Minimal sparse template override:

```json
[
  {
    "dataName": "AnExistingRecordName",
    "someField": 123
  }
]
```

Use strict JSON: no comments, duplicate keys, or trailing commas. The older handbook contains illustrative snippets with trailing commas; those should not be copied literally. The filename must be a real template filename unless code explicitly registers and loads a custom type.

Terra Invicta's manifest also supports advanced merge controls such as `TemplatesToConcatArrays`, `TemplatesToReplaceArrays`, and `TemplatesToReplace`. Arrays are a particular compatibility risk: index-based merging can make two mods that touch the same array conflict. Inspect the current game data and consuming code before choosing a merge mode.

To test a manual native mod:

1. Put its directory in `Terra Invicta/Mods/Enabled/`.
2. Open the in-game Mods screen, enable **Use Mods**, and ensure the mod is enabled.
3. Restart the game.
4. Check the game log and verify the effect in a new campaign when the data is applied only during campaign creation.

## How code mods work

A code mod targets .NET Framework 4.8 and references the installed game's `Assembly-CSharp.dll`, required Unity assemblies, UMM, and Harmony. `ModInfo.json` adds fields similar to:

```json
{
  "Id": "YourName.YourMod",
  "DisplayName": "Your Mod",
  "Title": "YourName.YourMod",
  "Author": "Your Name",
  "Version": "0.1.0",
  "ManagerVersion": "0.33.0",
  "AssemblyName": "YourName.YourMod.dll",
  "EntryMethod": "YourName.YourMod.Main.Load",
  "Requirements": [],
  "Description": "What the mod changes."
}
```

The entry point registers UMM lifecycle callbacks and applies Harmony patches from the mod assembly. Determine patch targets from the installed version of `Assembly-CSharp.dll`; decompiled names and private APIs can change between game builds. A successful compile is not enough—test patch timing, enable/disable behavior, save/load, relevant scenarios and DLC combinations, and the packaged Release build.

Static data belongs in templates. Persisted runtime data belongs in game state and requires deliberate save/load integration. Avoid storing campaign state only in static fields or template objects.

## Assets and publishing

- Asset bundles must be authored with Unity 2020.3.49f1 according to the current community documentation; a different Unity version can make bundles fail to load or crash the game.
- Portrait animation uses WebM; the handbook documents the required conversion/layout.
- UI modification generally requires code and inspection of the live Unity hierarchy.
- Workshop upload/update is performed from Terra Invicta's Mods screen. Preserve the generated `WorkshopItemInfo.xml`, especially its `PublishedFileId`, so an existing item can be updated.

## Compatibility and debugging rules

- Pin the exact Terra Invicta build, DLC set, UMM version, and game-assembly hash used for a release.
- Re-inspect patch targets after every game update. Treat private methods and fields as unstable.
- Namespace new `dataName` values (for example `YourName_YourMod_Record`) to avoid collisions.
- Never redistribute Terra Invicta DLLs or extracted proprietary data.
- Validate JSON and unique `dataName` values before launching.
- Test interaction with other mods that touch the same templates or Harmony methods.
- Keep feature-specific runtime evidence; offline validation cannot prove in-game semantics.

## Sources reviewed

- [TROYTRON/ti-mods](https://github.com/TROYTRON/ti-mods): community handbook covering templates, UMM/Harmony, UI, assets, portraits, maps, audio, and Workshop publishing.
- [Laurentiu-Andronache/ti-mod-template](https://github.com/Laurentiu-Andronache/ti-mod-template): current scaffold, inspection tools, validation, packaging, and runtime test workflow.
- [Steam guide: How to install and use mods for Terra Invicta](https://steamcommunity.com/sharedfiles/filedetails/?id=3260872800): current player installation behavior for native and code mods.
- [elordis/ti_tools](https://github.com/elordis/ti_tools): auxiliary tools for working with Terra Invicta template data.

## Next validation step

The first feature is implemented as a code mod. In-game validation remains outstanding; see the acceptance checklist in [implementation notes](docs/implementation.md). It requires an authorized test installation, while the specified local installation remains strictly read-only.
