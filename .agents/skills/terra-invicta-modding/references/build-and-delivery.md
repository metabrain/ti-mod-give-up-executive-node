# Build, package, install, and retain

## Scaffold and toolchain

References used in this workflow:

- https://github.com/Laurentiu-Andronache/ti-mod-template — build/UMM scaffold; inspected commit `394dd0ca817d9a9709a13f3bb6fc9607d143cadc`.
- https://github.com/TROYTRON/ti-mods — modding handbook, especially `tutorials/IntroToUI.md`.
- https://github.com/metabrain/ti-mod-give-up-executive-node — concrete v0.1.1 implementation and local build wrapper, commit `ea2af33`.

Read relevant upstream instructions/license at the version actually used. Do not assume upstream tooling remains unchanged. The inspected template's `setup` builds and deploys a bridge into the game; runtime recipes deploy mods and launch the game. Do not run those against a read-only installation. An adapted local build wrapper is appropriate when deployment is excluded. Preserve notices when copying scaffold code.

A workable reference toolchain was Python 3.12, .NET SDK 8.0.425, a net48 mod target, and installed UMM 0.32.4.0. Unity asset authoring was unnecessary because the mod cloned native controls. Do not install Unity just to compile this code mod.

Discover an SDK with `dotnet --list-sdks`. If none exists, obtain Microsoft's installer from its official distribution and install into a workspace-local directory or temporary directory using `--install-dir`/`--no-path`; pin and record the selected SDK for reproducibility. Installing in `/tmp` is temporary. Download permissions remain subject to the environment. Keep CLI home and NuGet caches outside the game:

```bash
DOTNET_CLI_HOME="$PWD/.local/dotnet-home" \
NUGET_PACKAGES="$PWD/.local/nuget" \
DOTNET_CLI_TELEMETRY_OPTOUT=1 \
dotnet build src/Mod/Mod.csproj -c Release \
  -p:TerraInvictaDir='/path/to/Terra Invicta'
```

Use the repo's own wrapper when present. In the reference repo:

```bash
python3 tools/build.py --game-dir '/path/to/Terra Invicta' --dotnet /path/to/dotnet
```

This builds, runs the offline action checks, creates the ZIP, and verifies its allowlist/CRC. `--no-restore` works only after the appropriate project's dependencies are restored; it is not a fresh-environment setup option.

## Project requirements

- Target the framework required by the inspected game/UMM scaffold (net48 here). A Linux SDK can compile using `Microsoft.NETFramework.ReferenceAssemblies` 1.0.3.
- Reference the installed `Assembly-CSharp`, relevant firstpass/Unity modules, TextMeshPro/UI modules when used, UMM, and its matching Harmony. Verify exact DLL names; do not infer `UnityEngine.TextMeshPro.dll` when the installation has `Unity.TextMeshPro.dll`.
- Set each game/library reference `Private=false`. Do not package these reference assemblies.
- Check nonempty HintPaths for existence; framework-generated references can have no HintPath.
- Put generated files in ignored local directories. Do not run build steps that write back to the source game folder.
- Keep metadata version and assembly version consistent. Use a stable mod ID, matching DLL name and entry method. Set the minimum UMM version based on the implementation's evidence, not an unrelated scaffold default.

## Packaging

Read installed UMM `Config.xml` to discover the configured mods directory and metadata filename. The reference installation used `Mods/Enabled` and `ModInfo.json`.

Build a fresh archive from an explicit allowlist. A simple code-only mod may contain:

```text
Author.ModName/
  ModInfo.json
  Author.ModName.dll
  README.md
  LICENSE
  THIRD_PARTY_NOTICES.md
```

Manifest fields used here: `Id`, `DisplayName`, `Title`, `Author`, `Version`, `ManagerVersion`, `AssemblyName`, `EntryMethod`, `Requirements`, `Description`, `LoadOrder`. Verify `EntryMethod` resolves to the built class/method. `Title` should match the mod folder/ID for the inspected loader.

Native content scanning may treat other deployed JSON files as game templates: keep `mod.project.json`, test reports, and build evidence out of the package. Include only intentionally authored native JSON. Inspect ZIP members and CRCs, and hash the game assembly, mod DLL, and ZIP. Preserve license/attribution requirements of any reused scaffold.

## Authorized deployment

Prepare and check the final package before requesting any still-required authorization. Prior user authorization for installation persists within its scope; filesystem escalation may still be required.

1. Confirm the target game is closed before replacing the mod DLL; do not kill a running game without instruction.
2. Resolve one exact mod directory from the package ID and installed UMM config. Reject path traversal, unexpected files, nested duplicate folders, symlinks escaping the destination, or a mismatched ID.
3. For updates, compare the installed owned files with the previous package when available. Back them up into the workspace, not into the game's native JSON scan tree. Stop if unexpected edits would be overwritten without authorization.
4. Copy only the owned allowlisted files. Leave unrelated game files, mod files, settings, and Workshop metadata alone. Keep enough backup data to restore owned files if copying fails; do not broaden cleanup to the whole Mods folder.
5. Verify installed bytes/hashes and manifest version. Restart the game and confirm UMM activation, then test the actual feature on a disposable save.

A runtime failure requires a code fix, new package version, local checks, and a game-closed update; repeated reinstalling the same broken artifact is not progress. Report blocked runtime checks explicitly.

## Git retention

When commit/push is requested, inspect staged paths and diff checks first. Include source, tests, metadata, build tooling, docs, notices, and the reusable skill when requested. Exclude binaries, caches, extracted data, game DLLs, personal saves/logs, and secrets. Push to the intended remote/branch and verify success without force-pushing. A source commit does not publish the generated ZIP; upload a release asset only when requested.
