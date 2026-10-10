using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Uttu.Services;

/// <summary>
/// Manages a Windows Job Object configured with JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE.
/// When the Uttu parent process terminates (gracefully or forcefully), Windows automatically kills all assigned child processes.
/// </summary>
public static class JobObjectManager
{
    private static IntPtr _jobHandle = IntPtr.Zero;
    private static readonly object _lock = new();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string? lpName);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobInformation, uint cbJobInformationLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    [StructLayout(LayoutKind.Sequential)]
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryLimit;
        public UIntPtr PeakJobMemoryLimit;
    }

    public static void Initialize()
    {
        lock (_lock)
        {
            if (_jobHandle != IntPtr.Zero)
                return;

            try
            {
                _jobHandle = CreateJobObject(IntPtr.Zero, null);
                if (_jobHandle == IntPtr.Zero)
                {
                    Debug.WriteLine($"[JobObjectManager] Failed to create Job Object. Win32 error: {Marshal.GetLastWin32Error()}");
                    return;
                }

                var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                info.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;

                int length = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                IntPtr extendedInfoPtr = Marshal.AllocHGlobal(length);
                try
                {
                    Marshal.StructureToPtr(info, extendedInfoPtr, false);
                    if (!SetInformationJobObject(_jobHandle, JobObjectExtendedLimitInformation, extendedInfoPtr, (uint)length))
                    {
                        Debug.WriteLine($"[JobObjectManager] Failed to configure Job Object. Win32 error: {Marshal.GetLastWin32Error()}");
                    }
                    else
                    {
                        Debug.WriteLine("[JobObjectManager] Job Object successfully configured with KILL_ON_JOB_CLOSE.");
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(extendedInfoPtr);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[JobObjectManager] Exception initializing Job Object: {ex.Message}");
            }
        }
    }

    public static bool AssignProcess(Process process)
    {
        lock (_lock)
        {
            if (_jobHandle == IntPtr.Zero)
            {
                Initialize();
            }

            if (_jobHandle == IntPtr.Zero || process == null || process.HasExited)
                return false;

            try
            {
                bool result = AssignProcessToJobObject(_jobHandle, process.Handle);
                if (!result)
                {
                    Debug.WriteLine($"[JobObjectManager] Failed to assign process PID={process.Id}. Win32 error: {Marshal.GetLastWin32Error()}");
                }
                else
                {
                    Debug.WriteLine($"[JobObjectManager] Process PID={process.Id} successfully assigned to Job Object.");
                }
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[JobObjectManager] Exception assigning process: {ex.Message}");
                return false;
            }
        }
    }

    public static void Dispose()
    {
        lock (_lock)
        {
            if (_jobHandle != IntPtr.Zero)
            {
                CloseHandle(_jobHandle);
                _jobHandle = IntPtr.Zero;
            }
        }
    }
}
