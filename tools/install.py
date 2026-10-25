#!/usr/bin/env python3
"""Install a verified local mod package, backing up only owned files."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import zipfile

ROOT = Path(__file__).resolve().parents[1]
MOD_ID = "Local.GiveUpNation"
FILES = {"ModInfo.json", MOD_ID + ".dll", "README.md", "LICENSE", "THIRD_PARTY_NOTICES.md"}
DEFAULT_GAME = "/mnt/g/SteamLibrary/steamapps/common/Terra Invicta"


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(data):
    return hashlib.sha256(data).hexdigest()


def version(value):
    require(isinstance(value, str) and re.fullmatch(r"[0-9]+(?:\.[0-9]+){2,3}", value), "Invalid version: " + str(value))
    return value


def no_symlinks(path):
    for part in (path, *path.parents):
        require(not part.is_symlink(), "Symlink in installation/backup path: " + str(part))


def read_package(path):
    with zipfile.ZipFile(path) as archive:
        expected = {MOD_ID + "/" + name for name in FILES}
        require(len(archive.infolist()) == len(expected) and set(archive.namelist()) == expected,
                "Package must contain exactly the five owned mod files")
        for entry in archive.infolist():
            require(not stat.S_ISLNK(entry.external_attr >> 16), "Package contains a symlink")
            require(entry.file_size < 64 * 1024 * 1024, "Package member exceeds size limit")
        require(archive.testzip() is None, "Package CRC check failed")
        data = {name: archive.read(MOD_ID + "/" + name) for name in FILES}
    manifest = json.loads(data["ModInfo.json"])
    require(manifest.get("Id") == MOD_ID and manifest.get("Title") == MOD_ID
            and manifest.get("AssemblyName") == MOD_ID + ".dll"
            and manifest.get("EntryMethod") == "GiveUpNation.Main.Load", "Unexpected mod manifest")
    return data, version(manifest.get("Version"))


def install(package, game, artifacts, evidence=None, dry_run=False):
    package, game, artifacts = Path(package).absolute(), Path(game).absolute(), Path(artifacts).absolute()
    destination = game / "Mods/Enabled" / MOD_ID
    no_symlinks(destination)
    no_symlinks(artifacts)
    require(not artifacts.resolve().is_relative_to(game.resolve()), "Backups must be outside the game directory")
    assembly = game / "TerraInvicta_Data/Managed/Assembly-CSharp.dll"
    require(assembly.is_file(), "Game assembly not found: " + str(assembly))
    new, new_version = read_package(package)
    if evidence is None:
        sidecar = package.with_suffix(".evidence.json")
        evidence = sidecar if sidecar.exists() else package.parent / "build-evidence.json"
    report = json.loads(Path(evidence).read_text())
    require(report.get("packageSha256") == digest(package.read_bytes()), "Build evidence does not match package")
    require(report.get("modSha256") == digest(new[MOD_ID + ".dll"]), "DLL hash does not match build evidence")
    require(report.get("gameAssemblySha256") == digest(assembly.read_bytes()), "Installed game differs from the build reference; rebuild first")
    old = {}
    for name in FILES:
        path = destination / name
        no_symlinks(path)
        if path.exists():
            require(path.is_file(), "Owned path is not a regular file: " + str(path))
            old[name] = path.read_bytes()
    previous = "not installed"
    if old:
        require(set(old) == FILES, "Partial installation; refusing to overwrite")
        previous = version(json.loads(old["ModInfo.json"]).get("Version"))
        known, _ = read_package(artifacts / (MOD_ID + "-" + previous + ".zip"))
        require(old == known, "Installed owned files differ from their previous package; inspect edits first")
    if old == new:
        print("Already installed and verified: " + new_version)
        return None
    print(f"{'Would install' if dry_run else 'Installing'} {previous} -> {new_version}: {destination}")
    if dry_run:
        return None
    backup = None
    if old:
        stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%S.%fZ")
        backup = artifacts / "backups" / stamp / MOD_ID
        no_symlinks(backup)
        backup.mkdir(parents=True, exist_ok=False)
        for name, data in old.items():
            (backup / name).write_bytes(data)
            require((backup / name).read_bytes() == data, "Backup verification failed")
    destination.mkdir(parents=True, exist_ok=True)
    touched = []
    try:
        for name in sorted(FILES):
            path = destination / name
            no_symlinks(path)
            require((path.read_bytes() if path.exists() else None) == old.get(name), "Installed files changed during deployment")
            touched.append(name)
            path.write_bytes(new[name])
        require(all((destination / name).read_bytes() == new[name] for name in FILES), "Installed verification failed")
    except Exception as error:
        failures = []
        for name in touched:
            try:
                if name in old:
                    (destination / name).write_bytes(old[name])
                    require((destination / name).read_bytes() == old[name], "Rollback verification failed")
                else:
                    (destination / name).unlink(missing_ok=True)
            except Exception as rollback_error:
                failures.append(str(rollback_error))
        if failures:
            raise RuntimeError(f"Install failed: {error}. Restore backup {backup}. Rollback errors: {failures}") from error
        raise
    print(f"Installed and verified {new_version}. Backup: {backup or 'fresh installation'}")
    print("Restart the game to load the installed DLL.")
    return backup


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    choice = parser.add_mutually_exclusive_group()
    choice.add_argument("--version", help="Version in artifacts; defaults to mod.project.json")
    choice.add_argument("--package", type=Path, help="Explicit package ZIP")
    parser.add_argument("--game-dir", type=Path, default=os.environ.get("TI_GAME_DIR", DEFAULT_GAME))
    parser.add_argument("--artifacts-dir", type=Path, default=ROOT / "artifacts", help="Prior packages and backup destination")
    parser.add_argument("--evidence", type=Path, help="Build evidence JSON; normally detected beside package")
    parser.add_argument("--dry-run", action="store_true", help="Validate everything without writing files")
    args = parser.parse_args()
    try:
        selected = args.package
        if selected is None:
            selected_version = version(args.version or json.loads((ROOT / "mod.project.json").read_text())["Version"])
            selected = args.artifacts_dir / (MOD_ID + "-" + selected_version + ".zip")
        install(selected, args.game_dir, args.artifacts_dir, args.evidence, args.dry_run)
    except (OSError, ValueError, KeyError, zipfile.BadZipFile, RuntimeError) as error:
        parser.exit(1, "Install failed: " + str(error) + "\n")


if __name__ == "__main__":
    main()
