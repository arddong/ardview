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
        Check("돌리기 후 크기 바뀜", () => { using var p = Engine.Load(Path.Combine(S, "plain.png")); p.Transform(RotateFlipType.Rotate90FlipNone); return p.Width == 20 && p.Height == 40 && Near(p.Frames[0].GetPixel(17, 2), 255, 0, 0); });
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
        foreach (var ext in new[] { "tga", "ppm", "psd" })
            Check($"열기 {ext} 40x20·왼쪽 위 빨강", () =>
            {
                using var p = Engine.Load(Path.Combine(S, "plain." + ext));
                return p.Width == 40 && p.Height == 20 && Near(p.Frames[0].GetPixel(2, 2), 255, 0, 0) && Near(p.Frames[0].GetPixel(37, 17), 0, 0, 255);
            });
        Check("EXIF 카메라·GPS 읽기", () =>
        {
            using var p = Engine.Load(Path.Combine(S, "gps.jpg"));
            var cam = p.Meta.Rows.FirstOrDefault(r => r.K == "카메라").V;
            Console.WriteLine($"      카메라={cam} GPS={p.Meta.Lat:0.0000},{p.Meta.Lon:0.0000}");
            return cam == "Canon EOS R6" && Math.Abs(p.Meta.Lat!.Value - 35.176) < 0.001 && Math.Abs(p.Meta.Lon!.Value - 126.912) < 0.001;
        });
        Check("EXIF 없는 그림 = 빈 정보, 오류 없음", () => { using var p = Engine.Load(Path.Combine(S, "plain.png")); return p.Meta.Rows.Count == 0 && p.Meta.Lat == null; });
        Check("히스토그램 = 빨강 1/4", () =>
        {
            using var p = Engine.Load(Path.Combine(S, "plain.png"));
            var h = p.Histogram!; int total = h[0].Sum();
            return total == 800 && Math.Abs(h[1][255] - 200) <= 2;
        });
        Check("여러 쪽 tiff 3쪽 (움직이는 그림 아님)", () => { using var p = Engine.Load(Path.Combine(S, "pages.tif")); return p.Frames.Length == 3 && p.MultiPage && !p.Animated && Near(p.Frames[1].GetPixel(5, 5), 0, 255, 0); });
        Check("RAW = 가장 큰 미리보기 64x32", () => { using var p = Engine.Load(Path.Combine(S, "fake.nef")); return p.Width == 64 && p.Height == 32 && Near(p.Frames[0].GetPixel(2, 2), 255, 0, 0); });
        Check("zip 안 그림 목록 = 그림만·이름 순서", () =>
        {
            var l = Engine.List(Path.Combine(S, "comic.zip"), out var i).Select(s => s.Entry).ToList();
            Console.WriteLine("      " + string.Join(" | ", l));
            return i == 0 && l.SequenceEqual(new[] { "chap/p1.png", "chap/p2.png", "chap/p10.png", "표지.png" });
        });
        Check("zip 안 그림 열기", () =>
        {
            var l = Engine.List(Path.Combine(S, "comic.zip"), out _);
            using var p = Engine.Load(l[1]);
            return p.Width == 20 && p.Height == 30 && Near(p.Frames[0].GetPixel(3, 3), 0, 255, 0);
        });
        Check("책갈피 키(파일|항목) 다시 열기", () =>
        {
            var l = Engine.List(Path.Combine(S, "comic.zip"), out _);
            var again = Engine.List(l[2].Key, out var i);
            return i == 2 && again[i].Entry == "chap/p10.png";
        });
        Check("이웃 폴더·압축 파일", () =>
        {
            var next = Engine.NeighborContainer(Path.Combine(S, "comic.zip"), 1);
            Console.WriteLine("      다음 = " + Path.GetFileName(next));
            return next != null && Path.GetFileName(next) == "order";
        });
        Check("두 쪽 크기 = 높이 맞춰 나란히", () => { var s = ViewMath.Pair(new Size(100, 200), new Size(50, 100)); return s.Width == 200 && s.Height == 200; });
        Check("변환 저장 jpg·png·webp·bmp 다시 열림", () =>
        {
            using var p = Engine.Load(Path.Combine(S, "plain.png"));
            var dir = Path.Combine(Path.GetTempPath(), "ardview_conv"); Directory.CreateDirectory(dir);
            foreach (var f in Engine.SaveFormats)
            {
                var path = Path.Combine(dir, "t." + f.ToLowerInvariant());
                Engine.Save(p.Frames[0], path, f, 90);
                using var back = Engine.Load(path);
                if (back.Width != 40 || !Near(back.Frames[0].GetPixel(2, 2), 255, 0, 0)) return false;
            }
            return true;
        });
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
