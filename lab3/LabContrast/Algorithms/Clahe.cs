using LabContrast.Core;

namespace LabContrast.Algorithms;


public static class Clahe
{
    public static byte[] Apply(byte[] src, int w, int h, int tilesX, int tilesY, double clipLimit)
    {
        tilesX = Math.Max(1, Math.Min(tilesX, w));
        tilesY = Math.Max(1, Math.Min(tilesY, h));
        var luts = new byte[tilesX * tilesY][];


        Parallel.For(0, tilesX * tilesY, t =>
        {
            int tx = t % tilesX, ty = t / tilesX;
            int x0 = tx * w / tilesX, x1 = (tx + 1) * w / tilesX;
            int y0 = ty * h / tilesY, y1 = (ty + 1) * h / tilesY;
            var hist = new int[256];
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++) hist[src[y * w + x]]++;
            int area = (x1 - x0) * (y1 - y0);
            ClipHistogram(hist, area, clipLimit);
            var lut = new byte[256];
            long cdf = 0;
            for (int i = 0; i < 256; i++)
            {
                cdf += hist[i];
                lut[i] = (byte)Math.Min(255, (int)Math.Round(cdf * 255.0 / area));
            }
            luts[t] = lut;
        });

        double tw = (double)w / tilesX, th = (double)h / tilesY;
        var xa = new int[w]; var xb = new int[w]; var wxs = new double[w];
        for (int x = 0; x < w; x++)
        {
            double fx = (x + 0.5) / tw - 0.5; int x0 = (int)Math.Floor(fx);
            wxs[x] = fx - x0;
            xa[x] = PixelBuffer.Pad(x0, tilesX, EdgeMode.Clamp);
            xb[x] = PixelBuffer.Pad(x0 + 1, tilesX, EdgeMode.Clamp);
        }
        var dst = new byte[w * h];
        Parallel.For(0, h, y =>
        {
            double fy = (y + 0.5) / th - 0.5; int y0 = (int)Math.Floor(fy); double wy = fy - y0;
            int ya = PixelBuffer.Pad(y0, tilesY, EdgeMode.Clamp) * tilesX;
            int yb = PixelBuffer.Pad(y0 + 1, tilesY, EdgeMode.Clamp) * tilesX;
            for (int x = 0; x < w; x++)
            {
                int v = src[y * w + x];
                double wx = wxs[x];
                double top = luts[ya + xa[x]][v] * (1 - wx) + luts[ya + xb[x]][v] * wx;
                double bot = luts[yb + xa[x]][v] * (1 - wx) + luts[yb + xb[x]][v] * wx;
                dst[y * w + x] = (byte)Math.Clamp((int)Math.Round(top * (1 - wy) + bot * wy), 0, 255);
            }
        });
        return dst;
    }

    static void ClipHistogram(int[] hist, int area, double clipLimit)
    {
        int clip = Math.Max(1, (int)(clipLimit * area / 256.0));
        int excess = 0;
        for (int i = 0; i < 256; i++)
            if (hist[i] > clip) { excess += hist[i] - clip; hist[i] = clip; }
        int add = excess / 256, rem = excess % 256;
        for (int i = 0; i < 256; i++) hist[i] += add;
        if (rem > 0)
        {
            int step = Math.Max(256 / rem, 1);
            for (int i = 0; i < 256 && rem > 0; i += step) { hist[i]++; rem--; }
        }
    }
}
