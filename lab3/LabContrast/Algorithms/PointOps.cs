using LabContrast.Core;

namespace LabContrast.Algorithms;


public static class PointOps
{
    public static byte[] BuildLut(Func<int, double> f)
    {
        var lut = new byte[256];
        for (int i = 0; i < 256; i++) lut[i] = (byte)Math.Clamp((int)Math.Round(f(i)), 0, 255);
        return lut;
    }

    public static unsafe PixelBuffer ApplyLut(PixelBuffer src, byte[][] lut)
    {
        var dst = new PixelBuffer(src.Width, src.Height);
        int stride = src.Stride, w = src.Width;
        byte[] lb = lut[0], lg = lut[1], lr = lut[2];
        Parallel.For(0, src.Height, y =>
        {
            fixed (byte* ps = &src.Data[y * stride])
            fixed (byte* pd = &dst.Data[y * stride])
            {
                for (int x = 0; x < w; x++)
                {
                    int i = x * 3;
                    pd[i] = lb[ps[i]];
                    pd[i + 1] = lg[ps[i + 1]];
                    pd[i + 2] = lr[ps[i + 2]];
                }
            }
        });
        return dst;
    }

    static byte[][] Same(byte[] lut) => new[] { lut, lut, lut };

    public static PixelBuffer Negative(PixelBuffer s) => ApplyLut(s, Same(BuildLut(i => 255 - i)));


    public static PixelBuffer Linear(PixelBuffer s, double a, double b) => ApplyLut(s, Same(BuildLut(i => a * i + b)));


    public static PixelBuffer Gamma(PixelBuffer s, double gamma) =>
        ApplyLut(s, Same(BuildLut(i => 255.0 * Math.Pow(i / 255.0, gamma))));

 
    public static PixelBuffer Log(PixelBuffer s, double c) =>
        ApplyLut(s, Same(BuildLut(i => c * 255.0 / Math.Log(256.0) * Math.Log(1 + i))));


    public static PixelBuffer AutoStretch(PixelBuffer s, double lowCutPct, double highCutPct, out int lo, out int hi)
    {
        var luma = Histogram.Compute(s)[3];
        long total = 0; foreach (var v in luma) total += v;
        double lowTarget = total * lowCutPct / 100.0, highTarget = total * (1 - highCutPct / 100.0);
        long cum = 0; lo = 0; hi = 255; bool loSet = false, hiSet = false;
        for (int i = 0; i < 256; i++)
        {
            cum += luma[i];
            if (!loSet && cum > lowTarget) { lo = i; loSet = true; }
            if (!hiSet && cum >= highTarget) { hi = i; hiSet = true; }
        }
        if (hi <= lo) return ApplyLut(s, Same(BuildLut(i => i)));
        int l = lo, h = hi;
        return ApplyLut(s, Same(BuildLut(i => (i - l) * 255.0 / (h - l))));
    }
}
