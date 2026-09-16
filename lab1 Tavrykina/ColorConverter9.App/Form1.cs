using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using ColorConverter9.Core;

namespace ColorConverter9
{
    
    public partial class Form1 : Form
    {
        private readonly ColorViewModel vm = new ColorViewModel();
        private bool isUpdatingUi = false;

        // HSV
        private TrackBar trackH, trackS, trackV;
        private TextBox txtH, txtS, txtV;
        private Panel gradH, gradS, gradV;

        // XYZ
        private TrackBar trackX, trackY, trackZ;
        private TextBox txtX, txtY, txtZ;
        private Panel gradX, gradY, gradZ;

        // LAB
        private TrackBar trackL, trackA, trackB2;
        private TextBox txtL, txtA, txtB2;
        private Panel gradL, gradA, gradB2;

        private TextBox txtHex;
        private PictureBox picPreview;
        private Label lblWarning, lblRgbInfo;
        private ComboBox cmbIlluminant, cmbGamutStrategy;

        public Form1()
        {
            this.Text = "HSV ↔ XYZ ↔ LAB - Вариант 9";
            this.Size = new Size(980, 820);
            this.BackColor = Color.FromArgb(30, 30, 50);
            this.StartPosition = FormStartPosition.CenterScreen;

            CreateUI();

            vm.StateChanged += OnStateChanged;
            vm.SetFromRgbHex(255, 0, 0);
        }

