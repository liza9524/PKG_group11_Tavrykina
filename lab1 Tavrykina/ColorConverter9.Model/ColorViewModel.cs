using System;

namespace ColorConverter9.Core
{
    public class ColorViewModel
    {
        public Illuminant Illuminant { get; private set; } = Illuminant.D65;
        public GamutStrategy GamutStrategy { get; private set; } = GamutStrategy.Clip;

        
        private double currentX, currentY, currentZ;
        private double currentH, currentS, currentV;
        private bool hsvAuthoritative = false;

        public event Action<ColorState> StateChanged;

        public ColorViewModel()
        {
            var xyz = ColorMath.RgbToXyz(255, 0, 0, Illuminant);
            currentX = xyz.x; currentY = xyz.y; currentZ = xyz.z;
            Publish();
        }

        public void SetIlluminant(Illuminant illum)
        {
            Illuminant = illum;
            hsvAuthoritative = false;
            Publish();
        }

        public void SetGamutStrategy(GamutStrategy strategy)
        {
            GamutStrategy = strategy;
            Publish();
        }

        public void SetFromHsv(double h, double s, double v)
        {
            currentH = h;
            currentS = s;
            currentV = v;
            hsvAuthoritative = true;

            var xyz = ColorMath.HsvToXyz(h, s / 100.0, v / 100.0, Illuminant);
            currentX = xyz.x; currentY = xyz.y; currentZ = xyz.z;
            Publish();
        }

        public void SetFromXyz(double x, double y, double z)
        {
            currentX = x; currentY = y; currentZ = z;
            hsvAuthoritative = false;
            Publish();
        }

        public void SetFromLab(double l, double a, double b)
        {
            var xyz = ColorMath.LabToXyz(l, a, b, Illuminant);
            currentX = xyz.x; currentY = xyz.y; currentZ = xyz.z;
            hsvAuthoritative = false;
            Publish();
        }

        public void SetFromRgbHex(int r, int g, int b)
        {
            var xyz = ColorMath.RgbToXyz(r, g, b, Illuminant);
            currentX = xyz.x; currentY = xyz.y; currentZ = xyz.z;
            hsvAuthoritative = false;
            Publish();
        }

        public (int r, int g, int b) PreviewRgbForXyz(double x, double y, double z)
        {
            var rgb = ColorMath.XyzToRgb(x, y, z, Illuminant, GamutStrategy);
            return (rgb.r, rgb.g, rgb.b);
        }

        public (int r, int g, int b) PreviewRgbForHsv(double h, double s, double v)
        {
            var xyz = ColorMath.HsvToXyz(h, s / 100.0, v / 100.0, Illuminant);
            return PreviewRgbForXyz(xyz.x, xyz.y, xyz.z);
        }

        public (int r, int g, int b) PreviewRgbForLab(double l, double a, double b)
        {
            var xyz = ColorMath.LabToXyz(l, a, b, Illuminant);
            return PreviewRgbForXyz(xyz.x, xyz.y, xyz.z);
        }

        private void Publish()
        {
            double h, s, v;
            bool hsvOutOfGamut = false;

            if (hsvAuthoritative)
            {
                h = currentH;
                s = currentS;
                v = currentV;
            }
            else
            {
                var hsvRaw = ColorMath.XyzToHsvRaw(currentX, currentY, currentZ, Illuminant);
                h = hsvRaw.h;
                s = hsvRaw.s * 100;
                v = hsvRaw.v * 100;
                hsvOutOfGamut = hsvRaw.outOfGamut;
                currentH = h; currentS = s; currentV = v;
            }

            var lab = ColorMath.XyzToLab(currentX, currentY, currentZ, Illuminant);
            var rgb = ColorMath.XyzToRgb(currentX, currentY, currentZ, Illuminant, GamutStrategy);

            var state = new ColorState
            {
                H = (int)Math.Round(h),
                S = (int)Math.Round(s),
                V = (int)Math.Round(v),
                X = (int)Math.Round(currentX),
                Y = (int)Math.Round(currentY),
                Z = (int)Math.Round(currentZ),
                L = (int)Math.Round(lab.l),
                A = (int)Math.Round(lab.a),
                B = (int)Math.Round(lab.b),
                R = rgb.r,
                G = rgb.g,
                Bl = rgb.b,
                GamutClipped = hsvOutOfGamut || rgb.clipped,
                Illuminant = Illuminant,
                GamutStrategy = GamutStrategy
            };

            StateChanged?.Invoke(state);
        }
    }
}