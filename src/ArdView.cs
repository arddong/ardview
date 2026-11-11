// arddong ArdView = 광고 없는 이미지 보기(빠른 넘기기·확대·전체 화면)
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic.FileIO;
using SkiaSharp;

namespace ArdView;

// 열어 둔 그림 한 장 = 프레임(움직이는 그림이면 여럿) + 프레임 시간 + 축소본 사다리
public sealed class Picture : IDisposable
{
    public Bitmap[] Frames { get; private set; }
    public int[] Delays { get; }
    public string Format { get; }
    public long FileSize { get; }
    List<Bitmap>? _levels;

    public Picture(Bitmap[] frames, int[] delays, string format, long fileSize)
    {
        Frames = frames; Delays = delays; Format = format; FileSize = fileSize;
    }

    public int Width => Frames[0].Width;
    public int Height => Frames[0].Height;
    public bool Animated => Frames.Length > 1;

    // 큰 그림을 줄여 볼 때마다 원본에서 줄이면 느리다. 반씩 줄인 사본을 미리 만들어 두고 알맞은 층에서 그린다
    public IReadOnlyList<Bitmap> Levels
    {
        get
        {
            if (_levels != null) return _levels;
            var list = new List<Bitmap> { Frames[0] };
            if (!Animated)
            {
                var cur = Frames[0];
                while (cur.Width >= 1024 && cur.Height >= 1024)
                {
                    var next = new Bitmap(cur.Width / 2, cur.Height / 2, PixelFormat.Format32bppPArgb);
                    using (var g = Graphics.FromImage(next))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.PixelOffsetMode = PixelOffsetMode.Half;
                        g.CompositingMode = CompositingMode.SourceCopy;
                        using var wrap = new ImageAttributes();
                        wrap.SetWrapMode(WrapMode.TileFlipXY);
                        g.DrawImage(cur, new Rectangle(0, 0, next.Width, next.Height), 0, 0, cur.Width, cur.Height, GraphicsUnit.Pixel, wrap);
                    }
                    list.Add(next);
                    cur = next;
                }
            }
            return _levels = list;
        }
    }

    // 보는 동안 돌리기. 파일은 건드리지 않는다
    public void Rotate(bool right)
    {
        foreach (var f in Frames) f.RotateFlip(right ? RotateFlipType.Rotate90FlipNone : RotateFlipType.Rotate270FlipNone);
        DropLevels();
    }

    void DropLevels()
    {
        if (_levels == null) return;
        foreach (var l in _levels.Skip(1)) l.Dispose();
        _levels = null;
    }

    public void Dispose()
    {
        DropLevels();
        foreach (var f in Frames) f.Dispose();
    }
}

// ───────────────────────── 엔진 (열기·목록·순서) ─────────────────────────
public static class Engine
{
    public static readonly string[] Exts =
        { ".jpg", ".jpeg", ".jfif", ".jpe", ".png", ".gif", ".webp", ".bmp", ".dib", ".ico", ".tif", ".tiff", ".wbmp" };

    public static bool IsImage(string p) => Exts.Contains(Path.GetExtension(p).ToLowerInvariant());

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    static extern int StrCmpLogicalW(string a, string b);

    // 탐색기와 같은 순서 = 사진2 가 사진10 보다 앞
    public static int NaturalCompare(string a, string b) => StrCmpLogicalW(a, b);

