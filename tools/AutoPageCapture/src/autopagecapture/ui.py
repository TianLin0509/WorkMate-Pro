from __future__ import annotations

import os
import threading
import tkinter as tk
from datetime import datetime
from pathlib import Path
from tkinter import filedialog, messagebox, ttk

from .core import Region, create_run_directory
from .smart_capture import (
    SmartCaptureOptions,
    SmartCaptureResult,
    SmartProgressEvent,
    SmartScrollCaptureEngine,
    parse_advance_ratio,
)
from .windows import (
    activate_target_at,
    capture_region,
    get_cursor_position,
    is_key_down,
    place_window,
    scroll_target,
    set_cursor_position,
    user32,
    virtual_screen_region,
)


APP_TITLE = "自动翻页截图工具"
VK_F8 = 0x77


def _default_output_root() -> Path:
    candidates = [
        Path.home() / "Pictures" / "网页分页截图",
        Path.home() / "Desktop" / "网页分页截图",
        Path.home() / "Documents" / "网页分页截图",
    ]
    for candidate in candidates:
        if candidate.parent.exists():
            return candidate
    return candidates[-1]


def _open_in_explorer(path: Path) -> None:
    path.mkdir(parents=True, exist_ok=True)
    os.startfile(str(path))  # type: ignore[attr-defined]


class RegionSelector:
    def __init__(self, root: tk.Tk, callback) -> None:
        self.root = root
        self.callback = callback
        self.virtual = virtual_screen_region()
        self.start_x: int | None = None
        self.start_y: int | None = None
        self.rectangle_id: int | None = None
        self.dimension_id: int | None = None
        self.finished = False

        self.window = tk.Toplevel(root)
        self.window.withdraw()
        self.window.overrideredirect(True)
        self.window.attributes("-topmost", True)
        self.window.attributes("-alpha", 0.34)
        self.window.configure(bg="#050a12", cursor="crosshair")
        # Give Tk the final physical-pixel size before mapping the window.
        # Calling SetWindowPos while the Toplevel is still withdrawn is not
        # enough: on high-DPI displays Tk otherwise restores its default size
        # during deiconify and the selector covers only the top-left corner.
        self.window.geometry(f"{self.virtual.width}x{self.virtual.height}+0+0")

        self.canvas = tk.Canvas(
            self.window,
            bg="#050a12",
            highlightthickness=0,
            cursor="crosshair",
        )
        self.canvas.pack(fill="both", expand=True)
        self.canvas.bind("<ButtonPress-1>", self._press)
        self.canvas.bind("<B1-Motion>", self._drag)
        self.canvas.bind("<ButtonRelease-1>", self._release)
        self.canvas.bind("<Button-3>", lambda _event: self.cancel())
        self.window.bind("<Escape>", lambda _event: self.cancel())

        self.window.deiconify()
        self.window.update_idletasks()
        place_window(self.window.winfo_id(), self.virtual, topmost=True)
        self.window.lift()
        self.window.focus_force()

        center_x = self.virtual.width // 2
        self.canvas.create_text(
            center_x,
            62,
            text="按住鼠标左键拖动，框住网页正文区域",
            fill="white",
            font=("Microsoft YaHei UI", 19, "bold"),
        )
        self.canvas.create_text(
            center_x,
            110,
            text="松开完成 · Esc 或鼠标右键取消",
            fill="#d8e7ff",
            font=("Microsoft YaHei UI", 12),
        )

    def _press(self, event: tk.Event) -> None:
        self.start_x = int(event.x)
        self.start_y = int(event.y)
        if self.rectangle_id is not None:
            self.canvas.delete(self.rectangle_id)
        if self.dimension_id is not None:
            self.canvas.delete(self.dimension_id)
        self.rectangle_id = self.canvas.create_rectangle(
            self.start_x,
            self.start_y,
            self.start_x,
            self.start_y,
            outline="#40d9ff",
            width=4,
            dash=(10, 5),
        )

    def _drag(self, event: tk.Event) -> None:
        if self.start_x is None or self.start_y is None or self.rectangle_id is None:
            return
        current_x = max(0, min(self.virtual.width - 1, int(event.x)))
        current_y = max(0, min(self.virtual.height - 1, int(event.y)))
        self.canvas.coords(self.rectangle_id, self.start_x, self.start_y, current_x, current_y)
        width = abs(current_x - self.start_x)
        height = abs(current_y - self.start_y)
        text_x = min(max(current_x + 70, 110), self.virtual.width - 110)
        text_y = min(max(current_y + 34, 28), self.virtual.height - 28)
        if self.dimension_id is None:
            self.dimension_id = self.canvas.create_text(
                text_x,
                text_y,
                text=f"{width} × {height}",
                fill="white",
                font=("Consolas", 12, "bold"),
            )
        else:
            self.canvas.coords(self.dimension_id, text_x, text_y)
            self.canvas.itemconfigure(self.dimension_id, text=f"{width} × {height}")

    def _release(self, event: tk.Event) -> None:
        if self.start_x is None or self.start_y is None:
            return
        end_x = max(0, min(self.virtual.width, int(event.x)))
        end_y = max(0, min(self.virtual.height, int(event.y)))
        left = min(self.start_x, end_x) + self.virtual.left
        top = min(self.start_y, end_y) + self.virtual.top
        right = max(self.start_x, end_x) + self.virtual.left
        bottom = max(self.start_y, end_y) + self.virtual.top
        region = Region(left, top, right, bottom)
        try:
            region.validate()
        except ValueError:
            self.canvas.create_text(
                self.virtual.width // 2,
                150,
                text="区域太小，请重新拖动（至少 80 × 80 像素）",
                fill="#ffcf70",
                font=("Microsoft YaHei UI", 12, "bold"),
                tags=("size_warning",),
            )
            self.canvas.after(1800, lambda: self.canvas.delete("size_warning"))
            return
        self._finish(region)

    def cancel(self) -> None:
        self._finish(None)

    def _finish(self, region: Region | None) -> None:
        if self.finished:
            return
        self.finished = True
        try:
            self.window.grab_release()
        except tk.TclError:
            pass
        self.window.destroy()
        self.callback(region)


