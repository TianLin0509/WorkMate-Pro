from __future__ import annotations

import json
import sys
import tempfile
import threading
import unittest
from datetime import datetime
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "src"))

from autopagecapture.core import (  # noqa: E402
    CaptureOptions,
    Region,
    ScrollCaptureEngine,
    create_run_directory,
    image_difference_ratio,
)


class CoreTests(unittest.TestCase):
    def test_region_validation_and_dimensions(self) -> None:
        region = Region(10, 20, 210, 320)
        region.validate()
        self.assertEqual(region.width, 200)
        self.assertEqual(region.height, 300)
        self.assertEqual(region.center, (110, 170))
        with self.assertRaises(ValueError):
            Region(0, 0, 20, 20).validate()

    def test_image_difference_ratio(self) -> None:
        first = Image.new("RGB", (400, 300), "white")
        same = first.copy()
        changed = first.copy()
        draw = ImageDraw.Draw(changed)
        draw.rectangle((0, 0, 200, 300), fill="black")
        self.assertEqual(image_difference_ratio(first, same), 0.0)
        self.assertGreater(image_difference_ratio(first, changed), 0.2)

    def test_create_run_directory_is_collision_safe(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            fixed = datetime(2026, 8, 9, 12, 30, 45)
            first = create_run_directory(root, fixed)
            second = create_run_directory(root, fixed)
            self.assertEqual(first.name, "网页截图_20260809_123045")
            self.assertEqual(second.name, "网页截图_20260809_123045_02")

    def test_engine_saves_each_changed_page_and_stops_on_unchanged(self) -> None:
        frames = [
            Image.new("RGB", (160, 120), color)
            for color in ("#ffffff", "#bbbbbb", "#777777", "#777777", "#777777")
        ]
        capture_index = 0
        scroll_count = 0

        def capture(_region: Region) -> Image.Image:
            nonlocal capture_index
            frame = frames[min(capture_index, len(frames) - 1)]
            capture_index += 1
            return frame.copy()

        def scroll(_region: Region, _mode: str, _notches: int) -> None:
            nonlocal scroll_count
            scroll_count += 1

        options = CaptureOptions(
            max_pages=20,
            scroll_delay_seconds=0.1,
            unchanged_threshold=0.001,
            unchanged_limit=2,
        )
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary)
            engine = ScrollCaptureEngine(capture, scroll)
            result = engine.run(Region(0, 0, 160, 120), options, output)
            pages = sorted(output.glob("page_*.png"))
            manifest = json.loads((output / "capture_info.json").read_text(encoding="utf-8"))

            self.assertEqual(result.reason, "end_detected")
            self.assertEqual(result.pages_saved, 3)
            self.assertEqual(len(pages), 3)
            self.assertEqual(scroll_count, 4)
            self.assertEqual(manifest["状态"], "end_detected")
            self.assertEqual(manifest["已保存页数"], 3)

    def test_engine_honors_cancellation_before_first_capture(self) -> None:
        stop = threading.Event()
        stop.set()

        def fail_capture(_region: Region) -> Image.Image:
            raise AssertionError("capture should not run")

        with tempfile.TemporaryDirectory() as temporary:
            engine = ScrollCaptureEngine(fail_capture, lambda *_args: None, stop_event=stop)
            result = engine.run(
                Region(0, 0, 100, 100),
                CaptureOptions(scroll_delay_seconds=0.1),
                Path(temporary),
            )
            self.assertEqual(result.reason, "cancelled")
            self.assertEqual(result.pages_saved, 0)

    def test_engine_jpg_output(self) -> None:
        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary)
            engine = ScrollCaptureEngine(
                lambda _region: Image.new("RGBA", (100, 100), (20, 30, 40, 128)),
                lambda *_args: None,
            )
            result = engine.run(
                Region(0, 0, 100, 100),
                CaptureOptions(max_pages=1, scroll_delay_seconds=0.1, image_format="jpg"),
                output,
            )
            self.assertEqual(result.reason, "max_pages")
            self.assertTrue((output / "page_001.jpg").is_file())


if __name__ == "__main__":
    unittest.main()
