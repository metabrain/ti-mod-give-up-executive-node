#!/usr/bin/env python3
"""Build, check and package locally. This tool has no install/deploy/launch command."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import zipfile

ROOT = Path(__file__).resolve().parents[1]

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-dir', type=Path, required=True, help='Read-only Terra Invicta installation')
    parser.add_argument('--dotnet', default='dotnet', help='.NET 8 SDK executable')
    parser.add_argument('--no-restore', action='store_true', help='Use previously restored packages')
    args = parser.parse_args()
    game = args.game_dir.resolve()
    for directory in (ROOT / '.local', ROOT / 'artifacts', ROOT / 'src/Mod/bin', ROOT / 'src/Mod/obj', ROOT / 'tests/bin', ROOT / 'tests/obj', ROOT / 'tests/ProbeArrival/bin', ROOT / 'tests/ProbeArrival/obj', ROOT / 'tests/ProspectingFilters/bin', ROOT / 'tests/ProspectingFilters/obj'):
        resolved = directory.resolve()
        if not resolved.is_relative_to(ROOT) or resolved.is_relative_to(game):
            parser.error(f'Output must remain in the workspace, outside the game: {resolved}')
    managed = game / 'TerraInvicta_Data/Managed'
    assembly = managed / 'Assembly-CSharp.dll'
    if not assembly.is_file():
        parser.error(f'Missing read-only game assembly: {assembly}')
    config = json.loads((ROOT / 'mod.project.json').read_text())
    env = os.environ.copy()
    env.setdefault('DOTNET_CLI_HOME', str(ROOT / '.local/dotnet-home'))
    env.setdefault('NUGET_PACKAGES', str(ROOT / '.local/nuget'))
    env['DOTNET_CLI_TELEMETRY_OPTOUT'] = '1'
    env['DOTNET_SKIP_FIRST_TIME_EXPERIENCE'] = '1'
    restore = ['--no-restore'] if args.no_restore else []
    def run(*command):
        subprocess.run([args.dotnet, *command], cwd=ROOT, env=env, check=True)
    run('build', 'src/Mod/Mod.csproj', '-c', 'Release', '--nologo', f'-p:TerraInvictaDir={game}', *restore)
    run('run', '--project', 'tests/ReleaseRequest.Tests.csproj', '-c', 'Release', '--nologo', *restore)
    run('run', '--project', 'tests/ProbeArrival/ProbeArrival.Tests.csproj', '-c', 'Release', '--nologo', *restore)
    run('run', '--project', 'tests/ProspectingFilters/ProspectingFilters.Tests.csproj', '-c', 'Release', '--nologo', f'-p:TerraInvictaDir={game}', *restore)
    artifacts = ROOT / 'artifacts'
    artifacts.mkdir(exist_ok=True)
    dll_name = config['Id'] + '.dll'
    manifest = dict(config, Title=config['Id'], AssemblyName=dll_name,
                    EntryMethod='GiveUpNation.Main.Load', Requirements=[], LoadOrder=0)
    # An explicit allowlist prevents distributing proprietary references or stale build files.
    files = {
        dll_name: ROOT / 'src/Mod/bin/Release/net48' / dll_name,
        'README.md': ROOT / 'docs/USER-GUIDE.md',
        'LICENSE': ROOT / 'LICENSE',
        'THIRD_PARTY_NOTICES.md': ROOT / 'THIRD_PARTY_NOTICES.md',
    }
    archive = artifacts / f"{config['Id']}-{config['Version']}.zip"
    with zipfile.ZipFile(archive, 'w', zipfile.ZIP_DEFLATED) as package:
        package.writestr(f"{config['Id']}/ModInfo.json", json.dumps(manifest, indent=2) + '\n')
        for name, path in files.items():
            package.write(path, f"{config['Id']}/{name}")
    with zipfile.ZipFile(archive) as package:
        expected = {f"{config['Id']}/{name}" for name in [*files, 'ModInfo.json']}
        assert set(package.namelist()) == expected
        assert package.testzip() is None
    evidence = {
        'gameAssemblySha256': hashlib.sha256(assembly.read_bytes()).hexdigest(),
        'modSha256': hashlib.sha256(files[dll_name].read_bytes()).hexdigest(),
        'packageSha256': hashlib.sha256(archive.read_bytes()).hexdigest(),
        'checks': ['Release build', '13 offline confirmation/action tests using game stand-ins', '17 offline probe-tooltip data/startup tests using game stand-ins', 'Prospecting status combinations and executable transpiler checks using game stand-ins', 'ZIP allowlist and CRC'],
        'runtimeTested': False,
        'gameFilesWritten': False,
    }
    (artifacts / 'build-evidence.json').write_text(json.dumps(evidence, indent=2) + '\n')
    print(f'Package: {archive}\nNo installation or game launch performed.')

if __name__ == '__main__':
    main()
