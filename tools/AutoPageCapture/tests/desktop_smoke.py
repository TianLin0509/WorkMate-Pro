"""Manual Windows desktop smoke test.

This opens a disposable scrollable document, captures it through the real
screen/SendInput path, verifies multiple images, then closes only that window.
It is intentionally not part of the normal unittest discovery run.
"""

from __future__ import annotations

import ctypes
import json
import os
import subprocess
import sys
import time
from ctypes import wintypes
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PROJECT_ROOT / "src"))

from autopagecapture.windows import enable_dpi_awareness  # noqa: E402


enable_dpi_awareness()

from PIL import Image  # noqa: E402

from autopagecapture.core import Region, create_run_directory  # noqa: E402
from autopagecapture.smart_capture import SmartCaptureOptions, SmartScrollCaptureEngine  # noqa: E402
from autopagecapture.windows import (  # noqa: E402
    POINT as WINDOWS_POINT,
    capture_region,
    get_cursor_position,
    scroll_target,
    set_cursor_position,
    user32,
)


TARGET_TITLE = "AutoPageCapture Desktop Smoke Target"


class POINT(ctypes.Structure):
    _fields_ = [("x", wintypes.LONG), ("y", wintypes.LONG)]


class RECT(ctypes.Structure):
    _fields_ = [
        ("left", wintypes.LONG),
        ("top", wintypes.LONG),
        ("right", wintypes.LONG),
        ("bottom", wintypes.LONG),
    ]


def run_target() -> None:
    import tkinter as tk

    root = tk.Tk()
    root.title(TARGET_TITLE)
    root.geometry("900x1120+180+180")
    root.attributes("-topmost", True)
    root.configure(bg="#eff4fa")

    canvas = tk.Canvas(
        root,
        bg="white",
        highlightthickness=0,
        yscrollincrement=120,
    )
    canvas.pack(fill="both", expand=True, padx=10, pady=10)
    colors = ("#184e77", "#2a9d8f", "#e76f51", "#7b2cbf")
    y = 30
    for page_number, color in enumerate(colors, 1):
        canvas.create_rectangle(24, y - 10, 836, y + 58, fill="#eef4fb", outline="")
        canvas.create_text(
            32,
            y,
            anchor="nw",
            text=f"测试文档 · 第 {page_number} 页",
            fill=color,
            font=("Microsoft YaHei UI", 16, "bold"),
        )
        y += 78
        for line_number in range(1, 19):
            canvas.create_rectangle(
                24,
                y - 5,
                836,
                y + 49,
                fill="#f7f9fc" if line_number % 2 else "#ffffff",
                outline="",
            )
            canvas.create_text(
                32,
                y + 7,
                anchor="nw",
                width=805,
                text=f"P{page_number}-L{line_number:02d} · 智能对齐真实桌面测试 · 唯一行编号",
                fill="#172033",
                font=("Microsoft YaHei UI", 10),
            )
            y += 62
        y += 48
    canvas.configure(scrollregion=(0, 0, 860, y + 30))
    canvas.yview_moveto(0)

    def on_wheel(event: tk.Event) -> str:
        canvas.yview_scroll(-int(event.delta / 120), "units")
        return "break"

    canvas.bind("<MouseWheel>", on_wheel)
    canvas.focus_set()
    root.mainloop()


def _find_visible_windows_by_title(title: str) -> set[int]:
    found: list[int] = []
    enum_proc_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

    @enum_proc_type
    def callback(hwnd, _lparam):
        if not user32.IsWindowVisible(hwnd):
            return True
        length = user32.GetWindowTextLengthW(hwnd)
        if length <= 0:
            return True
        buffer = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buffer, len(buffer))
        if buffer.value == title:
            found.append(int(hwnd))
        return True

    user32.EnumWindows(callback, 0)
    return set(found)


def _client_region(hwnd: int) -> Region:
    rectangle = RECT()
    origin = POINT(0, 0)
    if not user32.GetClientRect(hwnd, ctypes.byref(rectangle)):
        raise OSError("GetClientRect failed")
    if not user32.ClientToScreen(hwnd, ctypes.byref(origin)):
        raise OSError("ClientToScreen failed")
    # Leave a small margin so the test captures only the document surface.
    return Region(
        origin.x + 16,
        origin.y + 16,
        origin.x + rectangle.right - 16,
        origin.y + rectangle.bottom - 16,
    )


