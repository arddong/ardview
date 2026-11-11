# arddong ArdView 아이콘을 그리는 스크립트 (ArdZip 파랑과 헷갈리지 않게 = 꿀색(호박→주황) 바탕 + 흰 렌즈)
from PIL import Image, ImageDraw
import sys, os

TOP = (255, 190, 40)
BOT = (244, 120, 18)
W = (255, 255, 255, 255)

def draw(s):
    k = 4
    S = s * k
    grad = Image.new("RGBA", (S, S))
    gd = ImageDraw.Draw(grad)
    for y in range(S):
        t = y / (S - 1)
        gd.line([(0, y), (S, y)], fill=tuple(int(TOP[i] + (BOT[i] - TOP[i]) * t) for i in range(3)) + (255,))
    mask = Image.new("L", (S, S), 0)
    m = S * 0.06
    ImageDraw.Draw(mask).rounded_rectangle([m, m, S - m, S - m], radius=S * 0.22, fill=255)
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    im.paste(grad, (0, 0), mask)
    d = ImageDraw.Draw(im)
    c = S / 2
    # 렌즈 = 굵은 흰 고리 + 가운데 눈동자 + 반짝
    R = S * 0.27
    lw = S * (0.075 if s >= 32 else 0.10)
    d.ellipse([c - R, c - R, c + R, c + R], outline=W, width=int(lw))
    r = S * (0.11 if s >= 32 else 0.13)
    d.ellipse([c - r, c - r, c + r, c + r], fill=W)
    if False:
        g = S * 0.045
        gx, gy = c + R * 0.62, c - R * 0.62
        d.ellipse([gx - g, gy - g, gx + g, gy + g], fill=W)
    return im.resize((s, s), Image.LANCZOS)

out = sys.argv[1]
sizes = [16, 24, 32, 48, 64, 128, 256]
draw(256).save(out, format="ICO", sizes=[(x, x) for x in sizes], append_images=[draw(x) for x in sizes[:-1]])
draw(256).save(os.path.join(os.path.dirname(out), "..", "docs", "icon.png"))
# 미리보기 = ArdZip 과 나란히
prev = Image.new("RGBA", (620, 300), (250, 250, 251, 255))
prev.alpha_composite(draw(256), (12, 22))
zip_png = os.path.join(os.path.dirname(out), "..", "..", "ardzip", "docs", "icon.png")
x = 300
for sz in (16, 32, 48, 64):
    prev.alpha_composite(draw(sz), (x, 90 - sz // 2))
    if os.path.exists(zip_png):
        z = Image.open(zip_png).convert("RGBA").resize((sz, sz), Image.LANCZOS)
        prev.alpha_composite(z, (x, 200 - sz // 2))
    x += sz + 36
prev.save(os.path.join(os.path.dirname(out), "..", "icon_preview.png"))
