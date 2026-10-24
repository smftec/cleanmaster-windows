using System.Runtime.InteropServices;

namespace CleanMaster.Core.Analysis;

/// <summary>
/// 轻量系统监控：CPU 占用率（GetSystemTimes 差分）+ 物理内存（GlobalMemoryStatusEx）。
/// 无需管理员权限，不依赖性能计数器（避免初始化慢与本地化实例名问题）。
/// </summary>
public sealed class SystemMonitorService
{
    public sealed class Snapshot
    {
        /// <summary>CPU 占用百分比 0..100；不可用时为 null。</summary>
        public double? CpuPercent { get; init; }
        /// <summary>物理内存已用字节。</summary>
        public long RamUsedBytes { get; init; }
        /// <summary>物理内存总量字节。</summary>
        public long RamTotalBytes { get; init; }
        public double RamPercent => RamTotalBytes > 0 ? RamUsedBytes * 100.0 / RamTotalBytes : 0;
        /// <summary>系统开机时长。</summary>
        public TimeSpan Uptime { get; init; }
    }

    private long _lastIdle, _lastKernel, _lastUser;
    private bool _hasLast;

    /// <summary>采样一次。首次调用 CPU 返回 null（差分需要两次采样）。</summary>
    public Snapshot Sample()
    {
        double? cpu = null;
        if (GetSystemTimes(out var idle, out var kernel, out var user))
        {
            if (_hasLast)
            {
                var idleDelta = idle - _lastIdle;
                // kernel 含 idle，总时间 = kernel + user
                var totalDelta = (kernel - _lastKernel) + (user - _lastUser);
                if (totalDelta > 0)
                    cpu = Math.Clamp((totalDelta - idleDelta) * 100.0 / totalDelta, 0, 100);
            }
            _lastIdle = idle; _lastKernel = kernel; _lastUser = user;
            _hasLast = true;
        }

        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        GlobalMemoryStatusEx(ref mem);

        return new Snapshot
        {
            CpuPercent = cpu,
            RamUsedBytes = (long)(mem.ullTotalPhys - mem.ullAvailPhys),
            RamTotalBytes = (long)mem.ullTotalPhys,
            Uptime = TimeSpan.FromMilliseconds(GetTickCount64()),
        };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [DllImport("kernel32.dll")]
    private static extern ulong GetTickCount64();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out long lpIdleTime, out long lpKernelTime, out long lpUserTime);
}
