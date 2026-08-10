from __future__ import annotations

import ctypes
import time
from ctypes import wintypes

from .core import Region, ScrollMode


user32 = ctypes.windll.user32


def enable_dpi_awareness() -> None:
    """Use physical pixels so selection and screenshot coordinates match."""
    try:
        # PER_MONITOR_AWARE_V2. This must run before Tk creates a window.
        user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
    except (AttributeError, OSError):
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(2)
        except (AttributeError, OSError):
            user32.SetProcessDPIAware()


def virtual_screen_region() -> Region:
    left = user32.GetSystemMetrics(76)  # SM_XVIRTUALSCREEN
    top = user32.GetSystemMetrics(77)  # SM_YVIRTUALSCREEN
    width = user32.GetSystemMetrics(78)  # SM_CXVIRTUALSCREEN
    height = user32.GetSystemMetrics(79)  # SM_CYVIRTUALSCREEN
    return Region(left, top, left + width, top + height)


def capture_region(region: Region):
    from PIL import ImageGrab

    region.validate()
    return ImageGrab.grab(
        bbox=(region.left, region.top, region.right, region.bottom),
        all_screens=True,
    ).convert("RGB")


class POINT(ctypes.Structure):
    _fields_ = [("x", wintypes.LONG), ("y", wintypes.LONG)]


ULONG_PTR = wintypes.WPARAM


class MOUSEINPUT(ctypes.Structure):
    _fields_ = [
        ("dx", wintypes.LONG),
        ("dy", wintypes.LONG),
        ("mouseData", wintypes.DWORD),
        ("dwFlags", wintypes.DWORD),
        ("time", wintypes.DWORD),
        ("dwExtraInfo", ULONG_PTR),
    ]


class KEYBDINPUT(ctypes.Structure):
    _fields_ = [
        ("wVk", wintypes.WORD),
        ("wScan", wintypes.WORD),
        ("dwFlags", wintypes.DWORD),
        ("time", wintypes.DWORD),
        ("dwExtraInfo", ULONG_PTR),
    ]


class HARDWAREINPUT(ctypes.Structure):
    _fields_ = [
        ("uMsg", wintypes.DWORD),
        ("wParamL", wintypes.WORD),
        ("wParamH", wintypes.WORD),
    ]


class INPUT_UNION(ctypes.Union):
    _fields_ = [("mi", MOUSEINPUT), ("ki", KEYBDINPUT), ("hi", HARDWAREINPUT)]


class INPUT(ctypes.Structure):
    _anonymous_ = ("union",)
    _fields_ = [("type", wintypes.DWORD), ("union", INPUT_UNION)]


user32.WindowFromPoint.argtypes = [POINT]
user32.WindowFromPoint.restype = wintypes.HWND
user32.GetAncestor.argtypes = [wintypes.HWND, wintypes.UINT]
user32.GetAncestor.restype = wintypes.HWND
user32.SendInput.argtypes = [wintypes.UINT, ctypes.POINTER(INPUT), ctypes.c_int]
user32.SendInput.restype = wintypes.UINT


def get_cursor_position() -> tuple[int, int]:
    point = POINT()
    if not user32.GetCursorPos(ctypes.byref(point)):
        return (0, 0)
    return (point.x, point.y)


def set_cursor_position(position: tuple[int, int]) -> None:
    user32.SetCursorPos(int(position[0]), int(position[1]))


def activate_target_at(region: Region) -> int:
    center_x, center_y = region.center
    user32.SetCursorPos(center_x, center_y)
    window = user32.WindowFromPoint(POINT(center_x, center_y))
    if window:
        root_window = user32.GetAncestor(window, 2)  # GA_ROOT
        if root_window:
            user32.SetForegroundWindow(root_window)
            return int(root_window)
    return int(window or 0)


def scroll_target(region: Region, mode: ScrollMode, wheel_notches: int) -> None:
    activate_target_at(region)
    if mode == "pagedown":
        _send_key(0x22)  # VK_NEXT / Page Down
        return

    for _ in range(wheel_notches):
        mouse_input = INPUT(
            type=0,
            mi=MOUSEINPUT(
                dx=0,
                dy=0,
                mouseData=ctypes.c_ulong(-120).value,
                dwFlags=0x0800,  # MOUSEEVENTF_WHEEL
                time=0,
                dwExtraInfo=0,
            ),
        )
        sent = user32.SendInput(1, ctypes.byref(mouse_input), ctypes.sizeof(INPUT))
        if sent != 1:
            raise OSError("Windows 未能发送滚轮事件。")
        time.sleep(0.018)


def _send_key(virtual_key: int) -> None:
    inputs = (INPUT * 2)(
        INPUT(
            type=1,
            ki=KEYBDINPUT(
                wVk=virtual_key,
                wScan=0,
                dwFlags=0,
                time=0,
                dwExtraInfo=0,
            ),
        ),
        INPUT(
            type=1,
            ki=KEYBDINPUT(
                wVk=virtual_key,
                wScan=0,
                dwFlags=0x0002,  # KEYEVENTF_KEYUP
                time=0,
                dwExtraInfo=0,
            ),
        ),
    )
    sent = user32.SendInput(2, inputs, ctypes.sizeof(INPUT))
    if sent != 2:
        raise OSError("Windows 未能发送 Page Down 按键。")


def is_key_down(virtual_key: int) -> bool:
    return bool(user32.GetAsyncKeyState(virtual_key) & 0x8000)


def place_window(hwnd: int, region: Region, topmost: bool = True) -> None:
    insert_after = -1 if topmost else 0  # HWND_TOPMOST / HWND_TOP
    flags = 0x0040  # SWP_SHOWWINDOW
    user32.SetWindowPos(
        hwnd,
        insert_after,
        region.left,
        region.top,
        region.width,
        region.height,
        flags,
    )
