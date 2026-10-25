# -*- coding: utf-8 -*-
"""从 AI 生成的源图构建 app.ico(多尺寸 PNG 帧 ICO)。
用法: python scripts/make_icon_from_png.py [源图路径]
默认源图: output/imagegen/icon-raw.png (1024x1024 全幅图标面)
处理: 22% 圆角蒙版 → 16..256 帧;≤48px 帧加轻微锐化保证小尺寸清晰。
"""
import io, os, struct, sys
from PIL import Image, ImageDraw, ImageFilter

SRC = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    os.path.dirname(__file__), "..", "output", "imagegen", "icon-user.png")
SIZES = [16, 24, 32, 48, 64, 128, 256]
RADIUS = 0.22  # 与应用内卡片圆角一致


def rounded_master(img: Image.Image) -> Image.Image:
    """放大到 2048 打圆角蒙版再回 1024,保证 256 帧的圆角平滑。"""
    big = img.resize((2048, 2048), Image.LANCZOS)
    mask = Image.new("L", (2048, 2048), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        [0, 0, 2047, 2047], radius=int(2048 * RADIUS), fill=255)
    out = Image.new("RGBA", (2048, 2048), (0, 0, 0, 0))
    out.paste(big, (0, 0), mask)
    return out.resize((1024, 1024), Image.LANCZOS)


def frame(master: Image.Image, size: int) -> Image.Image:
    if size <= 32:
        # 小尺寸帧:中心 78% 裁切放大主体(背景为连续蓝渐变,裁切无缝)
        m = master.size[0]
        pad = int(m * 0.11)
        crop = master.crop((pad, pad, m - pad, m - pad))
        mask = Image.new("L", crop.size, 0)
        ImageDraw.Draw(mask).rounded_rectangle(
            [0, 0, crop.size[0] - 1, crop.size[1] - 1], radius=int(crop.size[0] * RADIUS), fill=255)
        sq = Image.new("RGBA", crop.size, (0, 0, 0, 0))
        sq.paste(crop, (0, 0), mask)
        im = sq.resize((size, size), Image.LANCZOS)
        # 小尺寸对比增强:主体(亮色)提白,背景压暗,避免 16px 糊成一团
        px = im.load()
        for y in range(size):
            for x in range(size):
                r, g, b, a = px[x, y]
                if r > 150 and g > 150:
                    px[x, y] = (255, 255, 255, a)
                elif r > 110 and g > 110:
                    t = min(255, r + 55)
                    px[x, y] = (t, t, t, a)
        im = im.filter(ImageFilter.UnsharpMask(radius=1.0, percent=60, threshold=2))
        return im
    im = master.resize((size, size), Image.LANCZOS)
    if size <= 48:
        im = im.filter(ImageFilter.UnsharpMask(radius=1.2, percent=60, threshold=2))
    return im


def main():
    src = Image.open(SRC).convert("RGB")
    if src.size != (1024, 1024):
        src = src.resize((1024, 1024), Image.LANCZOS)
    master = rounded_master(src)

    out_dir = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
    ico_path = os.path.join(out_dir, "src", "CleanMaster.App", "Assets", "app.ico")
    png_path = os.path.join(out_dir, "docs", "images", "icon.png")
    os.makedirs(os.path.dirname(png_path), exist_ok=True)

    frames = [frame(master, s) for s in SIZES]

    with open(ico_path, "wb") as f:
        f.write(struct.pack("<HHH", 0, 1, len(SIZES)))
        offset = 6 + 16 * len(SIZES)
        for im in frames:
            b = io.BytesIO()
            im.save(b, "PNG")
            data = b.getvalue()
            sz = im.size[0]
            f.write(struct.pack("<BBBBHHII", 0 if sz >= 256 else sz, 0 if sz >= 256 else sz,
                                0, 0, 1, 32, len(data), offset))
            offset += len(data)
        for im in frames:
            b = io.BytesIO()
            im.save(b, "PNG")
            f.write(b.getvalue())
    print("icon written:", ico_path)

    master.resize((512, 512), Image.LANCZOS).save(png_path, optimize=True)
    print("preview written:", png_path)


if __name__ == "__main__":
    main()
