using System.Diagnostics;
using System.Drawing.Imaging;
using LabContrast.Algorithms;
using LabContrast.Core;

namespace LabContrast.UI;

public sealed class MainForm : Form
{
    sealed record Method(string Name, string? P1, double D1, string? P2, double D2,
                         Func<PixelBuffer, double, double, PixelBuffer> Run);

    readonly Method[] _methods =
    {
        new("Линейное контрастирование (авто, перцентили)", "Отсечь снизу, %", 1, "Отсечь сверху, %", 1,
            (s, a, b) => PointOps.AutoStretch(s, a, b, out _, out _)),
        new("Линейное преобразование g = a·f + b", "a (контраст)", 1.5, "b (яркость)", -40,
            (s, a, b) => PointOps.Linear(s, a, b)),
        new("Негатив", null, 0, null, 0, (s, a, b) => PointOps.Negative(s)),
        new("Гамма-коррекция", "γ", 0.5, null, 0, (s, a, b) => PointOps.Gamma(s, a)),
        new("Логарифмическое преобразование", "c", 1, null, 0, (s, a, b) => PointOps.Log(s, a)),
        new("Эквализация: RGB по каналам", null, 0, null, 0, (s, a, b) => Equalization.EqualizeRgb(s)),
        new("Эквализация: яркость V в HSV", null, 0, null, 0, (s, a, b) => Equalization.EqualizeHsvV(s)),
        new("CLAHE (канал V в HSV)", "Clip limit", 3, "Плиток по стороне", 8,
            (s, a, b) => Equalization.ClaheHsvV(s, a, (int)b)),
    };

