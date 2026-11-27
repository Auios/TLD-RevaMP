#!/usr/bin/env python3
"""TLD Loader mods' settings in the mods list (ModsScreen.Loader.cs, ModHost.Settings.cs): every Loader mod has a row on
the Installed tab; Settings opens TLD Loader's window for a mod whose Config() has code and runs its Config() there
without an error; its values land in Settings.json in TLD Loader's format; a TLD Loader settings file in the prefix's
Documents/TheLongDrive/Mods/Config/Mod Settings/<ID> is taken over (IMPORT). A picture of each window goes to the
copy's evidence folder. The copy's own TLDLoaderMods are set aside for the run and put back after.
  LOADER_MODS=<folder of mod DLLs> [IMPORT=<id>:<json>] tools/loadersettings.py [--no-build]
IMPORT: a mod's settings as TLD Loader writes them, e.g. 'SomeMod:{"settings":[{"ID":"volume","Value":1.5}]}'."""
import os
os.environ.setdefault("TLD_TEST_MODS", "")
import sys, time, pathlib, shutil, json, re
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import game, tldenv
from bench import Bridge

VERDICTS = []
def verdict(name, ok, detail=""):
    VERDICTS.append((name, ok)); print(f"{'PASS' if ok else 'FAIL'}  {name}  {detail}", flush=True)

src = pathlib.Path(os.environ["LOADER_MODS"])
mods_dir = tldenv.GAME_DIR / "BepInEx/TLDLoaderMods"; aside = mods_dir.with_name("TLDLoaderMods.aside-settings")
cfg_dir = tldenv.GAME_DIR / "BepInEx/config/TLDLoaderMods"
imp_id, imp_json = (os.environ["IMPORT"].split(":", 1) if os.environ.get("IMPORT") else (None, None))
docs = pathlib.Path(os.environ.get("TLD_TEST_COMPAT", "")) / "pfx/drive_c/users/steamuser/Documents/TheLongDrive/Mods/Config/Mod Settings"

with game.ProtectedSession():
    if mods_dir.exists(): mods_dir.rename(aside)
    try:
        mods_dir.mkdir(parents=True)
        for dll in sorted(src.rglob("*.dll")): shutil.copy2(dll, mods_dir / dll.name)
        if imp_id:
            (docs / imp_id).mkdir(parents=True, exist_ok=True); (docs / imp_id / "Settings.json").write_text(imp_json)
            shutil.rmtree(cfg_dir / imp_id, ignore_errors=True)   # ours from an earlier run would count first
        if "--no-build" not in sys.argv: game.build()
        else: game.local_mods()
        game.quit_game(); game.launch()   # the mods list is the main menu's
        b = Bridge(); time.sleep(5)
        print(" open the list:", b("mods open")); time.sleep(3)
        v = b("mods view tab=installed").get("view", "")
        rows = [l for l in v.splitlines() if l.startswith("Mod loader:")]
        verdict("every Loader mod has a row on the Installed tab", len(rows) >= len(list(src.rglob("*.dll"))), f"{len(rows)} rows")
        b("mods view tab=installed scroll=0"); time.sleep(1); print(" list shot:", b("shot loadersettings-list").get("name"), flush=True)
        e0 = b("errors").get("count") or 0
        opened = 0
        for line in rows:
            mid = line.split(" :: ")[0][len("Mod loader:"):]
            has = "Primary=Settings" in line or "| Settings" in line or "=Settings" in line
            print(f" {mid}: {'Settings' if has else 'no settings'}", flush=True)
            if not has: continue
            r = b(f"mods view press=loader:{mid}:Settings"); time.sleep(2.5)
            now = b("get TLDRevamp.ModHost.ConfigOpenFor").get("value")
            err = b("errors"); last = [x for x in json.dumps(err).split("ModHost: ") if x.startswith(mid + " Config")]
            shot = b("shot loadersettings-" + re.sub(r"[^A-Za-z0-9_-]", "_", mid)).get("name")
            verdict(f"{mid}: its window opens and its Config() draws without an error", now == mid and not last, f"open for {now}, shot {shot}")
            b("call TLDRevamp.ModHost.CloseConfig"); time.sleep(0.5); opened += 1
        verdict("the window closes", b("get TLDRevamp.ModHost.ConfigOpenFor").get("value") is None)
        files = sorted(cfg_dir.glob("*/Settings.json"))
        good = []
        for f in files:
            try: d = json.loads(f.read_text()); good.append(isinstance(d.get("settings"), list) and all("ID" in x and "Value" in x for x in d["settings"]))
            except Exception: good.append(False)
        verdict("values saved in Settings.json, TLD Loader's format", files and all(good), ", ".join(f"{f.parent.name}: {f.read_text()[:80]}" for f in files[:4]))
        if imp_id:
            mine = cfg_dir / imp_id / "Settings.json"
            want = {x["ID"]: x["Value"] for x in json.loads(imp_json)["settings"]}
            got = {x["ID"]: x["Value"] for x in json.loads(mine.read_text())["settings"]} if mine.exists() else {}
            verdict("a player's TLD Loader settings were taken over", all(got.get(k) == v for k, v in want.items()), f"TLD Loader's {want} -> ours {got}")
        b("mods close")
        e1 = b("errors"); print("  errors before/after:", e0, e1.get("count"), flush=True)
    finally:
        game.quit_game()
        if mods_dir.exists(): shutil.rmtree(mods_dir)
        if aside.exists(): aside.rename(mods_dir)
print(f"{sum(ok for _, ok in VERDICTS)}/{len(VERDICTS)} passed")
