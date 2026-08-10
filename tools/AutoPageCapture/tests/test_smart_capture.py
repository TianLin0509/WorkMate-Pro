from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image, ImageChops


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "src"))

from autopagecapture.core import Region  # noqa: E402
from autopagecapture.smart_capture import (  # noqa: E402
    SmartCaptureOptions,
    SmartScrollCaptureEngine,
    parse_advance_ratio,
)
from test_alignment import make_document, make_periodic_document, stack_pages  # noqa: E402


class FakeScrollableDocument:
    def __init__(self, document: Image.Image, viewport_height: int, pixels_per_notch: int) -> None:
        self.document = document
        self.viewport_height = viewport_height
        self.pixels_per_notch = pixels_per_notch
        self.position = 0
        self.scroll_calls: list[int] = []

    def capture(self, region: Region) -> Image.Image:
        return self.document.crop(
            (
                0,
                self.position,
                region.width,
                self.position + self.viewport_height,
            )
        )

    def scroll(self, _region: Region, mode: str, notches: int) -> None:
        self.assert_wheel(mode)
        self.scroll_calls.append(notches)
        maximum = self.document.height - self.viewport_height
        self.position = min(maximum, self.position + notches * self.pixels_per_notch)

    @staticmethod
    def assert_wheel(mode: str) -> None:
        if mode != "wheel":
            raise AssertionError(f"unexpected mode {mode}")


class DroppedWheelDocument(FakeScrollableDocument):
    """Simulate Windows/browser occasionally consuming one notch from a batch."""

    def __init__(self, document: Image.Image, viewport_height: int, pixels_per_notch: int) -> None:
        super().__init__(document, viewport_height, pixels_per_notch)
        self.dropped_once = False

    def scroll(self, _region: Region, mode: str, notches: int) -> None:
        self.assert_wheel(mode)
        self.scroll_calls.append(notches)
        effective_notches = notches
        if not self.dropped_once and len(self.scroll_calls) > 1 and notches > 1:
            effective_notches -= 1
            self.dropped_once = True
        maximum = self.document.height - self.viewport_height
        self.position = min(maximum, self.position + effective_notches * self.pixels_per_notch)


class SmartCaptureTests(unittest.TestCase):
    def test_advance_ratio_accepts_copied_at_value(self) -> None:
        self.assertEqual(parse_advance_ratio("@0.75"), 0.75)
        self.assertEqual(parse_advance_ratio(" ＠0,62 "), 0.62)

    def test_smart_engine_calibrates_aligns_and_stops_at_end(self) -> None:
        document = make_document(width=320, height=1800)
        viewport_height = 360
        target = FakeScrollableDocument(document, viewport_height, pixels_per_notch=92)
        options = SmartCaptureOptions(
            max_pages=20,
            scroll_delay_seconds=0.1,
            capture_mode="smart",
            alignment_safety_px=2,
        )

        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary)
            engine = SmartScrollCaptureEngine(target.capture, target.scroll)
            result = engine.run(Region(0, 0, document.width, viewport_height), options, output)

            pages = sorted(output.glob("page_*.png"))
            manifest = json.loads((output / "capture_info.json").read_text(encoding="utf-8"))
            total_height = 0
            for path in pages:
                with Image.open(path) as page:
                    total_height += page.height

            self.assertEqual(result.reason, "end_detected")
            self.assertGreaterEqual(len(pages), 4)
            self.assertGreaterEqual(total_height, document.height)
            self.assertLessEqual(total_height, document.height + result.capture_steps * 12)
            self.assertGreater(result.capture_steps, 1)
            self.assertEqual(manifest["算法"], "智能对齐 + 流式去重分页")
            self.assertTrue(manifest["对齐记录"])
            self.assertEqual(manifest["保守估计次数"], 0)

    def test_fixed_ratio_is_recorded_as_target(self) -> None:
        document = make_document(width=280, height=1000)
        viewport_height = 300
        target = FakeScrollableDocument(document, viewport_height, pixels_per_notch=70)
        options = SmartCaptureOptions(
            max_pages=10,
            scroll_delay_seconds=0.1,
            capture_mode="fixed_ratio",
            fixed_advance_ratio=0.62,
        )

        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary)
            result = SmartScrollCaptureEngine(target.capture, target.scroll).run(
                Region(0, 0, document.width, viewport_height),
                options,
                output,
            )
            manifest = json.loads((output / "capture_info.json").read_text(encoding="utf-8"))

            self.assertEqual(result.reason, "end_detected")
            self.assertTrue(manifest["对齐记录"])
            for step in manifest["对齐记录"]:
                self.assertAlmostEqual(step["target_advance_ratio"], 0.62)

    def test_incremental_alignment_survives_dropped_wheel_notches_on_periodic_rows(self) -> None:
        document = make_periodic_document(height=3500)
        viewport_height = 1088
        target = DroppedWheelDocument(document, viewport_height, pixels_per_notch=120)
        options = SmartCaptureOptions(
            max_pages=20,
            scroll_delay_seconds=0.1,
            capture_mode="smart",
            alignment_safety_px=0,
        )

        with tempfile.TemporaryDirectory() as temporary:
            output = Path(temporary)
            result = SmartScrollCaptureEngine(target.capture, target.scroll).run(
                Region(0, 0, document.width, viewport_height),
                options,
                output,
            )
            pages: list[Image.Image] = []
            for path in sorted(output.glob("page_*.png")):
                with Image.open(path) as page:
                    pages.append(page.convert("RGB").copy())
            reconstructed = stack_pages(pages)
            manifest = json.loads((output / "capture_info.json").read_text(encoding="utf-8"))

            self.assertEqual(result.reason, "end_detected")
            self.assertEqual(
                reconstructed.size,
                document.size,
                f"result={result} scroll_calls={target.scroll_calls} position={target.position} "
                f"alignments={manifest['对齐记录']}",
            )
            self.assertIsNone(ImageChops.difference(reconstructed, document).getbbox())
            self.assertEqual(result.alignment_fallbacks, 0)


if __name__ == "__main__":
    unittest.main()
