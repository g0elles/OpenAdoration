import os, shutil, subprocess, pytest
from pathlib import Path
from pywinauto import Application

REPO = Path(__file__).resolve().parents[1]
APP  = os.environ.get("OA_EXE", str(REPO / "OpenAdoration.WPF/bin/Debug/net10.0-windows/OpenAdoration.exe"))
TITLE = "OpenAdoration"
LAUNCH_TIMEOUT = 30   # WPF cold start + EF migrate on first run is slow


@pytest.fixture
def oa(request, tmp_path):
    """Fresh isolated OpenAdoration via OA_DATA_DIR -> clean DB, real data untouched."""
    assert Path(APP).exists(), f"build first -- missing {APP}"
    data_dir = tmp_path / "oa"
    data_dir.mkdir(parents=True, exist_ok=True)

    seed = REPO / "e2e" / "fixtures" / "seed.db"
    if seed.exists():
        shutil.copy(seed, data_dir / "openadoration.db")

    env = os.environ.copy()
    env["OA_DATA_DIR"] = str(data_dir)

    proc = subprocess.Popen([APP], env=env)
    win = Application(backend="uia").connect(process=proc.pid, timeout=LAUNCH_TIMEOUT).window(title=TITLE)
    win.wait("visible", timeout=LAUNCH_TIMEOUT)
    request.node.data_dir = data_dir   # tests read back settings.json via request.node.data_dir
    yield win

    if getattr(getattr(request.node, "rep_call", None), "failed", False):
        art = REPO / "e2e" / "artifacts"; art.mkdir(parents=True, exist_ok=True)
        try: win.capture_as_image().save(str(art / f"FAIL_{request.node.name}.png"))
        except Exception: pass
    try:
        win.close(); proc.wait(timeout=5)
    except Exception:
        proc.kill()


@pytest.hookimpl(tryfirst=True, hookwrapper=True)
def pytest_runtest_makereport(item, call):
    outcome = yield
    setattr(item, f"rep_{outcome.get_result().when}", outcome.get_result())
