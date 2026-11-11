// arddong ArdView 엔진 = 그림 열기(파일·압축 파일 안)·형식별 디코더·EXIF·히스토그램·폴더 순서·저장
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using MetadataExtractor.Formats.Exif;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SkiaSharp;
using MX = MetadataExtractor;

namespace ArdView;

// 그림 한 장의 자리 = 파일 경로, 또는 압축 파일 안 항목
public sealed record Src(string File, string? Entry)
{
    public string Name => Path.GetFileName((Entry ?? File).Replace('/', '\\'));
    public string Key => Entry == null ? File : File + "|" + Entry;
    public string Display => Entry == null ? File : File + " › " + Entry;
    public string Ext => Path.GetExtension(Name).ToLowerInvariant();
    public bool InArchive => Entry != null;
    public string Container => Entry != null ? File : Path.GetDirectoryName(File)!;

    public static Src Parse(string key)
    {
        var i = key.IndexOf('|');
        return i < 0 ? new Src(Path.GetFullPath(key), null) : new Src(Path.GetFullPath(key[..i]), key[(i + 1)..]);
    }
}

// EXIF 에서 뽑은 것
public sealed class Meta
{
    public List<(string K, string V)> Rows { get; } = new();
    public double? Lat, Lon;
    public int Orientation = 1;
}

// 열어 둔 그림 한 장 = 프레임(움직이는 그림·여러 쪽 tiff 면 여럿) + 프레임 시간 + 축소본 사다리 + 정보
public sealed class Picture : IDisposable
{
    public Bitmap[] Frames { get; }
    public int[] Delays { get; }
    public string Format { get; }
    public long FileSize { get; set; }
    public bool Oriented { get; set; }
    public Meta Meta { get; set; } = new();
    public int[][]? Histogram { get; set; }
    List<Bitmap>? _levels;
    readonly object _gate = new();

    public Picture(Bitmap[] frames, int[] delays, string format, long fileSize = 0)
    {
        Frames = frames; Delays = delays; Format = format; FileSize = fileSize;
    }

    public int Width => Frames[0].Width;
    public int Height => Frames[0].Height;
    public bool Animated => Frames.Length > 1 && Delays.Any(d => d > 0);
    public bool MultiPage => Frames.Length > 1 && !Animated;
    public bool Portrait => Height > Width;

    // 큰 그림을 줄여 볼 때마다 원본에서 줄이면 느리다. 반씩 줄인 사본을 미리 만들어 두고 알맞은 층에서 그린다
    public IReadOnlyList<Bitmap> Levels
    {
        get
        {
            lock (_gate)
            {
                if (_levels != null) return _levels;
                var list = new List<Bitmap> { Frames[0] };
                if (!Animated)
                {
                    var cur = Frames[0];
                    while (cur.Width >= 1024 && cur.Height >= 1024)
                    {
                        var next = Engine.Resize(cur, cur.Width / 2, cur.Height / 2, InterpolationMode.HighQualityBilinear);
                        list.Add(next);
                        cur = next;
                    }
                }
                return _levels = list;
            }
        }
    }

    public Bitmap Smallest => Levels[^1];

    // 보는 동안 돌리기·뒤집기. 파일은 건드리지 않는다
    public void Transform(RotateFlipType t)
    {
        lock (_gate)
        {
            foreach (var f in Frames) f.RotateFlip(t);
            DropLevels();
        }
    }

    void DropLevels()
    {
        if (_levels == null) return;
        foreach (var l in _levels.Skip(1)) l.Dispose();
        _levels = null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            DropLevels();
            foreach (var f in Frames) f.Dispose();
        }
    }
}

// ───────────────────────── 압축 파일 안 그림 (풀지 않고 본다) ─────────────────────────
public static class Arc
{
    public static readonly string[] Exts = { ".zip", ".cbz", ".rar", ".cbr", ".7z", ".cb7", ".tar", ".cbt" };
    static readonly Encoding Cp949;
    static readonly Dictionary<string, IArchive> Open = new(StringComparer.OrdinalIgnoreCase);
    static readonly object Gate = new();

