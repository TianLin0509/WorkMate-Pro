from __future__ import annotations

import multiprocessing
import sys
import traceback
from pathlib import Path

from autopagecapture.windows import enable_dpi_awareness


def _show_startup_error(message: str) -> None:
    import ctypes

    ctypes.windll.user32.MessageBoxW(
        0,
        message,
        "自动翻页截图工具 - 启动失败",
        0x10,
    )


def main() -> int:
    enable_dpi_awareness()
    try:
        from autopagecapture.ui import run_app

        run_app()
        return 0
    except Exception:
        details = traceback.format_exc()
        base_dir = Path(sys.executable).resolve().parent if getattr(sys, "frozen", False) else Path.cwd()
        try:
            (base_dir / "AutoPageCapture-crash.log").write_text(details, encoding="utf-8")
        except OSError:
            pass
        _show_startup_error(f"工具启动失败。\n\n{details[-1600:]}")
        return 1


if __name__ == "__main__":
    multiprocessing.freeze_support()
    raise SystemExit(main())
