using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AltHub
{
    // Mutes / unmutes the sound of single Roblox windows through the Windows audio mixer (the same per-app mute as in
    // the volume mixer). Works for any process id, so a window can be muted before it even has started playing sound.
    public static class AudioMute
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumeratorCom { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, [MarshalAs(UnmanagedType.Interface)] out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.Interface)] out IAudioSessionManager2 manager);
        }

        [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionManager2
        {
            [PreserveSig] int GetAudioSessionControl(IntPtr sessionGuid, int flags, IntPtr control);
            [PreserveSig] int GetSimpleAudioVolume(IntPtr sessionGuid, int flags, IntPtr volume);
            [PreserveSig] int GetSessionEnumerator([MarshalAs(UnmanagedType.Interface)] out IAudioSessionEnumerator enumerator);
        }

        [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionEnumerator
        {
            [PreserveSig] int GetCount(out int count);
            [PreserveSig] int GetSession(int index, [MarshalAs(UnmanagedType.Interface)] out IAudioSessionControl2 session);
        }

        // IAudioSessionControl2 inherits IAudioSessionControl: its 9 methods come first, in this order.
        [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioSessionControl2
        {
            [PreserveSig] int GetState(out int state);
            [PreserveSig] int GetDisplayName(IntPtr name);
            [PreserveSig] int SetDisplayName(IntPtr name, IntPtr ctx);
            [PreserveSig] int GetIconPath(IntPtr path);
            [PreserveSig] int SetIconPath(IntPtr path, IntPtr ctx);
            [PreserveSig] int GetGroupingParam(out Guid g);
            [PreserveSig] int SetGroupingParam(ref Guid g, IntPtr ctx);
            [PreserveSig] int RegisterAudioSessionNotification(IntPtr n);
            [PreserveSig] int UnregisterAudioSessionNotification(IntPtr n);
            [PreserveSig] int GetSessionIdentifier(IntPtr id);
            [PreserveSig] int GetSessionInstanceIdentifier(IntPtr id);
            [PreserveSig] int GetProcessId(out uint pid);
            [PreserveSig] int IsSystemSoundsSession();
            [PreserveSig] int SetDuckingPreference(bool optOut);
        }

        [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface ISimpleAudioVolume
        {
            [PreserveSig] int SetMasterVolume(float level, ref Guid ctx);
            [PreserveSig] int GetMasterVolume(out float level);
            [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
            [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
        }

        static readonly Guid IID_IAudioSessionManager2 = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");

        // Sets the mute state of every audio session of these processes. Returns how many sessions were changed.
        public static int Set(ICollection<int> pids, bool mute)
        {
            int changed = 0;
            if (pids == null || pids.Count == 0) return 0;
            IMMDeviceEnumerator en = null;
            try
            {
                en = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
                IMMDevice dev;
                if (en.GetDefaultAudioEndpoint(0, 1, out dev) != 0 || dev == null) return 0;      // eRender, eMultimedia
                Guid iid = IID_IAudioSessionManager2;
                IAudioSessionManager2 mgr;
                if (dev.Activate(ref iid, 23, IntPtr.Zero, out mgr) != 0 || mgr == null) return 0;   // CLSCTX_ALL
                IAudioSessionEnumerator se;
                if (mgr.GetSessionEnumerator(out se) != 0 || se == null) return 0;
                int count;
                se.GetCount(out count);
                Guid ctx = Guid.Empty;
                for (int i = 0; i < count; i++)
                {
                    IAudioSessionControl2 c;
                    if (se.GetSession(i, out c) != 0 || c == null) continue;
                    try
                    {
                        uint pid;
                        if (c.GetProcessId(out pid) != 0 || !pids.Contains((int)pid)) continue;
                        ISimpleAudioVolume v = c as ISimpleAudioVolume;
                        if (v == null) continue;
                        bool now;
                        if (v.GetMute(out now) == 0 && now == mute) continue;
                        if (v.SetMute(mute, ref ctx) == 0) changed++;
                    }
                    finally { Marshal.ReleaseComObject(c); }
                }
            }
            catch (Exception ex) { Log.Write("AudioMute: " + ex.Message); }
            finally { if (en != null) Marshal.ReleaseComObject(en); }
            return changed;
        }
    }
}
