# -*- coding: utf-8 -*-
"""全分辨率截取主窗口并裁剪环形仪表区域(验证中心文字居中)。"""
import ctypes
import ctypes.wintypes as wt
import os
import time

ctypes.windll.shcore.SetProcessDpiAwareness(2)  # 物理像素坐标
from PIL import Image

OUT_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "shots", "v17")
OUT_DIR = os.path.abspath(OUT_DIR)
os.makedirs(OUT_DIR, exist_ok=True)

user32, gdi32 = ctypes.windll.user32, ctypes.windll.gdi32
hwnd = 0
r = wt.RECT()
for _ in range(40):
    hwnd = user32.FindWindowW(None, "清理优化大师")
    if hwnd:
        user32.GetWindowRect(hwnd, ctypes.byref(r))
        if r.right - r.left > 500:
            break
    time.sleep(0.5)
if not hwnd:
    print("NO WINDOW")
    raise SystemExit(1)

w, h = r.right - r.left, r.bottom - r.top
hdc = user32.GetWindowDC(hwnd)
mem = gdi32.CreateCompatibleDC(hdc)
bmp = gdi32.CreateCompatibleBitmap(hdc, w, h)
gdi32.SelectObject(mem, bmp)
user32.PrintWindow(hwnd, mem, 2)


class BMIH(ctypes.Structure):
    _fields_ = [("biSize", ctypes.c_uint32), ("biWidth", ctypes.c_int32),
                ("biHeight", ctypes.c_int32), ("biPlanes", ctypes.c_uint16),
                ("biBitCount", ctypes.c_uint16), ("biCompression", ctypes.c_uint32),
                ("biSizeImage", ctypes.c_uint32), ("biXPelsPerMeter", ctypes.c_int32),
                ("biYPelsPerMeter", ctypes.c_int32), ("biClrUsed", ctypes.c_uint32),
                ("biClrImportant", ctypes.c_uint32)]


class BMI(ctypes.Structure):
    _fields_ = [("bmiHeader", BMIH), ("bmiColors", ctypes.c_uint32 * 3)]


bmi = BMI()
bmi.bmiHeader.biSize = ctypes.sizeof(BMIH)
bmi.bmiHeader.biWidth = w
bmi.bmiHeader.biHeight = -h
bmi.bmiHeader.biPlanes = 1
bmi.bmiHeader.biBitCount = 32
buf = ctypes.create_string_buffer(w * h * 4)
gdi32.GetDIBits(mem, bmp, 0, h, buf, ctypes.byref(bmi), 0)
img = Image.frombuffer("RGBA", (w, h), buf.raw, "raw", "BGRA", 0, 1).convert("RGB")
img.save(os.path.join(OUT_DIR, "Home_full.png"))

SCALE = 1.5  # DPI 144
# 窗口内逻辑坐标(0 基)× SCALE → 物理像素
img.crop((int(950 * SCALE), int(40 * SCALE), w, int(400 * SCALE))).save(os.path.join(OUT_DIR, "crop_health.png"))
img.crop((int(250 * SCALE), int(600 * SCALE), w, int(740 * SCALE))).save(os.path.join(OUT_DIR, "crop_monitor.png"))
img.crop((int(250 * SCALE), int(750 * SCALE), int(900 * SCALE), int(1120 * SCALE))).save(os.path.join(OUT_DIR, "crop_disk.png"))
print("saved", w, h)
