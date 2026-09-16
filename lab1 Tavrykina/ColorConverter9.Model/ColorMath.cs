using System;

namespace ColorConverter9.Core
{
 
    public enum Illuminant
    {
        D65, 
        D50,   
        E     
    }

  
    public enum GamutStrategy
    {
        Clip,  
        Scale  
    }

    
    public static class ColorMath
    {
        private static readonly (double x, double y) PrimaryR = (0.6400, 0.3300);
        private static readonly (double x, double y) PrimaryG = (0.3000, 0.6000);
        private static readonly (double x, double y) PrimaryB = (0.1500, 0.0600);

        
        
        public static (double X, double Y, double Z) GetWhitePoint(Illuminant illum)
        {
            switch (illum)
            {
                case Illuminant.D50:
                    return (96.422, 100.000, 82.521);
                case Illuminant.E:
                    return (100.000, 100.000, 100.000);
                default: 
                    return (95.047, 100.000, 108.883);
            }
        }







 public static double[,] BuildRgbToXyzMatrix(Illuminant illum)
        {
            double[] xr = ChromaticityToXyzUnitY(PrimaryR);
            double[] xg = ChromaticityToXyzUnitY(PrimaryG);
            double[] xb = ChromaticityToXyzUnitY(PrimaryB);

            double[,] m = new double[3, 3]
            {
                { xr[0], xg[0], xb[0] },
                { xr[1], xg[1], xb[1] },
                { xr[2], xg[2], xb[2] }
            };

            var wp = GetWhitePoint(illum);
            double[] w = { wp.X / 100.0, wp.Y / 100.0, wp.Z / 100.0 }; 

            double[] s = SolveLinearSystem3x3(m, w);

            double[,] result = new double[3, 3];
            for (int row = 0; row < 3; row++)
                for (int col = 0; col < 3; col++)
                    result[row, col] = m[row, col] * s[col];

            return result;
        }

        private static double[] ChromaticityToXyzUnitY((double x, double y) c)
        {
            double X = c.x / c.y;
            double Y = 1.0;
            double Z = (1 - c.x - c.y) / c.y;
            return new[] { X, Y, Z };
        }

        
        
        private static double Determinant3x3(double[,] m)
        {
            return m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
                 - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
                 + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
        }

        private static double[] SolveLinearSystem3x3(double[,] m, double[] rhs)
        {
            double det = Determinant3x3(m);
            double[] result = new double[3];
            for (int col = 0; col < 3; col++)
            {
                double[,] mc = (double[,])m.Clone();
                for (int row = 0; row < 3; row++) mc[row, col] = rhs[row];
                result[col] = Determinant3x3(mc) / det;
            }
            return result;
        }

        public static double[,] Invert3x3(double[,] m)
        {
            double det = Determinant3x3(m);
            double[,] inv = new double[3, 3];

            inv[0, 0] = (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1]) / det;
            inv[0, 1] = (m[0, 2] * m[2, 1] - m[0, 1] * m[2, 2]) / det;
            inv[0, 2] = (m[0, 1] * m[1, 2] - m[0, 2] * m[1, 1]) / det;

            inv[1, 0] = (m[1, 2] * m[2, 0] - m[1, 0] * m[2, 2]) / det;
            inv[1, 1] = (m[0, 0] * m[2, 2] - m[0, 2] * m[2, 0]) / det;
            inv[1, 2] = (m[0, 2] * m[1, 0] - m[0, 0] * m[1, 2]) / det;

            inv[2, 0] = (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]) / det;
            inv[2, 1] = (m[0, 1] * m[2, 0] - m[0, 0] * m[2, 1]) / det;
            inv[2, 2] = (m[0, 0] * m[1, 1] - m[0, 1] * m[1, 0]) / det;

