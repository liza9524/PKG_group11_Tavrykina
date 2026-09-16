using System;

namespace ColorConverter9.Core
{
    public class ColorState
    {
        public double H, S, V;              
        public double X, Y, Z;              
        public double L, A, B;             
        public int R, G, Bl;               
        public bool GamutClipped;
        public Illuminant Illuminant;
        public GamutStrategy GamutStrategy;
    }

    public enum ChangedFrom { Hsv, Xyz, Lab, Hex, Settings }





    public class ColorViewModel
    {
        public Illuminant Illuminant { get; private set; } = Illuminant.D65;
        public GamutStrategy GamutStrategy { get; private set; } = GamutStrategy.Clip;


        private double currentX, currentY, currentZ;

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
            Publish();
        }

        public void SetGamutStrategy(GamutStrategy strategy)
        {
            GamutStrategy = strategy;
            Publish();
        }

        public void SetFromHsv(double h, double s, double v)
        {
            var xyz = ColorMath.HsvToXyz(h, s / 100.0, v / 100.0, Illuminant);
            currentX = xyz.x; currentY = xyz.y; currentZ = xyz.z;
            Publish();
        }

        public void SetFromXyz(double x, double y, double z)
        {
            currentX = x; currentY = y; currentZ = z;
            Publish();
        }

        public void SetFromLab(double l, double a, double b)
        {
            var xyz = ColorMath.LabToXyz(l, a, b, Illuminant);
            currentX = xyz.x; currentY = xyz.y; currentZ = xyz.z;
            Publish();
        }

        public void SetFromRgbHex(int r, int g, int b)
        {
            var xyz = ColorMath.RgbToXyz(r, g, b, Illuminant);
            currentX = xyz.x; currentY = xyz.y; currentZ = xyz.z;
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
            var hsv = ColorMath.XyzToHsv(currentX, currentY, currentZ, Illuminant, GamutStrategy);
            var lab = ColorMath.XyzToLab(currentX, currentY, currentZ, Illuminant);
            var rgb = ColorMath.XyzToRgb(currentX, currentY, currentZ, Illuminant, GamutStrategy);

            var state = new ColorState
            {
                H = hsv.h, S = hsv.s * 100, V = hsv.v * 100,
                X = currentX, Y = currentY, Z = currentZ,
                L = lab.l, A = lab.a, B = lab.b,
                R = rgb.r, G = rgb.g, Bl = rgb.b,
                GamutClipped = hsv.clipped || rgb.clipped,
                Illuminant = Illuminant,
                GamutStrategy = GamutStrategy
            };

            StateChanged?.Invoke(state);
        }
    }
}
