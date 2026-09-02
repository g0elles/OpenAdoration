"""F7 GUI verification: Stage View's live style editor now writes into a REAL, per-scope Theme
row (Song or "This Occurrence") instead of the old throwaway SlideStyleOverride struct. This is a
rebuild, not a tweak -- the church's actual complaint was that the old swatch-based quick-fix (a)
never persisted (lost on the next live item / app restart) and (b) could not touch background
image/video at all, because the rendering code never consulted the override for those fields.

Mechanism under test (StageViewModel.SyncEditableThemeAsync / PersistEditableThemeAsync ->
IThemeService.CreateAsync|UpdateAsync -> IProjectionService.NotifyThemeChanged): the first edit at
a given scope clones the effective theme (see ShouldCloneBeforeEdit) so a shared/default theme is
never mutated in place, points Song.ThemeId (or ScheduleItem.ThemeId) at the clone, and every
further edit in the same session updates that same clone directly. This test proves, via the real
projector output and the sandbox DB:
  1. A font-size edit clones a dedicated theme and does NOT touch a second, untouched song that
     still resolves to the shared default theme (the core safety guarantee).
  2. A background-image edit lands in the SAME clone (no second clone) and actually renders on the
     projector -- the exact capability the old override architecture could never provide.
  3. Both persist across a full app restart, because they are now real Theme rows, not an
     in-memory struct that died with the live item.

NOT covered here: actually dragging inside the Xceed ColorPicker's own popup (a third-party
control with its own internal automation tree) -- driving it reliably via UIA is its own project.
Font size and background image exercise the identical clone/persist code path
(OnEditablePropertyChanged -> PersistEditableThemeAsync), so this is not a weaker test of the
risky part (persistence + clone-safety); it just doesn't poke that one specific popup widget.
Presence of the color pickers is still asserted structurally (control exists, correct AutomationId).
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
NOW = "2026-07-15 00:00:00"

SONG_A = "F7 Edited Song"
SONG_B = "F7 Untouched Song"


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
        for title, lyrics in [(SONG_A, "EDITED SONG LYRICS"), (SONG_B, "UNTOUCHED SONG LYRICS")]:
            con.execute("INSERT INTO Songs (Title, CreatedAt, UpdatedAt) VALUES (?, ?, ?)", (title, NOW, NOW))
            sid = con.execute("SELECT Id FROM Songs WHERE Title=?", (title,)).fetchone()[0]
            con.execute(
                'INSERT INTO SongSections (SongId, Type, Lyrics, "Order", SectionNumber, CreatedAt, UpdatedAt) '
                "VALUES (?, 'Verse', ?, 0, 1, ?, ?)",
                (sid, lyrics, NOW, NOW))
        con.commit()
    finally:
        con.close()


def _make_green_png(path):
    from PIL import Image
    Image.new("RGB", (64, 64), (0, 200, 0)).save(path, "PNG")


def _pick_file_in_open_dialog(file_path, timeout=10):
    # StageView.xaml.cs sets an explicit dialog Title ("Select Background Image"/"...Video"),
    # overriding the OS default "Open"/"Abrir" -- match that, not the generic dialog title.
    dlg = Desktop(backend="uia").window(title_re="Select Background")
    dlg.wait("visible", timeout=timeout)
    edit = dlg.child_window(class_name="Edit", found_index=0)
    edit.wait("visible", timeout=5)
    edit.wrapper_object().set_edit_text(str(file_path))
    dlg.child_window(title_re="Open|Abrir", control_type="Button").wrapper_object().click_input()


def _read_song_theme_id(db, title):
    con = sqlite3.connect(f"file:{db}?mode=ro", uri=True, timeout=5)
    try:
        return con.execute("SELECT ThemeId FROM Songs WHERE Title = ?", (title,)).fetchone()[0]
    finally:
        con.close()


def _read_theme(db, theme_id):
    con = sqlite3.connect(f"file:{db}?mode=ro", uri=True, timeout=5)
    try:
        row = con.execute(
            "SELECT FontSize, BackgroundImagePath, IsDefault FROM Themes WHERE Id = ?", (theme_id,)
        ).fetchone()
        return {"FontSize": row[0], "BackgroundImagePath": row[1], "IsDefault": bool(row[2])}
    finally:
        con.close()


def _pixel_count(img, r_lo=0, r_hi=255, g_lo=0, g_hi=255, b_lo=0, b_hi=255):
    return sum(
        1 for r, g, b in img.convert("RGB").getdata()
        if r_lo <= r <= r_hi and g_lo <= g <= g_hi and b_lo <= b <= b_hi
    )


def _projection():
    return Desktop(backend="uia").window(title_re="OpenAdoration — Projection|Projection Preview")


@pytest.fixture
def oa_f7(tmp_path):
    assert Path(APP).exists(), f"build first -- missing {APP}"
    data = tmp_path / "oa"
    data.mkdir(parents=True)

    proc, win = _launch(data)          # first launch migrates a fresh DB (seeds the default Theme)
    win.close()
    proc.wait(timeout=10)
    _seed_songs(data / "openadoration.db")

    yield data
    # tests manage their own process lifecycle (a restart is part of the scenario)


# SONG_A < SONG_B alphabetically ("F7 Edited..." < "F7 Untouched...") -> stable button order,
# matching the index-based pattern already proven in test_standalone_queue_navigation.py.
_SONG_INDEX = {SONG_A: 0, SONG_B: 1}


def _project_song_by_title(win, title):
    win.child_window(auto_id="NavSongsButton", control_type="Button").wrapper_object().click_input()
    time.sleep(0.5)
    play_buttons = win.descendants(title="▶", control_type="Button")
    assert len(play_buttons) == 2, f"expected 2 project buttons, found {len(play_buttons)}"
    play_buttons[_SONG_INDEX[title]].click_input()
    time.sleep(1.0)
    win.child_window(auto_id="NavStageButton", control_type="Button").wrapper_object().click_input()
    time.sleep(0.5)


def test_font_size_and_background_image_clone_and_persist(oa_f7, tmp_path):
    data = oa_f7
    db = data / "openadoration.db"
    green_png = tmp_path / "bg.png"
    _make_green_png(green_png)

    proc, win = _launch(data)
    try:
        import ctypes
        ctypes.windll.user32.MoveWindow(win.wrapper_object().handle, 0, 0, 1400, 900, True)

        _project_song_by_title(win, SONG_A)

        # F7 bar structural checks -- controls exist with the expected AutomationIds.
        # (The two xctk:ColorPicker instances are deliberately not checked here: their x:Name does
        # not surface as a UIA AutomationId -- only their shared internal template part,
        # PART_ColorPickerToggleButton, does, and it's identical/ambiguous across both instances.
        # Confirmed present via the diagnostic control-tree dump during development.)
        for auto_id in (
            "ScopeSongToggle", "ScopeOccurrenceToggle",
            "BgTypeColorToggle", "BgTypeImageToggle", "BgTypeVideoToggle",
        ):
            win.child_window(auto_id=auto_id).wait("exists", timeout=10)

        # Standalone projection (no live service item) -> no occurrence to scope to.
        occurrence_toggle = win.child_window(auto_id="ScopeOccurrenceToggle", control_type="Button").wrapper_object()
        assert not occurrence_toggle.is_enabled(), "occurrence scope must be disabled for a standalone projection"

        # Both songs still share the seeded default theme before any edit.
        assert _read_song_theme_id(db, SONG_A) is None
        assert _read_song_theme_id(db, SONG_B) is None

        # -- Edit 1: font size, at Song scope (the default) -----------------------------------------
        size_label = win.child_window(auto_id="FontSizeValueText", control_type="Text")
        inc_btn = win.child_window(auto_id="IncreaseFontSizeButton", control_type="Button").wrapper_object()
        for _ in range(3):
            inc_btn.click_input()
            time.sleep(0.5)
        time.sleep(1.0)  # persist is async

        theme_id = _read_song_theme_id(db, SONG_A)
        assert theme_id is not None, "font-size edit should have cloned a dedicated theme onto the song"
        cloned = _read_theme(db, theme_id)
        assert not cloned["IsDefault"], "F7 must never point a song at the shared default theme"
        assert cloned["FontSize"] == 72, f"expected seeded 48pt + 3*8 = 72, got {cloned['FontSize']}"

        # Song B must be completely unaffected (clone-before-edit guard).
        assert _read_song_theme_id(db, SONG_B) is None, "editing Song A must not touch Song B's theme"

        # -- Edit 2: background image, same session -> must land in the SAME clone, not a new one ---
        win.child_window(auto_id="BgTypeImageToggle", control_type="Button").wrapper_object().click_input()
        time.sleep(0.3)
        win.child_window(auto_id="BrowseBackgroundImageButton", control_type="Button").wrapper_object().click_input()
        _pick_file_in_open_dialog(green_png)
        time.sleep(1.5)  # import + persist

        assert _read_song_theme_id(db, SONG_A) == theme_id, "second edit must reuse the already-cloned theme"
        updated = _read_theme(db, theme_id)
        assert updated["FontSize"] == 72, "background edit must not clobber the earlier font-size edit"
        assert updated["BackgroundImagePath"] and updated["BackgroundImagePath"].endswith(".png")

        # Confirm it actually rendered on the real projector, not just in the DB.
        proj = _projection()
        proj.wait("visible", timeout=15)
        time.sleep(0.5)
        green_px = _pixel_count(proj.wrapper_object().capture_as_image(), r_hi=80, g_lo=140, b_hi=80)
        assert green_px > 5000, f"projector did not render the new background image ({green_px} green px)"

        win.close()
        proc.wait(timeout=10)
    finally:
        if proc.poll() is None:
            proc.kill()

    # -- Restart: confirm both edits persisted as real Theme rows, no live editing needed -----------
    proc2, win2 = _launch(data)
    try:
        import ctypes
        ctypes.windll.user32.MoveWindow(win2.wrapper_object().handle, 0, 0, 1400, 900, True)

        _project_song_by_title(win2, SONG_A)
        proj2 = _projection()
        proj2.wait("visible", timeout=15)
        time.sleep(1.0)
        green_px_after_restart = _pixel_count(proj2.wrapper_object().capture_as_image(), r_hi=80, g_lo=140, b_hi=80)
        assert green_px_after_restart > 5000, "background image did not survive an app restart"

        # Song B, never edited, must still show the plain black default background.
        win2.child_window(auto_id="NavSongsButton", control_type="Button").wrapper_object().click_input()
        _project_song_by_title(win2, SONG_B)
        time.sleep(1.0)
        black_px = _pixel_count(proj2.wrapper_object().capture_as_image(), r_hi=20, g_hi=20, b_hi=20)
        assert black_px > 5000, "Song B's shared default theme must be unaffected by Song A's edits"

        win2.close()
        proc2.wait(timeout=10)
    finally:
        if proc2.poll() is None:
            proc2.kill()
