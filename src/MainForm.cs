// arddong ArdView 본 창 = 꿀뷰 단축키 배치·넘기기·두 쪽·슬라이드 쇼·책갈피·사진 폴더·변환·설정
using System.Diagnostics;
using System.Drawing.Printing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;

namespace ArdView;

// 남겨 두는 설정 (%APPDATA%\ArdView\settings.json)
public class Settings
{
    public string? Folder1 { get; set; }
    public string? Folder2 { get; set; }
    public List<string> Bookmarks { get; set; } = new();
    public bool Stretch { get; set; }
    public bool WheelScroll { get; set; }
    public bool WheelZoom { get; set; }
    public int Filter { get; set; } = 1;
    public bool ShowNav { get; set; } = true;
    public bool ConfirmDelete { get; set; } = true;
    public int[]? Window { get; set; }
    public bool Maximized { get; set; }

    static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArdView", "settings.json");

    public static Settings Load()
    {
        try { return JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new(); } catch { return new(); }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

enum Double { Off, Ltr, Rtl }

class MainForm : Form
{
    readonly Canvas _cv = new() { Dock = DockStyle.Fill };
    readonly OneLine _bar = new() { Dock = DockStyle.Fill, BackColor = Ui.Bar, ForeColor = Ui.Sub, Font = Ui.Body, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(10, 0, 10, 0), AutoEllipsis = true };
    readonly Panel _dock = new() { Dock = DockStyle.Bottom, Height = 38, BackColor = Ui.Bar };
    readonly FlowLayoutPanel _btns = new() { Dock = DockStyle.Left, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, BackColor = Ui.Bar, Padding = new Padding(6, 3, 0, 3) };
    readonly ToolButton _zoomLbl = new("100%", "배율 · 눌러서 고르기", text: true) { Width = 64 };
    readonly System.Windows.Forms.Timer _anim = new();
    readonly System.Windows.Forms.Timer _toastTimer = new() { Interval = 1300 };
    readonly System.Windows.Forms.Timer _slide = new();
    readonly Dictionary<string, Task<Picture>> _cache = new(StringComparer.OrdinalIgnoreCase);
    readonly Settings _set = Settings.Load();
    readonly Random _rnd = new();
    readonly Stack<int> _randBack = new();
    List<Src> _files = new();
    int _idx = -1;
    int _shown = 1;
    int _ticket;
    Point _drag;
    bool _dragging;
    bool _full;
    bool _fixZoom;
    Double _double = Double.Off;
    FormWindowState _prevState;
    Rectangle _prevBounds;
    FormBorderStyle _prevBorder = FormBorderStyle.Sizable;

    Src? Cur => _idx >= 0 && _idx < _files.Count ? _files[_idx] : null;

    public MainForm(string? open)
    {
        Text = "ArdView"; Icon = Ui.AppIcon; BackColor = Ui.Bg; Font = Ui.Body; KeyPreview = true;
        StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size(1100, 760); MinimumSize = new Size(360, 260);
        AllowDrop = true;
        BuildToolbar();
        Controls.Add(_cv); Controls.Add(_dock);
        RestoreWindow();
        _cv.Stretch = _set.Stretch; _cv.Filter = (Filter)_set.Filter; _cv.ShowNav = _set.ShowNav;
        _cv.Message = "그림 파일·폴더·압축 파일을 끌어다 놓으세요\n\n두 번 누르면 파일을 고릅니다 · 오른쪽 클릭 = 메뉴 · F1 = 단축키";
        _cv.ContextMenuStrip = BuildMenu();

        _anim.Tick += (_, _) => NextFrame();
        _toastTimer.Tick += (_, _) => { _toastTimer.Stop(); _cv.Toast = ""; _cv.Invalidate(); };
        _slide.Tick += (_, _) => { if (_idx + _shown >= _files.Count) GoTo(0); else Go(1); };
        _cv.Resize += (_, _) => _cv.Relayout();
        _cv.MouseWheel += OnWheel;
        _cv.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) { _drag = e.Location; _dragging = true; }
            if (e.Button == MouseButtons.Middle) { if (ModifierKeys.HasFlag(Keys.Control)) Zoom100(); else ToggleFull(); }
        };
        _cv.MouseMove += (_, e) =>
        {
            if (!_dragging) return;
            _cv.Pan(e.X - _drag.X, e.Y - _drag.Y); _drag = e.Location;
            if (_cv.Overflows) _cv.Cursor = Cursors.SizeAll;
        };
        _cv.MouseUp += (_, e) =>
        {
            _dragging = false; _cv.Cursor = Cursors.Default;
            if (e.Button == MouseButtons.XButton1) Go(-1);
            if (e.Button == MouseButtons.XButton2) Go(1);
        };
        // 꿀뷰 = 두 번 누르기는 최대화·복원
        _cv.MouseDoubleClick += (_, e) =>
        {
            if (e.Button != MouseButtons.Left) return;
            if (_cv.Pic == null && Cur == null) OpenDialog();
            else if (_full) ToggleFull();
            else WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        };
        DragEnter += (_, e) => e.Effect = e.Data?.GetDataPresent(DataFormats.FileDrop) == true ? DragDropEffects.Copy : DragDropEffects.None;
        DragDrop += (_, e) => { if (e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } f) OpenPath(f[0]); };
        FormClosing += (_, _) => { SaveWindow(); _set.Save(); };
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

