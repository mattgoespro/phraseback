using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Phraseback.App.Windows;

/// <summary>Windows-only lifetime backstop. Normal shutdown still uses the protocol.</summary>
internal sealed class ChildJob : IDisposable
{
    private readonly SafeFileHandle handle;
    public ChildJob()
    {
        handle = CreateJobObject(IntPtr.Zero, null);
        if (handle.IsInvalid) throw new Win32Exception();
        var info = new ExtendedLimit { Basic = new BasicLimit { LimitFlags = 0x2000 } };
        if (!SetInformationJobObject(handle, 9, ref info, (uint)Marshal.SizeOf<ExtendedLimit>()))
        { handle.Dispose(); throw new Win32Exception(); }
    }
    public void Attach(int pid)
    {
        using var process = System.Diagnostics.Process.GetProcessById(pid);
        if (!AssignProcessToJobObject(handle, process.Handle)) throw new Win32Exception();
    }
    public void Dispose() => handle.Dispose();

    [StructLayout(LayoutKind.Sequential)] private struct BasicLimit
    { public long ProcessTime, JobTime; public uint LimitFlags; public nuint MinimumWorkingSet, MaximumWorkingSet; public uint ActiveProcessLimit; public nuint Affinity; public uint PriorityClass, SchedulingClass; }
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters
    { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimit
    { public BasicLimit Basic; public IoCounters Io; public nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern SafeFileHandle CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(SafeFileHandle job, int infoClass, ref ExtendedLimit info, uint length);
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);
}
