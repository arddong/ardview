// arddong ArdView 화면 = 보기 계산·그림판(두 쪽·정보창·미리보기 창·효과)
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ArdView;

// ───────────────────────── 보기 계산 (화면에 맞추기·커서 기준 확대) ─────────────────────────
public static class ViewMath
{
    public static readonly float[] Steps =
        { 0.02f, 0.05f, 0.1f, 0.15f, 0.2f, 0.25f, 0.33f, 0.5f, 0.67f, 0.75f, 1f, 1.25f, 1.5f, 2f, 3f, 4f, 6f, 8f, 12f, 16f, 24f, 32f };

    // 화면보다 큰 그림만 줄인다. 작은 그림은 「작은 그림 늘리기(Z)」 를 켰을 때만 키운다
    public static float Fit(float w, float h, int vw, int vh, bool stretch = false)
    {
        if (w <= 0 || h <= 0 || vw <= 0 || vh <= 0) return 1f;
        var z = Math.Min(vw / w, vh / h);
        return stretch ? z : Math.Min(1f, z);
    }

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

    // 두 쪽 보기 = 높이를 맞춰 나란히. 반환은 두 쪽을 합친 크기
    public static SizeF Pair(Size a, Size? b)
    {
        if (b is not { } c) return a;
        float H = Math.Max(a.Height, c.Height);
        return new SizeF(a.Width * H / a.Height + c.Width * H / c.Height, H);
    }
}

// ───────────────────────── 화면 공통 ─────────────────────────
static class Ui
{
    public static readonly Color Bg = Color.FromArgb(24, 24, 26);
    public static readonly Color Bar = Color.FromArgb(36, 37, 40);
    public static readonly Color Ink = Color.FromArgb(236, 236, 238);
    public static readonly Color Sub = Color.FromArgb(150, 153, 160);
    public static readonly Color Accent = Color.FromArgb(79, 134, 247);
    public static readonly Font Body = new("맑은 고딕", 9.5f);
    public static readonly Font Bold = new("맑은 고딕", 9.5f, FontStyle.Bold);
    public static readonly Font Hint = new("맑은 고딕", 11f);
    public const string ReportUrl = "https://chal-na.notion.site/2550a4124c6843fcb1086c9edb7d75f3";
    public const string HomeUrl = "https://arddong.github.io/ardview/";

    // 오류 창 = 「예」를 누르면 오류 내용을 클립보드에 넣고 노션 신고 양식을 연다 (ArdZip 과 같은 창구)
    public static void ShowError(IWin32Window? owner, Exception ex, string doing)
    {
        var ver = typeof(Ui).Assembly.GetName().Version?.ToString(3) ?? "?";
        var report = $"ArdView {ver} / {Environment.OSVersion.VersionString}\n하던 일: {doing}\n오류: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}";
        var r = MessageBox.Show(owner, ex.Message + "\n\n이 오류를 개발자에게 보낼까요?\n「예」를 누르면 오류 내용이 복사되고 신고 화면이 열립니다. 「오류 내용」 칸에 Ctrl+V 로 붙여 넣어 주세요.", "ArdView", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (r != DialogResult.Yes) return;
        try { Clipboard.SetText(report); } catch { }
        Open(ReportUrl);
    }

    public static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    public static string Size(long b) => b < 1024 ? $"{b} B" : b < 1 << 20 ? $"{b / 1024.0:0.#} KB" : b < 1 << 30 ? $"{b / 1048576.0:0.#} MB" : $"{b / 1073741824.0:0.##} GB";

    public static Icon? AppIcon => Icon.ExtractAssociatedIcon(Application.ExecutablePath);
}

public enum Filter { Sharp = 0, Smooth = 1, Soft = 2 }

// ───────────────────────── 그림판 ─────────────────────────
class Canvas : Control
{
    public Picture? Pic;          // 지금 그림 (두 쪽이면 첫 쪽)
    public Picture? Pic2;         // 두 쪽 보기의 둘째 쪽
    public bool Rtl;              // 오른쪽에서 왼쪽 (만화)
    public int Frame;
    public float Zoom = 1f;
    public bool FitMode = true;
    public bool FitWidth;
    public bool Stretch;
    public PointF Offset;
    public string Message = "";
    public string Toast = "";
    public bool ShowInfo;
    public List<(string K, string V)> Info = new();
    public bool ShowNav = true;
    public Filter Filter = Filter.Smooth;
    public bool Invert;
    public float Gamma = 1f;

    public Canvas()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        BackColor = Ui.Bg;
    }

    public SizeF Virtual => Pic == null ? SizeF.Empty : ViewMath.Pair(Pic.Frames[0].Size, Pic2?.Frames[0].Size);

    public void Relayout()
    {
        if (Pic == null) { Invalidate(); return; }
        var v = Virtual;
        if (FitMode) Zoom = FitWidth ? ClientSize.Width / v.Width : ViewMath.Fit(v.Width, v.Height, ClientSize.Width, ClientSize.Height, Stretch);
        Offset = ViewMath.Clamp(Offset, v.Width * Zoom, v.Height * Zoom, ClientSize.Width, ClientSize.Height);
        Invalidate();
    }