    // ───────────── 메뉴 (오른쪽 클릭) ─────────────
    ContextMenuStrip BuildMenu()
    {
        var m = new ContextMenuStrip { Font = Ui.Body };
        ToolStripMenuItem Item(string text, string keys, Action act) => new(text, null, (_, _) => act()) { ShortcutKeyDisplayString = keys };
        ToolStripMenuItem Sub(string text, params ToolStripItem[] items) { var s = new ToolStripMenuItem(text); s.DropDownItems.AddRange(items); return s; }
        ToolStripSeparator Sep() => new();

        m.Items.AddRange(new ToolStripItem[]
        {
            Item("파일 열기", "F2", OpenDialog),
            Item("폴더 열기", "F", OpenFolderDialog),
            Item("이전 그림", "←", () => Go(-1)),
            Item("다음 그림", "→", () => Go(1)),
            Sub("이동",
                Item("처음 그림", "Home", () => GoTo(0)),
                Item("마지막 그림", "End", () => GoTo(_files.Count - 1)),
                Item("10장 뒤로", "Ctrl+PgDn", () => Jump(10)),
                Item("10장 앞으로", "Ctrl+PgUp", () => Jump(-10)),
                Item("아무 그림이나", "Ctrl+Alt+PgDn", RandomNext),
                Sep(),
                Item("다음 폴더·압축 파일", "]", () => NextContainer(1)),
                Item("이전 폴더·압축 파일", "[", () => NextContainer(-1)),
                Sep(),
                Item("다음 쪽 (여러 쪽 파일)", "Alt+PgDn", () => StepFrame(1)),
                Item("이전 쪽 (여러 쪽 파일)", "Alt+PgUp", () => StepFrame(-1))),
            Sep(),
            Sub("보기",
                Item("원래 크기 100%", "0", Zoom100),
                Item("창에 맞추기", "9", Fit),
                Item("폭에 맞추기", "8", FitWidth),
                Item("작은 그림 늘리기 켜기·끄기", "Z", ToggleStretch),
                Item("배율 고정 켜기·끄기", "Ctrl+K", ToggleFixZoom),
                Item("확대", "+", () => ZoomStep(true)),
                Item("축소", "-", () => ZoomStep(false)),
                Item("미리보기 창 켜기·끄기", "/", ToggleNav),
                Sep(),
                Item("두 쪽 보기 (왼→오)", "7", () => SetDouble(Double.Ltr)),
                Item("두 쪽 보기 (왼←오, 만화)", "6", () => SetDouble(Double.Rtl)),
                Item("두 쪽 좌우 바꾸기", "2", SwapDouble)),
            Sub("효과",
                Item("필터 없음 (또렷)", "U", () => SetFilter(Filter.Sharp)),
                Item("부드럽게", "I", () => SetFilter(Filter.Smooth)),
                Item("부드럽게+선명", "S", () => SetFilter(Filter.Soft)),
                Item("색 뒤집기", "Ctrl+I", ToggleInvert),
                Item("감마 바꾸기", "Ctrl+G", CycleGamma),
                Sep(),
                Item("오른쪽으로 돌리기", "Ctrl+R", () => Transform(RotateFlipType.Rotate90FlipNone)),
                Item("왼쪽으로 돌리기", "Ctrl+L", () => Transform(RotateFlipType.Rotate270FlipNone)),
                Item("뒤집어 돌리기", "Alt+↓", () => Transform(RotateFlipType.Rotate180FlipNone)),
                Item("좌우 뒤집기", "Ctrl+M", () => Transform(RotateFlipType.RotateNoneFlipX)),
                Item("상하 뒤집기", "Ctrl+F", () => Transform(RotateFlipType.RotateNoneFlipY)),
                Item("원래대로", "Alt+↑", Original)),
            Sub("슬라이드 쇼",
                Item("1초", "Ctrl+1", () => StartSlide(1)),
                Item("3초", "Ctrl+3", () => StartSlide(3)),
                Item("5초", "Ctrl+5", () => StartSlide(5)),
                Item("멈춤", "Ctrl+0", StopSlide)),
            Item("사진 정보·EXIF", "Tab", ToggleInfo),
            Item("전체 화면", "Alt+Enter", ToggleFull),
            Sep(),
            Sub("복사",
                Item("그림 복사", "Ctrl+C", CopyImage),
                Item("보이는 크기로 복사", "Ctrl+Alt+C", CopyResized),
                Item("파일 경로 복사", "Ctrl+Alt+Shift+C", CopyPath),
                Item("EXIF 정보 복사", "Ctrl+Shift+C", CopyExif)),
            Sub("사진 폴더",
                Item("사진 폴더 1 로 복사", "Insert", () => ToPhotoFolder(1, false)),
                Item("사진 폴더 1 로 옮기기", "Ctrl+Insert", () => ToPhotoFolder(1, true)),
                Item("사진 폴더 2 로 복사", "Shift+Insert", () => ToPhotoFolder(2, false))),
            Sub("책갈피",
                Item("책갈피 더하기", "B", AddBookmark),
                Item("최근 책갈피 열기", "Ctrl+B", OpenRecentBookmark),
                Item("책갈피 고치기", "F3", EditBookmarks)),
            Item("GPS 위치 지도로 보기", "Ctrl+Shift+G", ShowMap),
            Item("변환·크기 바꾸기", "Ctrl+T", ConvertDialog),
            Item("탐색기에서 보기", "Ctrl+Enter", ShowInFolder),
            Item("다른 프로그램으로 편집", "Ctrl+E", OpenEditor),
            Item("인쇄", "Ctrl+P", Print),
            Item("바탕 화면으로", "Ctrl+Alt+W", SetWallpaper),
            Item("휴지통으로", "Delete", () => DeleteCurrent(false)),
            Sep(),
            Sub("창",
                Item("최대화·복원", "M", () => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized),
                Item("최소화", "Q", () => WindowState = FormWindowState.Minimized),
                Item("항상 위", "Ctrl+A", ToggleTop),
                Item("테두리 숨기기·보이기", "Shift+F5", ToggleBorder),
                Item("새 창", "Ctrl+N", NewWindow)),
            Item("설정", "F5", SettingsDialog),
            Item("단축키·정보", "F1", HelpDialog),
            Item("버그 신고 · 기능 요청", "", () => Ui.Open(Ui.ReportUrl)),
            Item("닫기", "Esc", Close),
        });
        return m;
    }

