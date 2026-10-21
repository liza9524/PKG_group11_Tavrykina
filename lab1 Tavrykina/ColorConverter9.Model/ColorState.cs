using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorConverter9.Core
{
    public class ColorState
    {
        public int H, S, V;
        public int X, Y, Z;
        public int L, A, B;
        public int R, G, Bl;

        public bool GamutClipped;
        public Illuminant Illuminant;
        public GamutStrategy GamutStrategy;
    }

    public enum ChangedFrom { Hsv, Xyz, Lab, Hex, Settings }
}