    public void SetZoom(float z, PointF at)
    {
        if (Pic == null) return;
        z = Math.Clamp(z, 0.01f, 32f);
        Offset = ViewMath.ZoomAt(Offset, Zoom, z, at);
        Zoom = z; FitMode = false; FitWidth = false;
        var v = Virtual;
        Offset = ViewMath.Clamp(Offset, v.Width * Zoom, v.Height * Zoom, ClientSize.Width, ClientSize.Height);
        Invalidate();
    }

    public void Pan(float dx, float dy)
    {
        if (Pic == null) return;
        var v = Virtual;
        Offset = ViewMath.Clamp(new PointF(Offset.X + dx, Offset.Y + dy), v.Width * Zoom, v.Height * Zoom, ClientSize.Width, ClientSize.Height);
        Invalidate();
    }

    // 넘패드 = 끝·가운데로 (꿀뷰 7·8·9·4·5·6·1·2·3)
    public void ScrollTo(float fx, float fy)
    {
        if (Pic == null) return;
        var v = Virtual;
        float iw = v.Width * Zoom, ih = v.Height * Zoom;
        Offset = ViewMath.Clamp(new PointF(-(iw - ClientSize.Width) * fx, -(ih - ClientSize.Height) * fy), iw, ih, ClientSize.Width, ClientSize.Height);
        Invalidate();
    }

    public bool Overflows => Pic != null && (Virtual.Width * Zoom > ClientSize.Width + 1 || Virtual.Height * Zoom > ClientSize.Height + 1);

    protected override bool IsInputKey(Keys k) => true;

