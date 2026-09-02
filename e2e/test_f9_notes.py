"""F9 GUI verification: Notes/Sermon is a real library content type (full CRUD), like Songs --
not a bare reference like Bible. A schedule item just points at a Note (NoteId), mirroring
SongScheduleItem/SongId. F7 (live style editor) supports both scopes for Notes -- "Song" (the
note's own theme) and "This Occurrence" (when live via a service) -- exactly like Song, unlike the
Bible-shaped single-scope treatment the first F9 pass wrongly used.

Covers:
1. A seeded standalone Note: browse to it in the Notes library list, Project it, confirm the right
   slide count/content, and F7 offers "Song" scope but not "This Occurrence" (no schedule item).
2. A service-driven Notes item (seeded directly in the sandbox DB, bypassing the Add-Notes-panel UI
   -- same approach test_f7_stage_quick_style.py uses for songs) renders correctly live, and F7
   offers BOTH "Song" and "This Occurrence" (Notes is a real library entity, unlike Bible).
"""
import os
import sqlite3
import subprocess
import time
from pathlib import Path

import pytest
from pywinauto import Desktop
from pywinauto.application import Application

REPO = Path(__file__).resolve().parents[1]
APP = os.environ.get("OA_EXE", str(REPO / "OpenAdoration.WPF/bin/Debug/net10.0-windows/OpenAdoration.exe"))
TITLE = "OpenAdoration"
LAUNCH_TIMEOUT = 30
NOW = "2026-09-02 00:00:00"

NOTE_TITLE = "F9 Test Sermon"
NOTE_CONTENT = "First point here.\n\nSecond point here.\n\nThird point here."


def _launch(data_dir):
    env = os.environ.copy()
    env["OA_DATA_DIR"] = str(data_dir)
    proc = subprocess.Popen([APP], env=env)
    win = Application(backend="uia").connect(process=proc.pid, timeout=LAUNCH_TIMEOUT).window(title=TITLE)
    win.wait("visible", timeout=LAUNCH_TIMEOUT)
    return proc, win


def _seed_note(db, title=NOTE_TITLE, content=NOTE_CONTENT):
    con = sqlite3.connect(db)
    try:
        con.execute(
            "INSERT INTO Notes (Title, Content, CreatedAt, UpdatedAt) VALUES (?, ?, ?, ?)",
            (title, content, NOW, NOW))
        con.commit()
        return con.execute("SELECT Id FROM Notes WHERE Title=?", (title,)).fetchone()[0]
    finally:
        con.close()


def _seed_service_with_notes_item(db, note_id):
    con = sqlite3.connect(db)
    try:
        con.execute(
            "INSERT INTO WorshipServices (Name, Date, CreatedAt, UpdatedAt) VALUES (?, ?, ?, ?)",
            ("F9 Test Service", NOW, NOW, NOW))
        service_id = con.execute("SELECT Id FROM WorshipServices WHERE Name=?", ("F9 Test Service",)).fetchone()[0]
        con.execute(
            'INSERT INTO ScheduleItems (ServiceId, "Order", ItemType, NoteId, CreatedAt, UpdatedAt) '
            "VALUES (?, 0, 'Notes', ?, ?, ?)",
            (service_id, note_id, NOW, NOW))
        con.commit()
    finally:
        con.close()


def _read_note_theme_id(db, note_id):
    con = sqlite3.connect(f"file:{db}?mode=ro", uri=True, timeout=5)
    try:
        return con.execute("SELECT ThemeId FROM Notes WHERE Id = ?", (note_id,)).fetchone()[0]
    finally:
        con.close()


def _read_schedule_item_theme_id(db, note_id):
    con = sqlite3.connect(f"file:{db}?mode=ro", uri=True, timeout=5)
    try:
        return con.execute("SELECT ThemeId FROM ScheduleItems WHERE NoteId = ?", (note_id,)).fetchone()[0]
    finally:
        con.close()


def _read_theme_font_size(db, theme_id):
    con = sqlite3.connect(f"file:{db}?mode=ro", uri=True, timeout=5)
    try:
        return con.execute("SELECT FontSize, IsDefault FROM Themes WHERE Id = ?", (theme_id,)).fetchone()
    finally:
        con.close()


def _projection():
    proj = Desktop(backend="uia").window(title_re="OpenAdoration — Projection|Projection Preview")
    proj.wait("visible", timeout=15)
    return proj