    readonly PictureBox _picSrc = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
    readonly PictureBox _picRes = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black };
    readonly HistogramPanel _histSrc = new() { Dock = DockStyle.Fill };
    readonly HistogramPanel _histRes = new() { Dock = DockStyle.Fill };
    readonly ComboBox _cmb = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    readonly Label _lbl1 = new() { AutoSize = true, Margin = new Padding(8, 8, 0, 0) };
    readonly Label _lbl2 = new() { AutoSize = true, Margin = new Padding(8, 8, 0, 0) };
    readonly NumericUpDown _num1 = new() { DecimalPlaces = 2, Increment = 0.1M, Minimum = -1000, Maximum = 1000, Width = 70 };
    readonly NumericUpDown _num2 = new() { DecimalPlaces = 2, Increment = 0.1M, Minimum = -1000, Maximum = 1000, Width = 70 };
    readonly Button _btnOpen = new() { Text = "Открыть…", AutoSize = true };
    readonly Button _btnSave = new() { Text = "Сохранить результат…", AutoSize = true };
    readonly Button _btnTest = new() { Text = "Создать тестовые изображения", AutoSize = true };
    readonly Button _btnApply = new() { Text = "Применить", AutoSize = true };
    readonly ProgressBar _progress = new() { Style = ProgressBarStyle.Marquee, Width = 110, Visible = false };
    readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(6, 4, 6, 0) };

    PixelBuffer? _src, _res;
    int[][]? _hSrc;
    bool _busy;

    public MainForm()
    {
        Text = "Лабораторная работа 3, вариант 1: гистограммы и контрастирование";
        Width = 1300; Height = 840;

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(4) };
        foreach (var m in _methods) _cmb.Items.Add(m.Name);
        top.Controls.AddRange(new Control[] { _btnOpen, _btnSave, _btnTest, _cmb, _lbl1, _num1, _lbl2, _num2, _btnApply, _progress });

        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 62));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 38));
        grid.Controls.Add(Wrap("Исходное изображение", _picSrc), 0, 0);
        grid.Controls.Add(Wrap("Результат", _picRes), 1, 0);
        grid.Controls.Add(Wrap("Гистограмма: исходное", _histSrc), 0, 1);
        grid.Controls.Add(Wrap("Гистограмма: результат", _histRes), 1, 1);

        Controls.Add(grid);
        Controls.Add(_status);
        Controls.Add(top);

        _cmb.SelectedIndexChanged += (_, _) => UpdateParams();
        _cmb.SelectedIndex = 0;
        _btnOpen.Click += OnOpen;
        _btnSave.Click += OnSave;
        _btnTest.Click += OnTest;
        _btnApply.Click += OnApply;
        _status.Text = "Откройте изображение или создайте тестовые (кнопка сверху).";
    }

    static GroupBox Wrap(string title, Control c)
    {
        var gb = new GroupBox { Text = title, Dock = DockStyle.Fill };
        gb.Controls.Add(c);
        return gb;
    }

    void UpdateParams()
    {
        var m = _methods[_cmb.SelectedIndex];
        _lbl1.Text = m.P1 ?? ""; _num1.Enabled = m.P1 != null && !_busy; _num1.Value = (decimal)m.D1;
        _lbl2.Text = m.P2 ?? ""; _num2.Enabled = m.P2 != null && !_busy; _num2.Value = (decimal)m.D2;
    }
    
    void OnOpen(object? s, EventArgs e)
    {
        if (_busy) return;

        using var dlg = new OpenFileDialog { Filter = "Изображения|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff|Все файлы|*.*" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        _ = LoadImageAsync(dlg.FileName);
    }

    async Task LoadImageAsync(string path)
    {
        string name = Path.GetFileName(path);
        SetBusy(true);
        _status.Text = $"Загрузка {name}…";
        try
        {
            var (src, hSrc, bmp, w, h, st) = await Task.Run(() =>
            {
                using var b = new Bitmap(path);
                var p = PixelBuffer.FromBitmap(b);
                var hh = Histogram.Compute(p);
                var stt = Histogram.Stats(hh[3]);
                var bm = p.ToBitmap();
                return (p, hh, bm, p.Width, p.Height, stt);
            });

            _src = src;
            _hSrc = hSrc;
            _picSrc.Image?.Dispose();
            _picSrc.Image = bmp;
            _histSrc.SetData(_hSrc, "серое — яркость; цветные линии — B/G/R");

            _picRes.Image?.Dispose(); _picRes.Image = null; _res = null;
            _histRes.SetData(null, "");

            _status.Text = $"{name}: {w}×{h}. Исходное: {Fmt(st)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка загрузки");
        }
        finally { SetBusy(false); }
    }

    void OnSave(object? s, EventArgs e)
    {
        if (_res == null) return;
        using var dlg = new SaveFileDialog { Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp" };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        var fmt = dlg.FilterIndex == 2 ? ImageFormat.Jpeg : dlg.FilterIndex == 3 ? ImageFormat.Bmp : ImageFormat.Png;
        using var bmp = _res.ToBitmap();
        bmp.Save(dlg.FileName, fmt);
    }

    void OnTest(object? s, EventArgs e)
    {
        if (_busy) return;
        SetBusy(true);
        try
        {
            var dir = TestImages.Generate(Path.Combine(AppContext.BaseDirectory, "TestImages"));
            MessageBox.Show("Тестовые изображения созданы в папке:\n" + dir, "Готово");
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "Ошибка"); }
        finally { SetBusy(false); }
    }

    async void OnApply(object? s, EventArgs e)
    {
        if (_busy) return;
        if (_src == null || _hSrc == null) { MessageBox.Show("Сначала откройте изображение."); return; }

        var m = _methods[_cmb.SelectedIndex];
        double a = (double)_num1.Value, b = (double)_num2.Value;
        var src = _src;
        var hSrc = _hSrc;

        SetBusy(true);
        try
        {
            var (res, hist, ms) = await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                var r = m.Run(src, a, b);
                sw.Stop();
                var hh = Histogram.Compute(r);
                return (r, hh, sw.ElapsedMilliseconds);
            });

            _res = res;
            _picRes.Image?.Dispose();
            _picRes.Image = res.ToBitmap();
            _histRes.SetData(hist, m.Name);
            _status.Text = $"{m.Name}: {ms} мс.  Исходное: {Fmt(Histogram.Stats(hSrc[3]))}   |   Результат: {Fmt(Histogram.Stats(hist[3]))}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка применения");
        }
        finally { SetBusy(false); }
    }

    static string Fmt((double Mean, double Std, double Entropy, int Min, int Max) st) =>
        $"μ={st.Mean:F1}, σ={st.Std:F1}, H={st.Entropy:F2} бит, диапазон [{st.Min}..{st.Max}]";

    void SetBusy(bool busy)
    {
        _busy = busy;
        _progress.Visible = busy;
        _btnApply.Enabled = !busy;
        _btnOpen.Enabled = !busy;
        _btnTest.Enabled = !busy;
        _btnSave.Enabled = !busy;
        _cmb.Enabled = !busy;

        var m = _methods[_cmb.SelectedIndex];
        _num1.Enabled = !busy && m.P1 != null;
        _num2.Enabled = !busy && m.P2 != null;

        UseWaitCursor = busy;
    }
}