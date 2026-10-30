"""离线演讲倒计时（Python 3.9+，仅使用标准库）。

运行：python examples/manual-timers/countdown.py
Windows 打包（需要已安装 Python 和 PyInstaller）：
    python -m PyInstaller --onefile --windowed --name 演讲倒计时 --distpath .local/agent/apps/manual-timer --workpath .local/agent/tmp/pyinstaller --specpath .local/agent/tmp examples/manual-timers/countdown.py
产物：.local/agent/apps/manual-timer/演讲倒计时.exe。打包后使用者无需安装 Python。

操作：空格开始/暂停；R 重置；Esc 退出精简显示。
本版本手动启动，不检测 WPS；没有联网、安装服务或修改注册表的代码。
"""

import math
import sys
import threading
import time
import tkinter as tk
from tkinter import messagebox


BG = "#101827"
PANEL = "#1d293d"
TEXT = "#edf3ff"
GREEN = "#6ee7b7"
YELLOW = "#fbbf24"
RED = "#fb7185"


def remaining_seconds(deadline, now):
    """使用单调时钟，避免系统时间调整以及界面刷新造成计时漂移。"""
    return max(0, math.ceil(deadline - now))


def format_time(seconds):
    minutes, seconds = divmod(max(0, int(seconds)), 60)
    return f"{minutes:02d}:{seconds:02d}"