def _slide_text():
    return _projection().child_window(auto_id="SlideTextBlock", control_type="Text").wrapper_object().window_text()


@pytest.fixture
def oa_notes(tmp_path):
    assert Path(APP).exists(), f"build first -- missing {APP}"
    data = tmp_path / "oa"
    data.mkdir(parents=True)

    proc, win = _launch(data)  # first launch migrates a fresh DB (seeds the default Theme)
    win.close()
    proc.wait(timeout=10)
    yield data


def test_standalone_note_project_and_f7_song_scope_only(oa_notes):
    data = oa_notes
    note_id = _seed_note(data / "openadoration.db")

    proc, win = _launch(data)
    try:
        win.child_window(auto_id="NavNotesButton", control_type="Button").wrapper_object().click_input()
        time.sleep(0.5)

        # Exactly one seeded note -> the single "▶" row button is unambiguous (mirrors the F7 Song
        # test's index-based row lookup; Add/Edit/Delete panel rows have no per-item auto_id).
        project_buttons = win.descendants(title="▶", control_type="Button")
        assert len(project_buttons) == 1, f"expected 1 note row, found {len(project_buttons)}"
        project_buttons[0].click_input()
        time.sleep(0.5)

        # Auto-navigated to Stage View -- confirm via the projection control bar's label.
        label = win.child_window(auto_id="ContextLabelText", control_type="Text").wrapper_object().window_text()
        assert NOTE_TITLE in label, f"expected context label to show '{NOTE_TITLE}', got '{label}'"

        assert "First point here." in _slide_text()

        next_btn = win.child_window(auto_id="ProjectionNextButton", control_type="Button").wrapper_object()
        next_btn.click_input()
        time.sleep(0.3)
        assert "Second point here." in _slide_text()
        next_btn.click_input()
        time.sleep(0.3)
        assert "Third point here." in _slide_text()

        # F7: standalone Note is a real library entity -> "Song" scope (its own theme) is
        # available, but "This Occurrence" is not (no schedule item).
        occurrence_toggle = win.child_window(auto_id="ScopeOccurrenceToggle", control_type="Button").wrapper_object()
        song_toggle = win.child_window(auto_id="ScopeSongToggle", control_type="Button").wrapper_object()
        assert song_toggle.is_enabled(), "Song scope must be available -- Notes is a real library entity"
        assert not occurrence_toggle.is_enabled(), "This Occurrence must be disabled -- no schedule item"

        # A style edit should still apply live without losing slide content, and should patch the
        # NOTE's own ThemeId (Song-equivalent scope), not any app-wide setting.
        inc_btn = win.child_window(auto_id="IncreaseFontSizeButton", control_type="Button").wrapper_object()
        inc_btn.click_input()
        time.sleep(1.0)  # persist is async
        assert "Third point here." in _slide_text(), "style edit must not clobber slide content"

        db = data / "openadoration.db"
        assert _read_note_theme_id(db, note_id) is not None, "font-size edit should have cloned a theme onto the Note"

        win.close()
        proc.wait(timeout=10)
    finally:
        if proc.poll() is None:
            proc.kill()


