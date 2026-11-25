using LabContrast.Core;

namespace LabContrast.Algorithms;


public static class Equalization
{

    public static PixelBuffer EqualizeRgb(PixelBuffer s)
    {
        var h = Histogram.Compute(s);
        return PointOps.ApplyLut(s, new[] { Histogram.EqualizeLut(h[0]), Histogram.EqualizeLut(h[1]), Histogram.EqualizeLut(h[2]) });
    }


    public static PixelBuffer EqualizeHsvV(PixelBuffer s) => ProcessV(s, (v, w, h) =>
    {
        var hist = new int[256];
        foreach (var b in v) hist[b]++;
        var lut = Histogram.EqualizeLut(hist);
        var r = new byte[v.Length];
        for (int i = 0; i < v.Length; i++) r[i] = lut[v[i]];
        return r;
    });

    public static PixelBuffer ClaheHsvV(PixelBuffer s, double clipLimit, int tiles) =>
        ProcessV(s, (v, w, h) => Clahe.Apply(v, w, h, tiles, tiles, clipLimit));

    static PixelBuffer ProcessV(PixelBuffer src, Func<byte[], int, int, byte[]> f)
    {
        int w = src.Width, h = src.Height, n = w * h;
        var H = new float[n]; var S = new float[n]; var V = new byte[n];
        var d = src.Data;
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int p = y * w + x, o = p * 3;
                ColorSpace.RgbToHsv(d[o + 2], d[o + 1], d[o], out float hh, out float ss, out float vv);
                H[p] = hh; S[p] = ss; V[p] = (byte)Math.Round(vv * 255f);
            }
        });

        byte[] nv = f(V, w, h);

        var dst = new PixelBuffer(w, h);
        var o2 = dst.Data;
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int p = y * w + x, o = p * 3;
                ColorSpace.HsvToRgb(H[p], S[p], nv[p] / 255f, out byte r, out byte g, out byte b);
                o2[o] = b; o2[o + 1] = g; o2[o + 2] = r;
            }
        });
        return dst;
    }
}
