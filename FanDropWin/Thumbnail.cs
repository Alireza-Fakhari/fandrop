using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FanDropWin
{
    /// Shell thumbnails (images, videos, PDFs, documents) via IShellItemImageFactory, with icon fallback.
    static class Thumbnail
    {
        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItemImageFactory { [PreserveSig] int GetImage(SIZE size, int flags, out IntPtr phbm); }
        [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        static extern void SHCreateItemFromParsingName(string path, IntPtr pbc, [MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IShellItemImageFactory item);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
        [DllImport("gdi32.dll")] static extern int GetObject(IntPtr h, int size, out BITMAP bm);
        [StructLayout(LayoutKind.Sequential)]
        struct BITMAP { public int bmType, bmWidth, bmHeight, bmWidthBytes; public ushort bmPlanes, bmBitsPixel; public IntPtr bmBits; }

        public static ImageSource For(string path, int px = 160)
        {
            try
            {
                SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out var f);
                if (f.GetImage(new SIZE { cx = px, cy = px }, 0, out var hbm) == 0)
                {
                    try
                    {
                        // The shell returns a 32bpp premultiplied DIB section; read its bits directly to keep transparency.
                        GetObject(hbm, Marshal.SizeOf<BITMAP>(), out var bm);
                        BitmapSource src;
                        if (bm.bmBitsPixel == 32 && bm.bmBits != IntPtr.Zero)
                            src = BitmapSource.Create(bm.bmWidth, bm.bmHeight, 96, 96, PixelFormats.Pbgra32, null, bm.bmBits, bm.bmWidthBytes * bm.bmHeight, bm.bmWidthBytes);
                        else
                            src = Imaging.CreateBitmapSourceFromHBitmap(hbm, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                        src.Freeze(); return src;
                    }
                    finally { DeleteObject(hbm); }
                }
            }
            catch (Exception ex) { Log.Write("thumbnail failed: " + ex.Message); }
            return null;
        }
    }
}
