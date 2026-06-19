"""Round-trip proof for per-theme SlideTransition (M14): editing the seeded theme's
transition must persist through ThemeRepository.UpdateAsync (the M14.3-class bug surface).

Language-safe: the edit glyph (✎) and combo item names (Fade/Slide/Zoom) are not localized;
nav + save buttons are reached by AutomationId. Persistence is asserted by reading the
sandbox DB directly, so the assertion never depends on UI text."""
import sqlite3
import time
from pathlib import Path


def _read_seed_transition(db: Path):
    # read-only, short timeout; the app may still hold a write connection
    con = sqlite3.connect(f"file:{db}?mode=ro", uri=True, timeout=5)
    try:
        return con.execute("SELECT SlideTransition FROM Themes WHERE Id = 1").fetchone()[0]
    finally:
        con.close()


def test_theme_transition_round_trips(oa, request):
    db = Path(request.node.data_dir) / "openadoration.db"
    assert _read_seed_transition(db) is None, "seeded theme should start with no transition (inherit)"

    oa.child_window(auto_id="NavThemesButton", control_type="Button").wrapper_object().click_input()
    # fresh DB has exactly one theme -> exactly one edit glyph
    oa.child_window(title="✎", control_type="Button").wait("visible", timeout=10).click_input()

    combo = oa.child_window(auto_id="TransitionCombo", control_type="ComboBox")
    combo.wait("visible", timeout=10)
    combo.wrapper_object().select("Slide")  # SlideTransitionKind.Slide == 1

    oa.child_window(auto_id="SaveThemeButton", control_type="Button").wrapper_object().click_input()

    # poll the DB until the save lands
    deadline = time.time() + 10
    value = None
    while time.time() < deadline:
        value = _read_seed_transition(db)
        if value is not None:
            break
        time.sleep(0.3)
    assert value == 1, f"expected Slide (1) persisted via UpdateAsync, got {value!r}"
