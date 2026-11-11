// arddong ArdView 엔진 시험 = 실파일로 형식·회전·움직이는 그림·순서·보기 계산을 확인한다
using System.Drawing;
using ArdView;

namespace ArdViewTest;

static class T
{
    static int _fail, _pass;
    static readonly string S = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "samples");

    static void Check(string name, Func<bool> f)
    {
        bool ok;
        string why = "";
        try { ok = f(); } catch (Exception ex) { ok = false; why = ex.GetType().Name + ": " + ex.Message; }
        Console.WriteLine($"{(ok ? "통과" : "실패")}  {name} {why}");
        if (ok) _pass++; else _fail++;
    }

    static bool Near(Color c, int r, int g, int b) => Math.Abs(c.R - r) < 40 && Math.Abs(c.G - g) < 40 && Math.Abs(c.B - b) < 40;

    static int Main()
    {
        foreach (var ext in new[] { "jpg", "png", "bmp", "tif", "webp", "gif" })
            Check($"열기 {ext} 40x20·왼쪽 위 빨강", () =>
            {
                using var p = Engine.Load(Path.Combine(S, "plain." + ext));
                return p.Width == 40 && p.Height == 20 && Near(p.Frames[0].GetPixel(2, 2), 255, 0, 0) && Near(p.Frames[0].GetPixel(37, 17), 0, 0, 255);
            });
        Check("열기 ico", () => { using var p = Engine.Load(Path.Combine(S, "icon.ico")); return p.Width == 32 && p.Height == 32; });
        Check("EXIF 6 = 세로로 서고 빨강이 오른쪽 위", () =>
        {
            using var p = Engine.Load(Path.Combine(S, "exif6.jpg"));
            return p.Width == 20 && p.Height == 40 && Near(p.Frames[0].GetPixel(17, 2), 255, 0, 0) && Near(p.Frames[0].GetPixel(2, 2), 0, 0, 255);
        });
        foreach (var ext in new[] { "gif", "webp" })
            Check($"움직이는 {ext} 3장·시간 100/200/300·색 순서", () =>
            {
                using var p = Engine.Load(Path.Combine(S, "anim." + ext));
                return p.Frames.Length == 3 && p.Delays.SequenceEqual(new[] { 100, 200, 300 })
                    && Near(p.Frames[0].GetPixel(5, 5), 255, 0, 0) && Near(p.Frames[1].GetPixel(5, 5), 0, 255, 0) && Near(p.Frames[2].GetPixel(5, 5), 0, 0, 255);
            });
        Check("한글 경로", () => { using var p = Engine.Load(Path.Combine(S, "한글 사진.png")); return p.Width == 40; });
        Check("깨진 파일은 예외", () => { try { Engine.Load(Path.Combine(S, "broken.jpg")).Dispose(); return false; } catch { return true; } });
        Check("큰 그림 축소본 사다리 4100→2050→1025", () =>
        {
            using var p = Engine.Load(Path.Combine(S, "big.png"));
            return p.Levels.Select(l => l.Width).SequenceEqual(new[] { 4100, 2050, 1025 });
        });
        Check("돌리기 후 크기 바뀜", () => { using var p = Engine.Load(Path.Combine(S, "plain.png")); p.Rotate(true); return p.Width == 20 && p.Height == 40 && Near(p.Frames[0].GetPixel(17, 2), 255, 0, 0); });
        Check("폴더 순서 img1·img2·img10, txt 빠짐", () =>
        {
            var l = Engine.Siblings(Path.Combine(S, "order", "img2.png")).Select(Path.GetFileName);
            return l.SequenceEqual(new[] { "img1.png", "img2.png", "img10.png" });
        });
        Check("맞추기 = 큰 그림만 줄임", () => ViewMath.Fit(4000, 3000, 800, 600) == 0.2f && ViewMath.Fit(100, 100, 800, 600) == 1f);
        Check("커서 기준 확대 = 커서 밑 점 그대로", () =>
        {
            var o = new PointF(10, 20); var c = new PointF(300, 200);
            var n = ViewMath.ZoomAt(o, 0.5f, 2f, c);
            float ix = (c.X - o.X) / 0.5f, nx = (c.X - n.X) / 2f;
            return Math.Abs(ix - nx) < 0.01f;
        });
        Check("확대 단계 앞뒤", () => ViewMath.Next(1f, true) == 1.25f && ViewMath.Next(1f, false) == 0.75f && ViewMath.Next(0.3f, true) == 0.33f);
        Check("작은 그림은 가운데", () => { var p = ViewMath.Clamp(new PointF(-50, -50), 100, 100, 400, 300); return p.X == 150 && p.Y == 100; });
        Check("4000x3000 jpg 열기+축소본 1초 안", () =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var p = Engine.Load(Path.Combine(S, "photo4k.jpg"));
            _ = p.Levels;
            Console.WriteLine($"      걸린 시간 {sw.ElapsedMilliseconds} ms");
            return p.Width == 4000 && sw.ElapsedMilliseconds < 1000;
        });
        Console.WriteLine($"\n통과 {_pass} · 실패 {_fail}");
        return _fail == 0 ? 0 : 1;
    }
}