    static Arc()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp949 = Encoding.GetEncoding(949);
    }

    public static bool Is(string p) => Exts.Contains(Path.GetExtension(p).ToLowerInvariant());

    // UTF-8 표시가 없는 zip 이름은 한국 윈도우 기본(cp949)으로 읽는다 (ArdZip 과 같다)
    static ReaderOptions Opts(string path) => new()
    {
        ArchiveEncoding = new ArchiveEncoding { Default = Path.GetExtension(path).ToLowerInvariant() is ".zip" or ".cbz" ? Cp949 : Encoding.UTF8 },
    };

    static IArchive Get(string path)
    {
        if (!Open.TryGetValue(path, out var a))
        {
            a = ArchiveFactory.OpenArchive(path, Opts(path));
            Open[path] = a;
        }
        return a;
    }

    public static List<string> Entries(string path)
    {
        lock (Gate)
        {
            var list = Get(path).Entries.Where(e => !e.IsDirectory && e.Key != null && Engine.IsImageName(e.Key)).Select(e => e.Key!).ToList();
            list.Sort(Engine.NaturalCompare);
            return list;
        }
    }

    public static byte[] Read(string path, string key)
    {
        lock (Gate)
        {
            var e = Get(path).Entries.First(x => x.Key == key);
            using var s = e.OpenEntryStream();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            return ms.ToArray();
        }
    }

    // 지금 보는 압축 파일만 열어 두고 나머지는 닫는다
    public static void CloseExcept(string? keep)
    {
        lock (Gate)
        {
            foreach (var k in Open.Keys.Where(k => !string.Equals(k, keep, StringComparison.OrdinalIgnoreCase)).ToList())
            {
                try { Open[k].Dispose(); } catch { }
                Open.Remove(k);
            }
        }
    }
}

// ───────────────────────── 엔진 ─────────────────────────
public static class Engine
{
    public static readonly string[] Exts =
    {
        ".jpg", ".jpeg", ".jfif", ".jpe", ".png", ".apng", ".gif", ".webp", ".bmp", ".dib", ".ico", ".tif", ".tiff", ".wbmp",
        ".heic", ".heif", ".avif", ".jxr", ".wdp", ".hdp", ".dds", ".psd", ".tga", ".pnm", ".pbm", ".pgm", ".ppm",
    };

    // RAW 는 안에 든 가장 큰 미리보기 JPEG 으로 보여 준다
    public static readonly string[] Raw =
        { ".dng", ".cr2", ".cr3", ".crw", ".nef", ".nrw", ".orf", ".rw2", ".pef", ".sr2", ".arw", ".raf", ".srw" };

