using Microsoft.VisualStudio.TestTools.UnitTesting;
using ColorConverter9.Core;
using System;

namespace ColorConverter9.Tests
{
    [TestClass]
    public class ColorMathTests
    {
        private const double AbsTolSmall = 0.5;
        private const double RelTol = 0.01;

        private static void AssertCloseAbs(double expected, double actual, string what, double tol = AbsTolSmall)
        {
            Assert.IsTrue(Math.Abs(expected - actual) < tol,
                $"{what}: expected {expected:F4}, got {actual:F4} (tol {tol})");
        }

        private static void AssertCloseRel(double expected, double actual, string what)
        {
            double tol = Math.Max(AbsTolSmall, Math.Abs(expected) * RelTol);
            Assert.IsTrue(Math.Abs(expected - actual) < tol,
                $"{what}: expected {expected:F4}, got {actual:F4} (tol {tol})");
        }



        [TestMethod]
        public void RgbToLab_Red_MatchesReference()
        {
            var xyz = ColorMath.RgbToXyz(255, 0, 0, Illuminant.D65);
            var lab = ColorMath.XyzToLab(xyz.x, xyz.y, xyz.z, Illuminant.D65);
            AssertCloseAbs(41.2456, xyz.x, "X");
            AssertCloseAbs(21.2673, xyz.y, "Y");
            AssertCloseAbs(1.9334, xyz.z, "Z");
            AssertCloseAbs(53.2408, lab.l, "L");
            AssertCloseRel(80.0925, lab.a, "a");
            AssertCloseRel(67.2032, lab.b, "b");
        }

        [TestMethod]
        public void RgbToLab_Green_MatchesReference()
        {
            var xyz = ColorMath.RgbToXyz(0, 255, 0, Illuminant.D65);
            var lab = ColorMath.XyzToLab(xyz.x, xyz.y, xyz.z, Illuminant.D65);
            AssertCloseAbs(87.7347, lab.l, "L");
            AssertCloseRel(-86.1827, lab.a, "a");
            AssertCloseRel(83.1793, lab.b, "b");
        }

        [TestMethod]
        public void RgbToLab_Blue_MatchesReference()
        {
            var xyz = ColorMath.RgbToXyz(0, 0, 255, Illuminant.D65);
            var lab = ColorMath.XyzToLab(xyz.x, xyz.y, xyz.z, Illuminant.D65);
            AssertCloseAbs(32.2970, lab.l, "L");
            AssertCloseRel(79.1875, lab.a, "a");
            AssertCloseRel(-107.8602, lab.b, "b");
        }

        [TestMethod]
        public void RgbToLab_White_IsLabWhitePoint()
        {
            var xyz = ColorMath.RgbToXyz(255, 255, 255, Illuminant.D65);
            var lab = ColorMath.XyzToLab(xyz.x, xyz.y, xyz.z, Illuminant.D65);
            AssertCloseAbs(100.0, lab.l, "L");
            AssertCloseAbs(0.0, lab.a, "a");
            AssertCloseAbs(0.0, lab.b, "b");
        }

        [TestMethod]
        public void RgbToLab_Black_IsOrigin()
        {
            var xyz = ColorMath.RgbToXyz(0, 0, 0, Illuminant.D65);
            var lab = ColorMath.XyzToLab(xyz.x, xyz.y, xyz.z, Illuminant.D65);
            AssertCloseAbs(0.0, lab.l, "L");
            AssertCloseAbs(0.0, lab.a, "a");
            AssertCloseAbs(0.0, lab.b, "b");
        }



        [TestMethod]
        public void WhitePoint_Invariant_HoldsForEveryIlluminant()
        {
            foreach (Illuminant illum in new[] { Illuminant.D65, Illuminant.D50, Illuminant.E })
            {
                var xyz = ColorMath.RgbToXyz(255, 255, 255, illum);
                var lab = ColorMath.XyzToLab(xyz.x, xyz.y, xyz.z, illum);
                AssertCloseAbs(100.0, lab.l, $"L @ {illum}");
                AssertCloseAbs(0.0, lab.a, $"a @ {illum}");
                AssertCloseAbs(0.0, lab.b, $"b @ {illum}");
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
        public void RoundTrip_HsvXyzHsv_ReturnsOriginalHsv()
        {
            double h0 = 210, s0 = 0.6, v0 = 0.8;
            var xyz = ColorMath.HsvToXyz(h0, s0, v0, Illuminant.D65);
            var hsv = ColorMath.XyzToHsvRaw(xyz.x, xyz.y, xyz.z, Illuminant.D65);
            Assert.IsFalse(hsv.outOfGamut, "in-gamut HSV must not be out of gamut");
            AssertCloseAbs(h0, hsv.h, "H", 0.5);
            AssertCloseAbs(s0, hsv.s, "S", 0.005);
            AssertCloseAbs(v0, hsv.v, "V", 0.005);
        }

        [TestMethod]
        public void RoundTrip_HsvAtV100_PreservesHue_ForPurePrimaries()
        {
            foreach (double h0 in new double[] { 0, 60, 120, 180, 240, 300 })
            {
                var xyz = ColorMath.HsvToXyz(h0, 1.0, 1.0, Illuminant.D65);
                var hsv = ColorMath.XyzToHsvRaw(xyz.x, xyz.y, xyz.z, Illuminant.D65);
                AssertCloseAbs(h0, hsv.h, $"H @ {h0}", 0.5);
            }
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
            AssertCloseAbs(60, lab.l, "L", 1e-6);
            AssertCloseAbs(-20, lab.a, "a", 1e-6);
            AssertCloseAbs(35, lab.b, "b", 1e-6);
        }


        [TestMethod]
        public void HsvToRgb_PureGreen()
        {
            var rgb = ColorMath.HsvToRgb(120, 1.0, 1.0);
            AssertCloseAbs(0, rgb.r, "r", 1e-9);
            AssertCloseAbs(1, rgb.g, "g", 1e-9);
            AssertCloseAbs(0, rgb.b, "b", 1e-9);
        }

        [TestMethod]
        public void HsvToRgb_PureBlue()
        {
            var rgb = ColorMath.HsvToRgb(240, 1.0, 1.0);
            AssertCloseAbs(0, rgb.r, "r", 1e-9);
            AssertCloseAbs(0, rgb.g, "g", 1e-9);
            AssertCloseAbs(1, rgb.b, "b", 1e-9);
        }

        [TestMethod]
        public void HsvToRgb_Black_ForZeroValue()
        {
            var rgb = ColorMath.HsvToRgb(0, 0, 0);
            AssertCloseAbs(0, rgb.r, "r", 1e-9);
            AssertCloseAbs(0, rgb.g, "g", 1e-9);
            AssertCloseAbs(0, rgb.b, "b", 1e-9);
        }


        [TestMethod]
        public void RgbToHsv_Gray_ReturnsZeroHueAndSat()
        {
            var hsv = ColorMath.RgbToHsv(0.5, 0.5, 0.5);
            Assert.AreEqual(0, hsv.h, 1e-9);
            Assert.AreEqual(0, hsv.s, 1e-9);
            Assert.AreEqual(0.5, hsv.v, 1e-9);
        }
    }
}