using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace PGameLib.Services;

/// <summary>Resolved details of a Windows .lnk shortcut.</summary>
public sealed record ShortcutTarget(
    string TargetPath,
    string Arguments,
    string WorkingDirectory,
    string IconLocation,
    int IconIndex);

/// <summary>
/// Resolves .lnk shortcuts to their target executable using the Windows Shell
/// IShellLink COM interface. No third-party library required.
/// </summary>
public sealed class ShortcutResolver
{
    public bool IsShortcut(string path)
        => !string.IsNullOrWhiteSpace(path) &&
           Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads the target info from a .lnk file. Returns null when the path is not a shortcut
    /// or cannot be read.
    /// </summary>
    public ShortcutTarget? Resolve(string lnkPath)
    {
        if (!IsShortcut(lnkPath) || !File.Exists(lnkPath))
            return null;

        ShellLink? link = null;
        try
        {
            link = (ShellLink)new ShellLinkCoClass();
            var persist = (IPersistFile)link;
            persist.Load(lnkPath, 0 /* STGM_READ */);

            var sb = new StringBuilder(260);
            var data = new WIN32_FIND_DATAW();

            link.GetPath(sb, sb.Capacity, ref data, 0);
            var target = sb.ToString();

            sb.Clear();
            link.GetArguments(sb, sb.Capacity);
            var args = sb.ToString();

            sb.Clear();
            link.GetWorkingDirectory(sb, sb.Capacity);
            var workDir = sb.ToString();

            sb.Clear();
            link.GetIconLocation(sb, sb.Capacity, out var iconIndex);
            var iconLocation = sb.ToString();

            return new ShortcutTarget(target, args, workDir, iconLocation, iconIndex);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (link is not null)
                Marshal.FinalReleaseComObject(link);
        }
    }

    // ---- COM interop declarations for IShellLink ----

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLinkCoClass { }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface ShellLink
    {
        void GetPath([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch,
                     ref WIN32_FIND_DATAW pfd, int fFlags);
        void GetIDList(out IntPtr ppidl);
        void SetIDList(IntPtr pidl);
        void GetDescription([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch,
                             out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string? pszFileName,
                  [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATAW
    {
        public uint dwFileAttributes;
        public long ftCreationTime;
        public long ftLastAccessTime;
        public long ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }
}
