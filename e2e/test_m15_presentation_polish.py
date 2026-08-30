"""M15 GUI verification: crossfade transitions + ticker lower-third.

Uses its own fixture (not the shared `oa`) because the sandbox needs BOTH a pre-written
settings.json (long transition, ticker on, red band -> screenshot-assertable) and a seeded
song, which requires a migrate-close-seed-relaunch cycle.

Assertions are pixel-based against the projection window:
- crossfade: every frame sampled during the 1500 ms transition has bright (text) pixels,
  i.e. the screen never goes blank between slides (the pre-M15 behaviour faded in from 0).
- ticker: the bottom band shows the settings band colour and its pixels move between
  two captures 0.7 s apart.
"""
import json
import os
import sqlite3
import subprocess
import time
from pathlib import Path

import pytest
from pywinauto import Application, Desktop

REPO = Path(__file__).resolve().parents[1]
APP = os.environ.get("OA_EXE", str(REPO / "OpenAdoration.WPF/bin/Debug/net10.0-windows/OpenAdoration.exe"))
TITLE = "OpenAdoration"
LAUNCH_TIMEOUT = 30
NOW = "2026-07-15 00:00:00"


def _launch(data_dir):
    env = os.environ.copy()
    env["OA_DATA_DIR"] = str(data_dir)
    proc = subprocess.Popen([APP], env=env)
    win = Application(backend="uia").connect(process=proc.pid, timeout=LAUNCH_TIMEOUT).window(title=TITLE)
    win.wait("visible", timeout=LAUNCH_TIMEOUT)
    return proc, win


def _seed_song(db):
    con = sqlite3.connect(db)
    try:
        con.execute("INSERT INTO Songs (Title, CreatedAt, UpdatedAt) VALUES ('E2E Song', ?, ?)", (NOW, NOW))
        sid = con.execute("SELECT Id FROM Songs WHERE Title='E2E Song'").fetchone()[0]
        for order, lyrics in enumerate(["FIRST SLIDE LYRICS AAAA", "SECOND SLIDE LYRICS BBBB"]):
            con.execute(
                'INSERT INTO SongSections (SongId, Type, Lyrics, "Order", SectionNumber, CreatedAt, UpdatedAt) '
                "VALUES (?, 'Verse', ?, ?, ?, ?, ?)",
                (sid, lyrics, order, order + 1, NOW, NOW))
        con.commit()
    finally:
        con.close()


@pytest.fixture
def oa_m15(tmp_path):
    assert Path(APP).exists(), f"build first -- missing {APP}"
    data = tmp_path / "oa"
    data.mkdir(parents=True)
    (data / "settings.json").write_text(json.dumps({
        "SlideTransitionMilliseconds": 1500,
        "LowerThirdScroll": True,
        "LowerThirdScrollSpeed": 250,
        "LowerThirdBandColor": "#FFCC0000",
        "LowerThirdTextColor": "#FFFFFF",
        "LowerThirdFontSize": 48,
    }), encoding="utf-8")

    proc, win = _launch(data)          # first launch migrates a fresh DB
    win.close()
    proc.wait(timeout=10)
    _seed_song(data / "openadoration.db")

    proc, win = _launch(data)
    yield win
    try:
        win.close()
        proc.wait(timeout=5)
    except Exception:
        proc.kill()


def _projection():
    return Desktop(backend="uia").window(title_re="OpenAdoration — Projection|Projection Preview")


def _bright_pixels(img, threshold=200):
    hist = img.convert("L").histogram()
    return sum(hist[threshold:])


def _red_pixels(img):
    return sum(1 for r, g, b in img.convert("RGB").getdata() if r > 150 and g < 90 and b < 90)


def test_crossfade_and_ticker(oa_m15):
    win = oa_m15
    # Keep the main window clear of the bottom-right projection preview so screen captures
    # of the preview rectangle show the preview, not the main window.
    import ctypes
    ctypes.windll.user32.MoveWindow(win.wrapper_object().handle, 0, 0, 1000, 650, True)

    # Project the seeded song (only one row -> only one visible "▶" button).
    win.child_window(auto_id="NavSongsButton", control_type="Button").wrapper_object().click_input()
    win.child_window(title="▶", control_type="Button").wait("visible", timeout=10).click_input()

    proj = _projection()
    proj.wait("visible", timeout=15)
    pw = proj.wrapper_object()  # resolve the UIA lookup once; per-call lookups cost ~1 s each
    time.sleep(2.5)  # let the first slide's transition and theme resolve fully

    # -- crossfade: no blank frame while advancing ---------------------------------
    next_btn = win.child_window(auto_id="ProjectionNextButton", control_type="Button").wrapper_object()
    next_btn.invoke()
    frames, t0 = [], time.time()
    while time.time() - t0 < 1.4:
        frames.append(pw.capture_as_image())
    assert len(frames) >= 3, "too few frames captured to judge the transition"
    for i, img in enumerate(frames):
        assert _bright_pixels(img) > 50, f"frame {i}/{len(frames)} went blank mid-transition -- no crossfade"

    # -- ticker lower-third ---------------------------------------------------------
    win.child_window(auto_id="LowerThirdInputBox", control_type="Edit").wrapper_object() \
       .set_text("WELCOME TO THE E2E CHURCH SERVICE TICKER TAPE")
    win.child_window(auto_id="ShowLowerThirdButton", control_type="Button").wrapper_object().invoke()
    time.sleep(0.8)

    img1 = pw.capture_as_image()
    time.sleep(0.7)
    img2 = pw.capture_as_image()

    w, h = img1.size
    band1 = img1.crop((0, int(h * 0.85), w, h))
    band2 = img2.crop((0, int(h * 0.85), w, h))
    assert _red_pixels(band1) > 100, "settings band colour (#CC0000) not applied to the lower third"
    assert band1.tobytes() != band2.tobytes(), "lower-third text did not move -- ticker not scrolling"