            return inv;
        }

        private static (double x, double y, double z) MultiplyMatrixVector(double[,] m, double x, double y, double z)
        {
            return (
                m[0, 0] * x + m[0, 1] * y + m[0, 2] * z,
                m[1, 0] * x + m[1, 1] * y + m[1, 2] * z,
                m[2, 0] * x + m[2, 1] * y + m[2, 2] * z
            );
        }

        public static double Clamp(double v, double min, double max) => Math.Min(Math.Max(v, min), max);

      
        
        
        
        public static double SrgbToLinear(double c)
        {
            return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        public static double LinearToSrgb(double c)
        {
            return c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055;
        }

        // HSV <=> RGB
        public static (double r, double g, double b) HsvToRgb(double h, double s, double v)
        {
            h = ((h % 360) + 360) % 360;
            s = Clamp(s, 0, 1);
            v = Clamp(v, 0, 1);

            double c = v * s;
            double hp = h / 60.0;
            double x = c * (1 - Math.Abs(hp % 2 - 1));
            double m = v - c;

            double r1, g1, b1;
            if (hp < 1) { r1 = c; g1 = x; b1 = 0; }
            else if (hp < 2) { r1 = x; g1 = c; b1 = 0; }
            else if (hp < 3) { r1 = 0; g1 = c; b1 = x; }
            else if (hp < 4) { r1 = 0; g1 = x; b1 = c; }
            else if (hp < 5) { r1 = x; g1 = 0; b1 = c; }
            else { r1 = c; g1 = 0; b1 = x; }

            return (r1 + m, g1 + m, b1 + m);
        }

        public static (double h, double s, double v) RgbToHsv(double r, double g, double b)
        {
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            double h;
            if (delta < 1e-9) h = 0;
            else if (max == r) h = 60 * (((g - b) / delta) % 6);
            else if (max == g) h = 60 * ((b - r) / delta + 2);
            else h = 60 * ((r - g) / delta + 4);
            if (h < 0) h += 360;

            double s = max < 1e-9 ? 0 : delta / max;
            double v = max;

            return (h, s, v);
        }


        // RGB(0..255) <=> XYZ(0..100), 
        public static (double x, double y, double z) RgbToXyz(int r, int g, int b, Illuminant illum)
        {
            double rLin = SrgbToLinear(r / 255.0);
            double gLin = SrgbToLinear(g / 255.0);
            double bLin = SrgbToLinear(b / 255.0);

            var mtx = BuildRgbToXyzMatrix(illum);
            var xyz = MultiplyMatrixVector(mtx, rLin, gLin, bLin);
            return (xyz.x * 100, xyz.y * 100, xyz.z * 100);
        }

        public static (int r, int g, int b, bool clipped) XyzToRgb(double x, double y, double z, Illuminant illum, GamutStrategy strategy)
        {
            var mtx = BuildRgbToXyzMatrix(illum);
            var inv = Invert3x3(mtx);
            var lin = MultiplyMatrixVector(inv, x / 100.0, y / 100.0, z / 100.0);

            double rLin = lin.x, gLin = lin.y, bLin = lin.z;
            bool clipped = false;
            double r, g, b;

            if (strategy == GamutStrategy.Scale)
            {
                double maxVal = Math.Max(1.0, Math.Max(rLin, Math.Max(gLin, bLin)));
                double minVal = Math.Min(0.0, Math.Min(rLin, Math.Min(gLin, bLin)));
                if (maxVal > 1.0 || minVal < 0.0)
                {
                    clipped = true;
                    double range = maxVal - minVal;
                    r = range > 0 ? (rLin - minVal) / range : 0.5;
                    g = range > 0 ? (gLin - minVal) / range : 0.5;
                    b = range > 0 ? (bLin - minVal) / range : 0.5;
                }
                else
                {
                    r = rLin; g = gLin; b = bLin;
                }
            }
            else
            {
                if (rLin < 0 || rLin > 1 || gLin < 0 || gLin > 1 || bLin < 0 || bLin > 1) clipped = true;
                r = Clamp(rLin, 0, 1);
                g = Clamp(gLin, 0, 1);
                b = Clamp(bLin, 0, 1);
            }

            int ri = (int)Math.Round(Clamp(LinearToSrgb(r) * 255, 0, 255));
            int gi = (int)Math.Round(Clamp(LinearToSrgb(g) * 255, 0, 255));
            int bi = (int)Math.Round(Clamp(LinearToSrgb(b) * 255, 0, 255));

            return (ri, gi, bi, clipped);
        }

        // XYZ <=> LAB
        
        public static (double l, double a, double b) XyzToLab(double x, double y, double z, Illuminant illum)
        {
            var wp = GetWhitePoint(illum);

            double fx = LabF(x / wp.X);
            double fy = LabF(y / wp.Y);
            double fz = LabF(z / wp.Z);

            double L = 116 * fy - 16;
            double A = 500 * (fx - fy);
            double B = 200 * (fy - fz);

            return (Clamp(L, 0, 100), Clamp(A, -128, 128), Clamp(B, -128, 128));
        }

        public static (double x, double y, double z) LabToXyz(double l, double a, double b, Illuminant illum)
        {
            var wp = GetWhitePoint(illum);

            double fy = (l + 16) / 116.0;
            double fx = fy + a / 500.0;
            double fz = fy - b / 200.0;

            double x = LabFInv(fx) * wp.X;
            double y = LabFInv(fy) * wp.Y;
            double z = LabFInv(fz) * wp.Z;

            return (Clamp(x, 0, 100), Clamp(y, 0, 100), Clamp(z, 0, 100));
        }

        private static double LabF(double t)
        {
            double eps = Math.Pow(6.0 / 29.0, 3);
            return t > eps ? Math.Pow(t, 1.0 / 3.0) : (1.0 / 3.0) * Math.Pow(29.0 / 6.0, 2) * t + 4.0 / 29.0;
        }

        private static double LabFInv(double t)
        {
            return t > 6.0 / 29.0 ? Math.Pow(t, 3) : 3 * Math.Pow(6.0 / 29.0, 2) * (t - 4.0 / 29.0);
        }

        // HSV <=> XYZ
        public static (double x, double y, double z) HsvToXyz(double h, double s, double v, Illuminant illum)
        {
            var rgb = HsvToRgb(h, s, v); 
            int r = (int)Math.Round(Clamp(rgb.r, 0, 1) * 255);
            int g = (int)Math.Round(Clamp(rgb.g, 0, 1) * 255);
            int bl = (int)Math.Round(Clamp(rgb.b, 0, 1) * 255);
            return RgbToXyz(r, g, bl, illum);
        }

        public static (double h, double s, double v, bool clipped) XyzToHsv(double x, double y, double z, Illuminant illum, GamutStrategy strategy)
        {
            var rgb = XyzToRgb(x, y, z, illum, strategy);
            var hsv = RgbToHsv(rgb.r / 255.0, rgb.g / 255.0, rgb.b / 255.0);
            return (hsv.h, hsv.s, hsv.v, rgb.clipped);
        }
    }
}
