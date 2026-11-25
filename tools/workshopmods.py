#!/usr/bin/env python3
"""Every TLD Workshop mod on the beta, one at a time, single player on this machine's test copy: the mod alone in
BepInEx/TLDLoaderMods (with the workshop's dependency for it), a new game, then
  load    what the converter rewrote, the mod's ModHost error, game errors while the world came up;
  items   every item the mod adds to the item database (by name, against a run without mods) spawned a few metres
          apart: is it there (or what a spawner item made in its place), does it have colliders switched on, does
          it stay on the ground (6 s);
  drive   a car01 on the road, the autopilot to 15 m/s for 20 s: the speed reached, errors meanwhile;
  reload  the world saved and loaded again: errors, and which spawned items came back;
then Player.log's exceptions that a run without mods doesn't have. One JSON line per mod in --out; a mod already
there is skipped (the run resumes). The mods' files come from --mods (the workshop's DLLs, downloaded beforehand).
  tools/workshopmods.py --mods <folder> --out <results.jsonl> [--only a.dll,b.dll] [--logs <folder>] [--baseline] [--no-build]"""
import sys, time, json, re, math, shutil, pathlib, collections
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import game, tldenv
from bench import Bridge
from roadkit import clear_road, car_ready, g, until, seat_idx

def arg(name, default=None): return sys.argv[sys.argv.index(name) + 1] if name in sys.argv else default
MODS = pathlib.Path(arg("--mods"))
OUT = pathlib.Path(arg("--out"))
ONLY = [x for x in arg("--only", "").split(",") if x]
LOGS = pathlib.Path(arg("--logs")) if "--logs" in sys.argv else None   # keep each mod's Player.log here
PROBE = pathlib.Path(arg("--probe")) if "--probe" in sys.argv else None   # a test mod: WsProbe.Probe.SpawnAmt / Report
G = pathlib.Path(tldenv.GAME_DIR); LM = G / "BepInEx/TLDLoaderMods"; ASIDE = G / "BepInEx/TLDLoaderMods.aside"
SEED = 20261123
# --mods is a staged folder: <FileName>/ holds what the workshop puts into TLDLoaderMods for that mod (its DLL, or its
# zip's tree with Assets/ and Config/), _deps/ the files mods depend on, meta.json the list's Dependency per mod
META = json.loads((MODS / "meta.json").read_text()) if (MODS / "meta.json").exists() else {}
DEPS = META.get("deps", {})
EXC = re.compile(r"^([A-Za-z_][\w.]*(?:Exception|Error)\b.*)$")

def log(*a): print(*a, flush=True)

def exceptions():
    """Player.log's exceptions: the first line and the first frame, with how often each came."""
    try: lines = tldenv.PLAYER_LOG.read_text(errors="replace").splitlines()
    except OSError: return {}
    seen = collections.Counter()
    for i, l in enumerate(lines):
        m = EXC.match(l.strip())
        if not m or "Exception" not in l and "Error" not in l.split(":")[0]: continue
        frame = next((x.strip() for x in lines[i + 1:i + 4] if x.strip().startswith(("at ", "  at")) or " (at " in x or "(wrapper" in x or ".cs:" in x), "")
        seen[(m.group(1)[:300] + " | " + frame[:200])] += 1
    return dict(seen)

def items_now(b):
    """every item in the database by name (itemids matches part of a name: every letter and digit covers them all)"""
    out = {}
    for c in "abcdefghijklmnopqrstuvwxyz0123456789_":
        for i, n in b(f"itemids {c}")["items"]: out[n] = i
    return out

LOGSIZES = {}
def modlog():
    """what the mods wrote into their own logs (TLDLoaderMods/Logs) during this run that reads like a failure"""
    out = []
    for p in sorted((LM / "Logs").rglob("*")) if (LM / "Logs").exists() else []:
        if not p.is_file(): continue
        with p.open("rb") as f:
            f.seek(LOGSIZES.get(p, 0)); text = f.read().decode("utf-8", "replace")
        lines = text.splitlines()
        for i, l in enumerate(lines):
            if re.search(r"Critical|\[Error|Exception|failed", l): out.append((p.parent.name + ": " + l.strip() + " " + " ".join(x.strip() for x in lines[i + 1:i + 3]))[:400])
    return out[:12]

def errcount(b):
    e = b("errors raw"); return e.get("errors", 0) + e.get("exceptions", 0), e

def clear():
    for p in LM.iterdir():
        if p.name == "Logs": continue
        shutil.rmtree(p) if p.is_dir() else p.unlink()

def amt(mod): return mod == "#!AMT.dll" or "#!AMT.dll" in DEPS.get(mod, [])

