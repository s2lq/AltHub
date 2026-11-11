using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace AltHub
{
    // Keeps every Roblox window's memory (working set) around 250 MB by giving each process a hard working-set limit.
    // In "Auto" mode a window that struggles with the limit (constant page faults = stutter) is moved up to 500 MB, and
    // if it still struggles the limit is taken off that window. The window you are playing can be left unlimited.
    //
    // This limits how much of the process lives in physical RAM (what Task Manager shows as its memory). Pages that do
    // not fit are kept by Windows in its standby list / page file and are brought back when needed, so the game keeps
    // working, but a window that really needs more memory will stutter.
    public static class RamCap
    {
        class St
        {
            public int CapMb;               // limit that is wanted for this window (0 = none)
            public int AppliedMb = -1;      // limit that is really set right now (-1 = nothing set yet)
            public uint LastFaults;
            public DateTime LastT = DateTime.MinValue;
            public int Thrash;
            public DateTime Seen = DateTime.Now;
            public bool Loaded;
        }

        const uint ACCESS = 0x0510;                 // PROCESS_QUERY_INFORMATION | PROCESS_VM_READ | PROCESS_SET_QUOTA
        const uint HARDWS_MAX_ENABLE = 0x4, HARDWS_MIN_DISABLE = 0x2, HARDWS_MAX_DISABLE = 0x8;
        const int GraceSec = 75;                    // let a new window finish loading before it is limited
        const double ThrashFaultsPerSec = 2500;     // more soft page faults than this per second = the limit is too tight
        const int ThrashTicks = 4;                  // ... for this many checks in a row (a check is ~5 s)

        static readonly Dictionary<int, St> map = new Dictionary<int, St>();

        [StructLayout(LayoutKind.Sequential)]
        struct PMC
        {
            public uint cb;
            public uint PageFaultCount;
            public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage,
                QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage;
        }

        [DllImport("psapi.dll")] static extern bool GetProcessMemoryInfo(IntPtr h, out PMC counters, uint cb);
        [DllImport("kernel32.dll")] static extern bool SetProcessWorkingSetSizeEx(IntPtr h, UIntPtr min, UIntPtr max, uint flags);

        public static bool Enabled { get { return Store.Settings.RamCapMode != "Off"; } }

        static int StartMb()
        {
            string m = Store.Settings.RamCapMode;
            return m == "Fixed500" ? 500 : 250;
        }

        static bool Auto { get { return Store.Settings.RamCapMode == "Auto250"; } }

        // Called about every 5 seconds.
        public static void Tick(Action<string> log)
        {
            if (!Enabled) { if (map.Count > 0) ReleaseAll(); return; }
            List<int> pids = Launcher.RobloxPids();
            foreach (int dead in map.Keys.Where(k => !pids.Contains(k)).ToList()) map.Remove(dead);
            if (pids.Count == 0) return;
            int active = Store.Settings.RamCapSkipActive ? MaxAlts.ActiveRobloxPid() : 0;

            foreach (int pid in pids)
            {
                St s;
                if (!map.TryGetValue(pid, out s)) { s = new St(); s.CapMb = StartMb(); map[pid] = s; }
                if (!s.Loaded)
                {
                    if ((DateTime.Now - s.Seen).TotalSeconds < GraceSec) continue;
                    s.Loaded = true;
                }

                IntPtr h = Native.OpenProcess(ACCESS, false, pid);
                if (h == IntPtr.Zero) continue;
                try
                {
                    PMC c;
                    if (GetProcessMemoryInfo(h, out c, (uint)Marshal.SizeOf(typeof(PMC))))
                    {
                        DateTime now = DateTime.Now;
                        if (s.LastT != DateTime.MinValue && s.AppliedMb > 0)
                        {
                            double sec = Math.Max(1, (now - s.LastT).TotalSeconds);
                            double fps = (uint)(c.PageFaultCount - s.LastFaults) / sec;
                            if (Auto && fps > ThrashFaultsPerSec) s.Thrash++; else s.Thrash = 0;
                            if (s.Thrash >= ThrashTicks)
                            {
                                s.Thrash = 0;
                                if (s.CapMb < 500) { s.CapMb = 500; log("RAM limit: a Roblox window (PID " + pid + ") struggles with 250 MB - moved to 500 MB."); }
                                else if (s.CapMb == 500) { s.CapMb = 0; log("RAM limit: a Roblox window (PID " + pid + ") needs more than 500 MB - its limit was removed."); }
                            }
                        }
                        s.LastFaults = c.PageFaultCount;
                        s.LastT = now;
                    }

                    int want = (pid == active) ? 0 : s.CapMb;       // the window you are playing is left alone
                    if (want != s.AppliedMb) Apply(h, want);
                    s.AppliedMb = want;
                }
                finally { Native.CloseHandle(h); }
            }
        }

        static void Apply(IntPtr h, int mb)
        {
            try
            {
                if (mb > 0) SetProcessWorkingSetSizeEx(h, (UIntPtr)(1UL << 20), (UIntPtr)((ulong)mb << 20), HARDWS_MAX_ENABLE | HARDWS_MIN_DISABLE);
                else SetProcessWorkingSetSizeEx(h, (UIntPtr)(1UL << 20), (UIntPtr)(4UL << 30), HARDWS_MAX_DISABLE | HARDWS_MIN_DISABLE);
            }
            catch (Exception) { }
        }

        // Takes the limit off every window (mode switched off, or the app is closing).
        public static void ReleaseAll()
        {
            foreach (int pid in map.Keys.ToList())
            {
                IntPtr h = Native.OpenProcess(ACCESS, false, pid);
                if (h == IntPtr.Zero) continue;
                try { Apply(h, 0); } finally { Native.CloseHandle(h); }
            }
            map.Clear();
        }

        // Called when the mode changes: forget the learned limits so every window starts again from the new setting.
        public static void Reset()
        {
            ReleaseAll();
        }
    }
}
