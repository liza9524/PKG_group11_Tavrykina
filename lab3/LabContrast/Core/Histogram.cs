namespace LabContrast.Core;

public static class Histogram
{
    public static int[][] Compute(PixelBuffer p)
    {
        var res = new int[4][];
        for (int k = 0; k < 4; k++) res[k] = new int[256];
        var d = p.Data; int stride = p.Stride, w = p.Width;

        Parallel.For(0, p.Height, () => new int[1024], (y, _, loc) =>
        {
            int o = y * stride;
            for (int x = 0; x < w; x++, o += 3)
            {
                byte b = d[o], g = d[o + 1], r = d[o + 2];
                loc[b]++; loc[256 + g]++; loc[512 + r]++;
                loc[768 + (int)(0.114 * b + 0.587 * g + 0.299 * r + 0.5)]++;
            }
            return loc;
        }, loc =>
        {
            lock (res)
                for (int k = 0; k < 4; k++)
                    for (int i = 0; i < 256; i++) res[k][i] += loc[k * 256 + i];
        });
        return res;
    }

    public static byte[] EqualizeLut(int[] hist)
    {
        long total = 0;
        foreach (var v in hist) total += v;
        var lut = new byte[256];
        long cdfMin = 0; bool found = false;
        foreach (var v in hist) { if (v > 0) { cdfMin = v; found = true; break; } }
        long denom = total - cdfMin;
        if (!found || denom <= 0)
        {
            for (int i = 0; i < 256; i++) lut[i] = (byte)i;
            return lut;
        }
        long cdf = 0;
        for (int i = 0; i < 256; i++)
        {
            cdf += hist[i];
            lut[i] = cdf < cdfMin ? (byte)0 : (byte)Math.Clamp((int)Math.Round((cdf - cdfMin) * 255.0 / denom), 0, 255);
        }
        return lut;
    }

    public static (double Mean, double Std, double Entropy, int Min, int Max) Stats(int[] hist)
    {
        double total = hist.Sum(v => (double)v);
        if (total <= 0) return (0, 0, 0, 0, 0);
        double mean = 0;
        for (int i = 0; i < 256; i++) mean += i * (double)hist[i];
        mean /= total;
        double var = 0, ent = 0; int min = 255, max = 0;
        for (int i = 0; i < 256; i++)
        {
            if (hist[i] == 0) continue;
            double p = hist[i] / total;
            var += hist[i] * (i - mean) * (i - mean);
            ent -= p * Math.Log2(p);
            if (i < min) min = i;
            if (i > max) max = i;
        }
        return (mean, Math.Sqrt(var / total), ent, min, max);
    }
}