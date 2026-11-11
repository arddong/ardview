# arddong ArdView 아이콘을 그리는 스크립트 (ArdZip 과 같은 파란 둥근 사각 + 산·해 그림)
from PIL import Image, ImageDraw
import sys, os

BLUE = (37, 99, 235, 255)
W = (255, 255, 255, 255)

def draw(s):
    k = 4
    S = s * k
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    m = S * 0.06
    d.rounded_rectangle([m, m, S - m, S - m], radius=S * 0.22, fill=BLUE)
    # 사진 액자
    fx0, fy0, fx1, fy1 = S * 0.24, S * 0.28, S * 0.76, S * 0.72
    lw = max(k, int(S * 0.055))
    d.rounded_rectangle([fx0, fy0, fx1, fy1], radius=S * 0.06, outline=W, width=lw)
    # 해
    r = S * 0.055
    cx, cy = S * 0.61, S * 0.40
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=W)
    # 산 둘
    base = fy1 - lw * 0.6
    d.polygon([(fx0 + lw * 0.6, base), (S * 0.40, S * 0.45), (S * 0.56, base)], fill=W)
    d.polygon([(S * 0.46, base), (S * 0.60, S * 0.52), (fx1 - lw * 0.6, base)], fill=W)
    return im.resize((s, s), Image.LANCZOS)

out = sys.argv[1]
sizes = [16, 24, 32, 48, 64, 128, 256]
big = draw(256)
big.save(out, format="ICO", sizes=[(x, x) for x in sizes], append_images=[draw(x) for x in sizes[:-1]])
draw(256).save(os.path.join(os.path.dirname(out), "..", "docs", "icon.png"))
prev = Image.new("RGBA", (560, 280), (250, 250, 251, 255))
prev.alpha_composite(draw(256), (12, 12))
x = 300
for sz in (16, 32, 48):
    prev.alpha_composite(draw(sz), (x, 140 - sz // 2)); x += sz + 40
prev.save(os.path.join(os.path.dirname(out), "..", "icon_preview.png"))
