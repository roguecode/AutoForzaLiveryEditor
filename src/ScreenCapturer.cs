using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ForzaVinylPainting
{
    // https://www.c-sharpcorner.com/article/screen-capture-and-save-as-an-image/
    class User32
    {
        [DllImport("User32.dll")]
        public static extern int GetDesktopWindow();
        [DllImport("User32.dll")]
        public static extern int GetWindowDC(int hWnd);
        [DllImport("User32.dll")]
        public static extern int ReleaseDC(int hWnd, int hDC);
    }

    public class ScreenCapturer
    {
        [DllImport("GDI32.dll")]
        public static extern bool BitBlt(int hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, int hdcSrc, int nXSrc, int nYSrc, int dwRop);
        
        [DllImport("GDI32.dll")]
        public static extern int CreateCompatibleBitmap(int hdc, int nWidth, int nHeight);
        
        [DllImport("GDI32.dll")]
        public static extern int CreateCompatibleDC(int hdc);
       
        [DllImport("GDI32.dll")]
        public static extern bool DeleteDC(int hdc);
        
        [DllImport("GDI32.dll")]
        public static extern bool DeleteObject(int hObject);
        
        [DllImport("GDI32.dll")]
        public static extern int GetDeviceCaps(int hdc, int nIndex);
        
        [DllImport("GDI32.dll")]
        public static extern int SelectObject(int hdc, int hgdiobj);

        public Bitmap CaptureScreen()
        {
            int hdcSrc = User32.GetWindowDC(User32.GetDesktopWindow()),
            hdcDest = CreateCompatibleDC(hdcSrc),
            hBitmap = CreateCompatibleBitmap(hdcSrc,
            GetDeviceCaps(hdcSrc, 8), GetDeviceCaps(hdcSrc, 10));
            SelectObject(hdcDest, hBitmap);
            BitBlt(hdcDest, 0, 0, GetDeviceCaps(hdcSrc, 8),
            GetDeviceCaps(hdcSrc, 10), hdcSrc, 0, 0, 0x00CC0020);

            var image = new Bitmap(Image.FromHbitmap(new IntPtr(hBitmap)), Image.FromHbitmap(new IntPtr(hBitmap)).Width, Image.FromHbitmap(new IntPtr(hBitmap)).Height);

            Cleanup(hBitmap, hdcSrc, hdcDest);
            return image;
        }

        private void Cleanup(int hBitmap, int hdcSrc, int hdcDest)
        {
            User32.ReleaseDC(User32.GetDesktopWindow(), hdcSrc);
            DeleteDC(hdcDest);
            DeleteObject(hBitmap);
        }
    }
}
