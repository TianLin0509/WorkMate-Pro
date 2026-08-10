from __future__ import annotations

import json
import math
import os
import threading
from dataclasses import asdict, dataclass
from datetime import datetime
from pathlib import Path
from typing import Callable, Literal

from PIL import Image

from .alignment import AlignmentResult, RollingPageAssembler, VerticalFrameAligner
from .core import Region, image_difference_ratio


CaptureMode = Literal["smart", "fixed_ratio"]
ImageFormat = Literal["png", "jpg"]
SmartReason = Literal[
    "end_detected",
    "max_pages",
    "cancelled",
    "alignment_failed",
    "error",
]


@dataclass(frozen=True)
class SmartCaptureOptions:
    max_pages: int = 100
    scroll_delay_seconds: float = 0.8
    capture_mode: CaptureMode = "smart"
    fixed_advance_ratio: float = 0.80
    smart_initial_advance_ratio: float = 0.78
    smart_min_advance_ratio: float = 0.58
    smart_max_advance_ratio: float = 0.86
    max_scroll_adjustments: int = 5
    unchanged_threshold: float = 0.006
    unchanged_limit: int = 2
    image_format: ImageFormat = "png"
    page_height_ratio: float = 1.0
    alignment_safety_px: int = 0

    def validate(self) -> None:
        if not 1 <= self.max_pages <= 9999:
            raise ValueError("页数上限必须在 1 到 9999 之间。")
        if not 0.1 <= self.scroll_delay_seconds <= 30:
            raise ValueError("滚动后等待时间必须在 0.1 到 30 秒之间。")
        if self.capture_mode not in ("smart", "fixed_ratio"):
            raise ValueError("不支持的截图模式。")
        if not 0.30 <= self.fixed_advance_ratio <= 0.90:
            raise ValueError("固定推进比例必须在 @0.30 到 @0.90 之间。")
        if not 0.30 <= self.smart_min_advance_ratio < self.smart_max_advance_ratio <= 0.92:
            raise ValueError("智能推进比例范围无效。")
        if not self.smart_min_advance_ratio <= self.smart_initial_advance_ratio <= self.smart_max_advance_ratio:
            raise ValueError("智能初始推进比例必须位于智能范围内。")
        if not 1 <= self.max_scroll_adjustments <= 12:
            raise ValueError("单页滚动校正次数必须在 1 到 12 之间。")
        if not 0 <= self.unchanged_threshold <= 1:
            raise ValueError("无变化阈值必须在 0 到 1 之间。")
        if not 1 <= self.unchanged_limit <= 10:
            raise ValueError("无变化确认次数必须在 1 到 10 之间。")
        if self.image_format not in ("png", "jpg"):
            raise ValueError("图片格式只能是 PNG 或 JPG。")
        if not 0.50 <= self.page_height_ratio <= 2.0:
            raise ValueError("输出分页高度比例必须在 0.50 到 2.0 之间。")
        if not 0 <= self.alignment_safety_px <= 32:
            raise ValueError("安全重叠像素必须在 0 到 32 之间。")


@dataclass(frozen=True)
class SmartProgressEvent:
    kind: Literal["calibrating", "scrolling", "aligned", "page_saved", "warning"]
    pages_saved: int
    max_pages: int
    capture_step: int
    advance_ratio: float | None = None
    overlap_ratio: float | None = None
    confidence: float | None = None
    message: str = ""


@dataclass(frozen=True)
class AlignmentStep:
    step: int
    target_advance_ratio: float
    actual_advance_ratio: float
    overlap_ratio: float
    shift_px: int
    wheel_notches: int
    score: float
    confidence: float
    reliable: bool
    inferred: bool
    reached_end_after_step: bool


@dataclass(frozen=True)
class SmartCaptureResult:
    output_dir: Path
    pages_saved: int
    capture_steps: int
    reason: SmartReason
    started_at: str
    ended_at: str
    alignment_fallbacks: int = 0
    error: str | None = None


@dataclass(frozen=True)
class _StepOutcome:
    kind: Literal["accepted", "end", "failed", "cancelled"]
    frame: Image.Image | None
    alignment: AlignmentResult | None
    total_notches: int
    pixels_per_notch: float | None
    inferred: bool = False
    reached_end_after_step: bool = False
    error: str | None = None


