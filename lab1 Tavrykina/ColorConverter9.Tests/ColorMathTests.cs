using Microsoft.VisualStudio.TestTools.UnitTesting;
using ColorConverter9.Core;

namespace ColorConverter9.Tests
{
    [TestClass]
    public class ColorMathTests
    {
        private const double Tol = 0.05;

        private static void AssertClose(double expected, double actual, string what)
        {
            Assert.IsTrue(System.Math.Abs(expected - actual) < 0.1,
                $"{what}: expected {expected:F4}, got {actual:F4}");
        }

      


        [TestMethod]
        public void RgbToLab_Red_MatchesReference()
        {
            var xyz = ColorMath.RgbToXyz(255, 0, 0, Illuminant.D65);
            var lab = ColorMath.XyzToLab(xyz.x, xyz.y, xyz.z, Illuminant.D65);
            AssertClose(41.2456, xyz.x, "X");
            AssertClose(21.2673, xyz.y, "Y");
            AssertClose(1.9334, xyz.z, "Z");
            AssertClose(53.2408, lab.l, "L");
            AssertClose(80.0925, lab.a, "a");
            AssertClose(67.2032, lab.b, "b");
        }

        [TestMethod]
        public void RgbToLab_Green_MatchesReference()
        {
            var xyz = ColorMath.RgbToXyz(0, 255, 0, Illuminant.D65);
            var lab = ColorMath.XyzToLab(xyz.x, xyz.y, xyz.z, Illuminant.D65);
            AssertClose(87.7347, lab.l, "L");
            AssertClose(-86.1827, lab.a, "a");
            AssertClose(83.1793, lab.b, "b");
        }

        [TestMethod]
        public void RgbToLab_Blue_MatchesReference()
        {
            var xyz = ColorMath.RgbToXyz(0, 0, 255, Illuminant.D65);
            var lab = ColorMath.XyzToLab(xyz.x, xyz.y, xyz.z, Illuminant.D65);
            AssertClose(32.2970, lab.l, "L");
            AssertClose(79.1875, lab.a, "a");
            AssertClose(-107.8602, lab.b, "b");
        }

        [TestMethod]
        public void RgbToLab_White_IsLabWhitePoint()
        {
            var xyz = ColorMath.RgbToXyz(255, 255, 255, Illuminant.D65);
            var lab = ColorMath.XyzToLab(xyz.x, xyz.y, xyz.z, Illuminant.D65);
            AssertClose(100.0, lab.l, "L");
            AssertClose(0.0, lab.a, "a");
            AssertClose(0.0, lab.b, "b");
        }

        [TestMethod]
        public void RgbToLab_Black_IsOrigin()
        {
            var xyz = ColorMath.RgbToXyz(0, 0, 0, Illuminant.D65);
            var lab = ColorMath.XyzToLab(xyz.x, xyz.y, xyz.z, Illuminant.D65);
            AssertClose(0.0, lab.l, "L");
            AssertClose(0.0, lab.a, "a");
            AssertClose(0.0, lab.b, "b");
        }





        [TestMethod]
        public void WhitePoint_Invariant_HoldsForEveryIlluminant()
        {
            foreach (Illuminant illum in new[] { Illuminant.D65, Illuminant.D50, Illuminant.E })
            {
                var xyz = ColorMath.RgbToXyz(255, 255, 255, illum);
                var lab = ColorMath.XyzToLab(xyz.x, xyz.y, xyz.z, illum);
                AssertClose(100.0, lab.l, $"L @ {illum}");
                AssertClose(0.0, lab.a, $"a @ {illum}");
                AssertClose(0.0, lab.b, $"b @ {illum}");
            }
        }

        [TestMethod]
        public void MatrixForD50_DiffersFromD65()
        {
            var m65 = ColorMath.BuildRgbToXyzMatrix(Illuminant.D65);
            var m50 = ColorMath.BuildRgbToXyzMatrix(Illuminant.D50);
        
            Assert.AreNotEqual(m65[0, 0], m50[0, 0], 1e-9);
        }

       



        [TestMethod]
        public void RoundTrip_HsvXyzHsv_ReturnsOriginalHue()
        {
            var xyz = ColorMath.HsvToXyz(210, 0.6, 0.8, Illuminant.D65);
            var hsv = ColorMath.XyzToHsv(xyz.x, xyz.y, xyz.z, Illuminant.D65, GamutStrategy.Clip);
            Assert.IsFalse(hsv.clipped, "in-gamut HSV must not be reported as clipped");
            AssertClose(210, hsv.h, "H");
            AssertClose(60, hsv.s * 100, "S"); 
            AssertClose(80, hsv.v * 100, "V"); 
        }

        [TestMethod]
        public void RoundTrip_RgbXyzRgb_NoClippingForInGamutColor()
        {
            var xyz = ColorMath.RgbToXyz(120, 200, 40, Illuminant.D65);
            var rgb = ColorMath.XyzToRgb(xyz.x, xyz.y, xyz.z, Illuminant.D65, GamutStrategy.Clip);
            Assert.IsFalse(rgb.clipped);
            Assert.AreEqual(120, rgb.r, 1);
            Assert.AreEqual(200, rgb.g, 1);
            Assert.AreEqual(40, rgb.b, 1);
        }

        [TestMethod]
        public void RoundTrip_LabXyzLab_ReturnsOriginalLab()
        {
            var xyz = ColorMath.LabToXyz(60, -20, 35, Illuminant.D50);
            var lab = ColorMath.XyzToLab(xyz.x, xyz.y, xyz.z, Illuminant.D50);
            AssertClose(60, lab.l, "L");
            AssertClose(-20, lab.a, "a");
            AssertClose(35, lab.b, "b");
        }

      
        // HSV <=> RGB

        [TestMethod]
        public void HsvToRgb_PureGreen()
        {
            var rgb = ColorMath.HsvToRgb(120, 1.0, 1.0);
            AssertClose(0, rgb.r, "r");
            AssertClose(1, rgb.g, "g");
            AssertClose(0, rgb.b, "b");
        }

        [TestMethod]
        public void HsvToRgb_PureBlue()
        {
            var rgb = ColorMath.HsvToRgb(240, 1.0, 1.0);
            AssertClose(0, rgb.r, "r");
            AssertClose(0, rgb.g, "g");
            AssertClose(1, rgb.b, "b");
        }

        [TestMethod]
        public void HsvToRgb_Black_ForZeroValue()
        {
            var rgb = ColorMath.HsvToRgb(0, 0, 0);
            AssertClose(0, rgb.r, "r");
            AssertClose(0, rgb.g, "g");
            AssertClose(0, rgb.b, "b");
        }



        [TestMethod]
        public void XyzToRgb_OutOfGamut_IsFlaggedWithClipStrategy()
        {

            var rgb = ColorMath.XyzToRgb(10, 60, 5, Illuminant.D65, GamutStrategy.Clip);
            Assert.IsTrue(rgb.clipped);
        }

        [TestMethod]
        public void XyzToRgb_OutOfGamut_IsFlaggedWithScaleStrategy()
        {
            var rgb = ColorMath.XyzToRgb(10, 60, 5, Illuminant.D65, GamutStrategy.Scale);
            Assert.IsTrue(rgb.clipped);
        }
    }
}
