"""GUI verification for the standalone-browsing full queue (replaces the earlier single-hop
"standalone next item" design).

Real user feedback on the earlier design: "I can only move to the next item in the list...
If I want to go back I can't, and if I want to continue to another item I can't, only can move
from Item A to B, not to C or even move from B to A." That design (SetStandaloneNextItem, a single
consumed-on-use Slide deck) only supported one forward hop, ever, and no backward movement.

IProjectionService.SetStandaloneQueue now stores the operator's WHOLE displayed list
(SongsViewModel.ProjectSong feeds Songs) so Next()/Previous() can hop freely across every item,
any number of times, in both directions.

Seeds three songs (A, B, C -- one section each, distinct lyrics so they're unambiguous), projects
the first from the Songs page, then drives the main-window Next/Previous transport buttons and
asserts Stage View's context label (song title) at each step:

    project A -> Next -> B -> Next -> C -> Previous -> B -> Previous -> A

This is the exact multi-hop forward + multi-hop backward path the single-hop design could not do.
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

SONGS = [
    ("E2E Queue Song A", "ALPHA VERSE UNIQUE TEXT"),
    ("E2E Queue Song B", "BRAVO VERSE UNIQUE TEXT"),
    ("E2E Queue Song C", "CHARLIE VERSE UNIQUE TEXT"),
]


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
        for title, lyrics in SONGS:
            con.execute("INSERT INTO Songs (Title, CreatedAt, UpdatedAt) VALUES (?, ?, ?)", (title, NOW, NOW))
            sid = con.execute("SELECT Id FROM Songs WHERE Title=?", (title,)).fetchone()[0]
            con.execute(
                'INSERT INTO SongSections (SongId, Type, Lyrics, "Order", SectionNumber, CreatedAt, UpdatedAt) '
                "VALUES (?, 'Verse', ?, 0, 1, ?, ?)",
                (sid, lyrics, NOW, NOW))
        con.commit()
    finally:
        con.close()


@pytest.fixture
def oa_queue(tmp_path):
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


def test_standalone_queue_multi_hop_forward_and_backward(oa_queue):
    win = oa_queue

    win.child_window(auto_id="NavSongsButton", control_type="Button").wrapper_object().click_input()
    time.sleep(0.5)

    # Three songs -> three "Proyectar" ("Common_Project") buttons, sorted by Title -> A, B, C.
    play_buttons = win.descendants(title="▶", control_type="Button")
    assert len(play_buttons) == 3, f"expected 3 project buttons, found {len(play_buttons)}"
    play_buttons[0].click_input()   # project Song A
    time.sleep(1.0)

    win.child_window(auto_id="NavStageButton", control_type="Button").wrapper_object().click_input()
    time.sleep(1.0)

    label = win.child_window(auto_id="ContextLabelText", control_type="Text")
    label.wait("visible", timeout=10)
    assert label.window_text() == "E2E Queue Song A", f"expected Song A live, got {label.window_text()!r}"

    next_btn = win.child_window(auto_id="ProjectionNextButton", control_type="Button")
    prev_btn = win.child_window(auto_id="ProjectionPreviousButton", control_type="Button")

    # Forward: A -> B -> C (the second hop is exactly what the old single-hop design could not do).
    next_btn.wrapper_object().invoke()
    time.sleep(1.0)
    assert label.window_text() == "E2E Queue Song B", f"expected Song B after 1st Next, got {label.window_text()!r}"

    next_btn.wrapper_object().invoke()
    time.sleep(1.0)
    assert label.window_text() == "E2E Queue Song C", f"expected Song C after 2nd Next, got {label.window_text()!r}"

    # Backward: C -> B -> A (backward movement at all was entirely missing in the old design).
    prev_btn.wrapper_object().invoke()
    time.sleep(1.0)
    assert label.window_text() == "E2E Queue Song B", f"expected Song B after 1st Previous, got {label.window_text()!r}"

    prev_btn.wrapper_object().invoke()
    time.sleep(1.0)
    assert label.window_text() == "E2E Queue Song A", f"expected Song A after 2nd Previous, got {label.window_text()!r}"

    # Boundary: at the first item, Previous() must be a no-op (not wrap or error).
    prev_btn.wrapper_object().invoke()
    time.sleep(1.0)
    assert label.window_text() == "E2E Queue Song A", \
        f"expected Previous() at the first item to be a no-op, got {label.window_text()!r}"
