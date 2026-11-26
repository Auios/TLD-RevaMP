#!/usr/bin/env python3
"""The Head Unit example mod (TLD-Mods: mods/HeadUnit) in single player, on this machine: a Car01 and a head unit spawned,
the unit put into the car's radio slot as a player would, its screen's place and buttons, presses on them along a
ray as the crosshair does, and pictures of the screen (TheLongDrive_Data/headunit_*.png).
  TLD_TEST_MODS=CarTrailer,HeadUnit tools/headunit.py [--no-build] [--cache | --signin]
HU_VIDEOS=id1,id2 (the two queued videos) and HU_LINK=id (the link typed in the box): YouTube asks some machines to sign
in for some videos ("not a bot"); pick ones it serves there. A test game is muted: "its sound plays" then checks that the
video's sound track plays into its source (Probe.Sound), as nothing reaches the output.
--cache: only the video cache (BepInEx/cache/HeadUnit): a video's file deleted soon after nothing plays it or has it
next, downloaded again when it comes back, and the folder empty after a restart.
--signin: YouTube's sign-in check and the player's own login for it (signin below; HU_BLOCKED: a video YouTube asks
that for on this machine)."""
import os
os.environ.setdefault("TLD_TEST_MODS", "CarTrailer,HeadUnit")
import sys, time, pathlib, math, re
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import game, tldenv
from bench import Bridge
from roadkit import clear_road, CAR01

VERDICTS = []
def verdict(name, ok, detail=""):
    VERDICTS.append((name, ok)); print(f"{'PASS' if ok else 'FAIL'}  {name}  {detail}", flush=True)

def heard(sound):
    """A Probe.Sound line: sound came out, or (a test game is muted: AudioListener.volume 0, testdisplay.py, so
    nothing reaches the output or a filter) the video's sound track plays into its unmuted source."""
    m = re.search(r"peak ([\d.]+)", sound)
    if m and float(m.group(1)) > 0.001: return True
    return "listener 0.00" in sound and "source playing on" in sound and "enabled True target True" in sound

def call(b, what):
    try: return str(b("call HeadUnit.Probe." + what)["value"])
    except Exception as e: return "ERROR " + str(e)[:300]

