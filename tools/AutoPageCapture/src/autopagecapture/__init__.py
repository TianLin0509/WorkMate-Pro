"""Auto Page Capture - Windows region capture and automatic scrolling."""

from .core import (
    CaptureOptions,
    CaptureResult,
    ProgressEvent,
    Region,
    ScrollCaptureEngine,
    create_run_directory,
    image_difference_ratio,
)
from .alignment import AlignmentResult, RollingPageAssembler, VerticalFrameAligner
from .smart_capture import (
    AlignmentStep,
    SmartCaptureOptions,
    SmartCaptureResult,
    SmartProgressEvent,
    SmartScrollCaptureEngine,
    parse_advance_ratio,
)

__all__ = [
    "CaptureOptions",
    "CaptureResult",
    "ProgressEvent",
    "Region",
    "ScrollCaptureEngine",
    "create_run_directory",
    "image_difference_ratio",
    "AlignmentResult",
    "RollingPageAssembler",
    "VerticalFrameAligner",
    "AlignmentStep",
    "SmartCaptureOptions",
    "SmartCaptureResult",
    "SmartProgressEvent",
    "SmartScrollCaptureEngine",
    "parse_advance_ratio",
]
