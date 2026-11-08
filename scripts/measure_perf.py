# -*- coding: utf-8 -*-
"""PRD §42 性能实测:冷启动耗时 / Idle 内存 / 空闲 CPU。
用法: python scripts/measure_perf.py [exe路径]
"""
import ctypes
import ctypes.wintypes as wt
import os
import subprocess
import sys
import time

ctypes.windll.shcore.SetProcessDpiAwareness(2)
user32 = ctypes.windll.user32
psapi = ctypes.windll.psapi

exe = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "..",
    "src", "CleanMaster.App", "bin", "Debug", "net8.0-windows", "CleanMaster.exe")
exe = os.path.abspath(exe)
page = "Home"
for i, a in enumerate(sys.argv):
    if a == "--page" and i + 1 < len(sys.argv):
        page = sys.argv[i + 1]

# 杀掉残留实例(单实例互斥会让测量进程退出)
subprocess.run(["taskkill", "/F", "/IM", "CleanMaster.exe"], capture_output=True)
time.sleep(1.5)

def launch_and_wait():
    t0 = time.perf_counter()
    proc = subprocess.Popen([exe, "--page", page])
    hwnd = 0
    deadline = t0 + 15
    while time.perf_counter() < deadline:
        hwnd = user32.FindWindowW(None, "清理优化大师")
        if hwnd:
            r = wt.RECT()
            user32.GetWindowRect(hwnd, ctypes.byref(r))
            if r.right - r.left > 300:
                break
        time.sleep(0.02)
    return t0, time.perf_counter(), hwnd

# 冷启动两次:第一次含 Defender 对新文件的扫描,第二次为真实热启动
t0a, t1a, hwnd_a = launch_and_wait()
subprocess.run(["taskkill", "/F", "/IM", "CleanMaster.exe"], capture_output=True)
time.sleep(1.5)
t0, t1, hwnd = launch_and_wait()
cold = t1 - t0
print(f"[PERF] 冷启动(进程→主窗口就绪): 首跑 {t1a-t0a:.2f} s / 热跑 {cold:.2f} s  (目标 < 2.0 s, {'PASS' if cold < 2 else 'FAIL'})")

# 等待首页数据(启动项/应用枚举缓存)加载完,再测 Idle 内存与 CPU
time.sleep(25)

pid = wt.DWORD()
user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))


def process_memory(target_pid):
    PROCESS_QUERY_INFORMATION = 0x0400
    PROCESS_VM_READ = 0x0010
    h = ctypes.windll.kernel32.OpenProcess(
        PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, False, target_pid)
    if not h:
        return None
    counters = ctypes.c_uint64 * 1
    class PMC(ctypes.Structure):
        _fields_ = [("cb", ctypes.c_uint32), ("PageFaultCount", ctypes.c_uint32),
                    ("PeakWorkingSetSize", ctypes.c_size_t), ("WorkingSetSize", ctypes.c_size_t),
                    ("QuotaPeakPagedPoolUsage", ctypes.c_size_t), ("QuotaPagedPoolUsage", ctypes.c_size_t),
                    ("QuotaPeakNonPagedPoolUsage", ctypes.c_size_t), ("QuotaNonPagedPoolUsage", ctypes.c_size_t),
                    ("PagefileUsage", ctypes.c_size_t), ("PeakPagefileUsage", ctypes.c_size_t)]
    pmc = PMC()
    pmc.cb = ctypes.sizeof(PMC)
    ok = psapi.GetProcessMemoryInfo(h, ctypes.byref(pmc), pmc.cb)
    cpu = ctypes.c_uint64()
    ctypes.windll.kernel32.GetProcessTimes(
        h, ctypes.byref(ctypes.c_uint64()), ctypes.byref(ctypes.c_uint64()),
        ctypes.byref(cpu), ctypes.byref(cpu))
    ctypes.windll.kernel32.CloseHandle(h)
    return pmc.WorkingSetSize


m1 = process_memory(pid.value)
print(f"[PAGE] {page}")
print(f"[PERF] Idle 内存(WorkingSet, 首页稳定后): {m1/1024/1024:.1f} MB  (目标 < 150 MB, {'PASS' if m1/1024/1024 < 150 else 'FAIL'})")

# ---- 空闲 CPU:10 秒窗口内的进程 CPU 时间增量 ----
k = ctypes.windll.kernel32.OpenProcess(0x0400 | 0x0010, False, pid.value)
def cpu_time(h):
    create, exit_t, kernel, user = (ctypes.c_uint64() for _ in range(4))
    ctypes.windll.kernel32.GetProcessTimes(h, ctypes.byref(create), ctypes.byref(exit_t),
                                           ctypes.byref(kernel), ctypes.byref(user))
    return (kernel.value + user.value) / 1e7  # 100ns → 秒

c1 = cpu_time(k)
time.sleep(10)
c2 = cpu_time(k)
ctypes.windll.kernel32.CloseHandle(k)
cpu_pct = (c2 - c1) / 10 * 100
print(f"[PERF] 首页空闲 CPU(10s 均值): {cpu_pct:.2f} %  (目标 < 1.0 %, {'PASS' if cpu_pct < 1 else 'FAIL'})")

subprocess.run(["taskkill", "/F", "/IM", "CleanMaster.exe"], capture_output=True)
print("DONE")
