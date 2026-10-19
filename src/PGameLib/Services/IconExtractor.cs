using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using PGameLib.Helpers;

namespace PGameLib.Services;

/// <summary>
/// Extracts an application icon from an .exe, .lnk, .bat, .cmd or launcher using Windows Shell
/// APIs, then caches it as a PNG under %AppData%\PGameLib\icons. Everything is done with
/// WPF + Win32 interop — no System.Drawing dependency, so no extra NuGet package is needed.
///
/// It tries for a crisp 256px "jumbo" icon first (system image list), then falls back to the
/// standard 32px large icon. If nothing can be extracted it returns null and the UI shows a
/// generic placeholder.
/// </summary>
public sealed class IconExtractor
{
    public Task<string?> ExtractAndCacheAsync(string launchPath)
        => Task.Run(() => ExtractAndCache(launchPath));

    public string? ExtractAndCache(string launchPath)
    {
        if (string.IsNullOrWhiteSpace(launchPath) || !File.Exists(launchPath))
            return null;

        try
        {
            AppPaths.EnsureFolders();

            var source = GetIconBitmap(launchPath);
            if (source is null)
                return null;

            var outPath = Path.Combine(AppPaths.IconCacheFolder, HashKey(launchPath) + ".png");

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var fs = File.Create(outPath);
            encoder.Save(fs);

            return outPath;
        }
        catch
        {
            return null;
        }
    }

    private static string HashKey(string path)
    {
        var bytes = SHA1.HashData(Encoding.UTF8.GetBytes(PathNormalizer.Normalize(path)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static BitmapSource? GetIconBitmap(string path)
    {
        // First, resolve a jumbo icon through the system image list for a sharp large result.
        var jumbo = TryGetJumboIcon(path);
        if (jumbo is not null)
            return jumbo;

        // Fallback: a standard large (32px) icon straight from SHGetFileInfo.
        return TryGetLargeIcon(path);
    }

    private static BitmapSource? TryGetJumboIcon(string path)
    {
        var shfi = new SHFILEINFO();

        var res = SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, ref shfi, (uint)Marshal.SizeOf<SHFILEINFO>(),
            SHGFI_SYSICONINDEX);
        if (res == IntPtr.Zero)
            return null;

        var iconIndex = shfi.iIcon;

        if (SHGetImageList(SHIL_JUMBO, ref IID_IImageList, out var imageList) != 0 || imageList is null)
            return null;

        IntPtr hIcon = IntPtr.Zero;
        try
        {
            imageList.GetIcon(iconIndex, ILD_TRANSPARENT, ref hIcon);
            if (hIcon == IntPtr.Zero)
                return null;

            var bmp = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
        finally
        {
            if (hIcon != IntPtr.Zero)
                DestroyIcon(hIcon);
            Marshal.FinalReleaseComObject(imageList);
        }
    }

    private static BitmapSource? TryGetLargeIcon(string path)
    {
        var shfi = new SHFILEINFO();
        var res = SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, ref shfi,
            (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);
        if (res == IntPtr.Zero || shfi.hIcon == IntPtr.Zero)
            return null;

        try
        {
            var bmp = Imaging.CreateBitmapSourceFromHIcon(
                shfi.hIcon,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
        finally
        {
            DestroyIcon(shfi.hIcon);
        }
    }

    // ---- Win32 interop ----

    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    private const uint SHGFI_ICON = 0x000000100;
    private const uint SHGFI_LARGEICON = 0x000000000;
    private const uint SHGFI_SYSICONINDEX = 0x000004000;

    private const int SHIL_JUMBO = 0x4;   // 256x256
    private const int ILD_TRANSPARENT = 0x1;

    private static Guid IID_IImageList = new("46EB5926-582E-4017-9FDF-E8998DAA0950");

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes,
        ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    [DllImport("shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(int iImageList, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IImageList ppv);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [ComImport]
    [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IImageList
    {
        [PreserveSig] int Add(IntPtr hbmImage, IntPtr hbmMask, ref int pi);
        [PreserveSig] int ReplaceIcon(int i, IntPtr hicon, ref int pi);
        [PreserveSig] int SetOverlayImage(int iImage, int iOverlay);
        [PreserveSig] int Replace(int i, IntPtr hbmImage, IntPtr hbmMask);
        [PreserveSig] int AddMasked(IntPtr hbmImage, int crMask, ref int pi);
        [PreserveSig] int Draw(IntPtr pimldp);
        [PreserveSig] int Remove(int i);
        [PreserveSig] int GetIcon(int i, int flags, ref IntPtr picon);
        // Remaining methods are unused; the vtable order above is all we call.
    }
}
