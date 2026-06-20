from pathlib import Path


def test_app_launches(oa):
    assert oa.exists() and oa.is_visible()
    oa.capture_as_image()  # raises if the window isn't really rendered


def test_data_dir_is_isolated(oa, request):
    # OA_DATA_DIR worked: the app created its DB inside the sandbox, not in real LocalAppData
    sandbox_db = Path(request.node.data_dir) / "openadoration.db"
    assert sandbox_db.exists(), "app should have created its DB inside OA_DATA_DIR"