def run_smoke() -> int:
    original_cursor = get_cursor_position()
    original_foreground = int(user32.GetForegroundWindow() or 0)
    existing_target_windows = _find_visible_windows_by_title(TARGET_TITLE)
    process = subprocess.Popen([sys.executable, str(Path(__file__).resolve()), "--target"])
    hwnd = 0
    try:
        deadline = time.monotonic() + 12
        while time.monotonic() < deadline:
            new_windows = _find_visible_windows_by_title(TARGET_TITLE) - existing_target_windows
            if new_windows:
                hwnd = next(iter(new_windows))
                break
            time.sleep(0.1)
        if not hwnd:
            exit_detail = "running" if process.poll() is None else f"exit={process.returncode}"
            raise TimeoutError(f"Timed out waiting for smoke-test target window ({exit_detail})")

        # A background test runner is not always allowed to win SetForegroundWindow.
        # Keep the disposable target above unrelated always-on-top windows so the
        # real cursor/wheel path is verified against the intended surface.
        user32.ShowWindow(hwnd, 9)  # SW_RESTORE
        user32.SetWindowPos(hwnd, -1, 0, 0, 0, 0, 0x0043)  # TOPMOST | NOMOVE | NOSIZE | SHOW
        user32.BringWindowToTop(hwnd)
        user32.SetForegroundWindow(hwnd)
        time.sleep(0.6)
        region = _client_region(hwnd)

        def ensure_target_surface() -> None:
            user32.SetWindowPos(hwnd, -1, 0, 0, 0, 0, 0x0043)
            user32.BringWindowToTop(hwnd)
            user32.SetForegroundWindow(hwnd)
            center_x, center_y = region.center
            user32.SetCursorPos(center_x, center_y)
            window_at_center = user32.WindowFromPoint(WINDOWS_POINT(center_x, center_y))
            root_at_center = user32.GetAncestor(window_at_center, 2) if window_at_center else 0
            if int(root_at_center or 0) != hwnd:
                raise AssertionError("Smoke target is occluded at the scroll point")

        ensure_target_surface()
        output_root = PROJECT_ROOT / "artifacts" / "desktop-smoke-output"
        output_dir = create_run_directory(output_root)
        raw_dir: Path | None = None
        raw_index = 0
        if os.environ.get("AUTOPAGECAPTURE_SMOKE_TRACE") == "1":
            raw_dir = output_dir / "raw_frames"
            raw_dir.mkdir()

        def capture_callback(capture_area: Region) -> Image.Image:
            nonlocal raw_index
            ensure_target_surface()
            time.sleep(0.03)
            image = capture_region(capture_area)
            if raw_dir is not None:
                raw_index += 1
                image.save(raw_dir / f"raw_{raw_index:03d}.png")
            return image

        options = SmartCaptureOptions(
            max_pages=15,
            scroll_delay_seconds=0.25,
            capture_mode="smart",
            image_format="png",
        )
        def smoke_scroll(capture_area: Region, mode: str, wheel_notches: int) -> None:
            # The user's real browser is exposed after WorkMate retreats. This
            # standalone smoke may run while another topmost desktop app exists,
            # so reassert the disposable target immediately before each SendInput.
            ensure_target_surface()
            scroll_target(capture_area, mode, wheel_notches)  # type: ignore[arg-type]

        engine = SmartScrollCaptureEngine(capture_callback, smoke_scroll)
        result = engine.run(region, options, output_dir)

        pages = sorted(output_dir.glob("page_*.png"))
        if result.reason != "end_detected":
            raise AssertionError(f"Expected end_detected, got {result.reason}: {result.error}")
        if len(pages) < 3:
            raise AssertionError(f"Expected at least 3 real screenshots, got {len(pages)}")
        sizes: list[tuple[int, int]] = []
        for path in pages:
            with Image.open(path) as page:
                sizes.append(page.size)
        if any(width != region.width for width, _height in sizes):
            raise AssertionError(f"Screenshot widths differ: {sizes}")
        if any(height > round(region.height * 1.08) for _width, height in sizes):
            raise AssertionError(f"A paged image is unexpectedly tall: {sizes}")
        manifest = json.loads((output_dir / "capture_info.json").read_text(encoding="utf-8"))
        if manifest["保守估计次数"] != 0:
            raise AssertionError(f"Desktop smoke unexpectedly used alignment fallback: {manifest}")
        alignment_records = manifest["对齐记录"]
        if any(
            item["actual_advance_ratio"] > 0.88 and not item["reached_end_after_step"]
            for item in alignment_records
        ):
            raise AssertionError(f"Smart mode overshot its safe overlap range: {alignment_records}")
        streamed_height = sum(height for _width, height in sizes)
        recorded_height = region.height + sum(item["shift_px"] for item in alignment_records)
        if streamed_height != recorded_height:
            raise AssertionError(
                f"Paged stream height {streamed_height} differs from aligned content "
                f"height {recorded_height}"
            )

        evidence = {
            "result": result.reason,
            "pages_saved": result.pages_saved,
            "capture_steps": result.capture_steps,
            "image_sizes": [list(size) for size in sizes],
            "alignment_fallbacks": result.alignment_fallbacks,
            "streamed_height": streamed_height,
            "output_dir": str(output_dir),
            "files": [path.name for path in pages],
        }
        (output_dir / "desktop_smoke_result.json").write_text(
            json.dumps(evidence, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )
        # Keep CI/redirected consoles safe even when their encoding is CP1252.
        print(json.dumps(evidence, ensure_ascii=True))
        return 0
    finally:
        set_cursor_position(original_cursor)
        if original_foreground:
            user32.SetForegroundWindow(original_foreground)
        if hwnd:
            user32.PostMessageW(hwnd, 0x0010, 0, 0)  # WM_CLOSE
        try:
            process.wait(timeout=3)
        except subprocess.TimeoutExpired:
            process.terminate()
            process.wait(timeout=3)


if __name__ == "__main__":
    if "--target" in sys.argv:
        run_target()
    else:
        raise SystemExit(run_smoke())