def test_service_notes_item_projects_and_f7_both_scopes_available(oa_notes):
    data = oa_notes
    note_id = _seed_note(data / "openadoration.db")
    _seed_service_with_notes_item(data / "openadoration.db", note_id)

    proc, win = _launch(data)
    try:
        win.child_window(auto_id="NavScheduleButton", control_type="Button").wrapper_object().click_input()
        time.sleep(0.5)

        win.child_window(title_re="^Open$|^Abrir$", control_type="Button").wrapper_object().click_input()
        time.sleep(0.5)
        win.child_window(title_re="▶ Start Service|▶ Iniciar servicio", control_type="Button") \
            .wrapper_object().click_input()
        time.sleep(0.8)

        assert "First point here." in _slide_text()

        win.child_window(auto_id="NavStageButton", control_type="Button").wrapper_object().click_input()
        time.sleep(0.5)

        occurrence_toggle = win.child_window(auto_id="ScopeOccurrenceToggle", control_type="Button").wrapper_object()
        song_toggle = win.child_window(auto_id="ScopeSongToggle", control_type="Button").wrapper_object()
        assert occurrence_toggle.is_enabled(), "This Occurrence must be available for a service Notes item"
        assert song_toggle.is_enabled(), "Song scope must ALSO be available -- Notes is a real library entity, unlike Bible"

        db = data / "openadoration.db"
        assert _read_schedule_item_theme_id(db, note_id) is None

        # Default scope is "Song" -- editing here should patch the Note's own ThemeId, not the
        # schedule item's (mirrors Song's default-scope behavior exactly).
        inc_btn = win.child_window(auto_id="IncreaseFontSizeButton", control_type="Button").wrapper_object()
        for _ in range(2):
            inc_btn.click_input()
            time.sleep(0.5)
        time.sleep(1.0)

        theme_id = _read_note_theme_id(db, note_id)
        assert theme_id is not None, "font-size edit at Song scope should have cloned a theme onto the Note"
        assert _read_schedule_item_theme_id(db, note_id) is None, "Song-scope edit must not touch the schedule item"
        font_size, is_default = _read_theme_font_size(db, theme_id)
        assert not is_default, "F7 must never point a Note at the shared default theme"
        assert font_size == 64, f"expected seeded 48pt + 2*8 = 64, got {font_size}"
        assert "First point here." in _slide_text(), "style edit must not clobber slide content"

        win.close()
        proc.wait(timeout=10)
    finally:
        if proc.poll() is None:
            proc.kill()


def test_notes_crud_create_edit_delete(oa_notes):
    data = oa_notes
    proc, win = _launch(data)
    try:
        win.child_window(auto_id="NavNotesButton", control_type="Button").wrapper_object().click_input()
        time.sleep(0.5)

        # ── Create ──
        win.child_window(title_re=r"^\+ New$|^\+ Nuevo$", control_type="Button").wrapper_object().click_input()
        time.sleep(0.3)
        win.child_window(auto_id="NoteEditTitleBox", control_type="Edit").wait("exists", timeout=5)
        win.child_window(auto_id="NoteEditTitleBox", control_type="Edit").wrapper_object().set_edit_text(NOTE_TITLE)
        win.child_window(auto_id="NoteEditContentBox", control_type="Edit").wrapper_object().set_edit_text(NOTE_CONTENT)
        time.sleep(0.2)

        win.child_window(title_re="Save Note|Guardar nota", control_type="Button").wrapper_object().click_input()
        time.sleep(1.2)  # IsEditing=false + async LoadAsync reload

        db = data / "openadoration.db"
        con = sqlite3.connect(str(db))
        row = con.execute("SELECT Title, Content FROM Notes WHERE Title=?", (NOTE_TITLE,)).fetchone()
        con.close()
        assert row is not None, "note was not created"
        assert row[1] == NOTE_CONTENT

        # ── Edit ──
        win.child_window(title="✎", control_type="Button").wrapper_object().click_input()
        win.child_window(auto_id="NoteEditContentBox", control_type="Edit").wait("exists", timeout=5)
        win.child_window(auto_id="NoteEditContentBox", control_type="Edit").wrapper_object().set_edit_text(
            NOTE_CONTENT + "\n\nFourth point here.")
        win.child_window(title_re="Save Note|Guardar nota", control_type="Button").wrapper_object().click_input()
        time.sleep(1.2)

        con = sqlite3.connect(str(db))
        row = con.execute("SELECT Content FROM Notes WHERE Title=?", (NOTE_TITLE,)).fetchone()
        con.close()
        assert "Fourth point here." in row[0], "edit did not persist"

        # ── Delete ──
        delete_buttons = win.descendants(title="✕", control_type="Button")
        assert len(delete_buttons) == 1, f"expected 1 delete button, found {len(delete_buttons)}"
        delete_buttons[0].click_input()
        try:
            confirm = Desktop(backend="uia").window(title_re="Delete Note|Eliminar nota")
            confirm.wait("visible", timeout=10)
        except Exception:
            win.capture_as_image().save(str(data / "debug_after_delete_click.png"))
            raise
        confirm.child_window(title_re="^(Yes|Sí)$", control_type="Button").wrapper_object().click_input()
        time.sleep(1.0)

        con = sqlite3.connect(str(db))
        row = con.execute("SELECT Id FROM Notes WHERE Title=?", (NOTE_TITLE,)).fetchone()
        con.close()
        assert row is None, "note was not deleted"

        win.close()
        proc.wait(timeout=10)
    finally:
        if proc.poll() is None:
            proc.kill()