        private void CreateUI()
        {
            int y = 20;

            Label title = new Label
            {
                Text = "HSV <=> XYZ <=> LAB",
                Font = new Font("Segoe UI", 18, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 200, 100),
                Location = new Point(20, y),
                Size = new Size(600, 40)
            };
            this.Controls.Add(title);
            y += 50;

            picPreview = new PictureBox
            {
                Location = new Point(20, y),
                Size = new Size(180, 120),
                BackColor = Color.Red,
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(picPreview);

            Label lblHex = new Label
            {
                Text = "HEX:",
                Location = new Point(220, y + 10),
                Size = new Size(40, 25),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            this.Controls.Add(lblHex);

            txtHex = new TextBox
            {
                Location = new Point(260, y + 8),
                Size = new Size(130, 30),
                BackColor = Color.FromArgb(50, 50, 70),
                ForeColor = Color.White,
                Text = "#FF0000",
                Font = new Font("Consolas", 14)
            };
            this.Controls.Add(txtHex);
            txtHex.Leave += (s, e) => TryApplyHex();
            txtHex.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { TryApplyHex(); e.SuppressKeyPress = true; } };

            Label lblRgbLabel = new Label
            {
                Text = "RGB:",
                Location = new Point(220, y + 45),
                Size = new Size(40, 25),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            this.Controls.Add(lblRgbLabel);

            lblRgbInfo = new Label
            {
                Text = "R:255 G:0 B:0",
                Location = new Point(260, y + 45),
                Size = new Size(220, 25),
                ForeColor = Color.FromArgb(200, 200, 200),
                Font = new Font("Consolas", 11)
            };
            this.Controls.Add(lblRgbInfo);

            y += 140;

            Label lblSettings = new Label
            {
                Text = "Настройки:",
                Location = new Point(20, y),
                Size = new Size(90, 25),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            this.Controls.Add(lblSettings);

            cmbIlluminant = new ComboBox
            {
                Location = new Point(110, y),
                Size = new Size(220, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 70),
                ForeColor = Color.White
            };
            cmbIlluminant.Items.AddRange(new object[] { "D65 (sRGB, дневной свет)", "D50 (полиграфия)", "E (равноэнергетический)" });
            cmbIlluminant.SelectedIndex = 0;
            cmbIlluminant.SelectedIndexChanged += (s, e) =>
            {
                vm.SetIlluminant((Illuminant)cmbIlluminant.SelectedIndex);
            };
            this.Controls.Add(cmbIlluminant);

            cmbGamutStrategy = new ComboBox
            {
                Location = new Point(340, y),
                Size = new Size(220, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(50, 50, 70),
                ForeColor = Color.White
            };
            cmbGamutStrategy.Items.AddRange(new object[] { "Clip (обрезание)", "Scale (масштабирование)" });
            cmbGamutStrategy.SelectedIndex = 0;
            cmbGamutStrategy.SelectedIndexChanged += (s, e) =>
            {
                vm.SetGamutStrategy((GamutStrategy)cmbGamutStrategy.SelectedIndex);
            };
            this.Controls.Add(cmbGamutStrategy);

            y += 45;

            lblWarning = new Label
            {
                Location = new Point(20, y),
                Size = new Size(800, 30),
                ForeColor = Color.Orange,
                Font = new Font("Segoe UI", 10)
            };
            this.Controls.Add(lblWarning);
            y += 45;

            int col1 = 20, col2 = 330, col3 = 640;

            int yHsv = y;
            CreateSliderGroup("HSV", col1, ref yHsv,
                new[] { "H (0-360)", "S (0-100%)", "V (0-100%)" },
                out trackH, out trackS, out trackV,
                out txtH, out txtS, out txtV,
                out gradH, out gradS, out gradV,
                360, 100, 100);

            int yXyz = y;
            CreateSliderGroup("XYZ", col2, ref yXyz,
                new[] { "X (0-100)", "Y (0-100)", "Z (0-100)" },
                out trackX, out trackY, out trackZ,
                out txtX, out txtY, out txtZ,
                out gradX, out gradY, out gradZ,
                100, 100, 100);

            int yLab = y;
            CreateSliderGroup("LAB", col3, ref yLab,
                new[] { "L (0-100)", "a (-128..128)", "b (-128..128)" },
                out trackL, out trackA, out trackB2,
                out txtL, out txtA, out txtB2,
                out gradL, out gradA, out gradB2,
                100, 256, 256);

            trackH.Scroll += (s, e) => OnHsvSliderMoved();
            trackS.Scroll += (s, e) => OnHsvSliderMoved();
            trackV.Scroll += (s, e) => OnHsvSliderMoved();

            trackX.Scroll += (s, e) => OnXyzSliderMoved();
            trackY.Scroll += (s, e) => OnXyzSliderMoved();
            trackZ.Scroll += (s, e) => OnXyzSliderMoved();

            trackL.Scroll += (s, e) => OnLabSliderMoved();
            trackA.Scroll += (s, e) => OnLabSliderMoved();
            trackB2.Scroll += (s, e) => OnLabSliderMoved();

            txtH.Leave += (s, e) => TryApplyHsvText();
            txtS.Leave += (s, e) => TryApplyHsvText();
            txtV.Leave += (s, e) => TryApplyHsvText();
            txtX.Leave += (s, e) => TryApplyXyzText();
            txtY.Leave += (s, e) => TryApplyXyzText();
            txtZ.Leave += (s, e) => TryApplyXyzText();
            txtL.Leave += (s, e) => TryApplyLabText();
            txtA.Leave += (s, e) => TryApplyLabText();
            txtB2.Leave += (s, e) => TryApplyLabText();
        }

        private void CreateSliderGroup(string title, int x, ref int y,
            string[] labels, out TrackBar t1, out TrackBar t2, out TrackBar t3,
            out TextBox tb1, out TextBox tb2, out TextBox tb3,
            out Panel g1, out Panel g2, out Panel g3,
            int max1, int max2, int max3)
        {
            Label titleLabel = new Label
            {
                Text = title,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 200, 100),
                Location = new Point(x, y),
                Size = new Size(90, 30)
            };
            this.Controls.Add(titleLabel);
            y += 35;

            CreateSlider(labels[0], x + 5, ref y, out t1, out tb1, out g1, 0, max1);
            CreateSlider(labels[1], x + 5, ref y, out t2, out tb2, out g2, 0, max2);
            CreateSlider(labels[2], x + 5, ref y, out t3, out tb3, out g3, 0, max3);

            y += 20;
        }
 private void CreateSlider(string label, int x, ref int y,
            out TrackBar track, out TextBox text, out Panel gradient, int min, int max)
        {
            Label lbl = new Label
            {
                Text = label,
                Location = new Point(x, y),
                Size = new Size(95, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9)
            };
            this.Controls.Add(lbl);

            gradient = new Panel
            {
                Location = new Point(x + 95, y),
                Size = new Size(130, 8),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(gradient);

            track = new TrackBar
            {
                Location = new Point(x + 95, y + 8),
                Size = new Size(130, 25),
                Minimum = min,
                Maximum = max,
                Value = 0,
                BackColor = Color.FromArgb(40, 40, 60),
                TickStyle = TickStyle.None
            };
            this.Controls.Add(track);

            text = new TextBox
            {
                Location = new Point(x + 230, y + 8),
                Size = new Size(60, 25),
                BackColor = Color.FromArgb(50, 50, 70),
                ForeColor = Color.White,
                Text = "0",
                TextAlign = HorizontalAlignment.Center
            };
            this.Controls.Add(text);

            y += 40;
        }

        


        private void OnHsvSliderMoved()
        {
            if (isUpdatingUi) return;
            vm.SetFromHsv(trackH.Value, trackS.Value, trackV.Value);
        }

        private void OnXyzSliderMoved()
        {
            if (isUpdatingUi) return;
            vm.SetFromXyz(trackX.Value, trackY.Value, trackZ.Value);
        }

        private void OnLabSliderMoved()
        {
            if (isUpdatingUi) return;
            vm.SetFromLab(trackL.Value, trackA.Value - 128, trackB2.Value - 128);
        }

        private void TryApplyHsvText()
        {
            if (isUpdatingUi) return;
            if (double.TryParse(txtH.Text, out double h) &&
                double.TryParse(txtS.Text, out double s) &&
                double.TryParse(txtV.Text, out double v))
            {
                vm.SetFromHsv(h, s, v);
            }
        }

        private void TryApplyXyzText()
        {
            if (isUpdatingUi) return;
            if (double.TryParse(txtX.Text, out double x) &&
                double.TryParse(txtY.Text, out double y) &&
                double.TryParse(txtZ.Text, out double z))
            {
                vm.SetFromXyz(x, y, z);
            }
        }

        private void TryApplyLabText()
        {
            if (isUpdatingUi) return;
            if (double.TryParse(txtL.Text, out double l) &&
                double.TryParse(txtA.Text, out double a) &&
                double.TryParse(txtB2.Text, out double b))
            {
                vm.SetFromLab(l, a, b);
            }
        }

        private void TryApplyHex()
        {
            if (isUpdatingUi) return;
            try
            {
                string hex = txtHex.Text.TrimStart('#');
                if (hex.Length == 6)
                {
                    int r = Convert.ToInt32(hex.Substring(0, 2), 16);
                    int g = Convert.ToInt32(hex.Substring(2, 2), 16);
                    int b = Convert.ToInt32(hex.Substring(4, 2), 16);
                    vm.SetFromRgbHex(r, g, b);
                }
            }
            catch { /* wrong input */ }
        }

        
        private void OnStateChanged(ColorState st)
        {
            isUpdatingUi = true;
            try
            {
                trackH.Value = Clamp(RoundToInt(st.H), trackH.Minimum, trackH.Maximum);
                trackS.Value = Clamp(RoundToInt(st.S), trackS.Minimum, trackS.Maximum);
                trackV.Value = Clamp(RoundToInt(st.V), trackV.Minimum, trackV.Maximum);
                txtH.Text = RoundToInt(st.H).ToString();
                txtS.Text = RoundToInt(st.S).ToString();
                txtV.Text = RoundToInt(st.V).ToString();

                trackX.Value = Clamp(RoundToInt(st.X), trackX.Minimum, trackX.Maximum);
                trackY.Value = Clamp(RoundToInt(st.Y), trackY.Minimum, trackY.Maximum);
                trackZ.Value = Clamp(RoundToInt(st.Z), trackZ.Minimum, trackZ.Maximum);
                txtX.Text = st.X.ToString("F2");
                txtY.Text = st.Y.ToString("F2");
                txtZ.Text = st.Z.ToString("F2");

                trackL.Value = Clamp(RoundToInt(st.L), trackL.Minimum, trackL.Maximum);
                trackA.Value = Clamp(RoundToInt(st.A + 128), trackA.Minimum, trackA.Maximum);
                trackB2.Value = Clamp(RoundToInt(st.B + 128), trackB2.Minimum, trackB2.Maximum);
                txtL.Text = st.L.ToString("F2");
                txtA.Text = st.A.ToString("F2");
                txtB2.Text = st.B.ToString("F2");

                txtHex.Text = $"#{st.R:X2}{st.G:X2}{st.Bl:X2}";
                picPreview.BackColor = Color.FromArgb(st.R, st.G, st.Bl);
                lblRgbInfo.Text = $"R:{st.R} G:{st.G} B:{st.Bl}";

                lblWarning.Text = st.GamutClipped
                    ? $" Выход за границы RGB — применена стратегия «{st.GamutStrategy}»"
                    : $"Освещение: {st.Illuminant}  Стратегия: {st.GamutStrategy}";

                RedrawGradients(st);
            }
            finally
            {
                isUpdatingUi = false;
            }
        }

        private static int RoundToInt(double v) => (int)Math.Round(v);
        private static int Clamp(int v, int min, int max) => Math.Min(Math.Max(v, min), max);




        private void RedrawGradients(ColorState st)
        {
            DrawGradient(gradH, trackH, i => vm.PreviewRgbForHsv(i, st.S, st.V));
            DrawGradient(gradS, trackS, i => vm.PreviewRgbForHsv(st.H, i, st.V));
            DrawGradient(gradV, trackV, i => vm.PreviewRgbForHsv(st.H, st.S, i));

            DrawGradient(gradX, trackX, i => vm.PreviewRgbForXyz(i, st.Y, st.Z));
            DrawGradient(gradY, trackY, i => vm.PreviewRgbForXyz(st.X, i, st.Z));
            DrawGradient(gradZ, trackZ, i => vm.PreviewRgbForXyz(st.X, st.Y, i));

            DrawGradient(gradL, trackL, i => vm.PreviewRgbForLab(i, st.A, st.B));
            DrawGradient(gradA, trackA, i => vm.PreviewRgbForLab(st.L, i - 128, st.B));
            DrawGradient(gradB2, trackB2, i => vm.PreviewRgbForLab(st.L, st.A, i - 128));
        }

        private void DrawGradient(Panel panel, TrackBar track, Func<double, (int r, int g, int b)> sample)
        {
            const int steps = 24;
            var bmp = new Bitmap(Math.Max(panel.Width, 1), Math.Max(panel.Height, 1));
            using (var g = Graphics.FromImage(bmp))
            {
                double range = track.Maximum - track.Minimum;
                for (int i = 0; i < steps; i++)
                {
                    double t0 = track.Minimum + range * i / steps;
                    double t1 = track.Minimum + range * (i + 1) / steps;
                    var c0 = sample(t0);
                    var rect = new RectangleF(
                        (float)(bmp.Width * i / (double)steps), 0,
                        (float)(bmp.Width / (double)steps) + 1, bmp.Height);
                    using (var brush = new SolidBrush(Color.FromArgb(c0.r, c0.g, c0.b)))
                    {
                        g.FillRectangle(brush, rect);
                    }
                }
            }
            panel.BackgroundImage?.Dispose();
            panel.BackgroundImage = bmp;
            panel.BackgroundImageLayout = ImageLayout.Stretch;
        }
    }
}