    public static List<string> Siblings(string file)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(file))!;
        var list = Directory.EnumerateFiles(dir).Where(IsImage).ToList();
        list.Sort((a, b) => NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)));
        return list;
    }

    // 움직이는 그림이 너무 크면 첫 장만 (메모리 보호)
    const long MaxAnimBytes = 600L << 20;

    public static Picture Load(string path)
    {
        var bytes = File.ReadAllBytes(path);
        using (var data = SKData.CreateCopy(bytes))
        using (var codec = SKCodec.Create(data))
        {
            if (codec != null)
            {
                var pic = LoadSkia(codec, bytes.LongLength);
                if (pic != null) return pic;
            }
        }
        return LoadGdi(bytes);
    }

    static Picture? LoadSkia(SKCodec codec, long size)
    {
        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        var fmt = codec.EncodedFormat.ToString().ToUpperInvariant();
        int n = Math.Max(1, codec.FrameCount);
        if ((long)info.BytesSize * n > MaxAnimBytes) n = 1;
        var fi = codec.FrameInfo;
        var sk = new SKBitmap[n];
        var delays = new int[n];
        try
        {
            for (int i = 0; i < n; i++)
            {
                int req = n > 1 && i < fi.Length ? fi[i].RequiredFrame : -1;
                sk[i] = req >= 0 && req < i ? sk[req].Copy() : new SKBitmap(info);
                if (req < 0) sk[i].Erase(SKColors.Transparent);
                var opt = n > 1 ? new SKCodecOptions(i, req) : new SKCodecOptions();
                var r = codec.GetPixels(info, sk[i].GetPixels(), opt);
                if (r != SKCodecResult.Success && r != SKCodecResult.IncompleteInput)
                {
                    if (i == 0) return null;
                    n = i; break;
                }
                var d = n > 1 && i < fi.Length ? fi[i].Duration : 0;
                delays[i] = d <= 10 ? 100 : d;
            }
            var frames = new Bitmap[n];
            for (int i = 0; i < n; i++) frames[i] = ToGdi(sk[i]);
            var flip = Orient(codec.EncodedOrigin);
            if (flip != RotateFlipType.RotateNoneFlipNone) foreach (var f in frames) f.RotateFlip(flip);
            return new Picture(frames, delays[..n], fmt, size);
        }
        finally
        {
            foreach (var b in sk) b?.Dispose();
        }
    }

    static unsafe Bitmap ToGdi(SKBitmap src)
    {
        var bmp = new Bitmap(src.Width, src.Height, PixelFormat.Format32bppPArgb);
        var bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, bmp.PixelFormat);
        try
        {
            var s = (byte*)src.GetPixels();
            var d = (byte*)bd.Scan0;
            long row = src.Width * 4L;
            for (int y = 0; y < src.Height; y++)
                Buffer.MemoryCopy(s + (long)y * src.RowBytes, d + (long)y * bd.Stride, bd.Stride, row);
        }
        finally { bmp.UnlockBits(bd); }
        return bmp;
    }

    // EXIF 방향값 → 바로 세우는 돌리기
    public static RotateFlipType Orient(SKEncodedOrigin o) => o switch
    {
        SKEncodedOrigin.TopRight => RotateFlipType.RotateNoneFlipX,
        SKEncodedOrigin.BottomRight => RotateFlipType.Rotate180FlipNone,
        SKEncodedOrigin.BottomLeft => RotateFlipType.RotateNoneFlipY,
        SKEncodedOrigin.LeftTop => RotateFlipType.Rotate90FlipX,
        SKEncodedOrigin.RightTop => RotateFlipType.Rotate90FlipNone,
        SKEncodedOrigin.RightBottom => RotateFlipType.Rotate270FlipX,
        SKEncodedOrigin.LeftBottom => RotateFlipType.Rotate270FlipNone,
        _ => RotateFlipType.RotateNoneFlipNone,
    };

    // Skia 가 못 여는 것(tiff 등)은 윈도우 기본 디코더로
    static Picture LoadGdi(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var img = Image.FromStream(ms);
        var fmt = img.RawFormat.Equals(ImageFormat.Tiff) ? "TIFF" : img.RawFormat.Equals(ImageFormat.Icon) ? "ICO" : "IMAGE";
        var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppPArgb);
        using (var g = Graphics.FromImage(bmp)) g.DrawImage(img, 0, 0, img.Width, img.Height);
        if (img.PropertyIdList.Contains(0x0112))
        {
            var v = img.GetPropertyItem(0x0112)?.Value;
            if (v is { Length: > 0 }) { var f = Orient((SKEncodedOrigin)v[0]); if (f != RotateFlipType.RotateNoneFlipNone) bmp.RotateFlip(f); }
        }
        return new Picture(new[] { bmp }, new[] { 0 }, fmt, bytes.LongLength);
    }
}

