# -*- coding: utf-8 -*-
"""用 PrintWindow 直接抓取清理优化大师窗口内容（不受遮挡/多屏/DPI 影响）"""
import ctypes, ctypes.wintypes as wt, sys, time
from PIL import Image

out = sys.argv[1] if len(sys.argv) > 1 else r"T:\2、win\1-清理优化大师\shots\page.png"

user32, gdi32 = ctypes.windll.user32, ctypes.windll.gdi32

# 等待出现一个非最小化、尺寸正常的主窗口（避开正在销毁的旧实例）
hwnd = 0
for _ in range(40):
    hwnd = user32.FindWindowW(None, "清理优化大师")
    if hwnd:
        if user32.IsIconic(hwnd):
            user32.ShowWindow(hwnd, 9)
            time.sleep(0.5)
        r = wt.RECT()
        user32.GetWindowRect(hwnd, ctypes.byref(r))
        if r.right - r.left > 500 and r.bottom - r.top > 400:
            break
    time.sleep(0.5)
if not hwnd:
    print("NO WINDOW")
    sys.exit(1)

user32.SetForegroundWindow(hwnd)
user32.ShowWindow(hwnd, 9)
time.sleep(1.0)

rect = wt.RECT()
user32.GetWindowRect(hwnd, ctypes.byref(rect))
w, h = rect.right - rect.left, rect.bottom - rect.top
# 打印窗口实际渲染尺寸（DPI 缩放后），避免截图右侧/底部被截断
try:
    dpi = user32.GetDpiForWindow(hwnd)
except Exception:
    dpi = 96
scale = (dpi or 96) / 96.0
if scale > 1.01:
    w, h = int(w * scale), int(h * scale)
print("RECT", rect.left, rect.top, w, h, "dpi", dpi)

hdc = user32.GetWindowDC(hwnd)
mem = gdi32.CreateCompatibleDC(hdc)
bmp = gdi32.CreateCompatibleBitmap(hdc, w, h)
gdi32.SelectObject(mem, bmp)
ok = user32.PrintWindow(hwnd, mem, 2)  # PW_RENDERFULLCONTENT

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
bmi.bmiHeader.biCompression = 0
buf = ctypes.create_string_buffer(w * h * 4)
gdi32.GetDIBits(mem, bmp, 0, h, buf, ctypes.byref(bmi), 0)

img = Image.frombuffer("RGB", (w, h), buf.raw, "raw", "BGRX", 0, 1)
# 输出统一缩放到宽 1100 以内
tw = min(1100, w)
th = int(h * tw / w)
img = img.resize((tw, th))
img.save(out)
print("ok" if ok else "printwindow-false", "saved", out, img.size)

gdi32.DeleteObject(bmp)
gdi32.DeleteDC(mem)
user32.ReleaseDC(hwnd, hdc)
