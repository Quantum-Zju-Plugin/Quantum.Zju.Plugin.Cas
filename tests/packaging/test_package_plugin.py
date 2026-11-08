import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
import zipfile

spec = importlib.util.spec_from_file_location("package_plugin", Path(__file__).resolve().parents[2] / ".ci/package-plugin.py")
package_plugin = importlib.util.module_from_spec(spec)
spec.loader.exec_module(package_plugin)


class PackagePluginTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.directory = Path(self.temporary.name) / "plugin"
        self.directory.mkdir()
        for name in ("Quantum.Zju.Plugin.Cas.dll", "Quantum.Zju.Plugin.Cas.Native.dll", "Quantum.Plugin.Abstraction.dll", "LICENSE", "NOTICE"):
            (self.directory / name).write_bytes(b"fictional release fixture")
        (self.directory / "plugin.json").write_text(json.dumps({"id": "quantum.zju.plugin.cas", "version": "0.6.0"}), encoding="utf-8")

    def test_archive_is_reproducible_and_platform_specific(self):
        _, archive, _ = package_plugin.create_archive(self.directory, "windows")
        original = hashlib.sha256(archive.read_bytes()).hexdigest()
        package_plugin.create_archive(self.directory, "windows")
        self.assertEqual(original, hashlib.sha256(archive.read_bytes()).hexdigest())
        self.assertEqual("quantum.zju.plugin.cas-0.6.0-windows.zip", archive.name)
        with zipfile.ZipFile(archive) as package:
            self.assertEqual("0.6.0", json.loads(package.read("plugin.json"))["version"])
            self.assertIn("Quantum.Zju.Plugin.Cas.Native.dll", package.namelist())
        _, mac_archive, _ = package_plugin.create_archive(self.directory, "macos")
        self.assertNotEqual(archive.name, mac_archive.name)

    def test_private_debug_and_source_files_are_rejected(self):
        for name in (".env", "debug.pdb", "signing.log", ".signing/private-key", "src/Example.cs", "obj/build.json"):
            with self.subTest(name=name):
                path = self.directory / name
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text("fictional forbidden file", encoding="utf-8")
                with self.assertRaises(ValueError):
                    package_plugin.create_archive(self.directory, "windows")
                path.unlink()

    def test_private_paths_in_native_assembly_are_rejected(self):
        (self.directory / "Quantum.Zju.Plugin.Cas.Native.dll").write_bytes(b"C:\\Users\\fictional\\checkout")
        with self.assertRaises(ValueError):
            package_plugin.create_archive(self.directory, "windows")

    def test_missing_native_adapter_is_rejected(self):
        (self.directory / "Quantum.Zju.Plugin.Cas.Native.dll").unlink()
        with self.assertRaises(ValueError):
            package_plugin.create_archive(self.directory, "windows")


if __name__ == "__main__":
    unittest.main()
