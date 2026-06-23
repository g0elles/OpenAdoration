"""Live in-app check for the helloao Free Use Bible plugin (no key required): install the
real helloao.oaplugin into a sandbox, launch OpenAdoration, Fetch, and confirm real
translations load — grouped by language (helloao spans many languages, so this also
exercises the language grouping with real data).

Gated on the connector package existing (APIBIBLE_OAPLUGIN's sibling dir); skips in CI.
Hits the public helloao.org API (no key, no limit).
"""
import os, subprocess, time, zipfile
from pathlib import Path

import pytest
from pywinauto import Application

REPO = Path(__file__).resolve().parents[1]
APP = os.environ.get("OA_EXE", str(REPO / "OpenAdoration.WPF/bin/Debug/net10.0-windows/OpenAdoration.exe"))
OAPLUGIN = Path(os.environ.get("HELLOAO_OAPLUGIN", r"D:\Projects\OpenAdoration-plugin-connectors\dist\helloao.oaplugin"))
TITLE = "OpenAdoration"
LAUNCH_TIMEOUT = 30


@pytest.fixture
def oa_helloao(request, tmp_path):
    if not OAPLUGIN.exists():
        pytest.skip(f"helloao package not found: {OAPLUGIN} (run build.ps1 in the plugins repo)")
    assert Path(APP).exists(), f"build first -- missing {APP}"

    data_dir = tmp_path / "oa"
    plugin_dir = data_dir / "plugins" / "helloao"
    plugin_dir.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(OAPLUGIN) as z:
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


def test_helloao_lists_versions_in_app(oa_helloao, request):
    oa = oa_helloao
    oa.child_window(auto_id="NavSettingsButton", control_type="Button").wrapper_object().click_input()
    tab = oa.child_window(auto_id="PluginsTab", control_type="TabItem")
    tab.wait("visible", timeout=10)  # Settings page renders async (scope-per-navigation)
    tab.wrapper_object().select()

    plugins = oa.child_window(auto_id="InstalledPluginsList", control_type="List")
    plugins.wait("visible", timeout=10)
    plugins.descendants(control_type="ListItem")[0].click_input()  # helloao

    oa.child_window(auto_id="FetchVersionsButton", control_type="Button").wrapper_object().invoke()
    versions = oa.child_window(auto_id="VersionsList", control_type="List")
    versions.wait("visible", timeout=15)

    deadline = time.time() + 30
    checks = []
    while time.time() < deadline:
        checks = versions.descendants(control_type="CheckBox")
        if checks:
            break
        time.sleep(0.5)

    assert checks, "helloao returned no versions in-app"
    oa.capture_as_image().save(str(Path(request.node.data_dir).parent / "helloao_versions.png"))
