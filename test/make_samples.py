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