class CaptureApp:
    def __init__(self, root: tk.Tk) -> None:
        self.root = root
        self.region: Region | None = None
        self.running = False
        self.stop_event = threading.Event()
        self.latest_output_dir: Path | None = None
        self.original_cursor_position: tuple[int, int] | None = None
        self.close_after_run = False
        self._f8_was_down = False
        self._controls: list[tk.Widget] = []

        self._configure_window()
        self._build_ui()
        self.root.protocol("WM_DELETE_WINDOW", self._on_close)
        self.root.after(70, self._poll_f8)

    def _configure_window(self) -> None:
        self.root.title(APP_TITLE)
        self.root.configure(bg="#f5f7fb")
        try:
            dpi = int(user32.GetDpiForWindow(self.root.winfo_id()))
        except (AttributeError, OSError):
            dpi = 96
        display_scale = max(1.0, min(dpi / 96.0, 1.55))
        width = int(760 * display_scale)
        height = int(690 * display_scale)
        screen = virtual_screen_region()
        x = screen.left + max(0, (screen.width - width) // 2)
        y = screen.top + max(0, (screen.height - height) // 2)
        self.root.geometry(f"{width}x{height}+{x}+{y}")
        self.root.minsize(int(700 * display_scale), int(640 * display_scale))

        style = ttk.Style(self.root)
        available = style.theme_names()
        if "vista" in available:
            style.theme_use("vista")
        style.configure("TFrame", background="#f5f7fb")
        style.configure("Card.TLabelframe", background="#ffffff", relief="solid", borderwidth=1)
        style.configure(
            "Card.TLabelframe.Label",
            background="#f5f7fb",
            foreground="#203047",
            font=("Microsoft YaHei UI", 11, "bold"),
        )
        style.configure(
            "Title.TLabel",
            background="#f5f7fb",
            foreground="#142033",
            font=("Microsoft YaHei UI", 20, "bold"),
        )
        style.configure(
            "Subtle.TLabel",
            background="#f5f7fb",
            foreground="#5c6a7d",
            font=("Microsoft YaHei UI", 10),
        )
        style.configure(
            "Status.TLabel",
            background="#edf4ff",
            foreground="#1f4e8c",
            font=("Microsoft YaHei UI", 10, "bold"),
            padding=10,
        )
        style.configure(
            "Primary.TButton",
            font=("Microsoft YaHei UI", 12, "bold"),
            padding=(18, 11),
        )
        style.configure("Action.TButton", font=("Microsoft YaHei UI", 10, "bold"), padding=(12, 8))

    def _build_ui(self) -> None:
        outer = ttk.Frame(self.root, padding=(24, 20, 24, 18))
        outer.pack(fill="both", expand=True)
        outer.columnconfigure(0, weight=1)

        ttk.Label(outer, text=APP_TITLE, style="Title.TLabel").grid(row=0, column=0, sticky="w")
        ttk.Label(
            outer,
            text="框选网页 → 视觉对齐去重 → 自动切成多张独立图片",
            style="Subtle.TLabel",
        ).grid(row=1, column=0, sticky="w", pady=(3, 16))

        region_card = ttk.LabelFrame(outer, text="1  框选截图区域", style="Card.TLabelframe", padding=14)
        region_card.grid(row=2, column=0, sticky="ew", pady=(0, 12))
        region_card.columnconfigure(1, weight=1)
        self.select_button = ttk.Button(
            region_card,
            text="框选网页正文",
            style="Action.TButton",
            command=self._select_region,
        )
        self.select_button.grid(row=0, column=0, rowspan=2, padx=(0, 16), sticky="ns")
        self._controls.append(self.select_button)
        self.region_var = tk.StringVar(value="尚未选择区域")
        ttk.Label(
            region_card,
            textvariable=self.region_var,
            font=("Microsoft YaHei UI", 10, "bold"),
        ).grid(row=0, column=1, sticky="w")
        ttk.Label(
            region_card,
            text="建议只框正文，不要包含浏览器地址栏；开始前把文档停在第一页。",
            foreground="#68778c",
        ).grid(row=1, column=1, sticky="w", pady=(5, 0))

        settings_card = ttk.LabelFrame(outer, text="2  截图与翻页设置", style="Card.TLabelframe", padding=14)
        settings_card.grid(row=3, column=0, sticky="ew", pady=(0, 12))
        settings_card.columnconfigure(1, weight=1)
        settings_card.columnconfigure(3, weight=1)

        ttk.Label(settings_card, text="保存到").grid(row=0, column=0, sticky="w", padx=(0, 10))
        self.output_var = tk.StringVar(value=str(_default_output_root()))
        self.output_entry = ttk.Entry(settings_card, textvariable=self.output_var)
        self.output_entry.grid(row=0, column=1, columnspan=2, sticky="ew", padx=(0, 8))
        self.browse_button = ttk.Button(settings_card, text="浏览…", command=self._browse_output)
        self.browse_button.grid(row=0, column=3, sticky="e")
        self._controls.extend([self.output_entry, self.browse_button])

        ttk.Label(settings_card, text="对齐模式").grid(row=1, column=0, sticky="w", pady=(14, 0), padx=(0, 10))
        self.mode_var = tk.StringVar(value="smart")
        mode_frame = ttk.Frame(settings_card)
        mode_frame.grid(row=1, column=1, sticky="w", pady=(14, 0))
        self.smart_radio = ttk.Radiobutton(
            mode_frame,
            text="智能对齐（推荐）",
            value="smart",
            variable=self.mode_var,
            command=self._update_mode_controls,
        )
        self.smart_radio.pack(side="left")
        self.fixed_radio = ttk.Radiobutton(
            mode_frame,
            text="固定比例",
            value="fixed_ratio",
            variable=self.mode_var,
            command=self._update_mode_controls,
        )
        self.fixed_radio.pack(side="left", padx=(14, 0))
        self._controls.extend([self.smart_radio, self.fixed_radio])

        ttk.Label(settings_card, text="推进比例  @").grid(row=1, column=2, sticky="e", pady=(14, 0), padx=(10, 5))
        self.ratio_var = tk.StringVar(value="0.80")
        self.ratio_spin = ttk.Spinbox(
            settings_card,
            from_=0.30,
            to=0.90,
            increment=0.01,
            width=7,
            textvariable=self.ratio_var,
            state="disabled",
        )
        self.ratio_spin.grid(row=1, column=3, sticky="w", pady=(14, 0))
        self._controls.append(self.ratio_spin)

        ttk.Label(settings_card, text="滚动后等待").grid(row=2, column=0, sticky="w", pady=(12, 0), padx=(0, 10))
        delay_frame = ttk.Frame(settings_card)
        delay_frame.grid(row=2, column=1, sticky="w", pady=(12, 0))
        self.delay_var = tk.StringVar(value="0.8")
        self.delay_spin = ttk.Spinbox(
            delay_frame,
            from_=0.1,
            to=30,
            increment=0.1,
            width=7,
            textvariable=self.delay_var,
        )
        self.delay_spin.pack(side="left")
        ttk.Label(delay_frame, text=" 秒").pack(side="left")
        self._controls.append(self.delay_spin)

        ttk.Label(settings_card, text="页数上限").grid(row=2, column=2, sticky="e", pady=(12, 0), padx=(10, 8))
        self.max_pages_var = tk.StringVar(value="100")
        self.max_pages_spin = ttk.Spinbox(
            settings_card,
            from_=1,
            to=9999,
            width=7,
            textvariable=self.max_pages_var,
        )
        self.max_pages_spin.grid(row=2, column=3, sticky="w", pady=(12, 0))
        self._controls.append(self.max_pages_spin)

        self.mode_hint_var = tk.StringVar(
            value="自动试滚、测量真实位移，并动态调整到约 @0.58～@0.86。"
        )
        self.mode_hint = ttk.Label(
            settings_card,
            textvariable=self.mode_hint_var,
            foreground="#486784",
        )
        self.mode_hint.grid(row=3, column=0, columnspan=3, sticky="w", pady=(12, 0))

        format_frame = ttk.Frame(settings_card)
        format_frame.grid(row=3, column=3, sticky="e", pady=(12, 0))
        ttk.Label(format_frame, text="格式  ").pack(side="left")
        self.format_var = tk.StringVar(value="png")
        self.format_combo = ttk.Combobox(
            format_frame,
            textvariable=self.format_var,
            values=("png", "jpg"),
            state="readonly",
            width=7,
        )
        self.format_combo.pack(side="left")
        self._controls.append(self.format_combo)

        action_card = ttk.LabelFrame(outer, text="3  开始", style="Card.TLabelframe", padding=14)
        action_card.grid(row=4, column=0, sticky="ew", pady=(0, 12))
        action_card.columnconfigure(0, weight=1)
        action_card.columnconfigure(1, weight=0)
        self.start_button = ttk.Button(
            action_card,
            text="开始自动截图",
            style="Primary.TButton",
            command=self._start_capture,
            state="disabled",
        )
        self.start_button.grid(row=0, column=0, sticky="ew", padx=(0, 10))
        self.stop_button = ttk.Button(
            action_card,
            text="停止（F8）",
            style="Action.TButton",
            command=self._request_stop,
            state="disabled",
        )
        self.stop_button.grid(row=0, column=1, sticky="ew")
        ttk.Label(
            action_card,
            text="启动后窗口会最小化。截图期间请勿操作鼠标；任何时候按 F8 都能安全停止。",
            foreground="#68778c",
        ).grid(row=1, column=0, columnspan=2, sticky="w", pady=(9, 0))

        self.status_var = tk.StringVar(value="等待框选截图区域")
        self.status_label = ttk.Label(outer, textvariable=self.status_var, style="Status.TLabel", anchor="w")
        self.status_label.grid(row=5, column=0, sticky="ew")
        self.progress = ttk.Progressbar(outer, mode="determinate", maximum=100, value=0)
        self.progress.grid(row=6, column=0, sticky="ew", pady=(8, 0))

        footer = ttk.Frame(outer)
        footer.grid(row=7, column=0, sticky="ew", pady=(12, 0))
        footer.columnconfigure(0, weight=1)
        self.open_button = ttk.Button(footer, text="打开最近截图文件夹", command=self._open_latest)
        self.open_button.grid(row=0, column=1, sticky="e")
        self.open_after_var = tk.BooleanVar(value=True)
        self.open_after_check = ttk.Checkbutton(
            footer,
            text="完成后自动打开文件夹",
            variable=self.open_after_var,
        )
        self.open_after_check.grid(row=0, column=0, sticky="w")
        self._controls.append(self.open_after_check)

    def _select_region(self) -> None:
        if self.running:
            return
        self.root.withdraw()
        self.root.after(180, self._show_selector)

    def _show_selector(self) -> None:
        RegionSelector(self.root, self._region_selected)

    def _region_selected(self, region: Region | None) -> None:
        self.root.deiconify()
        self.root.lift()
        self.root.focus_force()
        if region is None:
            self.status_var.set("已取消框选")
            return
        self.region = region
        self.region_var.set(
            f"已选择：{region.width} × {region.height} px   坐标 ({region.left}, {region.top})"
        )
        self.status_var.set("区域已就绪；确认网页停在第一页后即可开始")
        self.start_button.configure(state="normal")

    def _browse_output(self) -> None:
        initial = Path(os.path.expandvars(self.output_var.get())).expanduser()
        if not initial.exists():
            initial = initial.parent if initial.parent.exists() else Path.home()
        selected = filedialog.askdirectory(title="选择截图保存目录", initialdir=str(initial))
        if selected:
            self.output_var.set(selected)

    def _update_mode_controls(self) -> None:
        if self.running:
            return
        fixed = self.mode_var.get() == "fixed_ratio"
        self.ratio_spin.configure(state="normal" if fixed else "disabled")
        self.mode_hint_var.set(
            "按用户设定的 @ 比例推进；仍会逐页核对并去掉实际重复区域。"
            if fixed
            else "自动试滚、测量真实位移，并动态调整到约 @0.58～@0.86。"
        )

    def _parse_options(self) -> SmartCaptureOptions:
        try:
            max_pages = int(self.max_pages_var.get().strip())
            delay = float(self.delay_var.get().strip().replace(",", "."))
            ratio = parse_advance_ratio(self.ratio_var.get())
        except ValueError as exc:
            raise ValueError("请检查页数、等待时间和推进比例是否为有效数字。") from exc
        options = SmartCaptureOptions(
            max_pages=max_pages,
            scroll_delay_seconds=delay,
            capture_mode=self.mode_var.get(),  # type: ignore[arg-type]
            fixed_advance_ratio=ratio,
            image_format=self.format_var.get(),  # type: ignore[arg-type]
        )
        options.validate()
        return options

    def _start_capture(self) -> None:
        if self.running or self.region is None:
            return
        try:
            options = self._parse_options()
            output_text = os.path.expandvars(self.output_var.get().strip())
            if not output_text:
                raise ValueError("请选择截图保存目录。")
            output_root = Path(output_text).expanduser()
            output_dir = create_run_directory(output_root)
        except (ValueError, OSError, RuntimeError) as exc:
            messagebox.showerror(APP_TITLE, str(exc), parent=self.root)
            return

        self.running = True
        self.stop_event.clear()
        self.latest_output_dir = output_dir
        self.original_cursor_position = get_cursor_position()
        self.progress.configure(maximum=options.max_pages, value=0)
        self.status_var.set("窗口最小化后约 2 秒开始；请不要操作鼠标…")
        self._set_controls_running(True)
        self.root.update_idletasks()
        self.root.iconify()

        worker = threading.Thread(
            target=self._capture_worker,
            args=(self.region, options, output_dir),
            name="capture-worker",
            daemon=True,
        )
        worker.start()

    def _capture_worker(self, region: Region, options: SmartCaptureOptions, output_dir: Path) -> None:
        started_at = datetime.now().astimezone().isoformat(timespec="seconds")
        try:
            if not self.stop_event.wait(1.8):
                activate_target_at(region)
                self.stop_event.wait(0.25)

            engine = SmartScrollCaptureEngine(
                capture=capture_region,
                scroll=scroll_target,
                stop_event=self.stop_event,
                progress=self._queue_progress,
            )
            result = engine.run(region, options, output_dir)
        except Exception as exc:
            # Activation and UI integration happen outside the engine. Never leave
            # the app minimized forever if Windows input/foreground APIs fail.
            result = SmartCaptureResult(
                output_dir=output_dir,
                pages_saved=0,
                capture_steps=0,
                reason="error",
                started_at=started_at,
                ended_at=datetime.now().astimezone().isoformat(timespec="seconds"),
                error=f"{type(exc).__name__}: {exc}",
            )
        try:
            self.root.after(0, self._capture_finished, result)
        except (RuntimeError, tk.TclError):
            # The user may have closed the Tk root while the worker was winding down.
            return

    def _queue_progress(self, event: SmartProgressEvent) -> None:
        self.root.after(0, self._apply_progress, event)

    def _apply_progress(self, event: SmartProgressEvent) -> None:
        if not self.running:
            return
        self.progress.configure(value=event.pages_saved)
        if event.kind == "aligned" and event.advance_ratio is not None:
            self.status_var.set(
                f"第 {event.capture_step} 次对齐：推进 @{event.advance_ratio:.2f}，"
                f"重叠 {(event.overlap_ratio or 0):.0%}，置信度 {(event.confidence or 0):.0%}"
            )
        elif event.kind == "page_saved":
            self.status_var.set(f"已生成第 {event.pages_saved} 张去重分页图片；继续采集…")
        elif event.kind == "warning":
            self.status_var.set(event.message)
        else:
            self.status_var.set(event.message or "正在滚动并分析相邻画面…")

    def _capture_finished(self, result: SmartCaptureResult) -> None:
        self.running = False
        if self.original_cursor_position is not None:
            set_cursor_position(self.original_cursor_position)
            self.original_cursor_position = None
        self.root.deiconify()
        self.root.lift()
        self.root.focus_force()
        self._set_controls_running(False)
        self.progress.configure(value=result.pages_saved)

        reason_text = {
            "end_detected": "检测到页面已到底",
            "max_pages": "达到页数上限",
            "cancelled": "已由用户停止",
            "alignment_failed": "智能对齐无法继续",
            "error": "发生错误",
        }[result.reason]
        self.status_var.set(f"{reason_text}；共保存 {result.pages_saved} 张图片")

        if self.close_after_run:
            self.root.destroy()
            return

        details = (
            f"共保存 {result.pages_saved} 张图片，完成 {result.capture_steps} 次画面对齐。"
            + (
                f"\n其中 {result.alignment_fallbacks} 次使用了保守位移估计。"
                if result.alignment_fallbacks
                else ""
            )
        )
        if result.reason in ("error", "alignment_failed"):
            messagebox.showerror(
                APP_TITLE,
                f"{reason_text}。已保存的图片不会丢失。\n\n{result.error}\n\n{details}\n输出目录：\n{result.output_dir}",
                parent=self.root,
            )
        else:
            messagebox.showinfo(
                APP_TITLE,
                f"{reason_text}。\n\n{details}\n输出目录：\n{result.output_dir}",
                parent=self.root,
            )
        if self.open_after_var.get():
            try:
                _open_in_explorer(result.output_dir)
            except OSError as exc:
                messagebox.showwarning(APP_TITLE, f"无法自动打开文件夹：{exc}", parent=self.root)

    def _set_controls_running(self, running: bool) -> None:
        for control in self._controls:
            try:
                control.configure(state="disabled" if running else "normal")
            except tk.TclError:
                pass
        if not running:
            self.format_combo.configure(state="readonly")
            self._update_mode_controls()
            self.start_button.configure(state="normal" if self.region is not None else "disabled")
            self.stop_button.configure(state="disabled")
        else:
            self.start_button.configure(state="disabled")
            self.stop_button.configure(state="normal")

    def _request_stop(self) -> None:
        if not self.running:
            return
        self.stop_event.set()
        self.status_var.set("正在安全停止；已保存的图片会保留…")

    def _poll_f8(self) -> None:
        try:
            pressed = is_key_down(VK_F8)
            if pressed and not self._f8_was_down and self.running:
                self._request_stop()
            self._f8_was_down = pressed
            self.root.after(70, self._poll_f8)
        except tk.TclError:
            return

    def _open_latest(self) -> None:
        target = self.latest_output_dir
        if target is None:
            target = Path(os.path.expandvars(self.output_var.get())).expanduser()
        try:
            _open_in_explorer(target)
        except OSError as exc:
            messagebox.showerror(APP_TITLE, f"无法打开文件夹：{exc}", parent=self.root)

    def _on_close(self) -> None:
        if self.running:
            self.close_after_run = True
            self._request_stop()
            return
        self.root.destroy()


def run_app() -> None:
    root = tk.Tk()
    CaptureApp(root)
    root.mainloop()
