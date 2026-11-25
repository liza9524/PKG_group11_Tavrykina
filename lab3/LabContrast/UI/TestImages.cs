using System.Drawing.Imaging;
using LabContrast.Core;

namespace LabContrast.UI;


public static class TestImages
{
    static double Scene(double u, double v)
    {
        double s = 0.35 + 0.25 * u + 0.1 * Math.Sin(12 * v);
        double dx = u - 0.3, dy = v - 0.45; if (dx * dx + dy * dy < 0.03) s = 0.9;
        dx = u - 0.72; dy = v - 0.6; if (dx * dx + dy * dy < 0.02) s = 0.1;
        if (u > 0.45 && u < 0.6 && v > 0.15 && v < 0.4) s = 0.65 + 0.2 * Math.Sin(80 * u);
        return Math.Clamp(s, 0, 1);
    }

    static PixelBuffer Make(int w, int h, double[] lo, double[] hi, double noise, Func<double, double, double>[] ch, Random rnd)
    {
        var p = new PixelBuffer(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double u = (double)x / w, v = (double)y / h;
                for (int c = 0; c < 3; c++)
                {
                    double t = Math.Clamp(ch[c](u, v), 0, 1);
                    double val = lo[c] + t * (hi[c] - lo[c]) + (rnd.NextDouble() - 0.5) * 2 * noise;
                    p.Data[(y * w + x) * 3 + c] = (byte)Math.Clamp((int)Math.Round(val), 0, 255);
                }
            }
        return p;
    }

    public static string Generate(string dir)
    {
        Directory.CreateDirectory(dir);
        var rnd = new Random(42);
        Func<double, double, double> sc = Scene;
        var gray = new[] { sc, sc, sc };
        int W = 640, H = 480;

        Save(dir, "01_low_contrast_gray.png", Make(W, H, new double[] { 110, 110, 110 }, new double[] { 150, 150, 150 }, 0, gray, rnd));
        Save(dir, "02_dark_underexposed.png", Make(W, H, new double[] { 0, 0, 0 }, new double[] { 55, 55, 55 }, 0, gray, rnd));
        Save(dir, "03_bright_overexposed.png", Make(W, H, new double[] { 200, 200, 200 }, new double[] { 255, 255, 255 }, 0, gray, rnd));
        Save(dir, "04_low_contrast_color_cast.png", Make(W, H,
            new double[] { 50, 70, 90 }, new double[] { 100, 120, 170 }, 0,
            new Func<double, double, double>[] { (u, v) => 1 - Scene(u, v), (u, v) => 0.5 * Scene(u, v) + 0.4 * u, Scene }, rnd));
        Save(dir, "05_noisy_low_contrast.png", Make(W, H, new double[] { 90, 90, 90 }, new double[] { 160, 160, 160 }, 12, gray, rnd));
        Func<double, double, double> uneven = (u, v) => 0.7 * u + 0.3 * Scene(u, v);
        Save(dir, "06_uneven_illumination.png", Make(W, H, new double[] { 20, 20, 20 }, new double[] { 230, 230, 230 }, 0,
            new[] { uneven, uneven, uneven }, rnd));
        return dir;
    }

    static void Save(string dir, string name, PixelBuffer p)
    {
        using var bmp = p.ToBitmap();
        bmp.Save(Path.Combine(dir, name), ImageFormat.Png);
    }
}
