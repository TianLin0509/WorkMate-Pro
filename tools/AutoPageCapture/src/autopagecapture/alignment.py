from __future__ import annotations

import math
from dataclasses import dataclass

from PIL import Image, ImageChops, ImageFilter, ImageStat


@dataclass(frozen=True)
class AlignmentResult:
    """Estimated vertical movement between two captures of the same viewport."""

    shift_px: int
    overlap_px: int
    score: float
    confidence: float
    reliable: bool
    uncertainty_px: int
    texture: float

    @property
    def advance_ratio(self) -> float:
        total = self.shift_px + self.overlap_px
        return self.shift_px / total if total else 0.0

    @property
    def overlap_ratio(self) -> float:
        total = self.shift_px + self.overlap_px
        return self.overlap_px / total if total else 1.0


class VerticalFrameAligner:
    """Match the lower part of one frame with the upper part of the next.

    This implementation intentionally uses only Pillow so the standalone EXE
    stays small. It combines grayscale and edge differences, then biases close
    candidates toward the movement predicted by the online wheel calibration.
    """

    def __init__(
        self,
        sample_width: int = 160,
        sample_height: int = 720,
        max_shift_ratio: float = 0.92,
        min_overlap_ratio: float = 0.08,
    ) -> None:
        self.sample_width = sample_width
        self.sample_height = sample_height
        self.max_shift_ratio = max_shift_ratio
        self.min_overlap_ratio = min_overlap_ratio

    def align(
        self,
        previous: Image.Image,
        current: Image.Image,
        expected_shift_px: int | None = None,
    ) -> AlignmentResult:
        if previous.size != current.size:
            return AlignmentResult(0, 0, 1.0, 0.0, False, 0, 0.0)

        original_height = previous.height
        gray_previous, edge_previous = self._prepare(previous)
        gray_current, edge_current = self._prepare(current)
        sample_height = gray_previous.height
        sample_width = gray_previous.width
        minimum_overlap = max(24, round(sample_height * self.min_overlap_ratio))
        maximum_shift = min(
            sample_height - minimum_overlap,
            round(sample_height * self.max_shift_ratio),
        )
        expected_sample = None
        if expected_shift_px is not None:
            expected_sample = expected_shift_px * sample_height / original_height

        candidates: list[tuple[float, float, int, float]] = []
        for shift in range(maximum_shift + 1):
            overlap_height = sample_height - shift
            gray_a = gray_previous.crop((0, shift, sample_width, sample_height))
            gray_b = gray_current.crop((0, 0, sample_width, overlap_height))
            edge_a = edge_previous.crop((0, shift, sample_width, sample_height))
            edge_b = edge_current.crop((0, 0, sample_width, overlap_height))

            gray_difference = ImageStat.Stat(ImageChops.difference(gray_a, gray_b)).mean[0] / 255.0
            edge_difference = ImageStat.Stat(ImageChops.difference(edge_a, edge_b)).mean[0] / 255.0
            raw_score = gray_difference * 0.65 + edge_difference * 0.35
            texture = (
                ImageStat.Stat(edge_a).mean[0] + ImageStat.Stat(edge_b).mean[0]
            ) / 510.0

            expected_penalty = 0.0
            if expected_sample is not None:
                # Repeated cards/table rows often produce several visually
                # convincing peaks one row-height apart. Once the first wheel
                # quantum is known, motion consistency is the best tie-breaker.
                expected_penalty = 0.060 * abs(shift - expected_sample) / sample_height
            candidates.append((raw_score + expected_penalty, raw_score, shift, texture))

        candidates_by_shift = list(candidates)
        candidates.sort(key=lambda item: item[0])
        raw_best = min(item[1] for item in candidates)
        if expected_sample is None:
            near_best_tolerance = max(0.006, min(0.014, raw_best * 0.36))
            near_best = [
                item for item in candidates if item[1] <= raw_best + near_best_tolerance
            ]
            # The calibration capture is deliberately only one wheel notch.
            # On periodic pages a tiny overlap can score slightly better than
            # the true, much smaller movement. Prefer the smallest shift among
            # candidates that are essentially tied with the raw optimum.
            selected = min(near_best, key=lambda item: (item[2], item[1]))
        else:
            # At reduced resolution, periodic rows can produce a false global
            # minimum both before and after the expected motion. Keep several
            # independent peaks and let exact-height evidence decide below.
            selected = min(
                candidates,
                key=lambda item: (abs(item[2] - expected_sample), item[1]),
            )

        local_minima = [
            item
            for index, item in enumerate(candidates_by_shift)
            if (index == 0 or item[1] <= candidates_by_shift[index - 1][1])
            and (
                index == len(candidates_by_shift) - 1
                or item[1] <= candidates_by_shift[index + 1][1]
            )
        ]
        shortlist_by_shift: dict[int, tuple[float, float, int, float]] = {
            item[2]: item for item in sorted(local_minima, key=lambda item: item[1])[:10]
        }
        shortlist_by_shift[selected[2]] = selected
        if expected_sample is not None:
            for item in sorted(
                local_minima,
                key=lambda item: (abs(item[2] - expected_sample), item[1]),
            )[:5]:
                shortlist_by_shift[item[2]] = item

        maximum_shift_px = original_height - round(
            minimum_overlap * original_height / sample_height
        )
        coarse_shift_pxs = [
            round(item[2] * original_height / sample_height)
            for item in shortlist_by_shift.values()
        ]
        refined_scores = self._score_full_height_candidates(
            previous,
            current,
            coarse_shift_pxs,
            maximum_shift_px,
        )
        full_raw_best = min(score for score, _shift in refined_scores)
        # Use the movement prior only when exact-height pixel evidence is
        # effectively tied. The fixed absolute cap keeps a visibly stronger
        # match from being overridden after a dropped wheel notch.
        evidence_tolerance = max(0.00002, min(0.00012, full_raw_best * 0.15))
        evidence_peers = [
            item for item in refined_scores if item[0] <= full_raw_best + evidence_tolerance
        ]
        if expected_shift_px is None:
            best_score, shift_px = min(evidence_peers, key=lambda item: (item[1], item[0]))
        else:
            best_score, shift_px = min(
                evidence_peers,
                key=lambda item: (abs(item[1] - expected_shift_px), item[0]),
            )

        exclusion_px = max(3, round(original_height * 0.01))
        alternatives = [
            score
            for score, shift in refined_scores
            if abs(shift - shift_px) > exclusion_px
        ]
        second_score = min(alternatives) if alternatives else 1.0
        margin = max(0.0, second_score - best_score)
        separated = [
            score
            for score, shift in refined_scores
            if abs(shift - shift_px) > 1
        ]
        fine_second_score = min(separated) if separated else 1.0
        uncertainty_px = 1 if fine_second_score - best_score >= 0.003 else 2
        selected_coarse = min(
            shortlist_by_shift.values(),
            key=lambda item: abs(
                round(item[2] * original_height / sample_height) - shift_px
            ),
        )
        texture = selected_coarse[3]

        expected_close = False
        if expected_shift_px is not None:
            expected_close = abs(shift_px - expected_shift_px) <= max(5, original_height * 0.10)

        quality = max(0.0, min(1.0, 1.0 - best_score / 0.12))
        separation = max(0.0, min(1.0, margin / 0.03))
        texture_factor = max(0.0, min(1.0, texture / 0.035))
        confidence = quality * (0.45 + 0.55 * separation) * texture_factor
        reliable = (
            texture >= 0.0025
            and best_score <= 0.075
            and (margin >= 0.0025 or best_score <= 0.035 or expected_close)
        )

        return AlignmentResult(
            shift_px=shift_px,
            overlap_px=max(0, original_height - shift_px),
            score=best_score,
            confidence=max(0.0, min(1.0, confidence)),
            reliable=reliable,
            uncertainty_px=uncertainty_px,
            texture=texture,
        )

    def _refine_at_full_height(
        self,
        previous: Image.Image,
        current: Image.Image,
        coarse_shift_px: int,
        maximum_shift_px: int,
    ) -> tuple[int, float, int]:
        """Resolve resize rounding to an exact screen-pixel displacement."""

        width = min(self.sample_width, previous.width)
        height = previous.height
        gray_previous = previous.convert("L").resize(
            (width, height),
            Image.Resampling.BILINEAR,
        )
        gray_current = current.convert("L").resize(
            (width, height),
            Image.Resampling.BILINEAR,
        )
        edge_previous = gray_previous.filter(ImageFilter.FIND_EDGES)
        edge_current = gray_current.filter(ImageFilter.FIND_EDGES)
        for edges in (edge_previous, edge_current):
            edges.paste(0, (0, 0, width, 2))
            edges.paste(0, (0, height - 2, width, height))
            edges.paste(0, (0, 0, 2, height))
            edges.paste(0, (width - 2, 0, width, height))

        radius = max(4, math.ceil(height / max(1, self.sample_height) * 4))
        low = max(0, coarse_shift_px - radius)
        high = min(maximum_shift_px, coarse_shift_px + radius)
        scores: list[tuple[float, int]] = []
        for shift in range(low, high + 1):
            overlap_height = height - shift
            if overlap_height <= 0:
                continue
            gray_a = gray_previous.crop((0, shift, width, height))
            gray_b = gray_current.crop((0, 0, width, overlap_height))
            edge_a = edge_previous.crop((0, shift, width, height))
            edge_b = edge_current.crop((0, 0, width, overlap_height))
            gray_difference = (
                ImageStat.Stat(ImageChops.difference(gray_a, gray_b)).mean[0] / 255.0
            )
            edge_difference = (
                ImageStat.Stat(ImageChops.difference(edge_a, edge_b)).mean[0] / 255.0
            )
            scores.append((gray_difference * 0.65 + edge_difference * 0.35, shift))

        if not scores:
            return coarse_shift_px, 1.0, max(2, radius)
        scores.sort()
        best_score, best_shift = scores[0]
        separated = [score for score, shift in scores[1:] if abs(shift - best_shift) > 1]
        second_score = min(separated) if separated else 1.0
        uncertainty_px = 1 if second_score - best_score >= 0.003 else 2
        return best_shift, best_score, uncertainty_px

    def _score_full_height_candidates(
        self,
        previous: Image.Image,
        current: Image.Image,
        coarse_shift_pxs: list[int],
        maximum_shift_px: int,
    ) -> list[tuple[float, int]]:
        """Score several coarse peaks without recreating full-height samples."""

        width = min(self.sample_width, previous.width)
        height = previous.height
        gray_previous = previous.convert("L").resize(
            (width, height),
            Image.Resampling.BILINEAR,
        )
        gray_current = current.convert("L").resize(
            (width, height),
            Image.Resampling.BILINEAR,
        )
        edge_previous = gray_previous.filter(ImageFilter.FIND_EDGES)
        edge_current = gray_current.filter(ImageFilter.FIND_EDGES)
        for edges in (edge_previous, edge_current):
            edges.paste(0, (0, 0, width, 2))
            edges.paste(0, (0, height - 2, width, height))
            edges.paste(0, (0, 0, 2, height))
            edges.paste(0, (width - 2, 0, width, height))

        radius = max(4, math.ceil(height / max(1, self.sample_height) * 4))
        shifts: set[int] = set()
        for center in coarse_shift_pxs:
            low = max(0, center - radius)
            high = min(maximum_shift_px, center + radius)
            shifts.update(range(low, high + 1))

        scores: list[tuple[float, int]] = []
        for shift in sorted(shifts):
            overlap_height = height - shift
            if overlap_height <= 0:
                continue
            gray_a = gray_previous.crop((0, shift, width, height))
            gray_b = gray_current.crop((0, 0, width, overlap_height))
            edge_a = edge_previous.crop((0, shift, width, height))
            edge_b = edge_current.crop((0, 0, width, overlap_height))
            gray_difference = (
                ImageStat.Stat(ImageChops.difference(gray_a, gray_b)).mean[0] / 255.0
            )
            edge_difference = (
                ImageStat.Stat(ImageChops.difference(edge_a, edge_b)).mean[0] / 255.0
            )
            scores.append((gray_difference * 0.65 + edge_difference * 0.35, shift))

        if not scores:
            return [(1.0, max(0, min(maximum_shift_px, coarse_shift_pxs[0])))]
        return scores

    def _prepare(self, image: Image.Image) -> tuple[Image.Image, Image.Image]:
        target_width = min(self.sample_width, image.width)
        # Preserve the vertical pixel grid whenever practical. Resizing every
        # viewport to an arbitrary fixed height can turn repeated text rows into
        # periodic false matches. Tall 4K selections are capped for speed.
        target_height = min(self.sample_height, image.height)
        gray = image.convert("L").resize(
            (target_width, target_height),
            Image.Resampling.BILINEAR,
        )
        edges = gray.filter(ImageFilter.FIND_EDGES)
        # FIND_EDGES creates an artificial frame border. Remove it so it cannot
        # win the alignment search when overlap is small.
        edges.paste(0, (0, 0, target_width, 2))
        edges.paste(0, (0, target_height - 2, target_width, target_height))
        edges.paste(0, (0, 0, 2, target_height))
        edges.paste(0, (target_width - 2, 0, target_width, target_height))
        return gray, edges


