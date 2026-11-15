# -*- coding: utf-8 -*-
"""全页冒烟测试:顺序深链 8 个页面 + 语言切换,自动比对运行时错误增量。
用法: python scripts/smoke_test.py [exe路径]
任何新增 Error 都会 FAIL 并打印堆栈。
"""
import ctypes
import ctypes.wintypes as wt
import json
import os
import subprocess
import sys
import time

ctypes.windll.shcore.SetProcessDpiAwareness(2)
user32 = ctypes.windll.user32

exe = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "..",
    "src", "CleanMaster.App", "bin", "Debug", "net8.0-windows", "CleanMaster.exe")
exe = os.path.abspath(exe)

HIST = os.environ['LOCALAPPDATA'] + r'\CleanMaster\history.json'


def error_count():
    try:
        h = json.load(open(HIST, encoding='utf-8'))
        return len([r for r in h if r['Type'] == 'Error']), h
    except Exception:
        return 0, []


def launch_and_wait(page, wait):
    subprocess.run(["taskkill", "/F", "/IM", "CleanMaster.exe"], capture_output=True)
    time.sleep(1.2)
    proc = subprocess.Popen([exe, "--page", page])
    t0 = time.perf_counter()
    hwnd = 0
    while time.perf_counter() - t0 < 15:
        hwnd = user32.FindWindowW(None, "清理优化大师")
        if hwnd:
            r = wt.RECT()
            user32.GetWindowRect(hwnd, ctypes.byref(r))
            if r.right - r.left > 300:
                break
        time.sleep(0.05)
    time.sleep(wait)
    subprocess.run(["taskkill", "/F", "/IM", "CleanMaster.exe"], capture_output=True)
    time.sleep(0.8)


pages = [
    ("Home", 6), ("SmartScan", 8), ("CleanSpace", 12), ("SpaceAnalysis", 8),
    ("Startup", 8), ("Apps", 10), ("Toolbox", 8), ("Settings", 6),
]

base, hist = error_count()
print(f"[SMOKE] 基线 Error 数: {base}")
fail = 0
for page, wait in pages:
    launch_and_wait(page, wait)
    now, h = error_count()
    if now > base:
        new = [r for r in h if r['Type'] == 'Error'][base:now]
        print(f"[FAIL] {page}: +{now-base} 个错误")
        for r in new:
            print("   ", r.get('Detail', '')[:300])
        fail += now - base
        base = now
    else:
        print(f"[PASS] {page}")

# 语言切换(en-US 启动,再切回)
sp = os.environ['LOCALAPPDATA'] + r'\CleanMaster\settings.json'
settings = json.load(open(sp, encoding='utf-8'))
orig_lang = settings.get('Language', 'auto')
for lang in ('en-US', 'auto'):
    settings['Language'] = lang
    json.dump(settings, open(sp, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)
    launch_and_wait("Home", 6)
    now, h = error_count()
    if now > base:
        new = [r for r in h if r['Type'] == 'Error'][base:now]
        print(f"[FAIL] 语言切换 {lang}: +{now-base} 个错误")
        for r in new:
            print("   ", r.get('Detail', '')[:300])
        fail += now - base
        base = now
    else:
        print(f"[PASS] 语言切换 {lang}")

settings['Language'] = orig_lang
json.dump(settings, open(sp, 'w', encoding='utf-8'), ensure_ascii=False, indent=2)

print(f"[SMOKE] {'全部通过' if fail == 0 else f'{fail} 个错误,需修复'}")
sys.exit(0 if fail == 0 else 1)