def place(mod):
    clear()
    shutil.copytree(MODS / mod, LM, dirs_exist_ok=True)
    for d in DEPS.get(mod, []): shutil.copy2(MODS / "_deps" / d, LM / d)
    if PROBE and amt(mod): shutil.copy2(PROBE, LM / PROBE.name)
    return sorted(str(p.relative_to(LM)) for p in LM.rglob("*") if p.is_file() and "Logs" not in p.parts)[:40]

# The game's own settings as they were when the sweep started, put back before every launch: a mod's change to them is
# saved by the game and stayed for every mod after it (an automatic gearbox mod turns the game's auto shift off, and
# every later drive sat in neutral at 0 m/s).
SETTINGS = game.DOCS / "settings.tldc"
SETTINGS_KEPT = None

def run_mod(mod, base):
    r = {"mod": mod, "t": time.strftime("%Y-%m-%d %H:%M:%S")}
    r["files"] = place(mod) if mod else []
    if SETTINGS_KEPT is not None: SETTINGS.write_bytes(SETTINGS_KEPT)
    global LOGSIZES
    LOGSIZES = {p: p.stat().st_size for p in (LM / "Logs").rglob("*") if p.is_file()} if (LM / "Logs").exists() else {}
    game.quit_game(); game.launch()
    b = Bridge()
    st = b("mods"); r["loaded"] = [{"name": m["name"], "version": m["version"], "error": m["lastError"][:400]} for m in st["mods"]]
    game.newgame(SEED); b = Bridge(); time.sleep(8)
    st = b("mods"); r["loaded"] = [{"name": m["name"], "version": m["version"], "error": m["lastError"][:400]} for m in st["mods"]]
    lo = (G / "BepInEx/LogOutput.log").read_text(errors="replace")
    stem = pathlib.Path(mod).stem if mod else "\0"
    r["log"] = [l[:700] for l in lo.splitlines() if stem in l and any(k in l for k in ("retarget", "ModHost", "rror", "xception", "failed", "Missing"))][:30]
    n0, e0 = errcount(b); r["errors_load"] = n0
    # ------------------------------------------------------------- items
    items = items_now(b)
    if base is None: r["items_all"] = items
    new = sorted((n, i) for n, i in items.items() if n not in base["items_all"]) if base else []
    r["items_new"] = len(new); r["items"] = []
    me = b("gpos"); gx0, gz0, gy0 = me["gx"], me["gz"], me["gy"]
    for k, (name, idx) in enumerate(new[:14]):
        gx, gz = gx0 + 12 * (k % 7) - 36, gz0 + 25 + 14 * (k // 7)
        try:
            game_tp = b(f"tpground {gx} {gz}"); time.sleep(2)
            p = b("gpos"); before = {x["save"] for x in b("findgo * 12")["found"] if x["save"] is not None}
            sp = b(f"devspawn {idx}"); time.sleep(3)
            f1 = [x for x in b(f"findgo {name.lower()} 12")["found"]]
            time.sleep(6)
            f2 = [x for x in b(f"findgo {name.lower()} 30")["found"]]
            x1 = f1[0] if f1 else None; x2 = f2[0] if f2 else None
            row = {"name": name, "spawned": "spawned" in sp, "found": bool(x1), "where": [round(p["gx"]), round(p["gz"])]}
            if not x1:
                # a spawner: the item makes another (a game prefab under a save id of its own: the gauge mods) and goes
                f1 = [x for x in b("findgo * 12")["found"] if x["save"] is not None and x["save"] not in before]
                x1 = f1[0] if f1 else None
                if x1:
                    row["became"] = x1["name"]; row["found"] = True
                    f2 = [x for x in b("findgo * 30")["found"] if x["save"] == x1["save"]]; x2 = f2[0] if f2 else None
            if x1: row.update(colliders=x1["colliders"], rb=x1["rb"] is not None, active=x1["active"], save=x1["save"], car="carscript" in x1["components"].split(), idx=idx)
            if x2: row.update(dy=round(x2["pos"][1] - p["gy"], 2), kinematic=(x2["rb"] or {}).get("kinematic"))
            row["fell"] = bool(x2 and x2["pos"][1] < p["gy"] - 4) or bool(x1 and not x2)
            r["items"].append(row)
        except Exception as ex:
            r["items"].append({"name": name, "error": str(ex)[:300]})
    if PROBE and mod and amt(mod):
        # the Advanced Modding Toolkit's items aren't in the item database: its own spawn, in front of the player
        try:
            b(f"tpground {gx0 + 20} {gz0 + 20}"); time.sleep(3)
            r["amt"] = {"spawn": str(b("call WsProbe.Probe.SpawnAmt")["value"])[:1500]}; time.sleep(8)
            r["amt"]["report"] = str(b("call WsProbe.Probe.Report")["value"])[:1500]
        except Exception as ex: r["amt"] = {**(r.get("amt") or {}), "error": str(ex)[:300]}
    n1, _ = errcount(b); r["errors_items"] = n1 - n0
    # ------------------------------------------------------------- drive
    try:
        b(f"tpground {gx0} {gz0}"); time.sleep(3)
        rp = b("roadpoint"); F = (math.sin(math.radians(rp["yaw"])), math.cos(math.radians(rp["yaw"])))
        r0 = clear_road(b, rp["gx"], rp["gz"], F[0], F[1]); yaw = r0["yaw"]
        R = (math.cos(math.radians(yaw)), -math.sin(math.radians(yaw)))
        seated = car_ready(b, r0["gx"] + R[0] * 1.8, r0["gz"] + R[1] * 1.8, yaw, "player")
        r["drive"] = {"seated": seated}
        if seated:
            b("handbrake off"); b("autodrive on 15"); top = 0.0; t0 = time.time()
            while time.time() - t0 < 25:
                try:
                    v = [float(x) for x in str(b("get mainscript.s.player.Car.RB.velocity")["value"]).strip("()").split(",")]
                    top = max(top, math.hypot(v[0], v[2]))
                except Exception: pass
                time.sleep(1)
            b("autodrive on 0"); until(lambda: abs(float(str(b("get mainscript.s.player.Car.RB.velocity")["value"]).strip("()").split(",")[0])) < 1, 20)
            b("autodrive off"); b("driveout"); time.sleep(2)
            r["drive"]["top_ms"] = round(top, 1)
    except BaseException as ex:
        if isinstance(ex, KeyboardInterrupt): raise
        r["drive"] = {"error": str(ex)[:300]}
    # ------------------------------------------------------------- the mod's own car, if it adds one
    car = next((i for i in r.get("items", []) if i.get("car")), None)
    if car:
        try:
            b(f"tpground {gx0} {gz0}"); time.sleep(3)
            rp = b("roadpoint"); F = (math.sin(math.radians(rp["yaw"])), math.cos(math.radians(rp["yaw"])))
            r0 = clear_road(b, rp["gx"], rp["gz"], F[0], F[1]); yaw = r0["yaw"]
            R = (math.cos(math.radians(yaw)), -math.sin(math.radians(yaw)))
            cx, cz = r0["gx"] + R[0] * 1.8, r0["gz"] + R[1] * 1.8
            r["modcar"] = {"name": car["name"], "spawn": str(b(f"spawncarg {car['idx']} 0 {cx} {cz} {yaw}"))[:200]}; time.sleep(4)
            b(f"tpground {cx + R[0] * 3} {cz + R[1] * 3}"); time.sleep(2)
            for _ in range(4):
                try: b(f"sitseat {seat_idx(b)} redirect")
                except Exception as ex: r["modcar"]["sit"] = str(ex)[:160]
                time.sleep(1)
                if g(b, "mainscript.s.player.Bsitting") is True: break
            b("fuelup")
            r["modcar"]["seated"] = g(b, "mainscript.s.player.Bsitting") is True
            if r["modcar"]["seated"]:
                b("handbrake off"); b("autodrive on 12"); top = 0.0; t0 = time.time()
                while time.time() - t0 < 20:
                    try:
                        v = [float(x) for x in str(b("get mainscript.s.player.Car.RB.velocity")["value"]).strip("()").split(",")]
                        top = max(top, math.hypot(v[0], v[2]))
                    except Exception: pass
                    time.sleep(1)
                b("autodrive on 0"); time.sleep(4); b("autodrive off"); b("driveout"); time.sleep(2)
                r["modcar"]["top_ms"] = round(top, 1)
        except BaseException as ex:
            if isinstance(ex, KeyboardInterrupt): raise
            r["modcar"] = {**(r.get("modcar") or {}), "error": str(ex)[:300]}
    n2, _ = errcount(b); r["errors_drive"] = n2 - n1
    # ------------------------------------------------------------- save and load again
    try:
        b("set TLDRevamp.TestSafetyTools.BlockAutoSave False")
        b("call savedatascript.s.saveScreen.AutoSave"); time.sleep(5)
        b("set TLDRevamp.TestSafetyTools.BlockAutoSave True")
        newest = max(game.SAVES.glob("TLDsaveData_autosave*.tlds"), key=lambda p: p.stat().st_mtime)
        b = game.loadsave(newest.name[len("TLDsaveData_"):-len(".tlds")])
        n3, _ = errcount(b); r["errors_reload"] = n3
        back = []; near = {}; back_as = {}
        for row in r["items"]:
            if not row.get("found"): continue
            b(f"tpground {row['where'][0]} {row['where'][1]}"); time.sleep(3)
            if row.get("became"): there = any(x["save"] == row["save"] for x in b("findgo * 30")["found"])
            else: there = bool(b(f"findgo {row['name'].lower()} 30")["found"])
            if there: back.append(row["name"]); continue
            found = b("findgo * 30")["found"]
            same = [x["name"] for x in found if row.get("save") and x["save"] == row["save"]]
            if same: back_as[row["name"]] = same[0]   # its save id came back under another name (the base prefab of a car, or another item)
            else: near[row["name"]] = [(x["name"], x["save"]) for x in found][:12]   # what is there instead
        r["reload"] = {"ok": True, "items_back": back, "back_as": back_as, "near": near}
    except BaseException as ex:
        if isinstance(ex, KeyboardInterrupt): raise
        r["reload"] = {"ok": False, "error": str(ex)[:300]}
    return r, b

def stop(signum, frame): raise KeyboardInterrupt   # a stopped service still runs the cleanup below

def main():
    global SETTINGS_KEPT
    import signal; signal.signal(signal.SIGTERM, stop)
    done = set()
    if OUT.exists():
        for l in OUT.read_text().splitlines():
            try: done.add(json.loads(l)["mod"])
            except Exception: pass
    base = None
    BASE = OUT.with_suffix(".baseline.json")
    if BASE.exists(): base = json.loads(BASE.read_text())
    mods = ONLY or sorted(p.name for p in MODS.iterdir() if p.is_dir() and p.name != "_deps")
    LM.mkdir(parents=True, exist_ok=True)
    if ASIDE.exists(): sys.exit(f"{ASIDE} is still there from an earlier run: put it back by hand first")
    with game.ProtectedSession():   # can refuse to start: set the mods aside only once it has
        ASIDE.mkdir()
        try:
            for p in LM.iterdir():
                if p.name != "Logs": shutil.move(str(p), ASIDE / p.name)
            if SETTINGS.exists():
                SETTINGS_KEPT = SETTINGS.read_bytes()
                if b'"BAutoShift": false' in SETTINGS_KEPT: sys.exit(f"{SETTINGS} has auto shift off: the drive needs it on")
            if "--no-build" not in sys.argv: game.build()
            if base is None:
                log("== baseline (no workshop mods)")
                game.quit_game(); r, b = run_mod(None, None); r["exceptions"] = exceptions(); game.quit_game()
                BASE.write_text(json.dumps(r)); base = r
                log(f"  {len(base['items_all'])} items, {len(base['exceptions'])} kinds of exceptions")
            for mod in mods:
                if mod in done: continue
                log(f"== {mod}")
                t0 = time.time()
                try: r, b = run_mod(mod, base)
                except BaseException as ex:
                    if isinstance(ex, KeyboardInterrupt): raise
                    r = {"mod": mod, "fatal": f"{type(ex).__name__}: {ex}"[:500]}
                try: game.quit_game()
                except Exception as ex: r["quit"] = str(ex)[:200]
                exc = exceptions()
                if LOGS:   # the whole Player.log (exceptions() keeps only each one's first frame)
                    LOGS.mkdir(parents=True, exist_ok=True)
                    try: shutil.copyfile(tldenv.PLAYER_LOG, LOGS / (mod + ".log"))
                    except OSError: pass
                r["modlog"] = modlog()
                r["exceptions"] = {k: v for k, v in exc.items() if k not in base["exceptions"]}
                r["category"] = META.get("category", {}).get(mod); r["version"] = META.get("version", {}).get(mod)
                r["secs"] = round(time.time() - t0)
                with OUT.open("a") as f: f.write(json.dumps(r) + "\n")
                log(f"  {r.get('fatal') or ''} loaded {[(m['name'], m['error'][:80]) for m in r.get('loaded', []) if m['name'] not in ('',)][:3]} "
                    f"items {r.get('items_new')} fell {sum(1 for i in r.get('items', []) if i.get('fell'))} drive {r.get('drive')} reload {str(r.get('reload'))[:120]} "
                    f"new exception kinds {len(r['exceptions'])} ({r['secs']} s)")
        finally:
            try: game.quit_game()
            except Exception: pass
            clear()
            if ASIDE.exists():
                for p in ASIDE.iterdir(): shutil.move(str(p), LM / p.name)
                ASIDE.rmdir()

if __name__ == "__main__":
    main()
