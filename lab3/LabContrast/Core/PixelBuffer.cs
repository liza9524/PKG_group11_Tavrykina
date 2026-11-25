using System.Drawing;
using System.Drawing.Imaging;

namespace LabContrast.Core;

public enum EdgeMode { Clamp, Reflect }

public sealed class PixelBuffer
{
    public const int Channels = 3;
    public int Width { get; }
    public int Height { get; }
    public byte[] Data { get; }
    public int Stride => Width * Channels;

    public PixelBuffer(int width, int height)
    {
        Width = width;
        Height = height;
        Data = new byte[width * height * Channels];
    }

    public static int Pad(int i, int n, EdgeMode mode)
    {
        if (i >= 0 && i < n) return i;
        if (mode == EdgeMode.Clamp) return i < 0 ? 0 : n - 1;
        if (n == 1) return 0;
        int period = 2 * (n - 1);
        i %= period;
        if (i < 0) i += period;
        return i < n ? i : period - i;
    }

    public byte GetPadded(int x, int y, int c, EdgeMode mode) =>
        Data[(Pad(y, Height, mode) * Width + Pad(x, Width, mode)) * Channels + c];

    public static unsafe PixelBuffer FromBitmap(Bitmap bmp)
    {
        int w = bmp.Width, h = bmp.Height;
        var buf = new PixelBuffer(w, h);
        var bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            byte* s0 = (byte*)bd.Scan0;
            int sStride = bd.Stride;
            fixed (byte* dst = buf.Data)
            {
                for (int y = 0; y < h; y++)
                {
                    byte* s = s0 + (long)y * sStride;
                    byte* d = dst + (long)y * buf.Stride;
                    int copy = Math.Min(sStride, buf.Stride);
                    Buffer.MemoryCopy(s, d, buf.Stride, copy);
                }
            }
        }
        finally { bmp.UnlockBits(bd); }
        return buf;
    }

    public unsafe Bitmap ToBitmap()
    {
        var bmp = new Bitmap(Width, Height, PixelFormat.Format24bppRgb);
        var bd = bmp.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            fixed (byte* src = Data)
            {
                for (int y = 0; y < Height; y++)
                {
                    byte* s = src + (long)y * Stride;
                    byte* d = (byte*)bd.Scan0 + (long)y * bd.Stride;
                    Buffer.MemoryCopy(s, d, bd.Stride, Stride);
                }
            }
        }
        finally { bmp.UnlockBits(bd); }
        return bmp;
    }
}