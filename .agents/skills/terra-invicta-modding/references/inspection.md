# Read-only game inspection

## Establish evidence and tools

Resolve the user-provided Windows directory under the current environment; do not assume a drive is mounted or casing matches. The example installation was `G:\SteamLibrary\steamapps\common\Terra Invicta`, exposed as `/mnt/g/SteamLibrary/steamapps/common/Terra Invicta`. Record SHA-256 of `TerraInvicta_Data/Managed/Assembly-CSharp.dll`, installed UMM version, game build and DLC set when verifiable. An assembly hash does not identify a game build number by itself.

Useful installed locations:

| Path relative to the game | Evidence |
| --- | --- |
| `TerraInvicta_Data/StreamingAssets/Templates` | Native data and available overrides |
| `TerraInvicta_Data/StreamingAssets/Localization/en` | Labels, tooltip keys, confirmation text |
| `TerraInvicta_Data/Managed/Assembly-CSharp.dll` | Controllers and game-state operations |
| `TerraInvicta_Data/Managed/UnityModManager` | Actual UMM/Harmony DLLs, Config.xml, Log.txt |
| `TerraInvicta_Data/StreamingAssets/AssetBundles/ui` | Serialized UI geometry, components, callbacks |

Search a visible label with `rg -n -i`, then follow its localization key into the assembly. Avoid dumping the entire game or all methods of a huge class into context.

Check existing tools first. A .NET runtime alone is not an SDK. Temporary paths from a prior session may no longer exist. A Linux SDK builds managed net48 code under WSL; it does not run the Windows game.

Prefer available ILSpy/ILSpyCmd for targeted decompilation. If unavailable, `dnfile` reads .NET metadata and `dncil` disassembles method bodies without executing game code. `UnityPy` reads serialized assets. Install them into an ignored workspace virtual environment or `/tmp`, never the game directory. Example Linux setup (downloads require permitted network access):

```bash
python3 -m venv .local/inspect-venv
.local/inspect-venv/bin/python -m pip install dnfile==0.18.0 dncil==1.0.2 UnityPy==1.25.3
```

These versions were used in the reference investigation, not a perpetual compatibility guarantee. Record the installed versions. If venv support is missing, `pip --target /tmp/ti-inspection-python` and `PYTHONPATH=/tmp/ti-inspection-python` provide an alternative. Keep all downloaded dependencies outside release packages.

## Metadata and IL

A focused metadata inspection can use:

```python
import dnfile
pe = dnfile.dnPE(assembly_path)
for t in pe.net.mdtables.TypeDef:
    if str(t.TypeName) == wanted_type:
        print(t.TypeNamespace, t.TypeName)
        for f in t.FieldList:
            print('field', f.row.Name, f.row.Flags, f.row.Signature.value.hex())
        for m in t.MethodList:
            print('method', m.row.Name, m.row.Flags,
                  [str(p.row.Name) for p in m.row.ParamList],
                  m.row.Signature.value.hex())
```

For a selected method with a nonzero RVA, parse bytes with `dncil.cil.body.reader.read_method_body_from_bytes(pe.get_data(method.Rva))`. Inspect only that method's instructions. Resolve operand tokens before drawing conclusions:

- `0x70` tokens refer to `pe.net.user_strings`; other metadata tokens select a table with the high byte and a 1-based row with the low 24 bits.
- MethodSpec wraps a generic MethodDef/MemberRef; resolve its `Method.row` too.
- Associate MethodDef/Field rows with their declaring TypeDef and distinguish overload signatures. Decode TypeDefOrRef signatures rather than guessing types from field names.
- Trace call sites with nearby argument loads to establish actual null/bool/enum behavior. Inspect called methods and relevant event listeners, not only the top-level handler.
- Method access flags matter: an apparently convenient overload may be private while the index-based overload is public.
- Preserve branches and enum values. Do not infer ownership loss from a label like “abandon.”

For runtime exception offsets, match the deployed DLL's version/hash to the build being inspected. dncil instruction offsets can include the method header; account for that when matching a Mono IL offset. A stack offset may identify the dereference after a method returning null. Add contextual diagnostics if the offset is not definitive.

## Serialized UI, without launching Unity

Load the bundle using `UnityPy.load(bundle_path)`. Inspect MonoBehaviour `read_typetree()` dictionaries for a distinctive controller field. Follow its PPtr to the referenced Button, GameObject, RectTransform, and script metadata.

PPtrs include **both file ID and path ID**. A path-ID-only index is sufficient only after verifying a pointer is local (`m_FileID == 0`) in the correct serialized file. Resolve external-file pointers through their dependency instead of conflating IDs across files.

Record:

- Parent/child hierarchy and actual component types, not just object names.
- Anchors, pivots, offsets, sizeDelta, parent layout groups, scroll bounds, canvas/raycast components, and neighboring controls.
- Button `m_OnClick.m_PersistentCalls.m_Calls` targets and method names.
- Controller references to labels, popups, buttons, tooltip triggers, and tutorial objects.
- Whether the original popup is inactive during initialization.

Static prefab inspection cannot establish final runtime layout: game code and other mods can resize or replace it. Retain runtime layout checks in acceptance criteria.