class RollingPageAssembler:
    """Build a de-duplicated content stream and emit screen-sized pages.

    The buffer is kept around one to two viewport heights; a giant stitched
    image is never created. Page boundaries are nudged toward visually quiet
    horizontal rows to reduce the chance of cutting through a text line.
    """

    def __init__(
        self,
        viewport_size: tuple[int, int],
        page_height: int | None = None,
        cut_search_ratio: float = 0.06,
        safety_overlap_px: int = 0,
    ) -> None:
        self.width, self.viewport_height = viewport_size
        self.page_height = page_height or self.viewport_height
        self.cut_search_ratio = cut_search_ratio
        self.safety_overlap_px = max(0, safety_overlap_px)
        self._buffer: Image.Image | None = None

    @property
    def buffered_height(self) -> int:
        return self._buffer.height if self._buffer is not None else 0

    def append_initial(self, frame: Image.Image) -> list[Image.Image]:
        self._validate_frame(frame)
        self._append(frame.copy())
        return []

    def append_aligned(self, frame: Image.Image, shift_px: int, uncertainty_px: int = 0) -> list[Image.Image]:
        self._validate_frame(frame)
        # Reliable shifts are refined on the original vertical pixel grid, so
        # uncertainty is diagnostic rather than extra repeated content. An
        # explicit safety overlap remains available for conservative callers.
        safe_shift = min(
            self.viewport_height,
            max(0, shift_px) + self.safety_overlap_px,
        )
        if safe_shift <= 0:
            return []
        novel_start = self.viewport_height - safe_shift
        self._append(frame.crop((0, novel_start, self.width, self.viewport_height)))
        return self.pop_ready()

    def pop_ready(self) -> list[Image.Image]:
        pages: list[Image.Image] = []
        search_radius = max(8, round(self.page_height * self.cut_search_ratio))
        while self._buffer is not None and self._buffer.height >= self.page_height + search_radius:
            cut = self._find_quiet_cut(self._buffer, self.page_height, search_radius)
            pages.append(self._buffer.crop((0, 0, self.width, cut)))
            remainder = self._buffer.crop((0, cut, self.width, self._buffer.height))
            self._buffer = remainder if remainder.height else None
        return pages

    def finish(self) -> list[Image.Image]:
        pages = self.pop_ready()
        if self._buffer is not None and self._buffer.height > 0:
            pages.append(self._buffer)
            self._buffer = None
        return pages

    def _append(self, segment: Image.Image) -> None:
        if segment.height <= 0:
            return
        if self._buffer is None:
            self._buffer = segment
            return
        combined = Image.new("RGB", (self.width, self._buffer.height + segment.height), "white")
        combined.paste(self._buffer.convert("RGB"), (0, 0))
        combined.paste(segment.convert("RGB"), (0, self._buffer.height))
        self._buffer = combined

    def _find_quiet_cut(self, image: Image.Image, target: int, radius: int) -> int:
        low = max(32, target - radius)
        high = min(image.height - 1, target + radius)
        if high <= low:
            return min(target, image.height)

        band = image.crop((0, low, image.width, high + 1)).convert("L")
        band = band.resize((128, band.height), Image.Resampling.BILINEAR)
        edges = band.filter(ImageFilter.FIND_EDGES)
        scores: list[tuple[float, int]] = []
        for row in range(2, edges.height - 2):
            activity = ImageStat.Stat(edges.crop((2, row, edges.width - 2, row + 1))).mean[0]
            distance_penalty = 2.0 * abs((low + row) - target) / max(1, radius)
            scores.append((activity + distance_penalty, low + row))
        return min(scores)[1] if scores else min(target, image.height)

    def _validate_frame(self, frame: Image.Image) -> None:
        if frame.size != (self.width, self.viewport_height):
            raise ValueError(
                f"截图尺寸发生变化：期望 {self.width} × {self.viewport_height}，"
                f"实际 {frame.width} × {frame.height}。"
            )