// ───────────────────────── 보기 계산 (화면에 맞추기·커서 기준 확대) ─────────────────────────
public static class ViewMath
{
    public static readonly float[] Steps =
        { 0.02f, 0.05f, 0.1f, 0.15f, 0.2f, 0.25f, 0.33f, 0.5f, 0.67f, 0.75f, 1f, 1.25f, 1.5f, 2f, 3f, 4f, 6f, 8f, 12f, 16f, 24f, 32f };

    // 화면보다 큰 그림만 줄인다. 작은 그림은 키우지 않는다(꿀뷰 기본)
    public static float Fit(int w, int h, int vw, int vh) =>
        w <= 0 || h <= 0 || vw <= 0 || vh <= 0 ? 1f : Math.Min(1f, Math.Min((float)vw / w, (float)vh / h));

    public static float Next(float z, bool up)
    {
        if (up) { foreach (var s in Steps) if (s > z * 1.001f) return s; return Steps[^1]; }
        for (int i = Steps.Length - 1; i >= 0; i--) if (Steps[i] < z * 0.999f) return Steps[i];
        return Steps[0];
    }

    // 커서 밑의 점이 그대로 남도록 새 위치를 구한다
    public static PointF ZoomAt(PointF offset, float oldZ, float newZ, PointF cursor) =>
        new(cursor.X - (cursor.X - offset.X) * newZ / oldZ, cursor.Y - (cursor.Y - offset.Y) * newZ / oldZ);

    // 그림이 화면보다 작으면 가운데, 크면 빈틈이 안 생기게 가둔다
    public static PointF Clamp(PointF o, float iw, float ih, int vw, int vh)
    {
        float x = iw <= vw ? (vw - iw) / 2 : Math.Min(0, Math.Max(vw - iw, o.X));
        float y = ih <= vh ? (vh - ih) / 2 : Math.Min(0, Math.Max(vh - ih, o.Y));
        return new PointF(x, y);
    }
}

// ───────────────────────── 화면 ─────────────────────────
static class Ui
{
    public static readonly Color Bg = Color.FromArgb(24, 24, 26);
    public static readonly Color Bar = Color.FromArgb(36, 37, 40);
    public static readonly Color Ink = Color.FromArgb(236, 236, 238);
    public static readonly Color Sub = Color.FromArgb(150, 153, 160);
    public static readonly Font Body = new("맑은 고딕", 9.5f);
    public static readonly Font Hint = new("맑은 고딕", 11f);
    public const string ReportUrl = "https://chal-na.notion.site/2550a4124c6843fcb1086c9edb7d75f3";

    // 오류 창 = 「예」를 누르면 오류 내용을 클립보드에 넣고 노션 신고 양식을 연다 (ArdZip 과 같은 창구)
    public static void ShowError(IWin32Window? owner, Exception ex, string doing)
    {
        var ver = typeof(Ui).Assembly.GetName().Version?.ToString(3) ?? "?";
        var report = $"ArdView {ver} / {Environment.OSVersion.VersionString}\n하던 일: {doing}\n오류: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}";
        var r = MessageBox.Show(owner, ex.Message + "\n\n이 오류를 개발자에게 보낼까요?\n「예」를 누르면 오류 내용이 복사되고 신고 화면이 열립니다. 「오류 내용」 칸에 Ctrl+V 로 붙여 넣어 주세요.", "ArdView", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (r != DialogResult.Yes) return;
        try { Clipboard.SetText(report); } catch { }
        Process.Start(new ProcessStartInfo(ReportUrl) { UseShellExecute = true });
    }

    public static string Size(long b) => b < 1024 ? $"{b} B" : b < 1 << 20 ? $"{b / 1024.0:0.#} KB" : b < 1 << 30 ? $"{b / 1048576.0:0.#} MB" : $"{b / 1073741824.0:0.##} GB";

    public static Icon? AppIcon => Icon.ExtractAssociatedIcon(Application.ExecutablePath);
}

// 그림을 그리는 판
class Canvas : Control
{
    public Picture? Pic;
    public int Frame;
    public float Zoom = 1f;
    public bool FitMode = true;
    public PointF Offset;
    public string Message = "";
    public string Toast = "";

