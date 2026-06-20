"""Round-trip proof for G27 Phase 3: the Settings appearance picker must live-swap
the chrome and persist AppSettings.Appearance (Dark=0 / Light=1).

Language-safe: the combo items are localized (Dark/Light vs Oscuro/Claro), so Light
is selected by index (1), not by title. Persistence is asserted by reading the sandbox
settings.json directly, so it never depends on UI text."""
import json
import time
from pathlib import Path


def _read_appearance(settings: Path):
    if not settings.exists():
        return None
    return json.loads(settings.read_text(encoding="utf-8")).get("Appearance")


def test_appearance_round_trips(oa, request):
    settings = Path(request.node.data_dir) / "settings.json"
    # default sandbox: no field or 0 (Dark)
    assert _read_appearance(settings) in (None, 0), "should start Dark"

    oa.child_window(auto_id="NavSettingsButton", control_type="Button").wrapper_object().click_input()

    combo = oa.child_window(auto_id="AppearanceCombo", control_type="ComboBox")
    combo.wait("visible", timeout=10)
    combo.wrapper_object().select(1)  # AvailableAppearances[1] == Light

    # Save sits at the bottom of a ScrollViewer; invoke via UIA (off-screen-safe), not physical click.
    oa.child_window(auto_id="SaveSettingsButton", control_type="Button").wrapper_object().invoke()

    deadline = time.time() + 10
    value = None
    while time.time() < deadline:
        value = _read_appearance(settings)
        if value == 1:
            break
        time.sleep(0.3)
    assert value == 1, f"expected Light (1) persisted, got {value!r}"

    # Visual proof for the human reviewer: full Light chrome (live swap held) +
    # the green "Saved ✓" confirmation rendering via the new SuccessBrush.
    oa.capture_as_image().save(str(Path(request.node.data_dir).parent / "appearance_light.png"))
