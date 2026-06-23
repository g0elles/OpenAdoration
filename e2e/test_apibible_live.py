"""Live in-app acceptance for the API.Bible connector (M13.4): install the real
apibible.oaplugin into a sandbox, launch OpenAdoration, and confirm Fetch lists the
real versions the key can access — proving the plugin loads in the actual app, the
masked key setting is used, and the live API call works through the UI.

Key-safe + gated: skips unless APIBIBLE_KEY is set; the key is written only into the
throwaway sandbox plugin dir (never committed). Connector package located via
APIBIBLE_OAPLUGIN (default: the sibling repo's dist build).

A whole-Bible *import* is deliberately not driven here — it's ~1,250 live requests and
only commits after the full fetch; that pipeline is already covered by the connector's
live parser test + the sample.echo DTO→DB test. This asserts the in-app live wiring.
"""
import json, os, subprocess, time, zipfile
from pathlib import Path

import pytest
from pywinauto import Application

REPO = Path(__file__).resolve().parents[1]
APP = os.environ.get("OA_EXE", str(REPO / "OpenAdoration.WPF/bin/Debug/net10.0-windows/OpenAdoration.exe"))
OAPLUGIN = Path(os.environ.get("APIBIBLE_OAPLUGIN", r"D:\Projects\OpenAdoration-plugin-connectors\dist\apibible.oaplugin"))
KEY = os.environ.get("APIBIBLE_KEY")
TITLE = "OpenAdoration"
LAUNCH_TIMEOUT = 30


@pytest.fixture
def oa_apibible(request, tmp_path):
    if not KEY:
        pytest.skip("set APIBIBLE_KEY to run the live in-app API.Bible test")
    if not OAPLUGIN.exists():
        pytest.skip(f"connector package not found: {OAPLUGIN} (run build.ps1 in the apibible repo)")
    assert Path(APP).exists(), f"build first -- missing {APP}"

    data_dir = tmp_path / "oa"
    plugin_dir = data_dir / "plugins" / "apibible"
    plugin_dir.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(OAPLUGIN) as z:
        z.extractall(plugin_dir)
    (plugin_dir / "settings.json").write_text(json.dumps({"apiKey": KEY}), encoding="utf-8")

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


def test_apibible_lists_versions_in_app(oa_apibible, request):
    oa = oa_apibible
    oa.child_window(auto_id="NavSettingsButton", control_type="Button").wrapper_object().click_input()
    oa.child_window(auto_id="PluginsTab", control_type="TabItem").wrapper_object().select()

    plugins = oa.child_window(auto_id="InstalledPluginsList", control_type="List")
    plugins.wait("visible", timeout=10)
    plugins.descendants(control_type="ListItem")[0].click_input()  # the apibible plugin

    oa.child_window(auto_id="FetchVersionsButton", control_type="Button").wrapper_object().invoke()
    versions = oa.child_window(auto_id="VersionsList", control_type="List")
    versions.wait("visible", timeout=15)

    deadline = time.time() + 30  # live network call
    checks = []
    while time.time() < deadline:
        checks = versions.descendants(control_type="CheckBox")
        if checks:
            break
        time.sleep(0.5)

    assert checks, "API.Bible returned no versions in-app — check key / connectivity"
    oa.capture_as_image().save(str(Path(request.node.data_dir).parent / "apibible_versions.png"))