def signin(b, vids):
    """--signin: YouTube's "Sign in to confirm you're not a bot" (HU_BLOCKED: a video it asks that for here). SIGN IN
    shows where the picture would be (not UPDATE); pressed, it gives yt-dlp the Firefox login used last, a made-up
    profile beside the game copy here (never this machine's own browser); yt-dlp runs with it and the screen still
    shows YouTube's error (a made-up login isn't accepted); a video YouTube serves still plays with it; a cookies.txt
    is used the same way, from a copy per run. Where SIGN IN would look on this machine is printed, nothing read there."""
    import sqlite3, shutil
    blocked = os.environ.get("HU_BLOCKED", "3BFTio5296w")
    cfg = tldenv.GAME_DIR / "BepInEx/config/HeadUnit"
    txt, aside = cfg / "cookies.txt", cfg / "cookies.txt.aside"
    if txt.exists(): txt.rename(aside)
    fake = tldenv.GAME_DIR.parent / "hu-firefox"
    shutil.rmtree(fake, ignore_errors=True)
    prof = fake / "test1234.default-release"; prof.mkdir(parents=True)
    db = sqlite3.connect(prof / "cookies.sqlite")
    db.execute("CREATE TABLE moz_cookies (id INTEGER PRIMARY KEY, originAttributes TEXT NOT NULL DEFAULT '', name TEXT, value TEXT, host TEXT, path TEXT, "
               "expiry INTEGER, lastAccessed INTEGER, creationTime INTEGER, isSecure INTEGER, isHttpOnly INTEGER, inBrowserElement INTEGER DEFAULT 0, "
               "sameSite INTEGER DEFAULT 0, rawSameSite INTEGER DEFAULT 0, schemeMap INTEGER DEFAULT 0)")
    db.execute("INSERT INTO moz_cookies (name, value, host, path, expiry, isSecure, isHttpOnly) VALUES ('PREF', 'hl=en', '.youtube.com', '/', 2000000000, 1, 0)")
    db.commit(); db.close()
    try:
        print(" sign-in before:", call(b, "SignOut"), "|", call(b, "SignInState"))
        look = call(b, "SignInLook"); print(" where SIGN IN would look here:", look)
        verdict("SIGN IN finds the Linux home under Proton", "wine home True" in look, look)
        print(" test profiles:", call(b, f"SignInRoots {fake}"))
        logn = lambda: sum(1 for l in tldenv.LOG.read_text(errors="replace").splitlines() if f"yt-dlp {blocked}" in l)
        print(" add the blocked one:", call(b, f"Add {blocked}"))
        for _ in range(12):
            st = call(b, "Video")
            m = re.search(r"index (\d+) of (\d+)", st)
            if not m or int(m.group(1)) == int(m.group(2)) - 1: break
            call(b, "Press next"); time.sleep(1.5)
        t0 = time.time()
        while time.time() - t0 < 240 and "pane Media SignIn" not in call(b, "SignInState"): time.sleep(2)
        st = call(b, "SignInState"); v = call(b, "Video")
        verdict("YouTube's sign-in check: SIGN IN where the picture would be, not UPDATE", "pane Media SignIn" in st and "signed in False" in st, f"{st} | {v}")
        print(" shot:", call(b, "Shot signin"))
        before = logn()
        r = call(b, "Press signin"); time.sleep(0.5)
        flash = call(b, "Tools").split("; flash ")[-1]; st = call(b, "SignInState")
        verdict("SIGN IN takes the Firefox login used last", r.startswith("pressed") and "signed in with Firefox" in flash and "test1234.default-release" in st, f"{r} | {flash} | {st}")
        t0 = time.time()   # every client tried again with the login (each logs its error), the screen back on SIGN IN
        while time.time() - t0 < 240 and not (logn() > before and "problem yt-dlp" in call(b, "Video")): time.sleep(2)
        v = call(b, "Video"); st = call(b, "SignInState")
        tail = [l.strip() for l in tldenv.LOG.read_text(errors="replace").splitlines() if f"yt-dlp {blocked}" in l][-2:]
        print("  log:", tail)
        verdict("yt-dlp ran with the login; the screen shows YouTube's error, not yt-dlp's cookie lines",
                logn() > before and "not a bot" in v and "Extracting cookies" not in v and "pane Media SignIn" in st, f"{logn() - before} runs | {v} | {st}")
        print(" shot:", call(b, "Shot signin_refused"))
        call(b, "Press prev"); time.sleep(1.5)
        for _ in range(12):
            st = call(b, "Video")
            if "index 0 " in st: break
            call(b, "Press prev"); time.sleep(1.5)
        t0 = time.time()
        while time.time() - t0 < 180:
            st = call(b, "Video")
            if ("index 0 " in st and "PLAYING" in st) or "problem yt-dlp" in st: break
            time.sleep(2)
        verdict("a video YouTube serves still plays with the login given", "index 0 " in st and "PLAYING" in st, st)
        print(" sign out:", call(b, "SignOut"))
        txt.write_text("# Netscape HTTP Cookie File\n.youtube.com\tTRUE\t/\tTRUE\t2000000000\tPREF\thl=en\n")
        txt_before = txt.read_bytes()
        n0 = int(re.search(r"of (\d+)", call(b, "Video")).group(1))
        print(" a link with cookies.txt:", call(b, f"Link https://youtu.be/{vids[0]}"))
        t0 = time.time()
        while time.time() - t0 < 90 and int(re.search(r"of (\d+)", call(b, "Video")).group(1)) < n0 + 1: time.sleep(1)
        t0 = time.time()   # the new video is fetched ahead with the cookies too: its copy goes when that run ends
        while time.time() - t0 < 240 and any(cfg.glob("cookies-run-*")): time.sleep(2)
        runs = sorted(p.name for p in cfg.glob("cookies-run-*"))
        n1 = int(re.search(r"of (\d+)", call(b, "Video")).group(1))
        verdict("a cookies.txt works the same, from a copy per run (none left, the file not written)",
                n1 == n0 + 1 and not runs and txt.read_bytes() == txt_before and "signed in True" in call(b, "SignInState"), f"{n0} -> {n1}, copies left {runs}")
        print(" a link, the game closed while yt-dlp looks it up:", call(b, f"Link https://youtu.be/{blocked}"))
        t0 = time.time()
        while time.time() - t0 < 10 and not any(cfg.glob("cookies-run-*")): time.sleep(0.2)
        during = sorted(p.name for p in cfg.glob("cookies-run-*"))
        game.quit_game()
        left = sorted(p.name for p in cfg.glob("cookies-run-*"))
        verdict("no copy of the cookies left after the game closes mid-run", during and not left, f"during {during}, after {left}")
    finally:
        if game.running(): call(b, "SignOut"); call(b, "SignInRoots -")
        if txt.exists(): txt.unlink()
        if aside.exists(): aside.rename(txt)
        shutil.rmtree(fake, ignore_errors=True)

