"""Phase A proof for M13.4 plugin UI: the Settings -> Plugins tab is now visible, a
loaded Bible-source plugin offers a checkbox version list + filter box, and "Import
selected" imports the ticked versions into the Bible library.

Isolation: the plugin is pre-seeded into the sandbox plugins dir (which honours
OA_DATA_DIR), so the real %LOCALAPPDATA% plugins are never touched. The import is
asserted at the DB level (BibleVersion 'ECHO'), language-independently; the dialog is
dismissed with ENTER so the title's locale doesn't matter.
"""
import os, sqlite3, subprocess, time, zipfile
from pathlib import Path

import pytest
from pywinauto import Application
from pywinauto.keyboard import send_keys

REPO = Path(__file__).resolve().parents[1]
APP = os.environ.get("OA_EXE", str(REPO / "OpenAdoration.WPF/bin/Debug/net10.0-windows/OpenAdoration.exe"))
SAMPLE = REPO / "installer" / "sample.echo.oaplugin"
TITLE = "OpenAdoration"
LAUNCH_TIMEOUT = 30


@pytest.fixture
def oa_plugin(request, tmp_path):
    """Fresh isolated app with sample.echo pre-installed in the sandbox plugins dir."""
    assert Path(APP).exists(), f"build first -- missing {APP}"
    assert SAMPLE.exists(), f"missing fixture {SAMPLE}"
    data_dir = tmp_path / "oa"
    plugin_dir = data_dir / "plugins" / "sample.echo"
    plugin_dir.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(SAMPLE) as z:
        z.extractall(plugin_dir)

    env = os.environ.copy()
    env["OA_DATA_DIR"] = str(data_dir)
    proc = subprocess.Popen([APP], env=env)
    win = Application(backend="uia").connect(process=proc.pid, timeout=LAUNCH_TIMEOUT).window(title=TITLE)
    win.wait("visible", timeout=LAUNCH_TIMEOUT)
    request.node.data_dir = data_dir
    yield win

    if getattr(getattr(request.node, "rep_call", None), "failed", False):
        art = REPO / "e2e" / "artifacts"; art.mkdir(parents=True, exist_ok=True)
        try: win.capture_as_image().save(str(art / f"FAIL_{request.node.name}.png"))
        except Exception: pass
    try:
        win.close(); proc.wait(timeout=5)
    except Exception:
        proc.kill()


def _echo_version_count(db: Path):
    if not db.exists():
        return 0
    con = sqlite3.connect(f"file:{db}?mode=ro", uri=True)
    try:
        n_ver = con.execute("SELECT COUNT(*) FROM BibleVersions WHERE Abbreviation='ECHO'").fetchone()[0]
        n_vrs = con.execute("SELECT COUNT(*) FROM BibleVerses").fetchone()[0]
        return n_ver and n_vrs
    finally:
        con.close()


def test_plugins_tab_import_flow(oa_plugin, request):
    oa = oa_plugin
    data_dir = Path(request.node.data_dir)

    # 1. Settings -> Plugins tab (visible again after un-hiding).
    oa.child_window(auto_id="NavSettingsButton", control_type="Button").wrapper_object().click_input()
    tab = oa.child_window(auto_id="PluginsTab", control_type="TabItem")
    tab.wait("visible", timeout=10)  # Settings page renders async (scope-per-navigation)
    tab.wrapper_object().select()

    # 2. Select the pre-loaded plugin -> reveals the bible-source panel.
    plugins = oa.child_window(auto_id="InstalledPluginsList", control_type="List")
    plugins.wait("visible", timeout=10)
    plugins.descendants(control_type="ListItem")[0].click_input()

    # 3. Fetch versions (Echo returns one), confirm filter box + checkbox list rendered.
    oa.child_window(auto_id="FetchVersionsButton", control_type="Button").wrapper_object().invoke()
    oa.child_window(auto_id="VersionFilterBox", control_type="Edit").wait("visible", timeout=10)
    versions = oa.child_window(auto_id="VersionsList", control_type="List")
    versions.wait("visible", timeout=10)

    deadline = time.time() + 10
    checks = []
    while time.time() < deadline:
        checks = versions.descendants(control_type="CheckBox")
        if checks:
            break
        time.sleep(0.3)
    assert checks, "fetched versions did not render as checkboxes"

    # Screenshot for the human reviewer: tab visible, filter box, checkbox version list.
    oa.capture_as_image().save(str(data_dir.parent / "plugins_tab.png"))

    # 4. Tick the version and import.
    checks[0].toggle()
    oa.child_window(auto_id="ImportSelectedButton", control_type="Button").wrapper_object().invoke()

    # 5. The import writes to the DB, then an Inform dialog pops. Poll the DB (the write
    #    precedes the dialog), then ENTER to dismiss the (localized-title) dialog.
    deadline = time.time() + 15
    ok = 0
    while time.time() < deadline:
        ok = _echo_version_count(data_dir / "openadoration.db")
        if ok:
            break
        send_keys("{ENTER}")
        time.sleep(0.5)
    assert ok, "ECHO version / verses were not imported into the sandbox DB"
    send_keys("{ENTER}")  # clear the confirmation dialog so the app can close