    // ───────────── 아래 도구줄 (꿀뷰처럼 확대·축소·넘기기를 눈에 보이게) ─────────────
    void BuildToolbar()
    {
        ToolButton B(string glyph, string tip, Action act) { var b = new ToolButton(glyph, tip); b.Click += (_, _) => { act(); _cv.Focus(); }; return b; }
        Control Gap() => new Panel { Width = 10, Height = 30, BackColor = Ui.Bar, Margin = Padding.Empty };
        _zoomLbl.Click += (_, _) =>
        {
            var m = new ContextMenuStrip { Font = Ui.Body };
            foreach (var z in new[] { 10, 25, 33, 50, 67, 100, 150, 200, 300, 400, 800 })
                m.Items.Add($"{z}%", null, (_, _) => { _cv.SetZoom(z / 100f, Center()); UpdateBar(); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("창에 맞추기", null, (_, _) => Fit());
            m.Items.Add("폭에 맞추기", null, (_, _) => FitWidth());
            m.Show(_zoomLbl, new Point(0, -m.PreferredSize.Height));
        };
        _btns.Controls.AddRange(new Control[]
        {
            B("\uE8E5", "열기 (F2)", OpenDialog),
            Gap(),
            B("\uE76B", "이전 그림 (←)", () => Go(-1)),
            B("\uE76C", "다음 그림 (→)", () => Go(1)),
            Gap(),
            B("\uE71F", "축소 (-)", () => ZoomStep(false)),
            _zoomLbl,
            B("\uE8A3", "확대 (+)", () => ZoomStep(true)),
            B("\uE9A6", "창에 맞추기 (9)", Fit),
            new ToolButton("1:1", "원래 크기 100% (0)", text: true).With(b => b.Click += (_, _) => { Zoom100(); _cv.Focus(); }),
            Gap(),
            B("\uE7AD", "오른쪽으로 돌리기 (Ctrl+R)", () => Transform(RotateFlipType.Rotate90FlipNone)),
            B("\uE946", "사진 정보·EXIF (Tab)", ToggleInfo),
            B("\uE740", "전체 화면 (Alt+Enter)", ToggleFull),
            B("\uE713", "설정 (F5)", SettingsDialog),
            Gap(),
        });
        _dock.Controls.Add(_bar);
        _dock.Controls.Add(_btns);
    }

    PointF Center() => new(_cv.ClientSize.Width / 2f, _cv.ClientSize.Height / 2f);

    // ───────────── 키 (꿀뷰 배치) ─────────────
    protected override bool ProcessCmdKey(ref Message msg, Keys k)
    {
        const Keys C = Keys.Control, S = Keys.Shift, A = Keys.Alt;
        switch (k)
        {
            // 파일
            case Keys.F2: case C | Keys.O: OpenDialog(); return true;
            case Keys.F: OpenFolderDialog(); return true;
            case Keys.F4: CloseFile(); return true;
            case C | Keys.Enter: ShowInFolder(); return true;
            case Keys.Tab: ToggleInfo(); return true;
            case C | A | Keys.W: SetWallpaper(); return true;
            case Keys.Delete: DeleteCurrent(false); return true;
            case S | Keys.Delete: DeleteCurrent(true); return true;
            case Keys.Insert: ToPhotoFolder(1, false); return true;
            case C | Keys.Insert: ToPhotoFolder(1, true); return true;
            case S | Keys.Insert: ToPhotoFolder(2, false); return true;
            case C | Keys.E: OpenEditor(); return true;
            case C | Keys.P: Print(); return true;
            case C | Keys.C: CopyImage(); return true;
            case C | A | Keys.C: CopyResized(); return true;
            case C | A | S | Keys.C: CopyPath(); return true;
            case C | S | Keys.C: CopyExif(); return true;
            case C | S | Keys.G: ShowMap(); return true;
            case C | Keys.T: ConvertDialog(); return true;
            case Keys.F5: SettingsDialog(); return true;
            case Keys.F1: HelpDialog(); return true;
            case Keys.Escape: if (_full) ToggleFull(); else if (_slide.Enabled) StopSlide(); else Close(); return true;
            case Keys.X: case C | Keys.W: Close(); return true;

            // 넘기기
            case Keys.PageDown: case Keys.Down: case Keys.Right: case Keys.Space: case Keys.NumPad0: Go(1); return true;
            case Keys.PageUp: case Keys.Up: case Keys.Left: case Keys.Back: case S | Keys.Space: Go(-1); return true;
            case Keys.Home: GoTo(0); return true;
            case Keys.End: GoTo(_files.Count - 1); return true;
            case C | Keys.PageDown: Jump(10); return true;
            case C | Keys.PageUp: Jump(-10); return true;
            case C | S | Keys.Right: Jump(100); return true;
            case C | S | Keys.Left: Jump(-100); return true;
            case C | A | Keys.PageDown: RandomNext(); return true;
            case C | A | Keys.PageUp: RandomBack(); return true;
            case Keys.OemCloseBrackets: NextContainer(1); return true;
            case Keys.OemOpenBrackets: NextContainer(-1); return true;
            case S | Keys.PageDown: Go(1, single: true); return true;
            case S | Keys.PageUp: Go(-1, single: true); return true;
            case A | Keys.PageDown: StepFrame(1); return true;
            case A | Keys.PageUp: StepFrame(-1); return true;

            // 보기
            case Keys.D0: case Keys.Multiply: Zoom100(); return true;
            case Keys.D9: case Keys.D1: Fit(); return true;
            case Keys.D8: FitWidth(); return true;
            case Keys.D7: SetDouble(Double.Ltr); return true;
            case Keys.D6: SetDouble(Double.Rtl); return true;
            case Keys.D2: SwapDouble(); return true;
            case Keys.Z: ToggleStretch(); return true;
            case Keys.Add: case Keys.Oemplus: case S | Keys.Oemplus: ZoomStep(true); return true;
            case Keys.Subtract: case Keys.OemMinus: ZoomStep(false); return true;
            case C | Keys.Add: case C | Keys.Oemplus: ZoomBy(0.01f); return true;
            case C | Keys.Subtract: case C | Keys.OemMinus: ZoomBy(-0.01f); return true;
            case C | Keys.K: ToggleFixZoom(); return true;
            case Keys.Divide: case Keys.OemQuestion: ToggleNav(); return true;
            case Keys.NumPad7: _cv.ScrollTo(0, 0); return true;
            case Keys.NumPad8: _cv.ScrollTo(0.5f, 0); return true;
            case Keys.NumPad9: _cv.ScrollTo(1, 0); return true;
            case Keys.NumPad4: _cv.ScrollTo(0, 0.5f); return true;
            case Keys.NumPad5: _cv.ScrollTo(0.5f, 0.5f); return true;
            case Keys.NumPad6: _cv.ScrollTo(1, 0.5f); return true;
            case Keys.NumPad1: _cv.ScrollTo(0, 1); return true;
            case Keys.NumPad2: _cv.ScrollTo(0.5f, 1); return true;
            case Keys.NumPad3: _cv.ScrollTo(1, 1); return true;

            // 효과
            case Keys.U: SetFilter(Filter.Sharp); return true;
            case Keys.I: SetFilter(Filter.Smooth); return true;
            case Keys.S: SetFilter(Filter.Soft); return true;
            case C | Keys.I: ToggleInvert(); return true;
            case C | Keys.G: CycleGamma(); return true;
            case C | Keys.F: Transform(RotateFlipType.RotateNoneFlipY); return true;
            case C | Keys.M: Transform(RotateFlipType.RotateNoneFlipX); return true;
            case A | Keys.Up: Original(); return true;
            case A | Keys.Left: case C | Keys.L: Transform(RotateFlipType.Rotate270FlipNone); return true;
            case A | Keys.Right: case C | Keys.R: Transform(RotateFlipType.Rotate90FlipNone); return true;
            case A | Keys.Down: Transform(RotateFlipType.Rotate180FlipNone); return true;

            // 슬라이드 쇼
            case C | Keys.D0: StopSlide(); return true;

            // 책갈피
            case Keys.B: AddBookmark(); return true;
            case Keys.F3: EditBookmarks(); return true;
            case C | Keys.B: OpenRecentBookmark(); return true;

            // 창
            case Keys.M: WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; return true;
            case A | Keys.Enter: case Keys.N: case Keys.F11: case Keys.Enter: ToggleFull(); return true;
            case Keys.Q: WindowState = FormWindowState.Minimized; return true;
            case C | Keys.A: ToggleTop(); return true;
            case S | Keys.F5: ToggleBorder(); return true;
            case C | Keys.N: NewWindow(); return true;
        }
        if ((k & Keys.Modifiers) == C && (k & Keys.KeyCode) is >= Keys.D1 and <= Keys.D9) { StartSlide((k & Keys.KeyCode) - Keys.D0); return true; }
        return base.ProcessCmdKey(ref msg, k);
    }

    void OnWheel(object? s, MouseEventArgs e)
    {
        if (ModifierKeys.HasFlag(Keys.Control) || _set.WheelZoom) { _cv.SetZoom(ViewMath.Next(_cv.Zoom, e.Delta > 0), e.Location); UpdateBar(); return; }
        if (_set.WheelScroll && _cv.Overflows)
        {
            var before = _cv.Offset;
            _cv.Pan(0, e.Delta);
            if (before != _cv.Offset) return; // 끝에 닿으면 넘긴다
        }
        Go(e.Delta > 0 ? -1 : 1);
    }

    // ───────────── 열기·넘기기 ─────────────
    public void OpenPath(string p)
    {
        try
        {
            var list = Engine.List(p, out var i);
            if (list.Count == 0) { ShowToast("그림이 없습니다"); return; }
            _files = list;
            _randBack.Clear();
            foreach (var t in _cache.Values) Drop(t);
            _cache.Clear();
            Arc.CloseExcept(list[0].InArchive ? list[0].File : null);
            GoTo(i);
        }
        catch (Exception ex) { Ui.ShowError(this, ex, "열기 " + p); }
    }

    void OpenDialog()
    {
        using var d = new OpenFileDialog
        {
            Title = "그림 열기",
            Filter = "그림·압축 파일|" + string.Join(";", Engine.Exts.Concat(Engine.Raw).Concat(Arc.Exts).Select(x => "*" + x)) + "|모든 파일|*.*",
        };
        if (Cur != null) d.InitialDirectory = Path.GetDirectoryName(Cur.File);
        if (d.ShowDialog(this) == DialogResult.OK) OpenPath(d.FileName);
    }

    void OpenFolderDialog()
    {
        using var d = new FolderBrowserDialog { Description = "그림이 있는 폴더", UseDescriptionForTitle = true };
        if (Cur != null) d.SelectedPath = Cur.InArchive ? Path.GetDirectoryName(Cur.File)! : Cur.Container;
        if (d.ShowDialog(this) == DialogResult.OK) OpenPath(d.SelectedPath);
    }

    void CloseFile()
    {
        StopSlide();
        _anim.Stop();
        foreach (var t in _cache.Values) Drop(t);
        _cache.Clear();
        _cv.Pic = null; _cv.Pic2 = null;
        _files = new(); _idx = -1;
        _cv.Message = "그림 파일·폴더·압축 파일을 끌어다 놓으세요";
        Text = "ArdView"; _cv.Invalidate(); UpdateBar();
    }

    void Go(int step, bool single = false)
    {
        if (_files.Count == 0) return;
        int amount = single || _double == Double.Off ? 1 : step > 0 ? _shown : 2;
        int n = _idx + step * amount;
        if (n < 0) { if (_idx > 0) n = 0; else { ShowToast("첫 그림입니다"); return; } }
        if (n >= _files.Count) { ShowToast("마지막 그림입니다"); StopSlide(); return; }
        GoTo(n, step);
    }

    void Jump(int d)
    {
        if (_files.Count == 0) return;
        GoTo(Math.Clamp(_idx + d, 0, _files.Count - 1), Math.Sign(d));
    }

    void RandomNext()
    {
        if (_files.Count < 2) return;
        int n;
        do n = _rnd.Next(_files.Count); while (n == _idx);
        _randBack.Push(_idx);
        GoTo(n);
    }

    void RandomBack()
    {
        if (_randBack.Count > 0) GoTo(_randBack.Pop());
    }

    void NextContainer(int step)
    {
        if (Cur == null) return;
        try
        {
            var next = Engine.NeighborContainer(Cur.Container, step);
            if (next == null) { ShowToast(step > 0 ? "다음 폴더가 없습니다" : "이전 폴더가 없습니다"); return; }
            OpenPath(next);
            ShowToast(Path.GetFileName(next));
        }
        catch (Exception ex) { Ui.ShowError(this, ex, "폴더 넘기기"); }
    }

    Task<Picture> Get(Src s)
    {
        if (!_cache.TryGetValue(s.Key, out var t))
        {
            t = Task.Run(() => Engine.Load(s));
            _cache[s.Key] = t;
        }
        return t;
    }

    void Drop(Task<Picture> t)
    {
        if (t.IsCompletedSuccessfully) { if (t.Result != _cv.Pic && t.Result != _cv.Pic2) t.Result.Dispose(); }
        else if (!t.IsCompleted) t.ContinueWith(x => { if (x.IsCompletedSuccessfully) x.Result.Dispose(); });
    }

    async void GoTo(int i, int dir = 1)
    {
        if (i < 0 || i >= _files.Count) return;
        _idx = i;
        var src = _files[i];
        var ticket = ++_ticket;
        _anim.Stop();
        Text = src.Name + " - ArdView";
        var task = Get(src);
        if (!task.IsCompleted) UpdateBar("여는 중…");
        Picture pic;
        try { pic = await task; }
        catch (Exception ex)
        {
            _cache.Remove(src.Key);
            if (ticket != _ticket) return;
            _cv.Pic = null; _cv.Pic2 = null; _shown = 1;
            _cv.Message = src.Name + "\n\n열 수 없는 그림입니다\n" + (ex.InnerException?.Message is { } im && ex is NotSupportedException ? ex.Message : ex.Message);
            _cv.Invalidate(); UpdateBar();
            return;
        }
        if (ticket != _ticket) return;

        // 두 쪽 보기 = 둘 다 세로 그림일 때만 나란히 (꿀뷰 「스마트 두 쪽」)
        Picture? second = null;
        if (_double != Double.Off && pic.Portrait && i + 1 < _files.Count)
        {
            try
            {
                var p2 = await Get(_files[i + 1]);
                if (ticket != _ticket) return;
                if (p2.Portrait) second = p2;
            }
            catch { }
        }

        _cv.Pic = pic; _cv.Pic2 = second; _cv.Rtl = _double == Double.Rtl; _shown = second != null ? 2 : 1;
        _cv.Frame = 0; _cv.Message = "";
        if (!_fixZoom) { _cv.FitMode = true; _cv.FitWidth = _cv.FitWidth && _cv.FitMode; }
        _cv.Offset = PointF.Empty;
        _cv.Info = BuildInfo();
        _cv.Relayout();
        if (pic.Animated) { _anim.Interval = Math.Max(20, pic.Delays[0]); _anim.Start(); }
        UpdateBar();
        Prefetch(dir);
    }

    // 앞뒤 그림을 미리 읽어 두고, 멀어진 것은 버린다 = 넘길 때 기다리지 않는다
    void Prefetch(int dir)
    {
        if (dir == 0) dir = 1;
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var ahead = _double != Double.Off ? new[] { 0, 1, dir * 2, dir * 3, -dir, -dir * 2 } : new[] { 0, dir, -dir, 2 * dir };
        foreach (var d in ahead)
        {
            int j = _idx + d;
            if (j >= 0 && j < _files.Count) keep.Add(_files[j].Key);
        }
        foreach (var k in _cache.Keys.Where(k => !keep.Contains(k)).ToList())
        {
            Drop(_cache[k]);
            _cache.Remove(k);
        }
        foreach (var d in ahead)
        {
            int j = _idx + d;
            if (j >= 0 && j < _files.Count) Get(_files[j]);
        }
    }

    void NextFrame()
    {
        var p = _cv.Pic;
        if (p == null || !p.Animated) { _anim.Stop(); return; }
        _cv.Frame = (_cv.Frame + 1) % p.Frames.Length;
        _anim.Interval = Math.Max(20, p.Delays[_cv.Frame]);
        _cv.Invalidate();
    }

    // 여러 쪽 tiff·움직이는 그림 한 장씩 (움직임은 멈춘다)
    void StepFrame(int d)
    {
        var p = _cv.Pic;
        if (p == null || p.Frames.Length < 2) { ShowToast("한 장짜리 그림입니다"); return; }
        _anim.Stop();
        _cv.Frame = (_cv.Frame + d + p.Frames.Length) % p.Frames.Length;
        _cv.Invalidate(); UpdateBar();
    }

    // ───────────── 보기 ─────────────
    void Fit() { _cv.FitMode = true; _cv.FitWidth = false; _cv.Relayout(); UpdateBar(); }
    void FitWidth() { _cv.FitMode = true; _cv.FitWidth = true; _cv.Offset = PointF.Empty; _cv.Relayout(); UpdateBar(); }
    void Zoom100() { _cv.SetZoom(1f, Center()); UpdateBar(); }
    void ZoomStep(bool up) { _cv.SetZoom(ViewMath.Next(_cv.Zoom, up), Center()); UpdateBar(); }
    void ZoomBy(float d) { _cv.SetZoom((float)Math.Round(_cv.Zoom + d, 2), Center()); UpdateBar(); }

    void ToggleStretch()
    {
        _set.Stretch = _cv.Stretch = !_cv.Stretch;
        ShowToast(_cv.Stretch ? "작은 그림 늘리기 켬" : "작은 그림 늘리기 끔");
        _cv.Relayout(); UpdateBar();
    }

    void ToggleFixZoom()
    {
        _fixZoom = !_fixZoom;
        ShowToast(_fixZoom ? $"배율 고정 {_cv.Zoom * 100:0}%" : "배율 고정 끔");
        UpdateBar();
    }

    void ToggleNav()
    {
        _set.ShowNav = _cv.ShowNav = !_cv.ShowNav;
        _cv.Invalidate();
    }

    void SetDouble(Double d)
    {
        _double = _double == d ? Double.Off : d;
        ShowToast(_double switch { Double.Ltr => "두 쪽 보기 (왼→오)", Double.Rtl => "두 쪽 보기 (왼←오)", _ => "한 쪽 보기" });
        if (_idx >= 0) GoTo(_idx, 1);
    }

    void SwapDouble()
    {
        if (_double == Double.Off) { ShowToast("두 쪽 보기(7·6)에서 씁니다"); return; }
        SetDouble(_double == Double.Ltr ? Double.Rtl : Double.Ltr);
        if (_double == Double.Off) SetDouble(Double.Ltr);
    }

    void SetFilter(Filter f)
    {
        _cv.Filter = f; _set.Filter = (int)f;
        ShowToast(f switch { Filter.Sharp => "필터 없음", Filter.Smooth => "부드럽게", _ => "부드럽게+선명" });
        _cv.Invalidate();
    }

    void ToggleInvert() { _cv.Invert = !_cv.Invert; _cv.Invalidate(); }

    void CycleGamma()
    {
        _cv.Gamma = _cv.Gamma switch { 1f => 0.7f, 0.7f => 1.4f, _ => 1f };
        ShowToast(_cv.Gamma == 1f ? "감마 원래대로" : $"감마 {_cv.Gamma:0.0}");
        _cv.Invalidate();
    }

    void Transform(RotateFlipType t)
    {
        if (_cv.Pic == null) return;
        _cv.Pic.Transform(t);
        _cv.Pic2?.Transform(t);
        _cv.FitMode = !_fixZoom; _cv.Relayout();
        UpdateBar();
    }

    void Original()
    {
        if (Cur == null) return;
        _cv.Invert = false; _cv.Gamma = 1f;
        if (_cache.Remove(Cur.Key, out var t)) Drop(t);
        _cv.Pic = null;
        GoTo(_idx);
    }

    void ToggleInfo()
    {
        _cv.ShowInfo = !_cv.ShowInfo;
        _cv.Info = BuildInfo();
        _cv.Invalidate();
    }

    // 정보창 내용 = 파일 정보 + EXIF
    List<(string, string)> BuildInfo()
    {
        var rows = new List<(string, string)>();
        var s = Cur;
        var p = _cv.Pic;
        if (s == null || p == null) return rows;
        rows.Add(("── 파일", ""));
        rows.Add(("이름", s.Name));
        rows.Add(("위치", s.InArchive ? s.File + " 안 " + Path.GetDirectoryName(s.Entry!.Replace('/', '\\')) : Path.GetDirectoryName(s.File)!));
        rows.Add(("순서", $"{_idx + 1} / {_files.Count}"));
        rows.Add(("크기", $"{Ui.Size(p.FileSize)} ({p.FileSize:N0} 바이트)"));
        rows.Add(("해상도", $"{p.Width} × {p.Height} ({p.Width * (long)p.Height / 1e6:0.0} MP)"));
        rows.Add(("형식", p.Format + (p.Animated ? $" · 움직이는 그림 {p.Frames.Length}장" : p.MultiPage ? $" · {p.Frames.Length}쪽" : "")));
        if (!s.InArchive)
        {
            try
            {
                var fi = new FileInfo(s.File);
                rows.Add(("고친 때", fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss")));
                rows.Add(("만든 때", fi.CreationTime.ToString("yyyy-MM-dd HH:mm:ss")));
            }
            catch { }
        }
        if (p.Meta.Rows.Count > 0)
        {
            rows.Add(("── EXIF", ""));
            rows.AddRange(p.Meta.Rows);
            if (p.Meta.Lat != null) rows.Add(("지도", "Ctrl+Shift+G"));
        }
        else rows.Add(("EXIF", "없음"));
        return rows;
    }

    // ───────────── 슬라이드 쇼 ─────────────
    void StartSlide(int sec)
    {
        if (_files.Count == 0) return;
        _slide.Interval = sec * 1000; _slide.Start();
        ShowToast($"슬라이드 쇼 {sec}초 · Ctrl+0 멈춤");
        UpdateBar();
    }

    void StopSlide()
    {
        if (!_slide.Enabled) return;
        _slide.Stop(); ShowToast("슬라이드 쇼 멈춤"); UpdateBar();
    }

    // ───────────── 책갈피 ─────────────
    void AddBookmark()
    {
        if (Cur == null) return;
        _set.Bookmarks.Remove(Cur.Key);
        _set.Bookmarks.Insert(0, Cur.Key);
        if (_set.Bookmarks.Count > 100) _set.Bookmarks.RemoveAt(100);
        _set.Save();
        ShowToast("책갈피에 더했습니다");
    }

    void OpenRecentBookmark()
    {
        if (_set.Bookmarks.Count == 0) { ShowToast("책갈피가 없습니다 (B 로 더하기)"); return; }
        OpenPath(_set.Bookmarks[0]);
    }

    void EditBookmarks()
    {
        using var f = Dialogs.Base("책갈피", 560, 380);
        var list = new ListBox { Left = 12, Top = 12, Width = 536, Height = 300, Font = Ui.Body, IntegralHeight = false, HorizontalScrollbar = true };
        foreach (var b in _set.Bookmarks) list.Items.Add(Src.Parse(b).Display);
        var open = Dialogs.Btn("열기", true, 248, 324);
        var del = Dialogs.Btn("지우기", false, 348, 324);
        var close = Dialogs.Btn("닫기", false, 448, 324);
        open.Click += (_, _) => { if (list.SelectedIndex >= 0) { var k = _set.Bookmarks[list.SelectedIndex]; f.Close(); OpenPath(k); } };
        list.DoubleClick += (_, _) => open.PerformClick();
        del.Click += (_, _) => { if (list.SelectedIndex >= 0) { _set.Bookmarks.RemoveAt(list.SelectedIndex); list.Items.RemoveAt(list.SelectedIndex); _set.Save(); } };
        close.Click += (_, _) => f.Close();
        f.Controls.AddRange(new Control[] { list, open, del, close });
        f.CancelButton = close;
        f.ShowDialog(this);
    }

    // ───────────── 파일 동작 ─────────────
    string? PhotoFolder(int n)
    {
        var cur = n == 1 ? _set.Folder1 : _set.Folder2;
        if (cur != null && Directory.Exists(cur)) return cur;
        using var d = new FolderBrowserDialog { Description = $"사진 폴더 {n} 을 고르세요 (F5 설정에서 바꿀 수 있습니다)", UseDescriptionForTitle = true };
        if (d.ShowDialog(this) != DialogResult.OK) return null;
        if (n == 1) _set.Folder1 = d.SelectedPath; else _set.Folder2 = d.SelectedPath;
        _set.Save();
        return d.SelectedPath;
    }

    void ToPhotoFolder(int n, bool move)
    {
        var s = Cur;
        if (s == null) return;
        if (move && s.InArchive) { ShowToast("압축 파일 안 그림은 옮길 수 없습니다 (Insert 로 복사)"); return; }
        var dir = PhotoFolder(n);
        if (dir == null) return;
        try
        {
            var target = Engine.Unique(Path.Combine(dir, s.Name));
            if (s.InArchive) File.WriteAllBytes(target, Engine.ReadBytes(s));
            else if (move) File.Move(s.File, target);
            else File.Copy(s.File, target);
            ShowToast($"사진 폴더 {n} 으로 {(move ? "옮겼습니다" : "복사했습니다")}");
            if (move) RemoveCurrent();
        }
        catch (Exception ex) { Ui.ShowError(this, ex, "사진 폴더 " + n); }
    }

    void DeleteCurrent(bool permanent)
    {
        var s = Cur;
        if (s == null) return;
        if (s.InArchive) { ShowToast("압축 파일 안 그림은 지울 수 없습니다"); return; }
        if (permanent || _set.ConfirmDelete)
        {
            var q = permanent ? "\n\n완전히 지울까요? 휴지통에 남지 않습니다." : "\n\n휴지통으로 보낼까요?";
            if (MessageBox.Show(this, s.Name + q, "ArdView", MessageBoxButtons.YesNo, permanent ? MessageBoxIcon.Warning : MessageBoxIcon.Question) != DialogResult.Yes) return;
        }
        try { FileSystem.DeleteFile(s.File, UIOption.OnlyErrorDialogs, permanent ? RecycleOption.DeletePermanently : RecycleOption.SendToRecycleBin); }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { Ui.ShowError(this, ex, "지우기 " + s.Name); return; }
        RemoveCurrent();
    }

    void RemoveCurrent()
    {
        var s = Cur!;
        if (_cache.Remove(s.Key, out var t)) Drop(t);
        _files.RemoveAt(_idx);
        if (_files.Count == 0) { CloseFile(); _cv.Message = "이 폴더에 남은 그림이 없습니다"; return; }
        GoTo(Math.Min(_idx, _files.Count - 1));
    }

    void CopyImage()
    {
        if (_cv.Pic == null) return;
        try
        {
            using var b = new Bitmap(_cv.Pic.Frames[Math.Min(_cv.Frame, _cv.Pic.Frames.Length - 1)]);
            Clipboard.SetImage(b);
            ShowToast("그림을 복사했습니다");
        }
        catch (Exception ex) { Ui.ShowError(this, ex, "그림 복사"); }
    }

    void CopyResized()
    {
        if (_cv.Pic == null) return;
        var p = _cv.Pic;
        using var b = Engine.Resize(p.Frames[0], (int)Math.Round(p.Width * _cv.Zoom), (int)Math.Round(p.Height * _cv.Zoom));
        Clipboard.SetImage(b);
        ShowToast($"{b.Width} × {b.Height} 로 복사했습니다");
    }

    void CopyPath()
    {
        if (Cur == null) return;
        Clipboard.SetText(Cur.Display);
        ShowToast("경로를 복사했습니다");
    }

    void CopyExif()
    {
        if (_cv.Pic == null) return;
        var sb = new StringBuilder();
        foreach (var (k, v) in BuildInfo()) sb.AppendLine(k.StartsWith("─") ? "[" + k.Trim('─', ' ') + "]" : $"{k}\t{v}");
        Clipboard.SetText(sb.ToString());
        ShowToast("정보를 복사했습니다");
    }

    void ShowMap()
    {
        var m = _cv.Pic?.Meta;
        if (m?.Lat == null) { ShowToast("GPS 정보가 없습니다"); return; }
        Ui.Open(FormattableString.Invariant($"https://www.google.com/maps?q={m.Lat:0.000000},{m.Lon:0.000000}"));
    }

    void ShowInFolder()
    {
        if (Cur == null) return;
        Process.Start("explorer.exe", $"/select,\"{Cur.File}\"");
    }

    void OpenEditor()
    {
        var s = Cur;
        if (s == null) return;
        if (s.InArchive) { ShowToast("압축 파일 안 그림은 편집기로 열 수 없습니다"); return; }
        try { Process.Start(new ProcessStartInfo(s.File) { UseShellExecute = true, Verb = "edit" }); }
        catch { Process.Start("mspaint.exe", $"\"{s.File}\""); }
    }

    void Print()
    {
        var p = _cv.Pic;
        if (p == null) return;
        using var doc = new PrintDocument { DocumentName = Cur?.Name ?? "ArdView" };
        doc.PrintPage += (_, e) =>
        {
            var m = e.MarginBounds;
            float z = Math.Min((float)m.Width / p.Width, (float)m.Height / p.Height);
            var r = new RectangleF(m.X + (m.Width - p.Width * z) / 2, m.Y + (m.Height - p.Height * z) / 2, p.Width * z, p.Height * z);
            e.Graphics!.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.DrawImage(p.Frames[0], r);
        };
        using var d = new PrintDialog { Document = doc, UseEXDialog = true };
        if (d.ShowDialog(this) == DialogResult.OK)
        {
            try { doc.Print(); } catch (Exception ex) { Ui.ShowError(this, ex, "인쇄"); }
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern bool SystemParametersInfo(int action, int param, string value, int flags);

    void SetWallpaper()
    {
        var p = _cv.Pic;
        if (p == null) return;
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArdView", "wallpaper.jpg");
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            Engine.Save(p.Frames[0], path, "JPG", 95);
            SystemParametersInfo(0x14, 0, path, 0x01 | 0x02);
            ShowToast("바탕 화면으로 정했습니다");
        }
        catch (Exception ex) { Ui.ShowError(this, ex, "바탕 화면"); }
    }

    void ConvertDialog()
    {
        if (_files.Count == 0) return;
        using var f = new ConvertForm(_files, _idx, _cv.Pic);
        f.ShowDialog(this);
    }

    void SettingsDialog()
    {
        using var f = Dialogs.Base("설정", 520, 300);
        int y = 16;
        TextBox Folder(string label, string? val)
        {
            f.Controls.Add(new Label { Text = label, Left = 16, Top = y + 4, Width = 90, ForeColor = Ui.Ink });
            var t = new TextBox { Left = 108, Top = y, Width = 300, Text = val ?? "" };
            var b = Dialogs.Btn("찾기", false, 416, y - 3); b.Width = 88;
            b.Click += (_, _) => { using var d = new FolderBrowserDialog(); if (d.ShowDialog(f) == DialogResult.OK) t.Text = d.SelectedPath; };
            f.Controls.AddRange(new Control[] { t, b });
            y += 40;
            return t;
        }
        var f1 = Folder("사진 폴더 1", _set.Folder1);
        var f2 = Folder("사진 폴더 2", _set.Folder2);
        CheckBox Check(string text, bool v) { var c = new CheckBox { Text = text, Left = 16, Top = y, Width = 480, Checked = v, ForeColor = Ui.Ink }; f.Controls.Add(c); y += 30; return c; }
        var wheelZoom = Check("휠 = 확대·축소 (끄면 꿀뷰처럼 휠 = 다음·이전 그림)", _set.WheelZoom);
        var wheel = Check("확대했을 때 휠로 위아래 이동 (끝에 닿으면 넘기기)", _set.WheelScroll);
        var stretch = Check("작은 그림을 창에 맞게 늘리기 (Z)", _set.Stretch);
        var confirm = Check("휴지통으로 보낼 때 묻기", _set.ConfirmDelete);
        var nav = Check("확대했을 때 미리보기 창 보이기 (/)", _set.ShowNav);
        var ok = Dialogs.Btn("저장", true, 304, y + 12);
        var no = Dialogs.Btn("취소", false, 404, y + 12);
        ok.DialogResult = DialogResult.OK; no.DialogResult = DialogResult.Cancel;
        f.Controls.AddRange(new Control[] { ok, no });
        f.AcceptButton = ok; f.CancelButton = no;
        f.ClientSize = new Size(520, y + 60);
        if (f.ShowDialog(this) != DialogResult.OK) return;
        _set.Folder1 = string.IsNullOrWhiteSpace(f1.Text) ? null : f1.Text;
        _set.Folder2 = string.IsNullOrWhiteSpace(f2.Text) ? null : f2.Text;
        _set.WheelScroll = wheel.Checked; _set.WheelZoom = wheelZoom.Checked; _set.Stretch = _cv.Stretch = stretch.Checked;
        _set.ConfirmDelete = confirm.Checked; _set.ShowNav = _cv.ShowNav = nav.Checked;
        _set.Save();
        _cv.Relayout();
    }

    void HelpDialog()
    {
        var ver = typeof(Ui).Assembly.GetName().Version?.ToString(3);
        using var f = Dialogs.Base($"ArdView {ver} · 단축키", 640, 600);
        var box = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Left = 12, Top = 12, Width = 616, Height = 530,
            Font = new Font("맑은 고딕", 10f), BackColor = Color.White, Text = Dialogs.HelpText.Replace("\n", "\r\n"),
        };
        var home = Dialogs.Btn("소개 페이지", false, 428, 552);
        var close = Dialogs.Btn("닫기", true, 528, 552);
        home.Click += (_, _) => Ui.Open(Ui.HomeUrl);
        close.Click += (_, _) => f.Close();
        f.Controls.AddRange(new Control[] { box, home, close });
        f.CancelButton = close;
        f.Shown += (_, _) => { box.SelectionStart = 0; box.SelectionLength = 0; close.Focus(); };
        f.ShowDialog(this);
    }

    // ───────────── 창 ─────────────
    void ToggleFull()
    {
        if (!_full)
        {
            _prevState = WindowState; _prevBounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds; _prevBorder = FormBorderStyle;
            _dock.Visible = false; FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Normal; Bounds = Screen.FromControl(this).Bounds;
            _cv.BackColor = Color.Black;
        }
        else
        {
            FormBorderStyle = _prevBorder; _dock.Visible = _prevBorder != FormBorderStyle.None; _cv.BackColor = Ui.Bg;
            Bounds = _prevBounds; WindowState = _prevState;
        }
        _full = !_full;
        _cv.Relayout();
    }

    void ToggleTop()
    {
        TopMost = !TopMost;
        ShowToast(TopMost ? "항상 위 켬" : "항상 위 끔");
    }

    void ToggleBorder()
    {
        if (_full) return;
        var b = Bounds;
        FormBorderStyle = FormBorderStyle == FormBorderStyle.None ? FormBorderStyle.Sizable : FormBorderStyle.None;
        _dock.Visible = FormBorderStyle != FormBorderStyle.None;
        Bounds = b;
    }

    void NewWindow()
    {
        var args = Cur != null ? $"\"{Cur.Key}\"" : "";
        Process.Start(new ProcessStartInfo(Application.ExecutablePath, args) { UseShellExecute = false });
    }

    void ShowToast(string s)
    {
        _cv.Toast = s; _cv.Invalidate();
        _toastTimer.Stop(); _toastTimer.Start();
    }

    void UpdateBar(string? extra = null)
    {
        _zoomLbl.Text = _cv.Pic == null ? "100%" : $"{_cv.Zoom * 100:0}%";
        _zoomLbl.Invalidate();
        var s = Cur;
        if (s == null) { _bar.Text = "← → 넘기기 · Ctrl+휠 확대 · Tab 사진 정보 · F1 단축키 · 오른쪽 클릭 메뉴"; return; }
        var parts = new List<string> { $"{_idx + 1}{(_shown == 2 ? "-" + (_idx + 2) : "")} / {_files.Count}", s.Name };
        if (extra != null) parts.Add(extra);
        else if (_cv.Pic is { } p)
        {
            parts.Add($"{p.Width} × {p.Height}");
            parts.Add(Ui.Size(p.FileSize));
            parts.Add(p.Format + (p.Animated ? $" · {p.Frames.Length}장" : p.MultiPage ? $" · {_cv.Frame + 1}/{p.Frames.Length}쪽" : ""));
            if (p.Meta.Rows.FirstOrDefault(r => r.K == "카메라").V is { } cam) parts.Add(cam);
            if (_fixZoom) parts.Add("배율 고정");
        }
        if (_double != Double.Off) parts.Add(_double == Double.Ltr ? "두 쪽 왼→오" : "두 쪽 왼←오");
        if (_slide.Enabled) parts.Add($"슬라이드 쇼 {_slide.Interval / 1000}초");
        _bar.Text = string.Join("   ·   ", parts);
    }

    // ── 창 크기 기억 ──
    void RestoreWindow()
    {
        if (_set.Window is not { Length: 4 } v) return;
        var r = new Rectangle(v[0], v[1], v[2], v[3]);
        if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(r))) return;
        StartPosition = FormStartPosition.Manual; Bounds = r;
        if (_set.Maximized) WindowState = FormWindowState.Maximized;
    }

    void SaveWindow()
    {
        var r = _full ? _prevBounds : WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        _set.Window = new[] { r.X, r.Y, r.Width, r.Height };
        _set.Maximized = (_full ? _prevState : WindowState) == FormWindowState.Maximized;
    }
}

// 상태 글자 = 한 줄, 넘치면 말줄임
class OneLine : Label
{
    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(BackColor);
        var r = new Rectangle(Padding.Left, 0, Width - Padding.Horizontal, Height);
        TextRenderer.DrawText(e.Graphics, Text, Font, r, ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
    }
}

// 아래 도구줄 버튼 = 아이콘 글꼴(Segoe MDL2) 또는 글자, 마우스를 올리면 밝아진다
class ToolButton : Control
{
    static readonly Font Glyph = new("Segoe MDL2 Assets", 11f);
    static readonly ToolTip Tips = new();
    readonly bool _text;
    bool _hover;

    public ToolButton(string glyph, string tip, bool text = false)
    {
        Text = glyph; _text = text;
        Size = new Size(36, 30); Margin = new Padding(1, 0, 1, 0); Cursor = Cursors.Hand;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        BackColor = Ui.Bar;
        Tips.SetToolTip(this, tip);
        AccessibleName = tip;
    }

    public ToolButton With(Action<ToolButton> a) { a(this); return this; }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(_hover ? Color.FromArgb(58, 60, 66) : BackColor);
        TextRenderer.DrawText(e.Graphics, Text, _text ? Ui.Body : Glyph, ClientRectangle, _hover ? Color.White : Ui.Ink,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
    }
}

// ───────────────────────── 작은 창들 ─────────────────────────
static class Dialogs
{
    public static Form Base(string title, int w, int h) => new()
    {
        Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false, ShowInTaskbar = false,
        StartPosition = FormStartPosition.CenterParent, ClientSize = new Size(w, h), BackColor = Color.FromArgb(250, 250, 251), Font = Ui.Body,
    };

    public static Button Btn(string text, bool primary, int x, int y)
    {
        var b = new Button
        {
            Text = text, Left = x, Top = y, Width = 92, Height = 32, FlatStyle = FlatStyle.Flat,
            BackColor = primary ? Color.FromArgb(37, 99, 235) : Color.White, ForeColor = primary ? Color.White : Color.FromArgb(32, 33, 36),
        };
        b.FlatAppearance.BorderColor = primary ? Color.FromArgb(37, 99, 235) : Color.FromArgb(225, 227, 232);
        return b;
    }

    public const string HelpText =
@"[파일]
F2 · Ctrl+O        파일 열기 (그림·zip·rar·7z·cbz 도 그대로)
F                  폴더 열기
F4                 닫기
Tab                사진 정보·EXIF·히스토그램 보이기
Ctrl+Enter         탐색기에서 보기
Ctrl+E             다른 프로그램으로 편집
Ctrl+P             인쇄
Ctrl+T             변환·크기 바꾸기 (한 장 · 목록 전체)
Ctrl+Alt+W         바탕 화면으로
Delete             휴지통으로     Shift+Delete  완전히 지우기
Insert             사진 폴더 1 로 복사
Ctrl+Insert        사진 폴더 1 로 옮기기
Shift+Insert       사진 폴더 2 로 복사
F5                 설정
F1                 이 창
Esc · X · Ctrl+W   닫기 (전체 화면이면 먼저 빠져나옴)

[넘기기]
→ ↓ PgDn Space 휠아래   다음 그림
← ↑ PgUp Backspace 휠위 이전 그림
Home · End          처음 · 마지막
Ctrl+PgDn · PgUp    10장 뒤 · 앞
Ctrl+Shift+→ · ←    100장 뒤 · 앞
Ctrl+Alt+PgDn · PgUp 아무 그림 · 되돌아가기
] · [               다음 · 이전 폴더 또는 압축 파일
Shift+PgDn · PgUp   두 쪽 보기에서 한 장씩
Alt+PgDn · PgUp     여러 쪽 파일(tiff·움직이는 그림) 한 장씩

[보기]
0 · *               원래 크기 100%
9 · 1               창에 맞추기
8                   폭에 맞추기
Z                   작은 그림 늘리기 켜기·끄기
+ · -               확대 · 축소       Ctrl++ · Ctrl+-  1% 씩
Ctrl+휠             커서 자리 기준 확대·축소
Ctrl+K              배율 고정 (다음 그림도 같은 배율)
/                   확대했을 때 미리보기 창
넘패드 7 8 9 4 5 6 1 2 3   확대했을 때 그 방향 끝으로
7 · 6               두 쪽 보기 왼→오 · 왼←오(만화)  (다시 누르면 한 쪽)
2                   두 쪽 좌우 바꾸기

[효과]  파일은 바뀌지 않습니다
U · I · S           필터 없음 · 부드럽게 · 부드럽게+선명
Ctrl+I · Ctrl+G     색 뒤집기 · 감마
Ctrl+R · Alt+→      오른쪽으로 돌리기
Ctrl+L · Alt+←      왼쪽으로 돌리기
Alt+↓               뒤집어 돌리기
Ctrl+M · Ctrl+F     좌우 · 상하 뒤집기
Alt+↑               원래대로

[슬라이드 쇼 · 책갈피]
Ctrl+1 ~ 9          슬라이드 쇼 (숫자 = 초)     Ctrl+0  멈춤
B · Ctrl+B · F3     책갈피 더하기 · 최근 책갈피 · 책갈피 고치기

[복사 · 정보]
Ctrl+C              그림 복사
Ctrl+Alt+C          보이는 크기로 복사
Ctrl+Alt+Shift+C    파일 경로 복사
Ctrl+Shift+C        EXIF 정보 복사
Ctrl+Shift+G        GPS 위치를 지도로

[창]
Alt+Enter · N · F11 · Enter · 가운데 버튼   전체 화면
M · 두 번 누르기     최대화 · 복원
Q                   최소화
Ctrl+A              항상 위
Shift+F5            테두리 숨기기
Ctrl+N              새 창
Ctrl+가운데 버튼     100%

[여는 형식]
jpg png gif webp bmp tiff ico psd tga pnm · jxr dds (윈도우)
heic avif (윈도우 확장 설치 시) · RAW(dng cr2 cr3 nef arw raf orf rw2 pef …)는 안의 미리보기
압축 파일 안: zip cbz rar cbr 7z cb7 tar";
}

// 변환 = 형식·품질·크기·저장 폴더, 지금 그림 또는 목록 전체 (꿀뷰 Ctrl+T)
class ConvertForm : Form
{
    readonly List<Src> _files;
    readonly int _idx;
    readonly Picture? _cur;
    readonly ComboBox _fmt = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
    readonly NumericUpDown _q = new() { Minimum = 1, Maximum = 100, Value = 90, Width = 70 };
    readonly ComboBox _size = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    readonly TextBox _dir = new() { Width = 300 };
    readonly RadioButton _one = new() { Text = "지금 그림 (돌린 모습 그대로)", Checked = true, AutoSize = true };
    readonly RadioButton _all = new() { AutoSize = true };
    readonly Label _state = new() { AutoSize = false, Width = 480, Height = 22, ForeColor = Color.FromArgb(110, 113, 120) };
    readonly Button _go;
    CancellationTokenSource? _cts;

    static readonly (string Name, int Long, float Ratio)[] Sizes =
        { ("원래 크기", 0, 1f), ("긴 변 3840", 3840, 0), ("긴 변 2560", 2560, 0), ("긴 변 1920", 1920, 0), ("긴 변 1280", 1280, 0), ("긴 변 800", 800, 0), ("50%", 0, 0.5f), ("25%", 0, 0.25f) };

    public ConvertForm(List<Src> files, int idx, Picture? cur)
    {
        _files = files; _idx = idx; _cur = cur;
        Text = "변환·크기 바꾸기"; FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent; ClientSize = new Size(520, 300); BackColor = Color.FromArgb(250, 250, 251); Font = Ui.Body;
        _fmt.Items.AddRange(Engine.SaveFormats); _fmt.SelectedIndex = 0;
        foreach (var s in Sizes) _size.Items.Add(s.Name);
        _size.SelectedIndex = 0;
        var src = files[idx];
        _dir.Text = Path.Combine(src.InArchive ? Path.GetDirectoryName(src.File)! : src.Container, "변환");
        _all.Text = $"목록 전체 {files.Count}장";

        void Row(int y, string label, params Control[] cs)
        {
            Controls.Add(new Label { Text = label, Left = 16, Top = y + 4, Width = 80 });
            int x = 100;
            foreach (var c in cs) { c.Left = x; c.Top = y; Controls.Add(c); x += c.Width + 10; }
        }
        Row(16, "형식", _fmt, new Label { Text = "품질", Width = 36, Height = 22, TextAlign = ContentAlignment.MiddleRight }, _q);
        Row(56, "크기", _size);
        var browse = Dialogs.Btn("찾기", false, 0, 0); browse.Width = 80; browse.Height = 26;
        browse.Click += (_, _) => { using var d = new FolderBrowserDialog(); if (d.ShowDialog(this) == DialogResult.OK) _dir.Text = d.SelectedPath; };
        Row(96, "저장 폴더", _dir, browse);
        Row(136, "대상", _one);
        Row(162, "", _all);
        _state.Left = 16; _state.Top = 204; Controls.Add(_state);
        _go = Dialogs.Btn("변환", true, 304, 252);
        var close = Dialogs.Btn("닫기", false, 404, 252);
        _go.Click += async (_, _) => await Run();
        close.Click += (_, _) => { _cts?.Cancel(); Close(); };
        Controls.AddRange(new Control[] { _go, close });
        CancelButton = close;
    }

    static Size Target(int w, int h, (string, int Long, float Ratio) s)
    {
        if (s.Long > 0 && Math.Max(w, h) > s.Long) { float z = (float)s.Long / Math.Max(w, h); return new Size((int)Math.Round(w * z), (int)Math.Round(h * z)); }
        if (s.Ratio is > 0 and < 1) return new Size((int)Math.Round(w * s.Ratio), (int)Math.Round(h * s.Ratio));
        return new Size(w, h);
    }

    async Task Run()
    {
        var fmt = (string)_fmt.SelectedItem!;
        int q = (int)_q.Value;
        var size = Sizes[_size.SelectedIndex];
        var dir = _dir.Text;
        var targets = _one.Checked ? new List<Src> { _files[_idx] } : _files.ToList();
        // 화면이 그리는 그림을 다른 일꾼에서 같이 만지지 않게 복사본으로 넘긴다
        Picture? cur = null;
        if (_one.Checked && _cur != null)
            cur = new Picture(new[] { new Bitmap(_cur.Frames[0]) }, new[] { 0 }, _cur.Format);
        _go.Enabled = false;
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        int done = 0, fail = 0;
        var prog = new Progress<string>(t => _state.Text = t);
        try
        {
            await Task.Run(() =>
            {
                Directory.CreateDirectory(dir);
                foreach (var s in targets)
                {
                    ct.ThrowIfCancellationRequested();
                    ((IProgress<string>)prog).Report($"{done + fail + 1} / {targets.Count} · {s.Name}");
                    try
                    {
                        var pic = cur ?? Engine.Load(s);
                        try
                        {
                            var t = Target(pic.Width, pic.Height, size);
                            var bmp = t.Width == pic.Width && t.Height == pic.Height ? pic.Frames[0] : Engine.Resize(pic.Frames[0], t.Width, t.Height);
                            try
                            {
                                var path = Engine.Unique(Path.Combine(dir, Path.GetFileNameWithoutExtension(s.Name) + "." + fmt.ToLowerInvariant()));
                                Engine.Save(bmp, path, fmt, q);
                            }
                            finally { if (bmp != pic.Frames[0]) bmp.Dispose(); }
                        }
                        finally { if (cur == null) pic.Dispose(); }
                        done++;
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { fail++; }
                }
            }, ct);
            _state.Text = $"끝 · {done}장 저장" + (fail > 0 ? $" · {fail}장 실패" : "") + " · " + dir;
            if (done > 0) Process.Start("explorer.exe", $"\"{dir}\"");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Ui.ShowError(this, ex, "변환"); }
        finally { _go.Enabled = true; cur?.Dispose(); }
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
