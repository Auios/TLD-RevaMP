#!/usr/bin/env python3
"""A mod's own settings in the mods list (ModsScreen.Settings.cs), and the session settings' explanations (MpScreen):
the installed Head Unit and World Map rows have Settings; Head Unit's card lists its config (Volume, Resolution,
PhosphorPicture...), its toggle and its - / + change the config file (press=<key>:<label>, _ for a space); World Map's key rows show their key and wait
for one when pressed; the session-settings note explains a row while the pointer is on it (Hardcore included).
Pictures of each go to the game copy's evidence folder.
  TLD_TEST_MODS=HeadUnit WORLDMAP_DLL=<WorldMap.dll> tools/modsettings.py [--no-build]
WORLDMAP_DLL: a built World Map (its repository's bin/Release/netstandard2.1/WorldMap.dll), put in the copy first."""
import os
os.environ.setdefault("TLD_TEST_MODS", "HeadUnit")
import sys, time, pathlib, shutil, re, json
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import game, tldenv
from bench import Bridge

VERDICTS = []
def verdict(name, ok, detail=""):
    VERDICTS.append((name, ok)); print(f"{'PASS' if ok else 'FAIL'}  {name}  {detail}", flush=True)

def view(b, *args):
    r = b("mods settings " + " ".join(args))
    return r.get("view", "") if isinstance(r, dict) else str(r)

def row(text, key):
    m = re.search(r"Setting " + re.escape(key) + r" :: ([^\n]*)", text)
    return m.group(1) if m else ""

with game.ProtectedSession():
    wm = os.environ.get("WORLDMAP_DLL")
    if wm:
        dst = tldenv.GAME_DIR / "BepInEx/plugins/WorldMap"; dst.mkdir(parents=True, exist_ok=True); shutil.copy2(wm, dst / "WorldMap.dll")
    if "--no-build" not in sys.argv: game.build()
    else: game.local_mods()
    game.quit_game(); game.launch()   # the mods list is the main menu's
    b = Bridge(); time.sleep(5)
    t0 = time.time()
    while time.time() - t0 < 90 and not b("mods updates").get("catalog"): time.sleep(2)   # the catalog comes from GitHub
    cfg = tldenv.GAME_DIR / "BepInEx/config/tldrevamp.headunit.cfg"

    print(" open the list:", b("mods open")); time.sleep(3)
    v = b("mods view tab=installed").get("view", "")
    for mod in ("tldrevamp.headunit", "tldrevamp.worldmap"):
        line = next((l for l in v.splitlines() if l.startswith("Mod " + mod)), "")
        verdict(f"{mod}: Settings on its row", "Settings" in line, line[:160])
    print(" press Settings:", str(b("mods view press=tldrevamp.headunit:Settings"))[:80])
    s = view(b); print(" Head Unit card:\n  " + s.replace("\n", "\n  ")[:1500])
    verdict("Head Unit's card lists its settings", all(k in s for k in ("Setting Volume", "Setting Resolution", "Setting PhosphorPicture", "Setting ConvertToVP8")), s[:200])
    print(" shot:", b("shot modsettings-headunit"))

    before = row(s, "PhosphorPicture"); on = "On" in before.split(" | ")[-1]
    s = view(b, "press=PhosphorPicture:" + ("On" if on else "Off")); after = row(s, "PhosphorPicture")
    time.sleep(0.5); text = cfg.read_text()
    verdict("a toggle changes the config file", ("PhosphorPicture = " + ("false" if on else "true")) in text, f"{before} -> {after}")
    view(b, "press=PhosphorPicture:" + ("Off" if on else "On"))

    num = lambda r: float(re.search(r"\| ([\d.]+) \|", r).group(1)) if re.search(r"\| ([\d.]+) \|", r) else None
    vol0 = row(s, "Volume")
    s = view(b, "press=Volume:+"); vol1 = row(s, "Volume")
    time.sleep(0.5); m = re.search(r"Volume = ([\d.]+)", cfg.read_text())
    ok = num(vol0) is not None and num(vol1) is not None and num(vol1) > num(vol0) and m is not None and abs(float(m.group(1)) - num(vol1)) < 0.01
    verdict("+ raises a number within its range, and saves it", ok, f"{vol0} -> {vol1}, file {m and m.group(1)}")
    view(b, "press=Volume:−")
    view(b, "close")

    s = view(b, "open=tldrevamp.worldmap"); print(" World Map card:\n  " + s.replace("\n", "\n  ")[:900])
    verdict("World Map's card shows its keys", "Setting ZoomIn" in s and "=" in row(s, "ZoomIn"), row(s, "ZoomIn"))
    s = view(b, "press=ZoomIn:="); verdict("a key row waits for a key when pressed", "Press a key" in row(s, "ZoomIn"), row(s, "ZoomIn"))
    print(" shot:", b("shot modsettings-worldmap"))
    view(b, "close"); b("mods close")

    print(" multiplayer screen:", b("call TLDRevamp.Net.MpScreen.Toggle")); time.sleep(1.5)
    b("set TLDRevamp.Net.MpScreen.ObservedSettingsOpen True"); b("call TLDRevamp.Net.MpScreen.Refresh"); time.sleep(1)
    h = str(b("call TLDRevamp.Net.MpScreen.HoverSettingRow 4").get("value", ""))
    verdict("the session settings explain Hardcore on hover", "Faucets" in h and "crash" in h, h[:120])
    print(" shot:", b("shot sessionsettings-hardcore"))
    d = str(b("call TLDRevamp.Net.MpScreen.HoverSettingRow -1").get("value", ""))
    verdict("off the rows the note is the usual one", d.startswith("Host changes apply now"), d[:80])
    er = b("errors"); print("  errors:", er.get("count"), flush=True)
    verdict("no exceptions", (er.get("count") or 0) == 0, json.dumps(er)[:300])
game.quit_game()
print(f"{sum(ok for _, ok in VERDICTS)}/{len(VERDICTS)} passed")
