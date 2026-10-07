using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace ImageInfoLab;

public sealed class MainForm : Form
{
    static readonly HashSet<string> Exts = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".jpe", ".jfif", ".gif", ".tif", ".tiff", ".bmp", ".dib", ".png", ".pcx" };
    static readonly string[] Heads = { "Имя файла", "Формат", "Размер (px)", "Разрешение (dpi)", "Глубина цвета (бит)", "Сжатие", "Размер файла", "Статус" };
    static readonly int[] Widths = { 220, 60, 100, 100, 80, 170, 90, 300 };

    readonly TextBox txtPath = new();
    readonly Button btnBrowse = new() { Text = "Обзор…" }, btnStart = new() { Text = "Сканировать" }, btnCancel = new() { Text = "Стоп", Enabled = false };
    readonly CheckBox chkRec = new() { Text = "Подпапки", Checked = true, AutoSize = true };
    readonly CheckBox chkAll = new() { Text = "Все файлы (по содержимому)", AutoSize = true };
    readonly DataGridView grid = new();
    readonly ProgressBar progress = new();
    readonly Label lblStatus = new() { Text = "Выберите папку", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft };
    readonly PictureBox pic = new() { SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle };
    readonly TextBox txtDetails = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Font = new Font("Consolas", 9f) };
    readonly System.Windows.Forms.Timer uiTimer = new() { Interval = 150 };
    readonly Stopwatch sw = new();

    ImageInfo[] results = Array.Empty<ImageInfo>();
    string[] files = Array.Empty<string>();
    int done, selToken;
    bool running;
    CancellationTokenSource cts;

    public MainForm()
    {
        Text = "Лабораторная 2 — информация о графических файлах";
        Width = 1350; Height = 780; StartPosition = FormStartPosition.CenterScreen;


        var top = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6 };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        txtPath.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        chkRec.Anchor = chkAll.Anchor = AnchorStyles.Left;
        foreach (var b in new[] { btnBrowse, btnStart, btnCancel }) b.Dock = DockStyle.Fill;
        top.Controls.AddRange(new Control[] { txtPath, btnBrowse, chkRec, chkAll, btnStart, btnCancel });


        grid.Dock = DockStyle.Fill; grid.VirtualMode = true; grid.ReadOnly = true;
        grid.AllowUserToAddRows = grid.AllowUserToDeleteRows = grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false; grid.MultiSelect = false; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None; grid.BackgroundColor = SystemColors.Window;
        for (int c = 0; c < Heads.Length; c++)
            grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = Heads[c], Width = Widths[c], SortMode = DataGridViewColumnSortMode.Programmatic });
        grid.CellValueNeeded += (s, e) => e.Value = CellText(e.RowIndex, e.ColumnIndex);
        grid.SelectionChanged += (s, e) => { if (grid.CurrentRow != null) ShowDetails(grid.CurrentRow.Index); };


        var right = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        pic.Dock = DockStyle.Fill; txtDetails.Dock = DockStyle.Fill;
        right.Controls.Add(pic, 0, 0); right.Controls.Add(txtDetails, 0, 1);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 850 };
        grid.Parent = split.Panel1; right.Parent = split.Panel2;


        var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        progress.Dock = DockStyle.Fill; lblStatus.Dock = DockStyle.Fill;
        bottom.Controls.Add(progress, 0, 0); bottom.Controls.Add(lblStatus, 1, 0);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        root.Controls.Add(top, 0, 0); root.Controls.Add(split, 0, 1); root.Controls.Add(bottom, 0, 2);
        Controls.Add(root);

        btnBrowse.Click += (s, e) =>
        {
            using var d = new FolderBrowserDialog();
            if (d.ShowDialog(this) == DialogResult.OK) { txtPath.Text = d.SelectedPath; StartScan(); }
        };
        btnStart.Click += (s, e) => StartScan();
        btnCancel.Click += (s, e) => cts?.Cancel();
        txtPath.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; StartScan(); } };
        uiTimer.Tick += (s, e) => UpdateProgress();
    }

    ImageInfo Get(int row)
    {
        var arr = results;
        return row >= 0 && row < arr.Length ? Volatile.Read(ref arr[row]) : null;
    }

    string CellText(int row, int col)
    {
        var r = Get(row);
        if (r == null) return col == 0 && row < files.Length ? Path.GetFileName(files[row]) : "…";
        return col switch
        {
            0 => r.Name, 1 => r.Format, 2 => r.SizeText, 3 => r.DpiText, 4 => r.DepthText,
            5 => r.Compression, 6 => Bytes(r.FileSize), _ => r.Status
        };
    }

    static string Bytes(long b) => b < 1024 ? b + " Б" : b < 1 << 20 ? $"{b / 1024.0:0.#} КБ" : b < 1L << 30 ? $"{b / 1048576.0:0.##} МБ" : $"{b / 1073741824.0:0.##} ГБ";

    void SetRunning(bool r)
    {
        running = r; btnStart.Enabled = btnBrowse.Enabled = !r; btnCancel.Enabled = r;
    }

    async void StartScan()
    {
        if (running) return;
        string dir = txtPath.Text.Trim().Trim('"');
        if (!Directory.Exists(dir)) { MessageBox.Show(this, "Папка не найдена.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }

        SetRunning(true);
        cts = new CancellationTokenSource(); var ct = cts.Token;
        bool rec = chkRec.Checked, all = chkAll.Checked, cancelled = false;
        done = 0; results = Array.Empty<ImageInfo>(); files = Array.Empty<string>();
        grid.RowCount = 0; txtDetails.Clear(); pic.Image = null;
        progress.Style = ProgressBarStyle.Marquee; lblStatus.Text = "Поиск файлов…";
        sw.Restart(); uiTimer.Start();
        try
        {
            await Task.Run(() =>
            {
                var opt = new EnumerationOptions { RecurseSubdirectories = rec, IgnoreInaccessible = true };
                var list = new List<string>();
                foreach (var f in Directory.EnumerateFiles(dir, "*", opt))
                {
                    ct.ThrowIfCancellationRequested();
                    if (all || Exts.Contains(Path.GetExtension(f))) list.Add(f);
                }
                var arr = list.ToArray(); var res = new ImageInfo[arr.Length];
                Invoke(new Action(() =>
                {
                    files = arr; results = res; grid.RowCount = arr.Length;
                    progress.Style = ProgressBarStyle.Continuous; progress.Maximum = Math.Max(1, arr.Length); progress.Value = 0;
                }));
                var po = new ParallelOptions { CancellationToken = ct, MaxDegreeOfParallelism = Math.Max(2, Environment.ProcessorCount) };
                Parallel.For(0, arr.Length, po, i =>
                {
                    var info = HeaderReader.Read(arr[i]);        
                    Volatile.Write(ref res[i], info);
                    Interlocked.Increment(ref done);
                });
            }, ct);
        }
        catch (OperationCanceledException) { cancelled = true; }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Error); }
        finally
        {
            uiTimer.Stop(); sw.Stop(); progress.Style = ProgressBarStyle.Continuous;
            UpdateProgress(); if (cancelled) lblStatus.Text += " (остановлено)";
            grid.Invalidate(); SetRunning(false);
        }
    }

    void UpdateProgress()
    {
        int d = done, n = files.Length;
        if (progress.Style == ProgressBarStyle.Continuous) progress.Value = Math.Min(d, progress.Maximum);
        double sec = Math.Max(0.001, sw.Elapsed.TotalSeconds);
        lblStatus.Text = $"Обработано {d} из {n}  |  {sw.Elapsed:mm\\:ss}  |  {d / sec:0} файл/с";
        grid.Invalidate();
    }

    async void ShowDetails(int row)
    {
        if (row < 0 || row >= files.Length) return;
        int token = ++selToken; string path = files[row];
        var (info, bmp) = await Task.Run(() => (HeaderReader.Read(path), LoadPreview(path)));
        if (token != selToken) { bmp?.Dispose(); return; }
        txtDetails.Text = string.Join(Environment.NewLine, new[]
        {
            "Файл: " + info.FullPath,
            "Формат (по сигнатуре): " + info.Format,
            "Размер: " + info.SizeText + " px",
            "Разрешение: " + info.DpiText + " dpi",
            "Глубина цвета: " + info.DepthText + " бит/пиксель",
            "Сжатие: " + info.Compression,
            "Размер файла: " + Bytes(info.FileSize),
            "Статус: " + info.Status,
            "", "── Дополнительно ──", info.Extra
        });
        var old = pic.Image; pic.Image = bmp; old?.Dispose();
    }


    static Bitmap LoadPreview(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var img = Image.FromStream(fs, false, false);
            double k = Math.Min(1.0, 700.0 / Math.Max(img.Width, img.Height));
            int w = Math.Max(1, (int)(img.Width * k)), h = Math.Max(1, (int)(img.Height * k));
            var bmp = new Bitmap(w, h);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
            g.DrawImage(img, 0, 0, w, h);
            return bmp;
        }
        catch { return null; }   
    }
}