class Countdown:
    def __init__(self, root):
        self.root = root
        self.running = False
        self.total = 600
        self.remaining = 600
        self.deadline = 0.0
        self.paused_remaining = 600.0
        self.warning = 60
        self.warned = False
        self.finished = False
        self.compact = False
        self.sound_lock = threading.Lock()
        self.animation_start = None
        self.animation_color = GREEN
        self.sound_enabled = tk.BooleanVar(value=True)
        self.topmost = tk.BooleanVar(value=True)
        self.minutes = tk.StringVar(value="10")
        self.warning_seconds = tk.StringVar(value="60")

        root.title("演讲倒计时")
        root.geometry("560x500")
        root.minsize(380, 430)
        root.configure(bg=BG)
        root.attributes("-topmost", True)

        self.header = tk.Label(root, text="演讲倒计时", fg=TEXT, bg=BG,
                               font=("Microsoft YaHei UI", 17, "bold"))
        self.header.pack(pady=(20, 4))
        self.status = tk.Label(root, text="设置时长，准备好后点击开始", fg=GREEN,
                               bg=BG, font=("Microsoft YaHei UI", 11))
        self.status.pack(pady=4)
        self.canvas = tk.Canvas(root, bg=BG, highlightthickness=0, height=165)
        self.canvas.pack(fill="both", expand=True, padx=16, pady=8)
        self.canvas.bind("<Configure>", lambda event: self.draw())

        self.settings = tk.Frame(root, bg=BG)
        self.settings.pack(pady=6)
        self.entries = []
        for row, (label, variable, suffix) in enumerate([
            ("倒计时时长", self.minutes, "分钟"),
            ("临近结束提醒", self.warning_seconds, "秒前"),
        ]):
            tk.Label(self.settings, text=label, bg=BG, fg=TEXT).grid(
                row=row, column=0, padx=8, pady=6, sticky="e")
            entry = tk.Entry(self.settings, textvariable=variable, width=8,
                             justify="center", font=("Arial", 13))
            entry.grid(row=row, column=1, padx=6)
            self.entries.append(entry)
            tk.Label(self.settings, text=suffix, bg=BG, fg=TEXT).grid(
                row=row, column=2, padx=6, sticky="w")

        self.options = tk.Frame(root, bg=BG)
        self.options.pack(pady=4)
        for label, variable, command in [
            ("提示音", self.sound_enabled, None),
            ("窗口置顶", self.topmost,
             lambda: root.attributes("-topmost", self.topmost.get())),
        ]:
            tk.Checkbutton(self.options, text=label, variable=variable,
                           command=command, bg=BG, fg=TEXT, selectcolor=PANEL,
                           activebackground=BG, activeforeground=TEXT).pack(side="left", padx=8)
        tk.Button(self.options, text="试听", command=lambda: self.play_sound(False)).pack(
            side="left", padx=8)

        self.buttons = tk.Frame(root, bg=BG)
        self.buttons.pack(pady=12)
        self.start_button = tk.Button(self.buttons, text="开始", width=9,
                                      command=self.toggle)
        self.start_button.pack(side="left", padx=5)
        tk.Button(self.buttons, text="重置", width=9, command=self.reset).pack(side="left", padx=5)
        self.compact_button = tk.Button(self.buttons, text="精简显示", command=self.toggle_compact)
        self.compact_button.pack(side="left", padx=5)
        self.footer = tk.Label(root, text="空格 开始/暂停  ·  R 重置  ·  可拖动到投影屏",
                               bg=BG, fg="#94a3b8", font=("Microsoft YaHei UI", 9))
        self.footer.pack(pady=(0, 14))
        root.bind("<space>", self.space_key)
        root.bind("<KeyPress-r>", self.reset_key)
        root.bind("<KeyPress-R>", self.reset_key)
        root.bind("<Escape>", lambda event: self.toggle_compact() if self.compact else None)
        self.tick()

    def space_key(self, event):
        if isinstance(event.widget, (tk.Entry, tk.Button, tk.Checkbutton)):
            return
        self.toggle()
        return "break"

    def reset_key(self, event):
        if not isinstance(event.widget, tk.Entry):
            self.reset()
            return "break"

    def read_settings(self):
        try:
            minutes = float(self.minutes.get())
            warning = int(self.warning_seconds.get())
            if not math.isfinite(minutes) or not (1 / 60 <= minutes <= 1440):
                raise ValueError
            total = round(minutes * 60)
            if not 0 <= warning < total:
                raise ValueError
        except (ValueError, OverflowError):
            messagebox.showerror("请检查时间", "时长需在 1 秒至 1440 分钟之间。\n提醒秒数必须为整数，小于总时长；填 0 关闭临近提醒。", parent=self.root)
            return False
        self.total = total
        self.remaining = total
        self.paused_remaining = float(total)
        self.warning = warning
        return True

    def toggle(self):
        if self.running:
            self.paused_remaining = max(0.0, self.deadline - time.monotonic())
            if self.paused_remaining <= 0:
                self.tick_state()
                return
            self.running = False
            self.start_button.configure(text="继续")
            self.status.configure(text="已暂停", fg=YELLOW)
            return
        if self.finished:
            if not self.reset():
                return
        if str(self.entries[0].cget("state")) == "normal":
            if not self.read_settings():
                return
            for entry in self.entries:
                entry.configure(state="disabled")
        self.deadline = time.monotonic() + self.paused_remaining
        self.running = True
        self.start_button.configure(text="暂停")
        self.status.configure(text="倒计时进行中", fg=GREEN)
        self.root.focus_set()

    def reset(self):
        if not self.read_settings():
            return False
        self.running = False
        self.finished = False
        self.warned = False
        self.animation_start = None
        for entry in self.entries:
            entry.configure(state="normal")
        self.start_button.configure(text="开始")
        self.status.configure(text="已重置，可以调整时长", fg=GREEN)
        self.draw()
        return True

    def alert(self, final):
        self.animation_start = time.monotonic()
        self.animation_color = RED if final else YELLOW
        self.status.configure(text="时间到！" if final else "即将结束，请注意时间",
                              fg=self.animation_color)
        self.play_sound(final)

    def play_sound(self, final):
        if not self.sound_enabled.get():
            return
        if sys.platform != "win32":
            self.root.bell()
            return
        # 声音在后台播放，避免阻塞倒计时与动画；不操作 Tk 控件。
        def worker():
            if not self.sound_lock.acquire(blocking=False):
                return
            try:
                import winsound
                notes = [(880, 170), (1100, 230)]
                if final:
                    notes = [(880, 220), (880, 220), (1175, 420)]
                for frequency, duration in notes:
                    winsound.Beep(frequency, duration)
                    time.sleep(0.09)
            except (RuntimeError, OSError):
                try:
                    winsound.MessageBeep()
                except (RuntimeError, OSError):
                    pass
            finally:
                self.sound_lock.release()
        threading.Thread(target=worker, daemon=True).start()

    def tick_state(self):
        if not self.running:
            return
        self.remaining = remaining_seconds(self.deadline, time.monotonic())
        if self.remaining == 0:
            self.running = False
            self.finished = True
            self.paused_remaining = 0
            self.start_button.configure(text="再来一次")
            self.alert(True)
        elif self.warning and self.remaining <= self.warning and not self.warned:
            self.warned = True
            self.alert(False)

    def tick(self):
        self.tick_state()
        self.draw()
        self.root.after(50, self.tick)

    def draw(self):
        canvas = self.canvas
        width, height = canvas.winfo_width(), canvas.winfo_height()
        if width < 10 or height < 10:
            return
        canvas.delete("all")
        color = RED if self.finished else YELLOW if self.warned else GREEN
        scale = 1.0
        if self.animation_start is not None:
            elapsed = time.monotonic() - self.animation_start
            if elapsed < 6:
                # 柔和呼吸边框和数字缩放，避免频繁明暗闪烁。
                pulse = (math.sin(elapsed * math.pi * 2) + 1) / 2
                scale += 0.035 * pulse
                canvas.create_rectangle(5, 5, width - 5, height - 5,
                                        outline=self.animation_color, width=2 + 4 * pulse)
            else:
                self.animation_start = None
        size = int(min(width / (len(format_time(self.remaining)) * 0.68), height * 0.49) * scale)
        canvas.create_text(width / 2, height * 0.45, text=format_time(self.remaining),
                           fill=color, font=("Consolas", max(20, size), "bold"))
        left, right = 22, width - 22
        bottom = height - 24
        canvas.create_rectangle(left, bottom, right, bottom + 6, fill=PANEL, outline="")
        fraction = self.remaining / self.total if self.total else 0
        if fraction > 0:
            canvas.create_rectangle(left, bottom, left + (right - left) * fraction,
                                    bottom + 6, fill=color, outline="")

    def toggle_compact(self):
        self.compact = not self.compact
        if self.compact:
            self.previous_geometry = self.root.geometry()
            for widget in (self.header, self.settings, self.options, self.footer):
                widget.pack_forget()
            self.root.minsize(330, 230)
            self.root.geometry("400x250")
            self.compact_button.configure(text="完整显示")
        else:
            self.header.pack(before=self.status, pady=(20, 4))
            self.settings.pack(before=self.buttons, pady=6)
            self.options.pack(before=self.buttons, pady=4)
            self.footer.pack(pady=(0, 14))
            self.root.minsize(380, 430)
            self.root.geometry(self.previous_geometry)
            self.compact_button.configure(text="精简显示")


if __name__ == "__main__":
    root = tk.Tk()
    Countdown(root)
    root.mainloop()