    public Canvas()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Ui.Bg;
    }

    public void Relayout()
    {
        if (Pic == null) return;
        if (FitMode) Zoom = ViewMath.Fit(Pic.Width, Pic.Height, ClientSize.Width, ClientSize.Height);
        Offset = ViewMath.Clamp(Offset, Pic.Width * Zoom, Pic.Height * Zoom, ClientSize.Width, ClientSize.Height);
        Invalidate();
    }

    public void SetZoom(float z, PointF at)
    {
        if (Pic == null) return;
        z = Math.Clamp(z, ViewMath.Steps[0], ViewMath.Steps[^1]);
        Offset = ViewMath.ZoomAt(Offset, Zoom, z, at);
        Zoom = z; FitMode = false;
        Offset = ViewMath.Clamp(Offset, Pic.Width * Zoom, Pic.Height * Zoom, ClientSize.Width, ClientSize.Height);
        Invalidate();
    }

    public void Pan(float dx, float dy)
    {
        if (Pic == null) return;
        Offset = ViewMath.Clamp(new PointF(Offset.X + dx, Offset.Y + dy), Pic.Width * Zoom, Pic.Height * Zoom, ClientSize.Width, ClientSize.Height);
        Invalidate();
    }

    protected override bool IsInputKey(Keys k) => true;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        if (Pic != null)
        {
            Bitmap src = Pic.Frames[Math.Min(Frame, Pic.Frames.Length - 1)];
            float scale = 1f;
            if (Frame == 0 && !Pic.Animated && Zoom < 1f)
            {
                // 그릴 크기 이상인 가장 작은 층을 고른다
                foreach (var l in Pic.Levels)
                    if (l.Width >= Pic.Width * Zoom - 0.5f) { src = l; scale = (float)l.Width / Pic.Width; }
            }
            var dest = new RectangleF(Offset.X, Offset.Y, Pic.Width * Zoom, Pic.Height * Zoom);
            var vis = RectangleF.Intersect(dest, new RectangleF(0, 0, ClientSize.Width, ClientSize.Height));
            if (vis.Width > 0 && vis.Height > 0)
            {
                // 보이는 부분만 원본에서 떼어 그린다 = 크게 확대해도 느려지지 않는다
                float k = scale / Zoom;
                var srcRect = new RectangleF((vis.X - dest.X) * k, (vis.Y - dest.Y) * k, vis.Width * k, vis.Height * k);
                g.InterpolationMode = Zoom >= 3f ? InterpolationMode.NearestNeighbor : Math.Abs(Zoom - 1f) < 0.001f ? InterpolationMode.NearestNeighbor : InterpolationMode.HighQualityBilinear;
                g.PixelOffsetMode = PixelOffsetMode.Half;
                g.CompositingQuality = CompositingQuality.HighSpeed;
                using var wrap = new ImageAttributes();
                wrap.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(src, Rectangle.Round(vis), srcRect.X, srcRect.Y, srcRect.Width, srcRect.Height, GraphicsUnit.Pixel, wrap);
            }
        }
        if (Message.Length > 0)
            TextRenderer.DrawText(g, Message, Ui.Hint, ClientRectangle, Ui.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        if (Toast.Length > 0)
        {
            var sz = TextRenderer.MeasureText(Toast, Ui.Hint);
            var r = new Rectangle((ClientSize.Width - sz.Width) / 2 - 16, ClientSize.Height / 2 - sz.Height / 2 - 10, sz.Width + 32, sz.Height + 20);
            using var b = new SolidBrush(Color.FromArgb(200, 0, 0, 0));
            g.FillRectangle(b, r);
            TextRenderer.DrawText(g, Toast, Ui.Hint, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}

// ───────────────────────── 본 창 ─────────────────────────
class MainForm : Form
{
    readonly Canvas _cv = new() { Dock = DockStyle.Fill };
    readonly Label _bar = new() { Dock = DockStyle.Bottom, Height = 26, BackColor = Ui.Bar, ForeColor = Ui.Sub, Font = Ui.Body, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 10, 0), AutoEllipsis = true };
    readonly System.Windows.Forms.Timer _anim = new();
    readonly System.Windows.Forms.Timer _toastTimer = new() { Interval = 1200 };
    readonly Dictionary<string, Task<Picture>> _cache = new(StringComparer.OrdinalIgnoreCase);
    List<string> _files = new();
    int _idx = -1;
    int _ticket;
    Point _drag;
    bool _dragging;
    bool _full;
    FormWindowState _prevState;
    Rectangle _prevBounds;

    static string StatePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArdView", "window.txt");

    public MainForm(string? open)
    {
        Text = "ArdView"; Icon = Ui.AppIcon; BackColor = Ui.Bg; Font = Ui.Body; KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(1100, 760); MinimumSize = new Size(360, 260);
        AllowDrop = true;
        Controls.Add(_cv); Controls.Add(_bar);
        LoadWindowState();
        _cv.Message = "그림 파일을 끌어다 놓으세요\n\n두 번 누르면 파일을 고릅니다 · 오른쪽 클릭 = 메뉴";
        _cv.ContextMenuStrip = BuildMenu();

        _anim.Tick += (_, _) => NextFrame();
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); _cv.Toast = ""; _cv.Invalidate(); };
        _cv.Resize += (_, _) => _cv.Relayout();
        _cv.MouseWheel += OnWheel;
        _cv.MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { _drag = e.Location; _dragging = true; } };
        _cv.MouseMove += (_, e) =>
        {
            if (!_dragging) return;
            _cv.Pan(e.X - _drag.X, e.Y - _drag.Y); _drag = e.Location;
            _cv.Cursor = Cursors.SizeAll;
        };
        _cv.MouseUp += (_, _) => { _dragging = false; _cv.Cursor = Cursors.Default; };
        _cv.MouseDoubleClick += (_, e) => { if (e.Button != MouseButtons.Left) return; if (_cv.Pic == null) OpenDialog(); else ToggleFull(); };
        _cv.MouseUp += (_, e) => { if (e.Button == MouseButtons.XButton1) Go(-1); if (e.Button == MouseButtons.XButton2) Go(1); };
        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } f) OpenPath(f[0]); };
        FormClosing += (_, _) => SaveWindowState();
        Shown += (_, _) => { _cv.Focus(); if (open != null) OpenPath(open); };
        UpdateBar();
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

    // 제목줄도 어둡게 = 그림 둘레가 한 색
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        int on = 1;
        try { DwmSetWindowAttribute(Handle, 20, ref on, 4); } catch { }
    }

    ContextMenuStrip BuildMenu()
    {
        var m = new ContextMenuStrip { Font = Ui.Body, ShowImageMargin = false };
        void Add(string text, string keys, Action act) { var it = new ToolStripMenuItem(text, null, (_, _) => act()) { ShortcutKeyDisplayString = keys }; m.Items.Add(it); }
        Add("열기", "Ctrl+O", OpenDialog);
        Add("이전 그림", "←", () => Go(-1));
        Add("다음 그림", "→", () => Go(1));
        m.Items.Add(new ToolStripSeparator());
        Add("화면에 맞추기", "0", Fit);
        Add("원래 크기", "1", () => _cv.SetZoom(1f, Center()));
        Add("확대", "+", () => _cv.SetZoom(ViewMath.Next(_cv.Zoom, true), Center()));
        Add("축소", "-", () => _cv.SetZoom(ViewMath.Next(_cv.Zoom, false), Center()));
        Add("전체 화면", "Enter", ToggleFull);
        m.Items.Add(new ToolStripSeparator());
        Add("오른쪽으로 돌리기", "R", () => Rotate(true));
        Add("왼쪽으로 돌리기", "L", () => Rotate(false));
        Add("그림 복사", "Ctrl+C", CopyImage);
        Add("폴더에서 보기", "Ctrl+E", ShowInFolder);
        Add("휴지통으로", "Delete", DeleteCurrent);
        m.Items.Add(new ToolStripSeparator());
        Add("버그 신고 · 기능 요청", "", () => Process.Start(new ProcessStartInfo(Ui.ReportUrl) { UseShellExecute = true }));
        return m;
    }

    PointF Center() => new(_cv.ClientSize.Width / 2f, _cv.ClientSize.Height / 2f);

    void Fit() { _cv.FitMode = true; _cv.Relayout(); UpdateBar(); }

    void OnWheel(object? s, MouseEventArgs e)
    {
        if (ModifierKeys.HasFlag(Keys.Control)) { _cv.SetZoom(ViewMath.Next(_cv.Zoom, e.Delta > 0), e.Location); UpdateBar(); }
        else Go(e.Delta > 0 ? -1 : 1);
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var c = Center();
        switch (keyData)
        {
            case Keys.Left: case Keys.PageUp: case Keys.Back: Go(-1); return true;
            case Keys.Right: case Keys.PageDown: case Keys.Space: Go(1); return true;
            case Keys.Home: GoTo(0); return true;
            case Keys.End: GoTo(_files.Count - 1); return true;
            case Keys.Up: _cv.Pan(0, 80); return true;
            case Keys.Down: _cv.Pan(0, -80); return true;
            case Keys.Oemplus: case Keys.Add: case Keys.Shift | Keys.Oemplus: _cv.SetZoom(ViewMath.Next(_cv.Zoom, true), c); UpdateBar(); return true;
            case Keys.OemMinus: case Keys.Subtract: _cv.SetZoom(ViewMath.Next(_cv.Zoom, false), c); UpdateBar(); return true;
            case Keys.D0: case Keys.NumPad0: case Keys.Multiply: Fit(); return true;
            case Keys.D1: case Keys.NumPad1: case Keys.Divide: _cv.SetZoom(1f, c); UpdateBar(); return true;
            case Keys.Enter: case Keys.F11: ToggleFull(); return true;
            case Keys.Escape: if (_full) ToggleFull(); else Close(); return true;
            case Keys.R: case Keys.Control | Keys.R: Rotate(true); return true;
            case Keys.L: case Keys.Control | Keys.L: Rotate(false); return true;
            case Keys.Delete: DeleteCurrent(); return true;
            case Keys.Control | Keys.C: CopyImage(); return true;
            case Keys.Control | Keys.O: OpenDialog(); return true;
            case Keys.Control | Keys.E: ShowInFolder(); return true;
            case Keys.F5: Reload(); return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ── 열기·넘기기 ──
    public void OpenPath(string p)
    {
        try
        {
            string? file = Directory.Exists(p) ? Engine.Siblings(Path.Combine(p, "_")).FirstOrDefault() : p;
            if (file == null) { ShowToast("이 폴더에 그림이 없습니다"); return; }
            _files = Engine.Siblings(file);
            var i = _files.FindIndex(f => string.Equals(f, Path.GetFullPath(file), StringComparison.OrdinalIgnoreCase));
            if (i < 0) { _files.Insert(0, Path.GetFullPath(file)); i = 0; }
            GoTo(i);
        }
        catch (Exception ex) { Ui.ShowError(this, ex, "열기 " + p); }
    }

    void OpenDialog()
    {
        using var d = new OpenFileDialog { Title = "그림 열기", Filter = "그림 파일|" + string.Join(";", Engine.Exts.Select(x => "*" + x)) + "|모든 파일|*.*" };
        if (_idx >= 0) d.InitialDirectory = Path.GetDirectoryName(_files[_idx]);
        if (d.ShowDialog(this) == DialogResult.OK) OpenPath(d.FileName);
    }

    void Reload()
    {
        if (_idx < 0) return;
        var cur = _files[_idx];
        _cache.Remove(cur);
        OpenPath(cur);
    }

    void Go(int step)
    {
        if (_files.Count == 0) return;
        int n = _idx + step;
        if (n < 0) { ShowToast("첫 그림입니다"); return; }
        if (n >= _files.Count) { ShowToast("마지막 그림입니다"); return; }
        GoTo(n, step);
    }

    Task<Picture> Get(string path)
    {
        if (!_cache.TryGetValue(path, out var t))
        {
            t = Task.Run(() =>
            {
                var p = Engine.Load(path);
                _ = p.Levels; // 축소본 사다리도 미리 만든다
                return p;
            });
            _cache[path] = t;
        }
        return t;
    }

    async void GoTo(int i, int dir = 1)
    {
        if (i < 0 || i >= _files.Count) return;
        _idx = i;
        var path = _files[i];
        var ticket = ++_ticket;
        _anim.Stop();
        Text = Path.GetFileName(path) + " - ArdView";
        var task = Get(path);
        if (!task.IsCompleted) { _cv.Message = ""; UpdateBar("여는 중…"); }
        Picture pic;
        try { pic = await task; }
        catch (Exception ex)
        {
            _cache.Remove(path);
            if (ticket != _ticket) return;
            _cv.Pic = null; _cv.Message = Path.GetFileName(path) + "\n\n열 수 없는 그림입니다 (" + ex.Message + ")"; _cv.Invalidate();
            UpdateBar();
            return;
        }
        if (ticket != _ticket) return;
        _cv.Pic = pic; _cv.Frame = 0; _cv.Message = ""; _cv.FitMode = true; _cv.Offset = PointF.Empty;
        _cv.Relayout();
        if (pic.Animated) { _anim.Interval = pic.Delays[0]; _anim.Start(); }
        UpdateBar();
        Prefetch(dir);
    }

    // 앞뒤 그림을 미리 읽어 두고, 멀어진 것은 버린다 = 넘길 때 기다리지 않는다
    void Prefetch(int dir)
    {
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in new[] { 0, dir, -dir, 2 * dir })
        {
            int j = _idx + d;
            if (j >= 0 && j < _files.Count) keep.Add(_files[j]);
        }
        foreach (var k in _cache.Keys.Where(k => !keep.Contains(k)).ToList())
        {
            var t = _cache[k];
            _cache.Remove(k);
            if (t.IsCompletedSuccessfully && t.Result != _cv.Pic) t.Result.Dispose();
            else if (!t.IsCompleted) t.ContinueWith(x => { if (x.IsCompletedSuccessfully) x.Result.Dispose(); });
        }
        foreach (var k in keep) Get(k);
    }

    void NextFrame()
    {
        var p = _cv.Pic;
        if (p == null || !p.Animated) { _anim.Stop(); return; }
        _cv.Frame = (_cv.Frame + 1) % p.Frames.Length;
        _anim.Interval = Math.Max(20, p.Delays[_cv.Frame]);
        _cv.Invalidate();
    }

    // ── 동작 ──
    void Rotate(bool right)
    {
        if (_cv.Pic == null) return;
        _anim.Stop();
        _cv.Pic.Rotate(right);
        _cv.FitMode = true; _cv.Relayout();
        if (_cv.Pic.Animated) _anim.Start();
        UpdateBar();
    }

    void CopyImage()
    {
        if (_cv.Pic == null) return;
        try
        {
            using var b = new Bitmap(_cv.Pic.Frames[_cv.Frame]);
            Clipboard.SetImage(b);
            ShowToast("그림을 복사했습니다");
        }
        catch (Exception ex) { Ui.ShowError(this, ex, "그림 복사"); }
    }

    void ShowInFolder()
    {
        if (_idx < 0) return;
        Process.Start("explorer.exe", $"/select,\"{_files[_idx]}\"");
    }

    void DeleteCurrent()
    {
        if (_idx < 0) return;
        var path = _files[_idx];
        if (MessageBox.Show(this, Path.GetFileName(path) + "\n\n휴지통으로 보낼까요?", "ArdView", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        try
        {
            FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { Ui.ShowError(this, ex, "휴지통으로 " + Path.GetFileName(path)); return; }
        if (_cache.Remove(path, out var t) && t.IsCompletedSuccessfully && t.Result != _cv.Pic) t.Result.Dispose();
        _files.RemoveAt(_idx);
        if (_files.Count == 0)
        {
            _idx = -1; _cv.Pic = null; _anim.Stop(); _cv.Message = "이 폴더에 남은 그림이 없습니다"; _cv.Invalidate(); Text = "ArdView"; UpdateBar();
            return;
        }
        GoTo(Math.Min(_idx, _files.Count - 1));
    }

    void ToggleFull()
    {
        if (!_full)
        {
            _prevState = WindowState; _prevBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            _bar.Visible = false; FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal; Bounds = Screen.FromControl(this).Bounds;
            _cv.BackColor = Color.Black;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable; _bar.Visible = true; _cv.BackColor = Ui.Bg;
            Bounds = _prevBounds; WindowState = _prevState;
        }
        _full = !_full;
        _cv.Relayout();
    }

    void ShowToast(string s)
    {
        _cv.Toast = s; _cv.Invalidate();
        _toastTimer.Stop(); _toastTimer.Start();
    }

    void UpdateBar(string? extra = null)
    {
        if (_idx < 0 || _files.Count == 0) { _bar.Text = "ArdView · ← → 넘기기 · Ctrl+휠 확대 · Enter 전체 화면 · 오른쪽 클릭 메뉴"; return; }
        var parts = new List<string> { $"{_idx + 1} / {_files.Count}", Path.GetFileName(_files[_idx]) };
        if (extra != null) parts.Add(extra);
        else if (_cv.Pic is { } p)
        {
            parts.Add($"{p.Width} × {p.Height}");
            parts.Add(Ui.Size(p.FileSize));
            parts.Add(p.Format + (p.Animated ? $" · {p.Frames.Length}장" : ""));
            parts.Add($"{_cv.Zoom * 100:0}%");
        }
        _bar.Text = string.Join("   ·   ", parts);
    }

    // ── 창 크기 기억 ──
    void LoadWindowState()
    {
        try
        {
            var v = File.ReadAllText(StatePath).Split(',').Select(int.Parse).ToArray();
            var r = new Rectangle(v[0], v[1], v[2], v[3]);
            if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(r)))
            {
                StartPosition = FormStartPosition.Manual; Bounds = r;
                if (v[4] == 1) WindowState = FormWindowState.Maximized;
            }
        }
        catch { }
    }

    void SaveWindowState()
    {
        try
        {
            var r = _full ? _prevBounds : WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
            var max = (_full ? _prevState : WindowState) == FormWindowState.Maximized ? 1 : 0;
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            File.WriteAllText(StatePath, $"{r.X},{r.Y},{r.Width},{r.Height},{max}");
        }
        catch { }
    }
}

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Ui.ShowError(null, e.Exception, "보기");
        Application.Run(new MainForm(args.FirstOrDefault()));
    }
}
