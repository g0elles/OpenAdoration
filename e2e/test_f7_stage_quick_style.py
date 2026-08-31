"""F7 GUI verification: Stage View's ad-hoc "quick style" fix (real church operator feedback --
being able to fix a song's font size / colours from Stage View at the last minute, without
navigating to Settings/Temas and creating a saved Theme first).

Mechanism under test (StageViewModel.IncreaseFontSizeCommand/DecreaseFontSizeCommand/
SetTextColorCommand/SetBackgroundColorCommand -> Slide.WithStyleOverride ->
IProjectionService.TryUpdateSlides): the override is patched directly onto the live Slide objects
and pushed through the existing live-update channel. Nothing is written to the Themes table.

Critically, OpenAdoration renders the live slide on TWO separate surfaces that each resolve theme
style independently:
  - ProjectionWindow (OpenAdoration.WPF/ProjectionWindow.xaml.cs) -- the ACTUAL projector output.
  - StageView's own embedded preview panel (StageViewModel.BuildPreview) -- an operator-facing mirror.
A bug that updates only one of these would look correct in a screenshot of the wrong window. This
test asserts against the PROJECTION window specifically (Desktop-level, title "OpenAdoration --
Projection" or the single-monitor fallback "Projection Preview"), not the main window.
"""
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
        con.execute("INSERT INTO Songs (Title, CreatedAt, UpdatedAt) VALUES ('F7 Quick Style Song', ?, ?)", (NOW, NOW))
        sid = con.execute("SELECT Id FROM Songs WHERE Title='F7 Quick Style Song'").fetchone()[0]
        con.execute(
            'INSERT INTO SongSections (SongId, Type, Lyrics, "Order", SectionNumber, CreatedAt, UpdatedAt) '
            "VALUES (?, 'Verse', 'QUICK STYLE TEST LYRICS', 0, 1, ?, ?)",
            (sid, NOW, NOW))
        con.commit()
    finally:
        con.close()


@pytest.fixture
def oa_f7(tmp_path):
    assert Path(APP).exists(), f"build first -- missing {APP}"
    data = tmp_path / "oa"
    data.mkdir(parents=True)

    proc, win = _launch(data)          # first launch migrates a fresh DB (seeds the default Theme)
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


def _pixel_count(img, r_lo=0, r_hi=255, g_lo=0, g_hi=255, b_lo=0, b_hi=255):
    return sum(
        1 for r, g, b in img.convert("RGB").getdata()
        if r_lo <= r <= r_hi and g_lo <= g <= g_hi and b_lo <= b <= b_hi
    )


def test_quick_style_changes_the_real_projector_output(oa_f7):
    win = oa_f7
    import ctypes
    ctypes.windll.user32.MoveWindow(win.wrapper_object().handle, 0, 0, 1400, 900, True)

    # Project the seeded song (only one row -> only one visible "▶" button).
    win.child_window(auto_id="NavSongsButton", control_type="Button").wrapper_object().click_input()
    win.child_window(title="▶", control_type="Button").wait("visible", timeout=10).click_input()
    time.sleep(1.0)

    proj = _projection()
    proj.wait("visible", timeout=15)
    pw = proj.wrapper_object()
    time.sleep(1.0)

    # -- Baseline: default seeded Theme is black background (#000000) / white text (#FFFFFF). ----
    baseline = pw.capture_as_image()
    black_bg = _pixel_count(baseline, r_hi=20, g_hi=20, b_hi=20)
    assert black_bg > 5000, f"expected a mostly-black baseline background, got {black_bg} black px"

    # Navigate to Stage View -- the quick style bar only appears once a SONG is live (IsSongLive).
    win.child_window(auto_id="NavStageButton", control_type="Button").wrapper_object().click_input()
    time.sleep(0.5)

    # -- Background colour swatch: dark red (#7F1D1D) --------------------------------------------
    win.child_window(auto_id="BgColor_#7F1D1D", control_type="Button").wrapper_object().click_input()
    time.sleep(0.8)

    after_bg = pw.capture_as_image()
    dark_red = _pixel_count(after_bg, r_lo=90, r_hi=170, g_hi=70, b_hi=70)
    assert dark_red > 5000, \
        f"projector background did not change to dark red after Stage View swatch click ({dark_red} px matched)"

    # -- Text colour swatch: yellow (#FFEB3B) -----------------------------------------------------
    win.child_window(auto_id="TextColor_#FFEB3B", control_type="Button").wrapper_object().click_input()
    time.sleep(0.8)

    after_text_color = pw.capture_as_image()
    yellow_before_resize = _pixel_count(after_text_color, r_lo=200, g_lo=200, b_hi=120)
    assert yellow_before_resize > 50, \
        f"projector text did not turn yellow after Stage View swatch click ({yellow_before_resize} px matched)"

    # -- Font size stepper: three clicks (+8pt each, default 48pt -> 72pt) -------------------------
    size_label = win.child_window(auto_id="FontSizeValueText", control_type="Text")
    baseline_size_text = size_label.window_text()
    assert baseline_size_text == "48pt", f"expected default 48pt before any stepper click, got {baseline_size_text!r}"

    inc_btn = win.child_window(auto_id="IncreaseFontSizeButton", control_type="Button").wrapper_object()
    for _ in range(3):
        inc_btn.click_input()
        time.sleep(0.4)

    assert size_label.window_text() == "72pt", f"expected 72pt after 3 increases, got {size_label.window_text()!r}"

    after_resize = pw.capture_as_image()
    yellow_after_resize = _pixel_count(after_resize, r_lo=200, g_lo=200, b_hi=120)
    assert yellow_after_resize > yellow_before_resize, (
        f"projector text did not visibly grow after 3 font-size increases "
        f"(yellow px {yellow_before_resize} -> {yellow_after_resize})"
    )

    # -- Also confirm Stage View's OWN preview panel (a separate render surface) reflects it -------
    stage_preview = win.capture_as_image()
    stage_dark_red = _pixel_count(stage_preview, r_lo=90, r_hi=170, g_hi=70, b_hi=70)
    assert stage_dark_red > 500, \
        f"Stage View's own preview panel did not reflect the background override ({stage_dark_red} px matched)"
