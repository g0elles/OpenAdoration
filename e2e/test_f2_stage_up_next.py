"""F2 GUI verification: standalone Song projection feeds Stage View's "up next" pane.

Operator feedback: Stage View's "A CONTINUACION" (up-next) pane already worked for Bible verses
(an artifact of the whole chapter being preloaded as one slide deck) but not for songs projected
standalone (outside a service schedule). SongsViewModel.ProjectSong now calls
IProjectionService.SetNextScheduleItemPreview with the next displayed song's first slide -- the
same mechanism ServiceScheduleViewModel already uses -- so StageViewModel's existing cross-item
fallback branch picks it up with zero StageViewModel changes.

Seeds two songs (2 sections each) directly into the sandbox DB, projects the first, and asserts:
  1. While on the first section, up-next shows the *within-song* next section (existing behaviour).
  2. After advancing to the last section, up-next switches to the *next song's* first slide --
     this is the new F2 wiring under verification.
"""
import os
import sqlite3
import subprocess
import time
from pathlib import Path

import pytest
from pywinauto import Application

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


def _seed_songs(db):
    con = sqlite3.connect(db)
    try:
        for title, lyrics in [
            ("F2 Test Song A", ["A SECTION ONE LYRICS", "A SECTION TWO LYRICS"]),
            ("F2 Test Song B", ["B SECTION ONE LYRICS", "B SECTION TWO LYRICS"]),
        ]:
            con.execute("INSERT INTO Songs (Title, CreatedAt, UpdatedAt) VALUES (?, ?, ?)", (title, NOW, NOW))
            sid = con.execute("SELECT Id FROM Songs WHERE Title=?", (title,)).fetchone()[0]
            for order, section_lyrics in enumerate(lyrics):
                con.execute(
                    'INSERT INTO SongSections (SongId, Type, Lyrics, "Order", SectionNumber, CreatedAt, UpdatedAt) '
                    "VALUES (?, 'Verse', ?, ?, ?, ?, ?)",
                    (sid, section_lyrics, order, order + 1, NOW, NOW))
        con.commit()
    finally:
        con.close()


@pytest.fixture
def oa_f2(tmp_path):
    assert Path(APP).exists(), f"build first -- missing {APP}"
    data = tmp_path / "oa"
    data.mkdir(parents=True)

    proc, win = _launch(data)          # first launch migrates a fresh DB
    win.close()
    proc.wait(timeout=10)
    _seed_songs(data / "openadoration.db")

    proc, win = _launch(data)
    yield win
    try:
        win.close()
        proc.wait(timeout=5)
    except Exception:
        proc.kill()


def test_standalone_song_up_next_shows_next_song(oa_f2):
    win = oa_f2

    win.child_window(auto_id="NavSongsButton", control_type="Button").wrapper_object().click_input()
    time.sleep(0.5)

    # Two songs -> two "Proyectar" ("Common_Project") buttons; the first row is Song A
    # (inserted first, default list order is by Title which sorts A before B).
    play_buttons = win.descendants(title="▶", control_type="Button")
    assert len(play_buttons) == 2, f"expected 2 project buttons, found {len(play_buttons)}"
    play_buttons[0].click_input()
    time.sleep(1.0)

    win.child_window(auto_id="NavStageButton", control_type="Button").wrapper_object().click_input()
    time.sleep(1.0)

    next_text = win.child_window(auto_id="NextPreviewContentText", control_type="Text")
    next_text.wait("visible", timeout=10)
    first_up_next = next_text.window_text()
    assert "A SECTION TWO" in first_up_next, \
        f"expected within-song next section while on section 1, got: {first_up_next!r}"

    win.child_window(auto_id="ProjectionNextButton", control_type="Button").wrapper_object().invoke()
    time.sleep(1.0)

    second_up_next = win.child_window(auto_id="NextPreviewContentText", control_type="Text").window_text()
    assert "B SECTION ONE" in second_up_next, \
        f"expected next SONG's first slide once song A's slides are exhausted, got: {second_up_next!r}"
