"""Legal-protection proof: removing a Bible-source plugin deletes the Bible versions it
downloaded, so licensed content never outlives the plugin/licence that provided it.

Drives the real app: pre-seed sample.echo, import its ECHO version (asserted in the DB),
then remove the plugin and assert the version + verses are gone from the DB. Isolated via
OA_DATA_DIR; dialogs dismissed with ENTER so locale doesn't matter.
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
        return con.execute("SELECT COUNT(*) FROM BibleVersions WHERE Abbreviation='ECHO'").fetchone()[0]
    finally:
        con.close()


def _open_plugins_tab(oa):
    oa.child_window(auto_id="NavSettingsButton", control_type="Button").wrapper_object().click_input()
    tab = oa.child_window(auto_id="PluginsTab", control_type="TabItem")
    tab.wait("visible", timeout=10)
    tab.wrapper_object().select()
    plugins = oa.child_window(auto_id="InstalledPluginsList", control_type="List")
    plugins.wait("visible", timeout=10)
    return plugins


def test_removing_plugin_deletes_its_bibles(oa_plugin, request):
    oa = oa_plugin
    db = Path(request.node.data_dir) / "openadoration.db"

    # 1. Import ECHO via the plugin.
    plugins = _open_plugins_tab(oa)
    plugins.descendants(control_type="ListItem")[0].click_input()
    oa.child_window(auto_id="FetchVersionsButton", control_type="Button").wrapper_object().invoke()
    versions = oa.child_window(auto_id="VersionsList", control_type="List")
    versions.wait("visible", timeout=10)
    deadline = time.time() + 10
    checks = []
    while time.time() < deadline:
        checks = versions.descendants(control_type="CheckBox")
        if checks:
            break
        time.sleep(0.3)
    assert checks, "fetched versions did not render"
    checks[0].toggle()
    oa.child_window(auto_id="ImportSelectedButton", control_type="Button").wrapper_object().invoke()

    deadline = time.time() + 15
    while time.time() < deadline:
        if _echo_version_count(db):
            break
        send_keys("{ENTER}")
        time.sleep(0.5)
    assert _echo_version_count(db), "ECHO was not imported — precondition failed"
    send_keys("{ENTER}")  # dismiss the import-complete dialog

    # 2. Remove the plugin → confirm. The version it downloaded must be gone.
    plugins.descendants(control_type="ListItem")[0].click_input()
    oa.child_window(auto_id="RemovePluginButton", control_type="Button").wrapper_object().invoke()

    deadline = time.time() + 15
    gone = False
    while time.time() < deadline:
        send_keys("{ENTER}")  # accept the "this also deletes N Bibles" confirmation
        time.sleep(0.5)
        if _echo_version_count(db) == 0:
            gone = True
            break
    assert gone, "ECHO version survived plugin removal — licensed content would outlive the plugin"

    con = sqlite3.connect(f"file:{db}?mode=ro", uri=True)
    try:
        assert con.execute("SELECT COUNT(*) FROM BibleVerses").fetchone()[0] == 0, "verses orphaned after removal"
    finally:
        con.close()
