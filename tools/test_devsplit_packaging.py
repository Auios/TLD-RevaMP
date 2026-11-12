"""Developer builds ship the optional plugin; player builds never do."""
import pathlib
import sys
import tempfile
import unittest
from unittest import mock
import zipfile

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import devrelease


class DevSplitPackaging(unittest.TestCase):
    def test_build_selects_the_plugin_for_the_edition(self):
        with mock.patch.object(devrelease.subprocess, "run") as run:
            devrelease.build("0.69.1", "0.69.1-test")
            self.assertIn("TLDRevamp.Dev.csproj", run.call_args_list[0].args[0][2])
            run.reset_mock()
            devrelease.build("0.69.1", "0.69.1-test", edition="player")
            self.assertIn("TLDRevamp.csproj", run.call_args_list[0].args[0][2])
            self.assertNotIn("TLDRevamp.Dev", str(run.call_args_list))

    def test_packages_follow_the_edition_even_with_a_stale_dev_binary(self):
        with tempfile.TemporaryDirectory() as directory:
            root = pathlib.Path(directory)
            game = root / "game"

            def put(path, text="fixture"):
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(text, encoding="utf-8")

            for name in ("TLDRevamp", "TLDRevamp.Dev", "TLDRevamp.Multiplayer", "TLDRevamp.Preloader"):
                put(root / "src" / name / devrelease.OUT / (name + ".dll"), "0.69.1-test")
            put(root / "src/TLDLoaderShim" / devrelease.OUT / "TLDLoader.dll")
            for name in ("approved-mods.txt", "INSTALL.txt"):
                put(root / "packaging" / name)
            put(root / "COPYING")
            put(root / "packaging/licenses/example.txt")
            for name in ("winhttp.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt", "BepInEx/core/BepInEx.dll"):
                put(game / name)
            with mock.patch.object(devrelease, "ROOT", root), mock.patch.object(devrelease.tldenv, "GAME_DIR", game):
                for edition in (None, "player"):
                    archive = devrelease.package("0.69.1-test", "fixture", edition=edition)
                    with zipfile.ZipFile(archive) as package:
                        self.assertIn("BepInEx/plugins/TLDRevamp/TLDRevamp.dll", package.namelist())
                        self.assertEqual(edition != "player", "BepInEx/plugins/TLDRevamp/TLDRevamp.Dev.dll" in package.namelist())


if __name__ == "__main__":
    unittest.main()
