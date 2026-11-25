namespace LabContrast.Core;

public static class ColorSpace
{

    public static void RgbToHsv(byte r8, byte g8, byte b8, out float h, out float s, out float v)
    {
        float r = r8 / 255f, g = g8 / 255f, b = b8 / 255f;
        float max = MathF.Max(r, MathF.Max(g, b));
        float min = MathF.Min(r, MathF.Min(g, b));
        float d = max - min;
        v = max;
        s = max <= 0f ? 0f : d / max;
        if (d <= 0f) h = 0f;
        else if (max == r) h = 60f * (((g - b) / d) % 6f);
        else if (max == g) h = 60f * ((b - r) / d + 2f);
        else h = 60f * ((r - g) / d + 4f);
        if (h < 0f) h += 360f;
    }

    public static void HsvToRgb(float h, float s, float v, out byte r, out byte g, out byte b)
    {
        float c = v * s;
        float hp = h / 60f;
        float x = c * (1f - MathF.Abs(hp % 2f - 1f));
        float m = v - c;
        float r1, g1, b1;
        switch ((int)hp)
        {
            case 0: r1 = c; g1 = x; b1 = 0; break;
            case 1: r1 = x; g1 = c; b1 = 0; break;
            case 2: r1 = 0; g1 = c; b1 = x; break;
            case 3: r1 = 0; g1 = x; b1 = c; break;
            case 4: r1 = x; g1 = 0; b1 = c; break;
            default: r1 = c; g1 = 0; b1 = x; break;
        }
        r = To8(r1 + m); g = To8(g1 + m); b = To8(b1 + m);
    }

    static byte To8(float f) => (byte)Math.Clamp((int)MathF.Round(f * 255f), 0, 255);
}
