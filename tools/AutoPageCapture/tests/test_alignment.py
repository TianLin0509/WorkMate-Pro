from __future__ import annotations

import sys
import unittest
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "src"))

from autopagecapture.alignment import RollingPageAssembler, VerticalFrameAligner  # noqa: E402


def make_document(width: int = 320, height: int = 1200) -> Image.Image:
    image = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(image)
    colors = ("#183b56", "#2a9d8f", "#e76f51", "#6a4c93")
    for index, y in enumerate(range(12, height, 34)):
        color = colors[index % len(colors)]
        draw.rectangle((12, y, 42 + (index % 7) * 9, y + 8), fill=color)
        draw.line((55, y + 4, width - 14 - (index % 5) * 11, y + 4), fill="#172033", width=3)
        draw.text((14, y + 11), f"row {index:03d}", fill="#34495e")
    return image


def make_periodic_document(width: int = 868, height: int = 3500) -> Image.Image:
    """List-like fixture whose repeated rows used to cause a false far match."""

    image = Image.new("RGB", (width, height), "white")
    draw = ImageDraw.Draw(image)
    y = 20
    for index in range(55):
        if index % 2 == 0:
            draw.rectangle((0, y - 5, width, y + 49), fill="#f7f9fc")
        draw.text(
            (20, y + 7),
            f"row {index:02d} repeated alignment sample",
            fill="#172033",
        )
        y += 62
    return image


def stack_pages(pages: list[Image.Image]) -> Image.Image:
    width = pages[0].width
    combined = Image.new("RGB", (width, sum(page.height for page in pages)), "white")
    y = 0
    for page in pages:
        combined.paste(page.convert("RGB"), (0, y))
        y += page.height
    return combined


class AlignmentTests(unittest.TestCase):
    def test_vertical_aligner_recovers_known_shift(self) -> None:
        document = make_document(height=1400)
        viewport_height = 420
        expected_shift = 287
        first = document.crop((0, 100, document.width, 100 + viewport_height))
        second = document.crop(
            (0, 100 + expected_shift, document.width, 100 + expected_shift + viewport_height)
        )

        result = VerticalFrameAligner().align(first, second)

        self.assertTrue(result.reliable)
        self.assertLessEqual(abs(result.shift_px - expected_shift), result.uncertainty_px + 2)
        self.assertAlmostEqual(result.advance_ratio, expected_shift / viewport_height, delta=0.02)

    def test_vertical_aligner_reports_zero_for_identical_frame(self) -> None:
        frame = make_document(height=420)
        result = VerticalFrameAligner().align(frame, frame.copy())
        self.assertTrue(result.reliable)
        self.assertEqual(result.shift_px, 0)

    def test_periodic_rows_use_small_calibration_and_motion_prior(self) -> None:
        document = make_periodic_document()
        viewport_height = 1088
        first = document.crop((0, 0, document.width, viewport_height))

        calibration_shift = 120
        calibration = document.crop(
            (0, calibration_shift, document.width, calibration_shift + viewport_height)
        )
        calibration_result = VerticalFrameAligner().align(first, calibration)
        self.assertTrue(calibration_result.reliable)
        self.assertLessEqual(abs(calibration_result.shift_px - calibration_shift), 4)

        cumulative_shift = 840
        cumulative = document.crop(
            (0, cumulative_shift, document.width, cumulative_shift + viewport_height)
        )
        cumulative_result = VerticalFrameAligner().align(
            first,
            cumulative,
            expected_shift_px=cumulative_shift,
        )
        self.assertTrue(cumulative_result.reliable)
        self.assertLessEqual(abs(cumulative_result.shift_px - cumulative_shift), 4)

    def test_stronger_pixel_evidence_beats_stale_motion_prior(self) -> None:
        document = make_periodic_document()
        viewport_height = 1088
        previous = document.crop((0, 120, document.width, 120 + viewport_height))
        actual_shift = 360
        current = document.crop(
            (0, 120 + actual_shift, document.width, 120 + actual_shift + viewport_height)
        )

        # The caller requested four notches, but the target consumed only
        # three. Repeated rows also produce a plausible false match near 480.
        result = VerticalFrameAligner().align(
            previous,
            current,
            expected_shift_px=480,
        )

        self.assertTrue(result.reliable)
        self.assertLessEqual(abs(result.shift_px - actual_shift), 4)

    def test_streaming_assembler_reconstructs_content_without_gaps(self) -> None:
        document = make_document(height=900)
        viewport_height = 300
        shift = 200
        assembler = RollingPageAssembler(
            (document.width, viewport_height),
            page_height=viewport_height,
            safety_overlap_px=0,
        )
        pages: list[Image.Image] = []
        positions = (0, 200, 400, 600)
        first = document.crop((0, 0, document.width, viewport_height))
        assembler.append_initial(first)
        for position in positions[1:]:
            frame = document.crop((0, position, document.width, position + viewport_height))
            pages.extend(assembler.append_aligned(frame, shift, uncertainty_px=0))
        pages.extend(assembler.finish())

        reconstructed = stack_pages(pages)
        self.assertEqual(reconstructed.size, document.size)
        self.assertIsNone(ImageChops.difference(reconstructed, document).getbbox())
        self.assertGreaterEqual(len(pages), 2)


if __name__ == "__main__":
    unittest.main()
