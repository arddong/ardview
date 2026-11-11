# arddong ArdView 시험 표본(형식별·EXIF 회전·움직이는 그림·한글 이름)을 만드는 스크립트
from PIL import Image
import os
o = os.path.join(os.path.dirname(__file__), "samples")
os.makedirs(o, exist_ok=True)

def quad(w, h):
    # 왼쪽 위 빨강 · 나머지 파랑 = 회전 확인용
    im = Image.new("RGB", (w, h), (0, 0, 255))
    im.paste((255, 0, 0), (0, 0, w // 2, h // 2))
    return im

base = quad(40, 20)
for ext, kw in [("jpg", {"quality": 95}), ("png", {}), ("bmp", {}), ("tif", {}), ("webp", {"lossless": True}), ("gif", {})]:
    base.save(os.path.join(o, f"plain.{ext}"), **kw)
quad(32, 32).save(os.path.join(o, "icon.ico"), sizes=[(32, 32)])
# EXIF 방향 6 = 시계 방향 90도 돌려 보여야 한다
ex = Image.Exif(); ex[0x0112] = 6
base.save(os.path.join(o, "exif6.jpg"), quality=95, exif=ex)
# 움직이는 gif·webp 3장
fr = [Image.new("RGB", (30, 30), c) for c in [(255, 0, 0), (0, 255, 0), (0, 0, 255)]]
fr[0].save(os.path.join(o, "anim.gif"), save_all=True, append_images=fr[1:], duration=[100, 200, 300], loop=0)
fr[0].save(os.path.join(o, "anim.webp"), save_all=True, append_images=fr[1:], duration=[100, 200, 300], loop=0, lossless=True)
# 한글 이름
base.save(os.path.join(o, "한글 사진.png"))
# 큰 그림 = 축소본 사다리
Image.new("RGB", (4100, 2100), (128, 128, 128)).save(os.path.join(o, "big.png"))
# 깨진 파일
open(os.path.join(o, "broken.jpg"), "wb").write(b"\xff\xd8\xff\xe0 not really a jpeg")
# 이름 순서용 폴더
s = os.path.join(o, "order"); os.makedirs(s, exist_ok=True)
for n in ["img10.png", "img2.png", "img1.png", "notes.txt"]:
    p = os.path.join(s, n)
    if n.endswith(".txt"): open(p, "w").write("x")
    else: Image.new("RGB", (4, 4)).save(p)
print("ok")
# 4K 사진 크기 jpg = 열기 속도
import random
g = Image.radial_gradient("L").resize((4000, 3000))
Image.merge("RGB", (g, g.rotate(90, expand=False), g.transpose(Image.FLIP_LEFT_RIGHT))).save(os.path.join(o, "photo4k.jpg"), quality=92)

# ── 1.1 표본 ──
import struct, zipfile, io
# TGA·PNM 은 Pillow 로
base.save(os.path.join(o, "plain.tga"))
base.save(os.path.join(o, "plain.ppm"))
# PSD = 손으로 (RGB 8비트 무압축 합친 그림)
w, h = base.size
px = base.load()
hdr = b"8BPS" + struct.pack(">H6xHIIHH", 1, 3, h, w, 8, 3)
body = struct.pack(">I", 0) + struct.pack(">I", 0) + struct.pack(">I", 0) + struct.pack(">H", 0)
planes = b"".join(bytes(px[x, y][c] for y in range(h) for x in range(w)) for c in range(3))
open(os.path.join(o, "plain.psd"), "wb").write(hdr + body + planes)
# EXIF 카메라·GPS
ex = Image.Exif()
ex[0x010F] = "Canon"; ex[0x0110] = "Canon EOS R6"
gps = {1: "N", 2: (35.0, 10.0, 33.6), 3: "E", 4: (126.0, 54.0, 43.2)}
ex[0x8825] = gps
base.save(os.path.join(o, "gps.jpg"), quality=95, exif=ex)
# 여러 쪽 tiff
quad(40, 20).save(os.path.join(o, "pages.tif"), save_all=True, append_images=[Image.new("RGB", (40, 20), (0, 255, 0)), Image.new("RGB", (40, 20), (0, 0, 255))])
# RAW 흉내 = 앞뒤 쓰레기 바이트 사이에 작은 썸네일 + 큰 미리보기 JPEG
small = io.BytesIO(); Image.new("RGB", (16, 8), (0, 255, 0)).save(small, "JPEG")
large = io.BytesIO(); quad(64, 32).save(large, "JPEG", quality=95)
open(os.path.join(o, "fake.nef"), "wb").write(b"II*\x00" + b"\x00" * 500 + small.getvalue() + b"\x11" * 300 + large.getvalue() + b"\x00" * 100)
# zip 안 그림 (한글 이름 · 하위 폴더 · 그림 아닌 것 섞음)
zp = os.path.join(o, "comic.zip")
with zipfile.ZipFile(zp, "w") as z:
    for name, col in [("p10.png", (0, 0, 255)), ("p2.png", (0, 255, 0)), ("p1.png", (255, 0, 0))]:
        b = io.BytesIO(); Image.new("RGB", (20, 30), col).save(b, "PNG"); z.writestr("chap/" + name, b.getvalue())
    z.writestr("readme.txt", "x")
    b = io.BytesIO(); Image.new("RGB", (20, 30), (9, 9, 9)).save(b, "PNG")
    zi = zipfile.ZipInfo("표지.png"); zi.flag_bits &= ~0x800
    z.writestr(zi, b.getvalue())
print("ok2")
