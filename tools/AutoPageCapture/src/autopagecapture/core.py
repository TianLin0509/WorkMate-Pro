from __future__ import annotations

import json
import os
import threading
from dataclasses import asdict, dataclass
from datetime import datetime
from pathlib import Path
from typing import Callable, Literal

from PIL import Image, ImageChops, ImageStat


ScrollMode = Literal["wheel", "pagedown"]
ImageFormat = Literal["png", "jpg"]


@dataclass(frozen=True)
class Region:
    left: int
    top: int
    right: int
    bottom: int

    @property
    def width(self) -> int:
        return self.right - self.left

    @property
    def height(self) -> int:
        return self.bottom - self.top

    @property
    def center(self) -> tuple[int, int]:
        return (self.left + self.width // 2, self.top + self.height // 2)

    def validate(self, minimum_size: int = 80) -> None:
        if self.right <= self.left or self.bottom <= self.top:
            raise ValueError("截图区域坐标无效。")
        if self.width < minimum_size or self.height < minimum_size:
            raise ValueError(f"截图区域至少需要 {minimum_size} × {minimum_size} 像素。")


@dataclass(frozen=True)
class CaptureOptions:
    max_pages: int = 100
    scroll_delay_seconds: float = 1.0
    scroll_mode: ScrollMode = "wheel"
    wheel_notches: int = 7
    auto_stop: bool = True
    unchanged_threshold: float = 0.006
    unchanged_limit: int = 2
    image_format: ImageFormat = "png"

    def validate(self) -> None:
        if not 1 <= self.max_pages <= 9999:
            raise ValueError("页数上限必须在 1 到 9999 之间。")
        if not 0.1 <= self.scroll_delay_seconds <= 30:
            raise ValueError("翻页等待时间必须在 0.1 到 30 秒之间。")
        if self.scroll_mode not in ("wheel", "pagedown"):
            raise ValueError("不支持的翻页方式。")
        if not 1 <= self.wheel_notches <= 100:
            raise ValueError("滚轮格数必须在 1 到 100 之间。")
        if not 0 <= self.unchanged_threshold <= 1:
            raise ValueError("画面差异阈值必须在 0 到 1 之间。")
        if not 1 <= self.unchanged_limit <= 20:
            raise ValueError("无变化检测次数必须在 1 到 20 之间。")
        if self.image_format not in ("png", "jpg"):
            raise ValueError("图片格式只能是 PNG 或 JPG。")


@dataclass(frozen=True)
class ProgressEvent:
    kind: Literal["captured", "unchanged", "scrolling"]
    pages_saved: int
    max_pages: int
    difference_ratio: float | None = None
    unchanged_count: int = 0


@dataclass(frozen=True)
class CaptureResult:
    output_dir: Path
    pages_saved: int
    reason: Literal["end_detected", "max_pages", "cancelled", "error"]
    started_at: str
    ended_at: str
    error: str | None = None


CaptureFunction = Callable[[Region], Image.Image]
ScrollFunction = Callable[[Region, ScrollMode, int], None]
ProgressFunction = Callable[[ProgressEvent], None]


def create_run_directory(output_root: Path, now: datetime | None = None) -> Path:
    """Create a collision-safe directory for a single capture run."""
    output_root = Path(output_root).expanduser().resolve()
    output_root.mkdir(parents=True, exist_ok=True)

    timestamp = (now or datetime.now()).strftime("%Y%m%d_%H%M%S")
    base_name = f"网页截图_{timestamp}"
    for index in range(1, 1000):
        suffix = "" if index == 1 else f"_{index:02d}"
        candidate = output_root / f"{base_name}{suffix}"
        try:
            candidate.mkdir()
            return candidate
        except FileExistsError:
            continue
    raise RuntimeError("无法创建新的截图文件夹，请更换输出目录。")


def image_difference_ratio(first: Image.Image, second: Image.Image) -> float:
    """Return a robust 0..1 visual difference score for end detection.

    Images are reduced to a small grayscale preview. This deliberately ignores
    tiny animated elements while remaining sensitive to a document scrolling.
    """
    if first.size != second.size:
        return 1.0
    target_size = (96, 96)
    first_sample = first.convert("L").resize(target_size, Image.Resampling.BILINEAR)
    second_sample = second.convert("L").resize(target_size, Image.Resampling.BILINEAR)
    difference = ImageChops.difference(first_sample, second_sample)
    mean = ImageStat.Stat(difference).mean[0]
    return float(mean / 255.0)


class ScrollCaptureEngine:
    def __init__(
        self,
        capture: CaptureFunction,
        scroll: ScrollFunction,
        stop_event: threading.Event | None = None,
        progress: ProgressFunction | None = None,
    ) -> None:
        self._capture = capture
        self._scroll = scroll
        self._stop_event = stop_event or threading.Event()
        self._progress = progress

    def run(self, region: Region, options: CaptureOptions, output_dir: Path) -> CaptureResult:
        region.validate()
        options.validate()
        output_dir = Path(output_dir).resolve()
        output_dir.mkdir(parents=True, exist_ok=True)

        started_at = datetime.now().astimezone().isoformat(timespec="seconds")
        pages_saved = 0
        reason: Literal["end_detected", "max_pages", "cancelled", "error"] = "error"
        error_message: str | None = None
        unchanged_count = 0
        extension = options.image_format
        page_digits = max(3, len(str(options.max_pages)))

        self._write_manifest(
            output_dir,
            region,
            options,
            started_at,
            None,
            pages_saved,
            "running",
            None,
        )

        try:
            if self._stop_event.is_set():
                reason = "cancelled"
            else:
                previous = self._capture(region)
                pages_saved = 1
                self._save_image(
                    previous,
                    output_dir / f"page_{pages_saved:0{page_digits}d}.{extension}",
                    extension,
                )
                self._emit(ProgressEvent("captured", pages_saved, options.max_pages))
                self._write_manifest(
                    output_dir,
                    region,
                    options,
                    started_at,
                    None,
                    pages_saved,
                    "running",
                    None,
                )

                while pages_saved < options.max_pages:
                    if self._stop_event.is_set():
                        reason = "cancelled"
                        break

                    self._emit(ProgressEvent("scrolling", pages_saved, options.max_pages))
                    self._scroll(region, options.scroll_mode, options.wheel_notches)
                    if self._stop_event.wait(options.scroll_delay_seconds):
                        reason = "cancelled"
                        break

                    candidate = self._capture(region)
                    difference = image_difference_ratio(previous, candidate)
                    if options.auto_stop and difference <= options.unchanged_threshold:
                        unchanged_count += 1
                        self._emit(
                            ProgressEvent(
                                "unchanged",
                                pages_saved,
                                options.max_pages,
                                difference,
                                unchanged_count,
                            )
                        )
                        if unchanged_count >= options.unchanged_limit:
                            reason = "end_detected"
                            break
                        continue

                    unchanged_count = 0
                    pages_saved += 1
                    self._save_image(
                        candidate,
                        output_dir / f"page_{pages_saved:0{page_digits}d}.{extension}",
                        extension,
                    )
                    previous = candidate
                    self._emit(
                        ProgressEvent(
                            "captured",
                            pages_saved,
                            options.max_pages,
                            difference,
                        )
                    )
                    self._write_manifest(
                        output_dir,
                        region,
                        options,
                        started_at,
                        None,
                        pages_saved,
                        "running",
                        None,
                    )
                else:
                    reason = "max_pages"

                if pages_saved >= options.max_pages and reason == "error":
                    reason = "max_pages"
        except Exception as exc:  # Manifest the error so partial output remains useful.
            reason = "error"
            error_message = f"{type(exc).__name__}: {exc}"

        ended_at = datetime.now().astimezone().isoformat(timespec="seconds")
        result = CaptureResult(
            output_dir=output_dir,
            pages_saved=pages_saved,
            reason=reason,
            started_at=started_at,
            ended_at=ended_at,
            error=error_message,
        )
        self._write_manifest(
            output_dir,
            region,
            options,
            started_at,
            ended_at,
            pages_saved,
            reason,
            error_message,
        )
        return result

    def _emit(self, event: ProgressEvent) -> None:
        if self._progress is not None:
            self._progress(event)

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
        options: CaptureOptions,
        started_at: str,
        ended_at: str | None,
        pages_saved: int,
        status: str,
        error: str | None,
    ) -> None:
        payload = {
            "工具": "Auto Page Capture",
            "状态": status,
            "已保存页数": pages_saved,
            "开始时间": started_at,
            "结束时间": ended_at,
            "截图区域": asdict(region),
            "设置": asdict(options),
            "错误": error,
        }
        destination = output_dir / "capture_info.json"
        temporary = output_dir / ".capture_info.json.tmp"
        temporary.write_text(
            json.dumps(payload, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )
        os.replace(temporary, destination)
