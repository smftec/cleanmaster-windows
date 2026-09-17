# -*- coding: utf-8 -*-
"""生成应用图标 app.ico：蓝色渐变圆角方块 + 白色刷子（多尺寸 PNG 帧 ICO）"""
from PIL import Image, ImageDraw
import io, struct, os

def make_icon(size: int) -> Image.Image:
    s = size * 4  # 4x 超采样抗锯齿
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    # 背景渐变圆角方块
    radius = int(s * 0.22)
    grad = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    gd = ImageDraw.Draw(grad)
    top = (66, 133, 244)
    bottom = (26, 100, 220)
    for y in range(s):
        t = y / max(1, s - 1)
        c = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,)
        gd.line([(0, y), (s, y)], fill=c)
    mask = Image.new("L", (s, s), 0)
    md = ImageDraw.Draw(mask)
    md.rounded_rectangle([0, 0, s - 1, s - 1], radius=radius, fill=255)
    img.paste(grad, (0, 0), mask)

    # 白色刷子：绕中心旋转 -45° 的手柄 + 刷头
    brush = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    bd = ImageDraw.Draw(brush)
    cx = cy = s / 2
    w = s * 0.115
    handle_h = s * 0.44
    head_h = s * 0.30
    head_w = w * 1.9
    white = (255, 255, 255, 255)
    # 手柄
    bd.rounded_rectangle([cx - w / 2, cy - handle_h, cx + w / 2, cy], radius=w / 2, fill=white)
    # 刷头
    bd.rounded_rectangle([cx - head_w / 2, cy - head_h * 0.12, cx + head_w / 2, cy + head_h],
                         radius=head_w * 0.30, fill=white)
    brush = brush.rotate(-45, resample=Image.BICUBIC, center=(cx, cy))
    img.alpha_composite(brush)
    return img.resize((size, size), Image.LANCZOS)

out = os.path.join(os.path.dirname(__file__), "..", "src", "CleanMaster.App", "Assets", "app.ico")
out = os.path.abspath(out)
os.makedirs(os.path.dirname(out), exist_ok=True)

sizes = [16, 24, 32, 48, 64, 128, 256]
frames = [make_icon(sz) for sz in sizes]

with open(out, "wb") as f:
    f.write(struct.pack("<HHH", 0, 1, len(sizes)))
    offset = 6 + 16 * len(sizes)
    for sz, im in zip(sizes, frames):
        b = io.BytesIO()
        im.save(b, "PNG")
        data = b.getvalue()
        szb = 0 if sz >= 256 else sz
        f.write(struct.pack("<BBBBHHII", szb, szb, 0, 0, 1, 32, len(data), offset))
        offset += len(data)
    for im in frames:
        b = io.BytesIO()
        im.save(b, "PNG")
        f.write(b.getvalue())

print("icon written:", out)