with game.ProtectedSession():
    if "--no-build" not in sys.argv: game.build()
    else: game.local_mods()
    game.quit_game(); game.launch(); game.newgame()
    b = Bridge()
    time.sleep(3)
    for kv in filter(None, os.environ.get("HU_SET", "").split(";")):   # A/B: "HeadUnit.Video.MakeGapS 0;..."
        print(" set", kv, b("set " + kv))
    # the car on the road outside, the player beside it (the start house hides the screen from the pictures)
    rp = b("roadpoint"); F = (math.sin(math.radians(rp["yaw"])), math.cos(math.radians(rp["yaw"])))
    r0 = clear_road(b, rp["gx"], rp["gz"], F[0], F[1]); yaw = r0["yaw"]
    R = (math.cos(math.radians(yaw)), -math.sin(math.radians(yaw)))
    cx, cz = r0["gx"] + R[0] * 1.8, r0["gz"] + R[1] * 1.8
    print(" spawn car:", b(f"spawncarg {CAR01} 0 {cx} {cz} {yaw}")); time.sleep(3)
    b(f"tpground {cx + R[0] * 3} {cz + R[1] * 3}"); time.sleep(2)
    print(" spawn unit:", b("devspawn 9100")); time.sleep(3)

    print(" units:", call(b, "List"))
    m = call(b, "Mount"); print(" mount:", m)
    verdict("mounts in the radio slot", "mounted" in m, m)
    time.sleep(2)
    print(" units:", call(b, "List"))
    w = call(b, "Where"); print(" where:", w)
    try: facing = float(w.split("faces the seat ")[1].split(" ")[0])
    except Exception: facing = -9
    verdict("screen faces the driver", facing > 0.3, w)
    verdict("radio found", "radio MISSING" not in w, w)
    print(" shot:", call(b, "Shot radio"))

    def radio(): return call(b, "Where").split("; radio ")[-1].split(";")[0]
    for bid in ("pwr", "vol+", "seek+"):
        before = call(b, "Video") + call(b, "Where")
        r = call(b, f"Press {bid}"); time.sleep(0.8)
        after = call(b, "Where")
        print(f" press {bid}:", r, "|", after.split("; buttons")[0])
        verdict(f"button {bid} works", r.startswith("pressed"), r)
    t = call(b, "Tools"); print(" tools:", t, flush=True)
    if "yt-dlp False" in t or "ffmpeg False" in t:   # a fresh copy: yt-dlp, Deno and ffmpeg first, as a player would
        print(" fetches the tools:", call(b, "GetTools"), flush=True)
        t0 = time.time()
        while time.time() - t0 < 300 and not ("yt-dlp True" in call(b, "Tools") and "downloading False" in call(b, "Tools")): time.sleep(2)
    r = call(b, "Press video"); time.sleep(1.0)
    verdict("YOUTUBE tab opens", "mode Video" in call(b, "Video") or "tab Video" in call(b, "Video"), call(b, "Video"))
    print(" shot:", call(b, "Shot video"))

    # a video: added, downloaded, playing; then the tab switch that crashed the game (2026-10-15), next, back
    VIDS = os.environ.get("HU_VIDEOS", "jNQXAC9IVRw,dQw4w9WgXcQ").split(",")
    for v in VIDS: print(" add:", call(b, f"Add {v}"))
    t0 = time.time(); st = ""
    while time.time() - t0 < 240:
        st = call(b, "Video")
        if "PLAYING" in st or "problem can't" in st or "yt-dlp:" in st: break
        time.sleep(3)
    print(" video:", st)
    verdict("first video downloads and plays", "PLAYING" in st, st)
    if "--signin" in sys.argv:
        signin(b, VIDS)
        game.quit_game()
        print(f"{sum(ok for _, ok in VERDICTS)}/{len(VERDICTS)} passed"); sys.exit(0)
    if "--cache" in sys.argv:
        CACHE = tldenv.GAME_DIR / "BepInEx/cache/HeadUnit"
        def files(): return sorted(f.name for f in CACHE.iterdir()) if CACHE.exists() else []
        def has(v): return any(f.startswith(v) for f in files())
        def whole(v): return any(f.startswith(v + "_") and f.count(".") == 1 and not f.endswith(".part") for f in files())
        # the second fetched ahead first: "next" then switches to a file already there (the case that lost it, run 4)
        t0 = time.time()
        while time.time() - t0 < 120 and not whole(VIDS[1]): time.sleep(0.5)
        print(f" the second fetched ahead after {time.time() - t0:.1f} s:", files())
        call(b, "Press next"); t0 = time.time(); gone = playing = None; lost = []
        while time.time() - t0 < 240 and (gone is None or playing is None or time.time() - t0 < 8):
            if gone is None and not has(VIDS[0]): gone = time.time() - t0
            if not has(VIDS[1]): lost.append(round(time.time() - t0, 2))
            if playing is None:
                st = call(b, "Video")
                if ("index 1" in st and "PLAYING" in st) or "ERROR" in st: playing = time.time() - t0
            time.sleep(0.25)
        print(f" after next: the second plays after {playing} s, the first's file gone after {gone} s:", files())
        verdict("the played video's file goes within ~5 s", gone is not None and gone < 6.5, f"{gone} s after next, {files()}")
        verdict("the playing video's file stays", has(VIDS[1]) and not lost, f"{files()}, missing at {lost[:5]} s")
        call(b, "Press prev"); time.sleep(1); call(b, "Press prev"); t0 = time.time()   # the first goes to the video's start
        while time.time() - t0 < 180:
            st = call(b, "Video")
            if ("index 0" in st and "PLAYING" in st) or "ERROR" in st or not game.running(): break
            time.sleep(1)
        print(f" prev after {time.time() - t0:.0f} s:", st, "| cache", files())
        verdict("back to the first video: downloaded again, plays", "index 0" in st and "PLAYING" in st and game.running(), st)
        for line in tldenv.LOG.read_text(errors="replace").splitlines():
            if "video cache" in line or "video make" in line or "video drop" in line: print("  log:", line.strip())
        game.quit_game()
        left = files(); print(" cache after the game closed:", left)
        (CACHE / "AAAAAAAAAAA_720.mp4").write_bytes(b"x" * 1000); (CACHE / "BBBBBBBBBBB_720.f136.mp4").write_bytes(b"x")
        game.launch(); time.sleep(5)
        print(" cache after a restart (two planted files):", files())
        verdict("the folder empty after a restart", files() == [] and left == [], f"{files()} (after closing: {left})")
        game.quit_game()
        print(f"{sum(ok for _, ok in VERDICTS)}/{len(VERDICTS)} passed"); sys.exit(0)
    snd = [call(b, "Sound") for _ in range(5) if not time.sleep(0.5)]
    print(" sound:", snd, "|", call(b, "Player"))
    verdict("its sound plays", any(heard(x) for x in snd), snd[-1])
    print(" shot:", call(b, "Shot playing"))
    call(b, "Press nav"); time.sleep(3)
    st = call(b, "Video"); print(" on NAV:", st)
    verdict("video keeps playing on NAV", "PLAYING" in st, st)
    print(" shot:", call(b, "Shot nav"))
    mp = call(b, "MapPng nav"); print(" map:", mp)
    verdict("NAV: the main road in its own colour", mp.startswith("main road") and int(mp.split()[2]) > 50, mp)
    for _ in range(3): call(b, "Press zoom-")
    time.sleep(1); print(" shot:", call(b, "Shot navfar")); print(" map far:", call(b, "MapPng far"))
    for _ in range(3): call(b, "Press zoom+")
    call(b, "Press video"); time.sleep(2)
    r = call(b, "Press play"); time.sleep(2)   # pause
    r2 = call(b, "Press play"); time.sleep(3)  # play again: the crash
    st = call(b, "Video"); print(" pause/play:", r, "|", r2, "|", st)
    verdict("pause and play again (no crash)", "PLAYING" in st and game.running(), st)
    r = call(b, "Press next"); print(" next:", r)
    t0 = time.time()
    while time.time() - t0 < 240:
        st = call(b, "Video")
        if ("index 1" in st and "PLAYING" in st) or "ERROR" in st: break
        time.sleep(3)
    verdict("next video plays (no crash)", "index 1" in st and "PLAYING" in st and game.running(), st)
    r = call(b, "Press prev"); time.sleep(1); call(b, "Press prev"); time.sleep(6)
    st = call(b, "Video"); print(" prev:", st)
    verdict("back to the first (cached) video", "index 0" in st and "PLAYING" in st and game.running(), st)
    r = call(b, "Press full"); time.sleep(1.5); print(" full:", r)
    print(" shot:", call(b, "Shot full"))
    call(b, "Press unfull")
    # a Spotify album: its songs found on YouTube Music and queued as they come (Spotify.cs)
    n0 = int(call(b, "Video").split(" of ")[1].split(",")[0]) if " of " in call(b, "Video") else 0
    print(" spotify:", call(b, "Link https://open.spotify.com/album/4m2880jivSbbyEGAKfITCa"))
    t0 = time.time(); n = n0; first = None
    while time.time() - t0 < 120:
        st = call(b, "Video")
        try: n = int(st.split(" of ")[1].split(",")[0])
        except Exception: pass
        if n > n0 and first is None: first = time.time() - t0
        if n >= n0 + 13: break
        time.sleep(1)
    print(f" spotify: {n - n0} songs added, the first after {first if first is not None else -1:.0f} s, all after {time.time() - t0:.0f} s; tools: {call(b, 'Tools')}")
    verdict("Spotify album: songs queued", n - n0 >= 10, f"{n - n0} of 13, first after {first} s")
    verdict("Spotify album: the first within 20 s", first is not None and first < 20, f"{first}")

    # + ADD with words: YouTube's results listed, one tapped into the queue; a link in the same box still adds directly
    def count():
        try: return int(call(b, "Video").split(" of ")[1].split(",")[0])
        except Exception: return -1
    call(b, "Press video"); n0 = count(); t0 = time.time()
    print(" search:", call(b, "Search rick_astley_never_gonna_give_you_up"))
    res = ""
    while time.time() - t0 < 60:
        res = call(b, "SearchResults")
        if not res.startswith("searching"): break
        time.sleep(1)
    print(f" results after {time.time() - t0:.1f} s:", res[:400])
    verdict("search: YouTube's videos listed", "dQw4w9WgXcQ" in res, res[:200])
    time.sleep(1); print(" shot:", call(b, "Shot search"))
    print(" screen text:", call(b, "ScreenText"))
    print(" font lacks:", call(b, "Missing |\"çğıöşüÇĞİÖŞÜéñ€—–’“”&#!?[]{}<>"))
    r = call(b, "Press r0"); time.sleep(2); n1 = count()
    print(" tap the first:", r[:120], "| queue", n0, "->", n1, "|", call(b, "SearchResults")[:120])
    verdict("search: a tapped result is queued", n1 == n0 + 1, f"{n0} -> {n1}")
    print(" shot:", call(b, "Shot searchadded"))
    call(b, "Press done")
    print(" link in the same box:", call(b, "Search https://www.youtube.com/watch?v=" + os.environ.get("HU_LINK", "jNQXAC9IVRw")))
    until_t = time.time()
    while time.time() - until_t < 30 and count() < n1 + 1: time.sleep(1)
    verdict("a link in the box still adds directly", count() == n1 + 1, f"{n1} -> {count()}")

    # stuck anywhere: a second unit stuck on the dashboard beside the radio, as the glue key does (Glue.cs)
    print(" spawn second unit:", b("devspawn 9101")); time.sleep(3)
    print(" aim:", call(b, "AimBeside 0.35 0.05")); time.sleep(0.5)
    st = call(b, "Stick"); print(" stick:", st)
    try: back = float(st.split("screen back ")[1].split(" ")[0]); faces = float(st.split("faces the player ")[1].split(" ")[0])
    except Exception: back, faces = 9, -9
    verdict("stuck where looked", "stuck True" in st and "car none" not in st, st)
    verdict("stuck: the radio body hidden and passable", "body shown False" in st and "solid colliders 0" in st, st)
    verdict("stuck: screen flush on the spot, facing the player", back < 0.03 and faces > 0.7, f"back {back} m, faces {faces}")
    time.sleep(1); print(" shot:", call(b, "Shot stuck"))
    if "--keep" not in sys.argv: game.quit_game()

print(f"{sum(ok for _, ok in VERDICTS)}/{len(VERDICTS)} passed")