CaptureFunction = Callable[[Region], Image.Image]
ScrollFunction = Callable[[Region, str, int], None]
ProgressFunction = Callable[[SmartProgressEvent], None]


def parse_advance_ratio(value: str) -> float:
    """Accept the UI's numeric form as well as copied @0.75 / full-width ＠0.75."""

    normalized = (value or "").strip().replace(",", ".")
    if normalized.startswith(("@", "＠")):
        normalized = normalized[1:].strip()
    return float(normalized)


class SmartScrollCaptureEngine:
    """Visual closed-loop scrolling with streaming de-duplication and paging."""

    def __init__(
        self,
        capture: CaptureFunction,
        scroll: ScrollFunction,
        stop_event: threading.Event | None = None,
        progress: ProgressFunction | None = None,
        aligner: VerticalFrameAligner | None = None,
    ) -> None:
        self._capture = capture
        self._scroll = scroll
        self._stop_event = stop_event or threading.Event()
        self._progress = progress
        self._aligner = aligner or VerticalFrameAligner()

    def run(self, region: Region, options: SmartCaptureOptions, output_dir: Path) -> SmartCaptureResult:
        region.validate()
        options.validate()
        output_dir = Path(output_dir).resolve()
        output_dir.mkdir(parents=True, exist_ok=True)

        started_at = datetime.now().astimezone().isoformat(timespec="seconds")
        pages_saved = 0
        capture_steps = 0
        fallback_count = 0
        alignment_log: list[AlignmentStep] = []
        reason: SmartReason = "error"
        error_message: str | None = None
        page_digits = max(3, len(str(options.max_pages)))
        pixels_per_notch: float | None = None
        target_ratio = (
            options.fixed_advance_ratio
            if options.capture_mode == "fixed_ratio"
            else options.smart_initial_advance_ratio
        )

        self._write_manifest(
            output_dir,
            region,
            options,
            started_at,
            None,
            pages_saved,
            capture_steps,
            "running",
            alignment_log,
            fallback_count,
            None,
        )

        try:
            if self._stop_event.is_set():
                reason = "cancelled"
                assembler = None
            else:
                anchor = self._capture(region).convert("RGB")
                assembler = RollingPageAssembler(
                    anchor.size,
                    page_height=max(1, round(anchor.height * options.page_height_ratio)),
                    safety_overlap_px=options.alignment_safety_px,
                )
                assembler.append_initial(anchor)

                if options.max_pages == 1:
                    pages_saved = self._save_pages(
                        assembler.finish(),
                        output_dir,
                        options.image_format,
                        page_digits,
                        pages_saved,
                        options.max_pages,
                    )
                    reason = "max_pages"
                else:
                    while pages_saved < options.max_pages:
                        if self._stop_event.is_set():
                            reason = "cancelled"
                            break

                        capture_steps += 1
                        outcome = self._capture_next_frame(
                            anchor,
                            region,
                            options,
                            target_ratio,
                            pixels_per_notch,
                            capture_steps,
                            pages_saved,
                        )
                        pixels_per_notch = outcome.pixels_per_notch

                        if outcome.kind == "cancelled":
                            reason = "cancelled"
                            break
                        if outcome.kind == "end":
                            reason = "end_detected"
                            break
                        if outcome.kind == "failed" or outcome.frame is None or outcome.alignment is None:
                            reason = "alignment_failed"
                            error_message = outcome.error or "无法可靠对齐相邻画面。"
                            break

                        alignment = outcome.alignment
                        if outcome.inferred:
                            fallback_count += 1
                        ready_pages = assembler.append_aligned(
                            outcome.frame,
                            alignment.shift_px,
                            alignment.uncertainty_px,
                        )
                        pages_saved = self._save_pages(
                            ready_pages,
                            output_dir,
                            options.image_format,
                            page_digits,
                            pages_saved,
                            options.max_pages,
                        )
                        step_record = AlignmentStep(
                            step=capture_steps,
                            target_advance_ratio=target_ratio,
                            actual_advance_ratio=alignment.advance_ratio,
                            overlap_ratio=alignment.overlap_ratio,
                            shift_px=alignment.shift_px,
                            wheel_notches=outcome.total_notches,
                            score=alignment.score,
                            confidence=alignment.confidence,
                            reliable=alignment.reliable,
                            inferred=outcome.inferred,
                            reached_end_after_step=outcome.reached_end_after_step,
                        )
                        alignment_log.append(step_record)
                        anchor = outcome.frame

                        self._emit(
                            SmartProgressEvent(
                                "aligned",
                                pages_saved,
                                options.max_pages,
                                capture_steps,
                                alignment.advance_ratio,
                                alignment.overlap_ratio,
                                alignment.confidence,
                                "已完成画面对齐",
                            )
                        )
                        if ready_pages:
                            self._emit(
                                SmartProgressEvent(
                                    "page_saved",
                                    pages_saved,
                                    options.max_pages,
                                    capture_steps,
                                    message=f"已保存第 {pages_saved} 页",
                                )
                            )

                        self._write_manifest(
                            output_dir,
                            region,
                            options,
                            started_at,
                            None,
                            pages_saved,
                            capture_steps,
                            "running",
                            alignment_log,
                            fallback_count,
                            None,
                        )

                        if options.capture_mode == "smart":
                            target_ratio = self._next_smart_target(target_ratio, alignment, options)

                        if outcome.reached_end_after_step:
                            reason = "end_detected"
                            break
                    else:
                        reason = "max_pages"

                    if pages_saved < options.max_pages:
                        pages_saved = self._save_pages(
                            assembler.finish(),
                            output_dir,
                            options.image_format,
                            page_digits,
                            pages_saved,
                            options.max_pages,
                        )
                    if pages_saved >= options.max_pages and reason == "error":
                        reason = "max_pages"
        except Exception as exc:
            reason = "error"
            error_message = f"{type(exc).__name__}: {exc}"

        ended_at = datetime.now().astimezone().isoformat(timespec="seconds")
        result = SmartCaptureResult(
            output_dir=output_dir,
            pages_saved=pages_saved,
            capture_steps=capture_steps,
            reason=reason,
            started_at=started_at,
            ended_at=ended_at,
            alignment_fallbacks=fallback_count,
            error=error_message,
        )
        self._write_manifest(
            output_dir,
            region,
            options,
            started_at,
            ended_at,
            pages_saved,
            capture_steps,
            reason,
            alignment_log,
            fallback_count,
            error_message,
        )
        return result

    def _capture_next_frame(
        self,
        anchor: Image.Image,
        region: Region,
        options: SmartCaptureOptions,
        target_ratio: float,
        pixels_per_notch: float | None,
        capture_step: int,
        pages_saved: int,
    ) -> _StepOutcome:
        target_px = max(1, round(anchor.height * target_ratio))
        maximum_safe_px = max(target_px, round(anchor.height * 0.90))
        total_notches = 0
        accumulated_shift = 0
        previous_candidate = anchor
        last_candidate: Image.Image | None = None
        last_alignment: AlignmentResult | None = None
        unchanged_increment_count = 0
        aggregate_score = 0.0
        aggregate_confidence = 1.0
        aggregate_uncertainty = 0
        aggregate_texture = 1.0

        for adjustment in range(options.max_scroll_adjustments):
            if self._stop_event.is_set():
                return _StepOutcome("cancelled", None, None, total_notches, pixels_per_notch)

            current_shift = accumulated_shift
            remaining = max(1, target_px - current_shift)
            if pixels_per_notch is None:
                notches = 1
                kind = "calibrating"
            else:
                predicted = math.floor((remaining / max(1.0, pixels_per_notch)) * 0.78)
                notches = max(1, min(12, predicted))
                safe_remaining = max(1, maximum_safe_px - current_shift)
                safe_notches = max(
                    1,
                    math.floor((safe_remaining / max(1.0, pixels_per_notch)) * 0.82),
                )
                notches = min(notches, safe_notches)
                kind = "scrolling"

            self._emit(
                SmartProgressEvent(
                    kind,
                    pages_saved,
                    options.max_pages,
                    capture_step,
                    message=(
                        "正在试滚并测量实际位移…"
                        if pixels_per_notch is None
                        else f"正在按 @{target_ratio:.2f} 目标推进并校正…"
                    ),
                )
            )
            self._scroll(region, "wheel", notches)
            total_notches += notches
            if self._stop_event.wait(options.scroll_delay_seconds):
                return _StepOutcome("cancelled", None, None, total_notches, pixels_per_notch)

            candidate = self._capture(region).convert("RGB")
            expected_increment = None
            if pixels_per_notch is not None:
                expected_increment = round(notches * pixels_per_notch)
            incremental_difference = image_difference_ratio(previous_candidate, candidate)
            incremental_alignment: AlignmentResult | None = None
            if incremental_difference <= options.unchanged_threshold:
                # Repeated table/list rows can move while changing fewer than the
                # global difference threshold. Ask the aligner before declaring
                # the page unchanged; an exactly identical bottom frame still
                # takes the cheap end-detection path.
                if incremental_difference > 0.000001:
                    probe = self._aligner.align(previous_candidate, candidate, expected_increment)
                    if probe.reliable and probe.shift_px > 1:
                        incremental_alignment = probe
                if incremental_alignment is None:
                    unchanged_increment_count += 1
                    if unchanged_increment_count >= options.unchanged_limit:
                        if last_alignment is None or last_candidate is None:
                            return _StepOutcome("end", None, None, total_notches, pixels_per_notch)
                        return _StepOutcome(
                            "accepted",
                            last_candidate,
                            last_alignment,
                            total_notches,
                            pixels_per_notch,
                            reached_end_after_step=True,
                        )
                    continue

            unchanged_increment_count = 0
            if incremental_alignment is None:
                incremental_alignment = self._aligner.align(
                    previous_candidate,
                    candidate,
                    expected_increment,
                )

            if not incremental_alignment.reliable:
                # Give lazy-loaded or smoothly animated content one extra chance
                # to settle, without applying another scroll that could create a gap.
                if self._stop_event.wait(min(0.5, options.scroll_delay_seconds)):
                    return _StepOutcome("cancelled", None, None, total_notches, pixels_per_notch)
                settled = self._capture(region).convert("RGB")
                retry = self._aligner.align(previous_candidate, settled, expected_increment)
                if retry.reliable:
                    candidate = settled
                    incremental_alignment = retry
                elif pixels_per_notch is not None:
                    # Inference is deliberately biased upward: including a little
                    # old content is safer than dropping unseen rows.
                    inferred_increment = max(1, round(notches * pixels_per_notch * 1.18))
                    inferred_shift = min(
                        round(anchor.height * 0.92),
                        accumulated_shift + inferred_increment,
                    )
                    alignment = AlignmentResult(
                        shift_px=inferred_shift,
                        overlap_px=anchor.height - inferred_shift,
                        score=max(aggregate_score, retry.score),
                        confidence=0.0,
                        reliable=False,
                        uncertainty_px=aggregate_uncertainty + max(8, round(anchor.height * 0.03)),
                        texture=min(aggregate_texture, retry.texture),
                    )
                    self._emit(
                        SmartProgressEvent(
                            "warning",
                            pages_saved,
                            options.max_pages,
                            capture_step,
                            alignment.advance_ratio,
                            alignment.overlap_ratio,
                            0.0,
                            "画面纹理不足，使用保守位移估计；可能保留少量重复内容。",
                        )
                    )
                    return _StepOutcome(
                        "accepted",
                        settled,
                        alignment,
                        total_notches,
                        pixels_per_notch,
                        inferred=True,
                    )
                else:
                    return _StepOutcome(
                        "failed",
                        None,
                        None,
                        total_notches,
                        pixels_per_notch,
                        error=(
                            "第一次试滚后无法识别重叠区域。请缩小固定标题栏/动画区域，"
                            "或改用包含更多正文纹理的框选范围。"
                        ),
                    )

            if incremental_alignment.shift_px <= 1:
                previous_candidate = candidate
                continue

            observed_pixels_per_notch = incremental_alignment.shift_px / max(1, notches)
            if pixels_per_notch is None:
                pixels_per_notch = observed_pixels_per_notch
            else:
                pixels_per_notch = pixels_per_notch * 0.60 + observed_pixels_per_notch * 0.40

            accumulated_shift = min(anchor.height, accumulated_shift + incremental_alignment.shift_px)
            aggregate_score = max(aggregate_score, incremental_alignment.score)
            aggregate_confidence = min(aggregate_confidence, incremental_alignment.confidence)
            aggregate_uncertainty += incremental_alignment.uncertainty_px
            aggregate_texture = min(aggregate_texture, incremental_alignment.texture)
            alignment = AlignmentResult(
                shift_px=accumulated_shift,
                overlap_px=max(0, anchor.height - accumulated_shift),
                score=aggregate_score,
                confidence=aggregate_confidence,
                reliable=True,
                uncertainty_px=aggregate_uncertainty,
                texture=aggregate_texture,
            )
            last_candidate = candidate
            last_alignment = alignment
            if alignment.shift_px >= target_px * 0.90 or alignment.shift_px >= maximum_safe_px:
                return _StepOutcome(
                    "accepted",
                    candidate,
                    alignment,
                    total_notches,
                    pixels_per_notch,
                )

            remaining = target_px - alignment.shift_px
            if remaining <= pixels_per_notch * 0.72:
                # A further whole wheel quantum would likely overshoot and erase
                # the overlap needed for visual proof, so accept the safe undershoot.
                return _StepOutcome(
                    "accepted",
                    candidate,
                    alignment,
                    total_notches,
                    pixels_per_notch,
                )
            previous_candidate = candidate

        if last_candidate is not None and last_alignment is not None:
            return _StepOutcome(
                "accepted",
                last_candidate,
                last_alignment,
                total_notches,
                pixels_per_notch,
            )
        return _StepOutcome(
            "failed",
            None,
            None,
            total_notches,
            pixels_per_notch,
            error="达到单页校正次数上限，但没有取得可验证的新画面。",
        )

    @staticmethod
    def _next_smart_target(
        current: float,
        alignment: AlignmentResult,
        options: SmartCaptureOptions,
    ) -> float:
        if not alignment.reliable or alignment.confidence < 0.30:
            return max(options.smart_min_advance_ratio, current - 0.08)
        if alignment.confidence > 0.72:
            return min(options.smart_max_advance_ratio, current + 0.025)
        return current

    def _emit(self, event: SmartProgressEvent) -> None:
        if self._progress is not None:
            self._progress(event)

    @classmethod
    def _save_pages(
        cls,
        pages: list[Image.Image],
        output_dir: Path,
        image_format: str,
        page_digits: int,
        pages_saved: int,
        max_pages: int,
    ) -> int:
        for page in pages:
            if pages_saved >= max_pages:
                break
            pages_saved += 1
            destination = output_dir / f"page_{pages_saved:0{page_digits}d}.{image_format}"
            cls._save_image(page, destination, image_format)
        return pages_saved

    @staticmethod
    def _save_image(image: Image.Image, destination: Path, extension: str) -> None:
        temporary = destination.with_name(f".{destination.name}.tmp")
        if extension == "jpg":
            image.convert("RGB").save(
                temporary,
                format="JPEG",
                quality=92,
                optimize=True,
                subsampling=0,
            )
        else:
            image.save(temporary, format="PNG", compress_level=6)
        os.replace(temporary, destination)

    @staticmethod
    def _write_manifest(
        output_dir: Path,
        region: Region,
        options: SmartCaptureOptions,
        started_at: str,
        ended_at: str | None,
        pages_saved: int,
        capture_steps: int,
        status: str,
        alignment_log: list[AlignmentStep],
        fallback_count: int,
        error: str | None,
    ) -> None:
        payload = {
            "工具": "Auto Page Capture",
            "算法": "智能对齐 + 流式去重分页",
            "状态": status,
            "已保存页数": pages_saved,
            "对齐步数": capture_steps,
            "保守估计次数": fallback_count,
            "开始时间": started_at,
            "结束时间": ended_at,
            "截图区域": asdict(region),
            "设置": asdict(options),
            "对齐记录": [asdict(item) for item in alignment_log],
            "错误": error,
        }
        destination = output_dir / "capture_info.json"
        temporary = output_dir / ".capture_info.json.tmp"
        temporary.write_text(
            json.dumps(payload, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )
        os.replace(temporary, destination)