    // 두 쪽일 때 각 쪽이 놓일 자리
    IEnumerable<(Picture p, int frame, RectangleF r)> Pages()
    {
        if (Pic == null) yield break;
        var v = Virtual;
        if (Pic2 == null) { yield return (Pic, Frame, new RectangleF(Offset.X, Offset.Y, v.Width * Zoom, v.Height * Zoom)); yield break; }
        float H = v.Height * Zoom;
        float w1 = Pic.Width * (v.Height / Pic.Height) * Zoom, w2 = Pic2.Width * (v.Height / Pic2.Height) * Zoom;
        var (left, lw, right, rw) = Rtl ? (Pic2, w2, Pic, w1) : (Pic, w1, Pic2, w2);
        yield return (left, left == Pic ? Frame : 0, new RectangleF(Offset.X, Offset.Y, lw, H));
        yield return (right, right == Pic ? Frame : 0, new RectangleF(Offset.X + lw, Offset.Y, rw, H));
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(BackColor);
        using var attr = new ImageAttributes();
        attr.SetWrapMode(WrapMode.TileFlipXY);
        if (Invert)
            attr.SetColorMatrix(new ColorMatrix(new[]
            {
                new float[] { -1, 0, 0, 0, 0 }, new float[] { 0, -1, 0, 0, 0 }, new float[] { 0, 0, -1, 0, 0 },
                new float[] { 0, 0, 0, 1, 0 }, new float[] { 1, 1, 1, 0, 1 },
            }));
        if (Math.Abs(Gamma - 1f) > 0.01f) attr.SetGamma(Gamma);
        foreach (var (p, frame, r) in Pages()) DrawPic(g, p, frame, r, attr);

        if (Message.Length > 0)
            TextRenderer.DrawText(g, Message, Ui.Hint, ClientRectangle, Ui.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
        if (ShowNav && Overflows && Pic2 == null) DrawNav(g);
        if (ShowInfo && Pic != null) DrawInfo(g);
        if (Toast.Length > 0)
        {
            var sz = TextRenderer.MeasureText(Toast, Ui.Hint);
            var r = new Rectangle((ClientSize.Width - sz.Width) / 2 - 16, ClientSize.Height / 2 - sz.Height / 2 - 10, sz.Width + 32, sz.Height + 20);
            using var b = new SolidBrush(Color.FromArgb(210, 0, 0, 0));
            g.FillRectangle(b, r);
            TextRenderer.DrawText(g, Toast, Ui.Hint, r, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    void DrawPic(Graphics g, Picture pic, int frame, RectangleF dest, ImageAttributes attr)
    {
        Bitmap src = pic.Frames[Math.Min(frame, pic.Frames.Length - 1)];
        float z = dest.Width / pic.Width;
        float scale = 1f;
        if (frame == 0 && !pic.Animated && z < 1f)
        {
            // 그릴 크기 이상인 가장 작은 층을 고른다
            foreach (var l in pic.Levels)
                if (l.Width >= pic.Width * z - 0.5f) { src = l; scale = (float)l.Width / pic.Width; }
        }
        var vis = RectangleF.Intersect(dest, new RectangleF(0, 0, ClientSize.Width, ClientSize.Height));
        if (vis.Width <= 0 || vis.Height <= 0) return;
        // 보이는 부분만 원본에서 떼어 그린다 = 크게 확대해도 느려지지 않는다
        float k = scale / z;
        var s = new RectangleF((vis.X - dest.X) * k, (vis.Y - dest.Y) * k, vis.Width * k, vis.Height * k);
        g.InterpolationMode = Filter == Filter.Sharp || z >= 3f || Math.Abs(z - 1f) < 0.001f ? InterpolationMode.NearestNeighbor
            : Filter == Filter.Soft ? InterpolationMode.HighQualityBicubic : InterpolationMode.HighQualityBilinear;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.CompositingQuality = CompositingQuality.HighSpeed;
        g.DrawImage(src, Rectangle.Round(vis), s.X, s.Y, s.Width, s.Height, GraphicsUnit.Pixel, attr);
    }

    // 확대했을 때 왼쪽 아래 작은 미리보기 + 지금 보는 자리 (꿀뷰 /)
    void DrawNav(Graphics g)
    {
        var p = Pic!;
        float box = 170 * DeviceDpi / 96f;
        float s = Math.Min(box / p.Width, box / p.Height);
        var r = new RectangleF(12, ClientSize.Height - p.Height * s - 12, p.Width * s, p.Height * s);
        using (var bg = new SolidBrush(Color.FromArgb(200, 0, 0, 0))) g.FillRectangle(bg, RectangleF.Inflate(r, 3, 3));
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
        g.DrawImage(p.Smallest, r);
        float iw = p.Width * Zoom, ih = p.Height * Zoom;
        var view = new RectangleF(r.X + -Offset.X / iw * r.Width, r.Y + -Offset.Y / ih * r.Height, Math.Min(1, ClientSize.Width / iw) * r.Width, Math.Min(1, ClientSize.Height / ih) * r.Height);
        using var pen = new Pen(Color.FromArgb(255, 80, 80), 2);
        g.DrawRectangle(pen, view.X, view.Y, view.Width, view.Height);
    }

    // 오른쪽 정보창 = 히스토그램 + 파일 정보 + EXIF (꿀뷰 Tab)
    void DrawInfo(Graphics g)
    {
        float dpi = DeviceDpi / 96f;
        int w = (int)(320 * dpi), pad = (int)(14 * dpi), lh = (int)(21 * dpi), kw = (int)(92 * dpi);
        var panel = new Rectangle(ClientSize.Width - w, 0, w, ClientSize.Height);
        using (var bg = new SolidBrush(Color.FromArgb(225, 18, 18, 20))) g.FillRectangle(bg, panel);
        int y = pad;
        if (Pic?.Histogram is { } hist)
        {
            var hr = new Rectangle(panel.X + pad, y, w - pad * 2, (int)(90 * dpi));
            using (var hb = new SolidBrush(Color.FromArgb(40, 255, 255, 255))) g.FillRectangle(hb, hr);
            float max = 1;
            for (int c = 1; c < 4; c++) for (int i = 1; i < 255; i++) max = Math.Max(max, hist[c][i]);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var cols = new[] { Color.FromArgb(150, 230, 230, 230), Color.FromArgb(150, 255, 80, 80), Color.FromArgb(150, 80, 220, 90), Color.FromArgb(150, 90, 140, 255) };
            for (int c = 3; c >= 0; c--)
            {
                var pts = new List<PointF> { new(hr.Left, hr.Bottom) };
                for (int i = 0; i < 256; i++) pts.Add(new PointF(hr.Left + hr.Width * i / 255f, hr.Bottom - Math.Min(1f, hist[c][i] / max) * hr.Height));
                pts.Add(new PointF(hr.Right, hr.Bottom));
                if (c == 0) { using var fb = new SolidBrush(Color.FromArgb(70, 230, 230, 230)); g.FillPolygon(fb, pts.ToArray()); }
                else { using var pen = new Pen(cols[c], 1.2f * dpi); g.DrawLines(pen, pts.Skip(1).Take(256).ToArray()); }
            }
            g.SmoothingMode = SmoothingMode.None;
            y = hr.Bottom + pad;
        }
        foreach (var (k, v) in Info)
        {
            if (y > ClientSize.Height - lh) break;
            if (k.StartsWith("─"))
            {
                y += lh / 3;
                TextRenderer.DrawText(g, k.Trim('─', ' '), Ui.Bold, new Point(panel.X + pad, y), Ui.Accent);
                y += lh;
                continue;
            }
            TextRenderer.DrawText(g, k, Ui.Body, new Rectangle(panel.X + pad, y, kw, lh), Ui.Sub, TextFormatFlags.EndEllipsis);
            var vr = new Rectangle(panel.X + pad + kw, y, w - pad * 2 - kw, lh * 3);
            var flags = TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis;
            var need = TextRenderer.MeasureText(g, v, Ui.Body, new Size(vr.Width, int.MaxValue), flags);
            int lines = Math.Clamp((need.Height + lh / 2) / Math.Max(1, (int)(17 * dpi)), 1, 3);
            vr.Height = lines * lh;
            TextRenderer.DrawText(g, v, Ui.Body, vr, Ui.Ink, flags);
            y += vr.Height;
        }
    }
}
