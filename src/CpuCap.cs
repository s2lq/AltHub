using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace AltHub
{
    // Maximum CPU for Roblox: every Roblox window (or all of them together) gets a hard CPU cap through a Windows job object
    // (Windows 8 and newer). The percentage is of the WHOLE PC, like Task Manager shows it: with 10% on an 8-core PC one window
    // can use at most 10% of all cores. A capped window runs slower (lower FPS) instead of hogging the CPU.
    public static class CpuCap
    {
        [StructLayout(LayoutKind.Sequential)]
        struct JOBOBJECT_CPU_RATE_CONTROL_INFORMATION { public uint ControlFlags; public uint CpuRate; }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateJobObject(IntPtr attrs, string name);
        [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref JOBOBJECT_CPU_RATE_CONTROL_INFORMATION info, int size);
        [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
        [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr h);

        const int JobObjectCpuRateControlInformation = 15;
        const uint ENABLE = 0x1, HARD_CAP = 0x4;
        const uint NoLimit = 10000;                      // 100.00 % of the PC = effectively no cap

        class St { public IntPtr Job; public uint Applied = uint.MaxValue; }

        static readonly Dictionary<int, St> map = new Dictionary<int, St>();
        static IntPtr totalJob = IntPtr.Zero;
        static uint totalApplied = uint.MaxValue;
        static readonly HashSet<int> inTotal = new HashSet<int>();
        static bool warned;

        public static bool Enabled { get { return Store.Settings.CpuCapMode != "Off"; } }

        // Called about every 5 seconds.
        public static void Tick(Action<string> log)
        {
            string mode = Store.Settings.CpuCapMode;
            if (mode == "Off") { if (map.Count > 0 || totalJob != IntPtr.Zero) Release(); return; }
            List<int> pids = Launcher.RobloxPids();
            foreach (int dead in map.Keys.Where(k => !pids.Contains(k)).ToList()) { CloseHandle(map[dead].Job); map.Remove(dead); }
            inTotal.RemoveWhere(p => !pids.Contains(p));
            if (pids.Count == 0) return;
            uint rate = (uint)(Math.Max(1, Math.Min(100, Store.Settings.CpuCapPercent)) * 100);

            if (mode == "Total")
            {
                if (totalJob == IntPtr.Zero) { totalJob = CreateJobObject(IntPtr.Zero, null); totalApplied = uint.MaxValue; }
                if (totalJob == IntPtr.Zero) return;
                if (totalApplied != rate && SetRate(totalJob, rate, log)) totalApplied = rate;
                foreach (int pid in pids) if (!inTotal.Contains(pid) && Assign(totalJob, pid, log)) inTotal.Add(pid);
                return;
            }

            int active = Store.Settings.CpuCapSkipActive ? MaxAlts.ActiveRobloxPid() : 0;
            foreach (int pid in pids)
            {
                St s;
                if (!map.TryGetValue(pid, out s))
                {
                    s = new St();
                    s.Job = CreateJobObject(IntPtr.Zero, null);
                    if (s.Job == IntPtr.Zero) continue;
                    if (!SetRate(s.Job, NoLimit, log)) { CloseHandle(s.Job); continue; }
                    s.Applied = NoLimit;
                    if (!Assign(s.Job, pid, log)) { CloseHandle(s.Job); continue; }
                    map[pid] = s;
                }
                uint want = pid == active ? NoLimit : rate;            // the window you are playing is left alone
                if (want != s.Applied && SetRate(s.Job, want, log)) s.Applied = want;
            }
        }

        static bool SetRate(IntPtr job, uint rate, Action<string> log)
        {
            JOBOBJECT_CPU_RATE_CONTROL_INFORMATION info = new JOBOBJECT_CPU_RATE_CONTROL_INFORMATION();
            info.ControlFlags = ENABLE | HARD_CAP;
            info.CpuRate = rate;
            bool ok = SetInformationJobObject(job, JobObjectCpuRateControlInformation, ref info, Marshal.SizeOf(typeof(JOBOBJECT_CPU_RATE_CONTROL_INFORMATION)));
            if (!ok && !warned) { warned = true; log("CPU limit: Windows refused the CPU cap (it needs Windows 8 or newer). Error " + Marshal.GetLastWin32Error() + "."); }
            return ok;
        }

        static bool Assign(IntPtr job, int pid, Action<string> log)
        {
            IntPtr h = OpenProcess(0x0101, false, pid);                // PROCESS_SET_QUOTA | PROCESS_TERMINATE
            if (h == IntPtr.Zero) return false;
            try
            {
                bool ok = AssignProcessToJobObject(job, h);
                if (!ok && !warned) { warned = true; log("CPU limit: could not attach a Roblox window to the limit (error " + Marshal.GetLastWin32Error() + ")."); }
                return ok;
            }
            finally { CloseHandle(h); }
        }

        // Lifts every cap (mode switched off or changed, or AltHub is closing). A job keeps its limit while its processes run,
        // so the cap is set to 100 % before the handle goes away.
        public static void Release()
        {
            foreach (St s in map.Values.ToList()) { JOBOBJECT_CPU_RATE_CONTROL_INFORMATION i = new JOBOBJECT_CPU_RATE_CONTROL_INFORMATION(); i.ControlFlags = ENABLE | HARD_CAP; i.CpuRate = NoLimit; SetInformationJobObject(s.Job, JobObjectCpuRateControlInformation, ref i, Marshal.SizeOf(typeof(JOBOBJECT_CPU_RATE_CONTROL_INFORMATION))); CloseHandle(s.Job); }
            map.Clear();
            if (totalJob != IntPtr.Zero)
            {
                JOBOBJECT_CPU_RATE_CONTROL_INFORMATION i2 = new JOBOBJECT_CPU_RATE_CONTROL_INFORMATION(); i2.ControlFlags = ENABLE | HARD_CAP; i2.CpuRate = NoLimit;
                SetInformationJobObject(totalJob, JobObjectCpuRateControlInformation, ref i2, Marshal.SizeOf(typeof(JOBOBJECT_CPU_RATE_CONTROL_INFORMATION)));
                CloseHandle(totalJob);
                totalJob = IntPtr.Zero;
            }
            inTotal.Clear();
            totalApplied = uint.MaxValue;
        }
    }
}
