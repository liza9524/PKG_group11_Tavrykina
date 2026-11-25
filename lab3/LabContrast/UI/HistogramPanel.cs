using System.Drawing.Drawing2D;

namespace LabContrast.UI;

public sealed class HistogramPanel : Control
{
    int[][]? _h;
    string _title = "";
    long _max = 1;

    public HistogramPanel()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
    }

    public void SetData(int[][]? h, string title)
    {
        _h = h; _title = title;
        _max = 1;
        if (h != null)
            foreach (var arr in h)
                foreach (var v in arr)
                    if (v > _max) _max = v;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Color.White);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int w = Width - 10, hgt = Height - 26;
        if (_h == null || w <= 10 || hgt <= 10) return;

        long max = _max;

        PointF[] Pts(int[] a)
        {
            var pts = new PointF[256];
            for (int i = 0; i < 256; i++)
                pts[i] = new PointF(5 + i * (w - 1) / 255f, 5 + hgt - (float)a[i] / max * hgt);
            return pts;
        }

        var yp = Pts(_h[3]);
        var poly = new PointF[258];
        Array.Copy(yp, poly, 256);
        poly[256] = new PointF(5 + w - 1, 5 + hgt);
        poly[257] = new PointF(5, 5 + hgt);
        using (var br = new SolidBrush(Color.FromArgb(110, Color.Gray))) g.FillPolygon(br, poly);

        Color[] cols = { Color.Blue, Color.Green, Color.Red };
        for (int k = 0; k < 3; k++)
        {
            using var pen = new Pen(Color.FromArgb(200, cols[k]), 1.2f);
            g.DrawLines(pen, Pts(_h[k]));
        }
        using var axis = new Pen(Color.Black);
        g.DrawLine(axis, 5, 5 + hgt, 5 + w, 5 + hgt);
        g.DrawString(_title, Font, Brushes.Black, 5, hgt + 8);
    }
}