    public static bool IsImageName(string p)
    {
        var e = Path.GetExtension(p).ToLowerInvariant();
        return Exts.Contains(e) || Raw.Contains(e);
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    static extern int StrCmpLogicalW(string a, string b);

    // 탐색기와 같은 순서 = 사진2 가 사진10 보다 앞
    public static int NaturalCompare(string a, string b) => StrCmpLogicalW(a, b);

    public static List<string> ImagesIn(string dir)
    {
        var list = System.IO.Directory.EnumerateFiles(dir).Where(IsImageName).ToList();
        list.Sort((a, b) => NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)));
        return list;
    }

    // 하위 호환 (1.0 시험)
    public static List<string> Siblings(string file) => ImagesIn(Path.GetDirectoryName(Path.GetFullPath(file))!);

    // 열 자리(파일·폴더·압축 파일) → 넘겨 볼 목록과 처음 보일 순번
    public static List<Src> List(string path, out int index)
    {
        index = 0;
        if (path.Contains('|'))
        {
            var s = Src.Parse(path);
            var list = Arc.Entries(s.File).Select(e => new Src(s.File, e)).ToList();
            index = Math.Max(0, list.FindIndex(x => x.Entry == s.Entry));
            return list;
        }
        var full = Path.GetFullPath(path);
        if (System.IO.Directory.Exists(full)) return ImagesIn(full).Select(f => new Src(f, null)).ToList();
        if (Arc.Is(full)) return Arc.Entries(full).Select(e => new Src(full, e)).ToList();
        var files = ImagesIn(Path.GetDirectoryName(full)!).Select(f => new Src(f, null)).ToList();
        index = files.FindIndex(f => string.Equals(f.File, full, StringComparison.OrdinalIgnoreCase));
        if (index < 0) { files.Insert(0, new Src(full, null)); index = 0; }
        return files;
    }

    // 이웃 폴더·압축 파일 (꿀뷰 [ ])
    public static string? NeighborContainer(string container, int step)
    {
        var parent = Path.GetDirectoryName(container.TrimEnd('\\'));
        if (parent == null) return null;
        var items = System.IO.Directory.EnumerateDirectories(parent).Concat(System.IO.Directory.EnumerateFiles(parent).Where(Arc.Is)).ToList();
        items.Sort((a, b) => NaturalCompare(Path.GetFileName(a), Path.GetFileName(b)));
        int i = items.FindIndex(x => string.Equals(x.TrimEnd('\\'), container.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        if (i < 0) return null;
        for (int j = i + step; j >= 0 && j < items.Count; j += step)
        {
            try
            {
                if (Arc.Is(items[j]) ? Arc.Entries(items[j]).Count > 0 : System.IO.Directory.EnumerateFiles(items[j]).Any(IsImageName)) return items[j];
            }
            catch { }
        }
        return null;
    }

    public static byte[] ReadBytes(Src s) => s.Entry == null ? System.IO.File.ReadAllBytes(s.File) : Arc.Read(s.File, s.Entry);

    // 열기 = 읽기 → 디코드 → EXIF → 방향 → 축소본 → 히스토그램 (모두 바탕 일꾼에서)
    public static Picture Load(Src s)
    {
        var bytes = ReadBytes(s);
        var pic = Decode(bytes, s.Ext);
        pic.FileSize = bytes.LongLength;
        pic.Meta = ReadMeta(bytes);
        if (!pic.Oriented && pic.Meta.Orientation > 1)
        {
            var f = Orient((SKEncodedOrigin)pic.Meta.Orientation);
            if (f != RotateFlipType.RotateNoneFlipNone) pic.Transform(f);
        }
        _ = pic.Levels;
        pic.Histogram = Histogram(pic.Smallest);
        return pic;
    }

    // 하위 호환 (1.0 시험)
    public static Picture Load(string path) => Load(new Src(Path.GetFullPath(path), null));

    // 움직이는 그림이 너무 크면 첫 장만 (메모리 보호)
    const long MaxAnimBytes = 600L << 20;

    public static Picture Decode(byte[] bytes, string ext)
    {
        if (Raw.Contains(ext)) return DecodeRaw(bytes);
        switch (ext)
        {
            case ".psd": return Formats.Psd(bytes);
            case ".tga": return Formats.Tga(bytes);
            case ".pnm": case ".pbm": case ".pgm": case ".ppm": return Formats.Pnm(bytes);
        }
        var sk = DecodeSkia(bytes);
        if (sk != null) return sk;
        Exception? last = null;
        try { return DecodeGdi(bytes); } catch (Exception ex) { last = ex; }
        try { return DecodeWic(bytes); } catch (Exception ex) { last = ex; }
        if (ext is ".heic" or ".heif" or ".avif")
            throw new NotSupportedException("윈도우에 HEIF·AV1 그림 확장이 없으면 열 수 없습니다 (Microsoft Store 「HEIF 이미지 확장」)", last);
        throw new NotSupportedException("이 그림 형식은 열 수 없습니다", last);
    }

    public static Picture? DecodeSkia(byte[] bytes)
    {
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        return codec == null ? null : FromCodec(codec);
    }

    static Picture? FromCodec(SKCodec codec)
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
                delays[i] = n > 1 ? (d <= 10 ? 100 : d) : 0;
            }
            var frames = new Bitmap[n];
            for (int i = 0; i < n; i++) frames[i] = ToGdi(sk[i]);
            var flip = Orient(codec.EncodedOrigin);
            if (flip != RotateFlipType.RotateNoneFlipNone) foreach (var f in frames) f.RotateFlip(flip);
            return new Picture(frames, delays[..n], fmt) { Oriented = true };
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

    // BGRA 바이트(곱하지 않은 알파) → 화면용 그림
    public static unsafe Bitmap FromBgra(int w, int h, byte[] bgra)
    {
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        var bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, bmp.PixelFormat);
        try
        {
            var d = (byte*)bd.Scan0;
            for (int y = 0; y < h; y++)
            {
                var row = d + (long)y * bd.Stride;
                int s = y * w * 4;
                for (int x = 0; x < w; x++, s += 4)
                {
                    int a = bgra[s + 3];
                    row[x * 4] = (byte)(bgra[s] * a / 255);
                    row[x * 4 + 1] = (byte)(bgra[s + 1] * a / 255);
                    row[x * 4 + 2] = (byte)(bgra[s + 2] * a / 255);
                    row[x * 4 + 3] = (byte)a;
                }
            }
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

    // Skia 가 못 여는 것(tiff 여러 쪽 등)은 윈도우 기본 디코더로
    static Picture DecodeGdi(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var img = Image.FromStream(ms);
        var fmt = img.RawFormat.Equals(ImageFormat.Tiff) ? "TIFF" : img.RawFormat.Equals(ImageFormat.Icon) ? "ICO" : "IMAGE";
        int pages = 1;
        try { pages = Math.Max(1, img.GetFrameCount(FrameDimension.Page)); } catch { }
        pages = Math.Min(pages, 500);
        var frames = new Bitmap[pages];
        for (int i = 0; i < pages; i++)
        {
            if (pages > 1) img.SelectActiveFrame(FrameDimension.Page, i);
            var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp)) g.DrawImage(img, 0, 0, img.Width, img.Height);
            frames[i] = bmp;
        }
        var pic = new Picture(frames, new int[pages], fmt);
        if (img.PropertyIdList.Contains(0x0112))
        {
            var v = img.GetPropertyItem(0x0112)?.Value;
            if (v is { Length: > 0 }) { var f = Orient((SKEncodedOrigin)v[0]); if (f != RotateFlipType.RotateNoneFlipNone) pic.Transform(f); }
        }
        pic.Oriented = true;
        return pic;
    }

    // 윈도우 이미징(WIC) = HEIC·AVIF(확장 설치 시)·JXR·DDS
    static unsafe Picture DecodeWic(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        var dec = System.Windows.Media.Imaging.BitmapDecoder.Create(ms, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        var f = dec.Frames[0];
        var conv = new System.Windows.Media.Imaging.FormatConvertedBitmap(f, System.Windows.Media.PixelFormats.Pbgra32, null, 0);
        int w = conv.PixelWidth, h = conv.PixelHeight;
        var bmp = new Bitmap(w, h, PixelFormat.Format32bppPArgb);
        var bd = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, bmp.PixelFormat);
        try { conv.CopyPixels(System.Windows.Int32Rect.Empty, bd.Scan0, bd.Stride * h, bd.Stride); }
        finally { bmp.UnlockBits(bd); }
        var name = dec.CodecInfo?.FriendlyName ?? "WIC";
        return new Picture(new[] { bmp }, new[] { 0 }, name.Replace(" Decoder", "").ToUpperInvariant());
    }

    // RAW 안의 JPEG 들 중 가장 큰 것 (꿀뷰도 미리보기로 보여 준다)
    static unsafe Picture DecodeRaw(byte[] b)
    {
        int bestAt = -1; long bestArea = 0;
        int probes = 0;
        fixed (byte* p = b)
        {
            for (int i = 0; i + 4 < b.Length && probes < 300; i++)
            {
                if (b[i] != 0xFF || b[i + 1] != 0xD8 || b[i + 2] != 0xFF) continue;
                probes++;
                int len = Math.Min(b.Length - i, 1 << 18);
                using var d = SKData.CreateCopy((IntPtr)(p + i), len);
                using var c = SKCodec.Create(d);
                if (c == null) continue;
                long area = (long)c.Info.Width * c.Info.Height;
                if (area > bestArea) { bestArea = area; bestAt = i; }
            }
            if (bestAt >= 0)
            {
                using var d = SKData.CreateCopy((IntPtr)(p + bestAt), b.Length - bestAt);
                using var c = SKCodec.Create(d);
                var pic = c == null ? null : FromCodec(c);
                if (pic != null) return new Picture(pic.Frames, pic.Delays, "RAW 미리보기");
            }
        }
        return DecodeSkia(b) ?? throw new NotSupportedException("RAW 안에서 미리보기 그림을 찾지 못했습니다");
    }

    // ── EXIF ──
    public static Meta ReadMeta(byte[] bytes)
    {
        var m = new Meta();
        IReadOnlyList<MX.Directory> dirs;
        try { using var ms = new MemoryStream(bytes); dirs = MX.ImageMetadataReader.ReadMetadata(ms); }
        catch { return m; }

        var exif = dirs.OfType<ExifDirectoryBase>().Where(d => d is not ExifThumbnailDirectory).ToList();
        string? Get(int tag) => exif.Where(d => d.ContainsTag(tag)).Select(d => d.GetDescription(tag)?.Trim()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
        void Add(string k, string? v) { if (!string.IsNullOrWhiteSpace(v)) m.Rows.Add((k, v)); }

        var make = Get(ExifDirectoryBase.TagMake);
        var model = Get(ExifDirectoryBase.TagModel);
        Add("카메라", model != null && make != null && !model.StartsWith(make.Split(' ')[0], StringComparison.OrdinalIgnoreCase) ? make + " " + model : model ?? make);
        Add("렌즈", Get(ExifDirectoryBase.TagLensModel));
        Add("찍은 때", Get(ExifDirectoryBase.TagDateTimeOriginal) ?? Get(ExifDirectoryBase.TagDateTime));
        Add("셔터 속도", Get(ExifDirectoryBase.TagExposureTime));
        Add("조리개", Get(ExifDirectoryBase.TagFNumber));
        Add("ISO", Get(ExifDirectoryBase.TagIsoEquivalent));
        var focal = Get(ExifDirectoryBase.TagFocalLength);
        var f35 = Get(ExifDirectoryBase.Tag35MMFilmEquivFocalLength);
        Add("초점 거리", focal != null && f35 != null && f35 != "0 mm" && f35 != focal ? $"{focal} (35mm 환산 {f35})" : focal);
        Add("노출 보정", Get(ExifDirectoryBase.TagExposureBias));
        Add("노출 프로그램", Get(ExifDirectoryBase.TagExposureProgram));
        Add("측광", Get(ExifDirectoryBase.TagMeteringMode));
        Add("플래시", Get(ExifDirectoryBase.TagFlash));
        Add("화이트밸런스", Get(ExifDirectoryBase.TagWhiteBalanceMode));
        Add("색 공간", Get(ExifDirectoryBase.TagColorSpace));
        Add("프로그램", Get(ExifDirectoryBase.TagSoftware));
        Add("만든이", Get(ExifDirectoryBase.TagArtist));
        Add("저작권", Get(ExifDirectoryBase.TagCopyright));

        foreach (var d in dirs.OfType<ExifIfd0Directory>())
            if (d.ContainsTag(ExifDirectoryBase.TagOrientation))
            {
                try { m.Orientation = Convert.ToInt32(d.GetObject(ExifDirectoryBase.TagOrientation)); } catch { }
                break;
            }

        var gps = dirs.OfType<GpsDirectory>().FirstOrDefault();
        if (gps?.GetGeoLocation() is { } loc && !(loc.Latitude == 0 && loc.Longitude == 0))
        {
            m.Lat = loc.Latitude; m.Lon = loc.Longitude;
            Add("GPS", $"{loc.Latitude:0.000000}, {loc.Longitude:0.000000}");
            Add("고도", gps.GetDescription(GpsDirectory.TagAltitude));
        }
        return m;
    }

    // ── 히스토그램 = 밝기·R·G·B 각 256 칸 ──
    public static unsafe int[][] Histogram(Bitmap b)
    {
        var h = new[] { new int[256], new int[256], new int[256], new int[256] };
        var bd = b.LockBits(new Rectangle(0, 0, b.Width, b.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            for (int y = 0; y < b.Height; y++)
            {
                var row = (byte*)bd.Scan0 + (long)y * bd.Stride;
                for (int x = 0; x < b.Width; x++)
                {
                    int a = row[x * 4 + 3];
                    if (a == 0) continue;
                    int bl = row[x * 4] * 255 / a, g = row[x * 4 + 1] * 255 / a, r = row[x * 4 + 2] * 255 / a;
                    bl = Math.Min(255, bl); g = Math.Min(255, g); r = Math.Min(255, r);
                    h[1][r]++; h[2][g]++; h[3][bl]++;
                    h[0][(r * 299 + g * 587 + bl * 114) / 1000]++;
                }
            }
        }
        finally { b.UnlockBits(bd); }
        return h;
    }

    // ── 크기 바꾸기·저장 (변환) ──
    public static Bitmap Resize(Bitmap src, int w, int h, InterpolationMode mode = InterpolationMode.HighQualityBicubic)
    {
        var next = new Bitmap(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppPArgb);
        using var g = Graphics.FromImage(next);
        g.InterpolationMode = mode;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.CompositingMode = CompositingMode.SourceCopy;
        using var wrap = new ImageAttributes();
        wrap.SetWrapMode(WrapMode.TileFlipXY);
        g.DrawImage(src, new Rectangle(0, 0, next.Width, next.Height), 0, 0, src.Width, src.Height, GraphicsUnit.Pixel, wrap);
        return next;
    }

    public static readonly string[] SaveFormats = { "JPG", "PNG", "WEBP", "BMP" };

    public static unsafe void Save(Bitmap bmp, string path, string fmt, int quality)
    {
        if (fmt == "BMP")
        {
            using var flat = new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format24bppRgb);
            using (var g = Graphics.FromImage(flat)) { g.Clear(Color.White); g.DrawImage(bmp, 0, 0, bmp.Width, bmp.Height); }
            flat.Save(path, ImageFormat.Bmp);
            return;
        }
        var info = new SKImageInfo(bmp.Width, bmp.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using var sk = new SKBitmap(info);
        var bd = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try
        {
            var d = (byte*)sk.GetPixels();
            for (int y = 0; y < bmp.Height; y++)
                Buffer.MemoryCopy((byte*)bd.Scan0 + (long)y * bd.Stride, d + (long)y * sk.RowBytes, sk.RowBytes, bmp.Width * 4L);
        }
        finally { bmp.UnlockBits(bd); }
        SKBitmap target = sk;
        SKBitmap? flatSk = null;
        if (fmt == "JPG")
        {
            // JPG 는 투명이 없다 = 흰 바탕에 얹는다
            flatSk = new SKBitmap(info);
            using var c = new SKCanvas(flatSk);
            c.Clear(SKColors.White);
            c.DrawBitmap(sk, 0, 0);
            target = flatSk;
        }
        try
        {
            using var img = SKImage.FromBitmap(target);
            var ef = fmt switch { "PNG" => SKEncodedImageFormat.Png, "WEBP" => SKEncodedImageFormat.Webp, _ => SKEncodedImageFormat.Jpeg };
            using var data = img.Encode(ef, quality) ?? throw new IOException(fmt + " 로 저장하지 못했습니다");
            using var fs = System.IO.File.Create(path);
            data.SaveTo(fs);
        }
        finally { flatSk?.Dispose(); }
    }

    public static string Unique(string path)
    {
        if (!System.IO.File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (int i = 2; ; i++)
        {
            var p = Path.Combine(dir, $"{stem} ({i}){ext}");
            if (!System.IO.File.Exists(p)) return p;
        }
    }
}

// ───────────────────────── Skia·윈도우가 못 여는 형식 (PSD 합친 그림·TGA·PNM) ─────────────────────────
public static class Formats
{
    static int U16(byte[] b, int o) => (b[o] << 8) | b[o + 1];
    static int U32(byte[] b, int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];

    // PSD = 파일 끝의 「합친 그림」 만 읽는다 (8·16비트, 회색·RGB·CMYK, 무압축·RLE)
    public static Picture Psd(byte[] b)
    {
        if (b.Length < 26 || Encoding.ASCII.GetString(b, 0, 4) != "8BPS") throw new InvalidDataException("PSD 머리가 아닙니다");
        if (U16(b, 4) != 1) throw new NotSupportedException("PSB(큰 PSD)는 아직 못 엽니다");
        int ch = U16(b, 12), h = U32(b, 14), w = U32(b, 18), depth = U16(b, 22), mode = U16(b, 24);
        if (depth != 8 && depth != 16) throw new NotSupportedException($"{depth}비트 PSD 는 못 엽니다");
        int o = 26;
        o += 4 + U32(b, o); // 색 모드 자료
        o += 4 + U32(b, o); // 이미지 리소스
        o += 4 + U32(b, o); // 레이어
        int comp = U16(b, o); o += 2;
        int bpc = depth / 8;
        int rowBytes = w * bpc;
        var planes = new byte[ch][];
        for (int c = 0; c < ch; c++) planes[c] = new byte[rowBytes * h];
        if (comp == 0)
        {
            for (int c = 0; c < ch; c++) { Buffer.BlockCopy(b, o, planes[c], 0, rowBytes * h); o += rowBytes * h; }
        }
        else if (comp == 1)
        {
            int counts = o; o += ch * h * 2;
            for (int c = 0; c < ch; c++)
                for (int y = 0; y < h; y++)
                {
                    int len = U16(b, counts + (c * h + y) * 2), end = o + len, dst = y * rowBytes, lim = dst + rowBytes;
                    while (o < end && dst < lim)
                    {
                        int n = (sbyte)b[o++];
                        if (n >= 0) { int k = Math.Min(n + 1, lim - dst); Buffer.BlockCopy(b, o, planes[c], dst, k); o += n + 1; dst += k; }
                        else if (n != -128) { byte v = b[o++]; for (int k = 0; k < 1 - n && dst < lim; k++) planes[c][dst++] = v; }
                    }
                    o = end;
                }
        }
        else throw new NotSupportedException("이 압축 방식의 PSD 는 못 엽니다");

        byte P(int c, int i) => planes[c][i * bpc];
        var px = new byte[w * h * 4];
        for (int i = 0; i < w * h; i++)
        {
            byte r, g, bl, a = 255;
            if (mode == 4 && ch >= 4)
            {
                // CMYK 는 뒤집혀 저장된다 (0 = 잉크 가득)
                int k = P(3, i);
                r = (byte)(P(0, i) * k / 255); g = (byte)(P(1, i) * k / 255); bl = (byte)(P(2, i) * k / 255);
                if (ch >= 5) a = P(4, i);
            }
            else if (mode == 1 || mode == 8 || ch < 3)
            {
                r = g = bl = P(0, i);
                if (ch >= 2) a = P(1, i);
            }
            else
            {
                r = P(0, i); g = P(1, i); bl = P(2, i);
                if (ch >= 4) a = P(3, i);
            }
            px[i * 4] = bl; px[i * 4 + 1] = g; px[i * 4 + 2] = r; px[i * 4 + 3] = a;
        }
        return new Picture(new[] { Engine.FromBgra(w, h, px) }, new[] { 0 }, "PSD");
    }

    // TGA = 트루컬러·회색, 무압축·RLE
    public static Picture Tga(byte[] b)
    {
        int idLen = b[0], cmapType = b[1], type = b[2];
        int cmapLen = b[5] | (b[6] << 8), cmapBits = b[7];
        int w = b[12] | (b[13] << 8), h = b[14] | (b[15] << 8), bpp = b[16], desc = b[17];
        if (type is not (2 or 3 or 10 or 11)) throw new NotSupportedException("이 종류의 TGA 는 못 엽니다");
        int o = 18 + idLen + (cmapType == 1 ? cmapLen * ((cmapBits + 7) / 8) : 0);
        int bytes = bpp / 8;
        bool gray = type is 3 or 11, rle = type >= 9;
        var px = new byte[w * h * 4];
        int count = 0;
        void Put(int at)
        {
            int i = count++;
            int x = i % w, y = i / w;
            if ((desc & 0x20) == 0) y = h - 1 - y;
            int d = (y * w + x) * 4;
            if (gray) { px[d] = px[d + 1] = px[d + 2] = b[at]; px[d + 3] = bytes == 2 ? b[at + 1] : (byte)255; }
            else if (bytes == 2)
            {
                int v = b[at] | (b[at + 1] << 8);
                px[d] = (byte)((v & 31) * 255 / 31); px[d + 1] = (byte)(((v >> 5) & 31) * 255 / 31); px[d + 2] = (byte)(((v >> 10) & 31) * 255 / 31); px[d + 3] = 255;
            }
            else { px[d] = b[at]; px[d + 1] = b[at + 1]; px[d + 2] = b[at + 2]; px[d + 3] = bytes == 4 ? b[at + 3] : (byte)255; }
        }
        while (count < w * h && o < b.Length)
        {
            if (!rle) { Put(o); o += bytes; continue; }
            int hdr = b[o++], n = (hdr & 0x7F) + 1;
            if ((hdr & 0x80) != 0) { for (int k = 0; k < n && count < w * h; k++) Put(o); o += bytes; }
            else for (int k = 0; k < n && count < w * h; k++) { Put(o); o += bytes; }
        }
        // 32비트인데 알파가 모두 0 이면 알파를 안 쓴 그림이다
        if (bytes == 4 && !Enumerable.Range(0, w * h).Any(i => px[i * 4 + 3] != 0))
            for (int i = 0; i < w * h; i++) px[i * 4 + 3] = 255;
        return new Picture(new[] { Engine.FromBgra(w, h, px) }, new[] { 0 }, "TGA");
    }

    // PNM = P1~P6
    public static Picture Pnm(byte[] b)
    {
        int o = 0;
        string Tok()
        {
            while (o < b.Length)
            {
                if (b[o] == '#') { while (o < b.Length && b[o] != '\n') o++; }
                else if (char.IsWhiteSpace((char)b[o])) o++;
                else break;
            }
            int s = o;
            while (o < b.Length && !char.IsWhiteSpace((char)b[o]) && b[o] != '#') o++;
            return Encoding.ASCII.GetString(b, s, o - s);
        }
        var magic = Tok();
        if (magic.Length != 2 || magic[0] != 'P' || magic[1] < '1' || magic[1] > '6') throw new InvalidDataException("PNM 머리가 아닙니다");
        int kind = magic[1] - '0';
        int w = int.Parse(Tok()), h = int.Parse(Tok());
        int max = kind is 1 or 4 ? 1 : int.Parse(Tok());
        o++; // 머리 뒤 공백 하나
        var px = new byte[w * h * 4];
        int ch = kind is 3 or 6 ? 3 : 1;
        int wide = max > 255 ? 2 : 1;
        for (int i = 0; i < w * h; i++)
        {
            var v = new int[3];
            for (int c = 0; c < ch; c++)
            {
                int val;
                if (kind <= 3) val = int.Parse(kind == 1 ? ((char)NextBit()).ToString() : Tok());
                else if (kind == 4) { int x = i % w, y = i / w, rb = (w + 7) / 8; val = (b[o + y * rb + x / 8] >> (7 - x % 8)) & 1; }
                else { val = wide == 2 ? (b[o] << 8) | b[o + 1] : b[o]; o += wide; }
                v[c] = kind is 1 or 4 ? (val == 1 ? 0 : 255) : val * 255 / max;
            }
            if (ch == 1) v[1] = v[2] = v[0];
            px[i * 4] = (byte)v[2]; px[i * 4 + 1] = (byte)v[1]; px[i * 4 + 2] = (byte)v[0]; px[i * 4 + 3] = 255;
        }
        return new Picture(new[] { Engine.FromBgra(w, h, px) }, new[] { 0 }, "PNM");

        byte NextBit()
        {
            while (o < b.Length && b[o] != '0' && b[o] != '1') o++;
            return b[o++];
        }
    }
}
