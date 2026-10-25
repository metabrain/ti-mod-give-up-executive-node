import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import zipfile

spec = importlib.util.spec_from_file_location('installer', Path(__file__).resolve().parents[2] / 'tools/install.py')
installer = importlib.util.module_from_spec(spec)
spec.loader.exec_module(installer)


class InstallerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.game = self.root / 'game'
        self.artifacts = self.root / 'artifacts'
        self.artifacts.mkdir()
        assembly = self.game / 'TerraInvicta_Data/Managed/Assembly-CSharp.dll'
        assembly.parent.mkdir(parents=True)
        assembly.write_bytes(b'synthetic game')
        self.destination = self.game / 'Mods/Enabled' / installer.MOD_ID
        self.old = self.package('1.0.0')
        self.new = self.package('1.0.1')

    def package(self, version):
        path = self.artifacts / f'{installer.MOD_ID}-{version}.zip'
        manifest = dict(Id=installer.MOD_ID, Title=installer.MOD_ID, AssemblyName=installer.MOD_ID+'.dll', EntryMethod='GiveUpNation.Main.Load', Version=version)
        data = {name: (version+name).encode() for name in installer.FILES}
        data['ModInfo.json'] = json.dumps(manifest).encode()
        with zipfile.ZipFile(path, 'w') as archive:
            for name, value in data.items():
                archive.writestr(installer.MOD_ID+'/'+name, value)
        report = dict(packageSha256=installer.digest(path.read_bytes()), modSha256=installer.digest(data[installer.MOD_ID+'.dll']), gameAssemblySha256=installer.digest(b'synthetic game'))
        path.with_suffix('.evidence.json').write_text(json.dumps(report))
        return path

    def install(self, package, **kwargs):
        return installer.install(package, self.game, self.artifacts, **kwargs)

    def test_upgrade_backup_preserves_unrelated_and_repeat_is_noop(self):
        self.install(self.old)
        (self.destination/'settings.xml').write_text('keep')
        backup = self.install(self.new)
        expected, _ = installer.read_package(self.old)
        self.assertEqual({p.name:p.read_bytes() for p in backup.iterdir()}, expected)
        self.assertEqual((self.destination/'settings.xml').read_text(), 'keep')
        self.assertIsNone(self.install(self.new))

    def test_dry_run_writes_nothing(self):
        self.install(self.new, dry_run=True)
        self.assertFalse(self.destination.exists())
        self.assertFalse((self.artifacts/'backups').exists())

    def test_modified_install_rejected(self):
        self.install(self.old)
        (self.destination/'README.md').write_text('local edit')
        with self.assertRaisesRegex(ValueError, 'differ'):
            self.install(self.new)
        self.assertEqual((self.destination/'README.md').read_text(), 'local edit')

    def test_wrong_evidence_rejected(self):
        with self.assertRaisesRegex(ValueError, 'evidence'):
            self.install(self.new, evidence=self.old.with_suffix('.evidence.json'))
        self.assertFalse(self.destination.exists())

    def test_extra_archive_member_rejected(self):
        with zipfile.ZipFile(self.new, 'a') as archive:
            archive.writestr('../escape', b'bad')
        with self.assertRaisesRegex(ValueError, 'five owned'):
            self.install(self.new)
        self.assertFalse(self.destination.exists())

    def test_symlink_rejected(self):
        self.install(self.old)
        target = self.destination/'README.md'
        target.unlink()
        outside = self.root/'outside'
        outside.write_text('untouched')
        target.symlink_to(outside)
        with self.assertRaisesRegex(ValueError, 'Symlink'):
            self.install(self.new)
        self.assertEqual(outside.read_text(), 'untouched')

    def test_failed_copy_rolls_back(self):
        self.install(self.old)
        original = Path.write_bytes
        failed = False
        def fail_once(path, data):
            nonlocal failed
            if path == self.destination/'README.md' and not failed:
                failed = True
                original(path, b'partial')
                raise OSError('simulated write failure')
            return original(path, data)
        with patch.object(Path, 'write_bytes', fail_once):
            with self.assertRaisesRegex(OSError, 'simulated'):
                self.install(self.new)
        expected, _ = installer.read_package(self.old)
        self.assertEqual({name:(self.destination/name).read_bytes() for name in installer.FILES}, expected)

    def test_game_update_rejected(self):
        (self.game/'TerraInvicta_Data/Managed/Assembly-CSharp.dll').write_bytes(b'updated')
        with self.assertRaisesRegex(ValueError, 'rebuild'):
            self.install(self.new)

if __name__ == '__main__':
    unittest.main()
