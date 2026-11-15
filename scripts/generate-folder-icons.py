"""Rebuild SideWidget's folder icons (requires Pillow)."""

from functools import lru_cache
from pathlib import Path
from struct import unpack_from

from PIL import Image, ImageChops, ImageColor, ImageDraw, ImageFilter, ImageOps


ASSETS = Path(__file__).resolve().parents[1] / "src/SideWidget.Infrastructure/Assets/FolderIcons"
SIZES = (16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
PALETTE = {
    "Document": "#285884", "Finance": "#21644D", "Legal": "#654B89",
    "Game": "#6244A0", "Application": "#36549C", "Log": "#396272",
    "Server": "#325C66", "Image": "#387144", "Music": "#954267",
    "Video": "#A84749", "Archive": "#865631", "Mixed": "#655C55",
}
SCALE = 4
SIZE = 64 * SCALE


def px(values):
    return tuple(round(value * SCALE) for value in values)


def gradient(image, mask, top, bottom, y1, y2):
    top = ImageColor.getrgb(top)
    bottom = ImageColor.getrgb(bottom)
    fill = Image.new("RGB", image.size)
    draw = ImageDraw.Draw(fill)
    for y in range(SIZE):
        ratio = min(1, max(0, (y / SCALE - y1) / (y2 - y1)))
        color = tuple(round(a + (b - a) * ratio) for a, b in zip(top, bottom))
        draw.line((0, y, SIZE - 1, y), fill=color)
    image.paste(fill, (0, 0), mask)


def draw_symbol(draw, kind, ink):

    def line(points, width=1.9):
        points = [px(point) for point in points]
        stroke = round(width * SCALE)
        draw.line(points, fill=ink, width=stroke, joint="curve")
        radius = stroke / 2
        for x, y in points:
            draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=ink)

    def circle(x, y, radius, fill=ink):
        draw.ellipse(px((x - radius, y - radius, x + radius, y + radius)), fill=fill)

    def outline(bounds, radius=1.5):
        draw.rounded_rectangle(px(bounds), radius=round(radius * SCALE), outline=ink, width=2 * SCALE)

    if kind == "Document":
        line([(35, 46), (35, 35), (41, 35), (45, 39), (45, 46), (35, 46)])
        line([(38, 42), (42, 42)], 1.5)
    elif kind == "Finance":
        for x, top in ((35, 42), (40, 38), (45, 34.5)):
            line([(x, 46), (x, top)], 2.6)
    elif kind == "Legal":
        line([(34, 46), (46, 46)])
        line([(40, 46), (40, 35), (34, 37), (46, 37)])
        line([(35, 38), (33, 42), (37, 42), (35, 38)], 1.3)
        line([(45, 38), (43, 42), (47, 42), (45, 38)], 1.3)
    elif kind == "Game":
        outline((32, 36, 48, 45), 4)
        line([(35, 40.5), (39, 40.5)], 1.5)
        line([(37, 38.5), (37, 42.5)], 1.5)
        circle(44, 40, 1.1)
    elif kind == "Application":
        for x, y in ((34, 35), (41, 35), (34, 42), (41, 42)):
            draw.rounded_rectangle(px((x, y, x + 5, y + 5)), radius=SCALE, fill=ink)
    elif kind == "Log":
        for y, width in ((36, 8), (41, 8), (46, 6)):
            circle(34.5, y, 0.9)
            line([(38, y), (38 + width, y)], 1.65)
    elif kind == "Server":
        for y in (35, 42):
            outline((32.5, y, 47.5, y + 4.5), 1.7)
            circle(44.5, y + 2.25, 0.7)
    elif kind == "Image":
        outline((33, 34.5, 47, 46.5))
        line([(34, 45), (38.5, 40.5), (41, 43), (43.5, 40.5), (46, 43)], 1.45)
        circle(43, 37.8, 1.15)
    elif kind == "Music":
        line([(39, 44), (39, 36.5), (46, 34.5), (46, 42)], 1.9)
        circle(37.3, 44.5, 2.1)
        circle(44.3, 42.5, 2.1)
    elif kind == "Video":
        outline((32, 35, 48, 46))
        draw.polygon([px(point) for point in ((38, 37.8), (44, 40.5), (38, 43.2))], fill=ink)
    elif kind == "Archive":
        outline((33.5, 35, 46.5, 46))
        line([(34, 39), (46, 39)], 1.65)
        line([(38.5, 42.5), (41.5, 42.5)], 1.65)
    else:
        for x, y in ((35, 43.5), (40, 36.8), (45, 43.5)):
            circle(x, y, 2.3)


def mix(first, second, amount):
    first, second = ImageColor.getrgb(first), ImageColor.getrgb(second)
    return "#" + "".join(f"{round(a + (b - a) * amount):02x}" for a, b in zip(first, second))


def symbol(kind, color, width, height, resampling=Image.Resampling.LANCZOS):
    glyph = Image.new("RGBA", (SIZE, SIZE))
    draw_symbol(ImageDraw.Draw(glyph), kind, color)
    glyph = glyph.crop(glyph.getbbox())
    scale = min(width / glyph.width, height / glyph.height)
    return glyph.resize((round(glyph.width * scale), round(glyph.height * scale)), resampling)


def curve(start, first, second, end):
    for step in range(1, 17):
        t = step / 16
        u = 1 - t
        yield tuple(u**3 * a + 3 * u*u*t * b + 3 * u*t*t * c + t**3 * d
                    for a, b, c, d in zip(start, first, second, end))


@lru_cache(maxsize=1)
def modern_folder():
    """Trace the folder from the 1200 × 1200 reference."""
    image = Image.new("RGBA", (SIZE, SIZE))

    def layer(points, start_color, end_color, x_weight, start, end):
        mask = Image.new("L", image.size)
        ImageDraw.Draw(mask).polygon([(round(x * SIZE / 1200), round(y * SIZE / 1200))
                                      for x, y in points], fill=255)
        shade = Image.new("L", image.size)
        shade.putdata([round(255 * min(1, max(0, (((x * x_weight + y * (1 - x_weight)) * 1200 / SIZE)
                                                 - start) / (end - start))))
                       for y in range(SIZE) for x in range(SIZE)])
        image.paste(ImageOps.colorize(shade, start_color, end_color), (0, 0), mask)

    layer([
        (25, 188), *curve((25, 188), (25, 153), (53, 125), (88, 125)), (371, 125),
        *curve((371, 125), (423, 125), (452, 145), (483, 183)), (538, 250), (1112, 250),
        *curve((1112, 250), (1147, 250), (1175, 278), (1175, 313)), (1175, 962),
        *curve((1175, 962), (1175, 997), (1147, 1025), (1112, 1025)), (88, 1025),
        *curve((88, 1025), (53, 1025), (25, 997), (25, 962)),
    ], "#F8BE1C", "#D59B1D", 0.45, 95, 1080)

    layer([
        (50, 325), *curve((50, 325), (50, 298), (72, 275), (100, 275)),
        (1112, 275), (1112, 460), (50, 460),
    ], "#F8FBF2", "#DFE2DF", 1, 80, 520)

    layer([
        (25, 387), *curve((25, 387), (25, 353), (53, 325), (88, 325)), (405, 325),
        *curve((405, 325), (462, 325), (503, 304), (538, 275)), (1112, 275),
        *curve((1112, 275), (1147, 275), (1175, 303), (1175, 338)), (1175, 937),
        *curve((1175, 937), (1175, 972), (1147, 1000), (1112, 1000)), (88, 1000),
        *curve((88, 1000), (53, 1000), (25, 972), (25, 937)),
    ], "#FFF0BA", "#FFDF83", 0.72, 100, 1040)

    layer([
        (25, 413), *curve((25, 413), (25, 378), (53, 350), (88, 350)), (408, 350),
        *curve((408, 350), (467, 350), (510, 331), (548, 300)), (1112, 300),
        *curve((1112, 300), (1147, 300), (1175, 328), (1175, 363)), (1175, 937),
        *curve((1175, 937), (1175, 972), (1147, 1000), (1112, 1000)), (88, 1000),
        *curve((88, 1000), (53, 1000), (25, 972), (25, 937)),
    ], "#FFE896", "#FFCA36", 0.40, 240, 940)
    return image


def render_modern(kind, color):
    image = modern_folder().copy()
    glyph = symbol(kind, mix(color, "#472A12", 0.4), 31 * SCALE, 19 * SCALE)
    image.alpha_composite(glyph, (32 * SCALE - glyph.width // 2, 38 * SCALE - glyph.height // 2))
    return image


def render_retro(kind, color):
    image = Image.new("RGBA", (32, 32))
    draw = ImageDraw.Draw(image)
    draw.polygon([(3, 5), (12, 5), (12, 6), (14, 6), (14, 8), (28, 8),
                  (28, 9), (30, 9), (30, 27), (29, 27), (29, 28), (2, 28),
                  (2, 27), (1, 27), (1, 7), (3, 7)], fill="#3C3027")
    draw.polygon([(3, 7), (4, 7), (4, 6), (11, 6), (11, 7), (13, 7), (13, 9),
                  (28, 9), (28, 12), (3, 12)], fill="#C58B41")
    draw.line([(4, 7), (10, 7)], fill="#FFF1A5")
    draw.rectangle((2, 11, 29, 26), fill="#FBC759")
    draw.rectangle((3, 12, 28, 24), fill="#FFDA75")
    draw.line([(3, 12), (28, 12)], fill="#FFF2B3")
    draw.line([(3, 13), (3, 24)], fill="#FFEAA2")
    draw.line([(3, 26), (29, 26), (29, 12)], fill="#AC6C32")
    for y in (23, 24, 25):
        for x in range(5 + y % 2, 28, 2):
            draw.point((x, y), fill="#D89A45")
    glyph = symbol(kind, mix(color, "#211A26", 0.45), 15, 10, Image.Resampling.NEAREST)
    image.alpha_composite(glyph, (16 - glyph.width // 2, 19 - glyph.height // 2))
    return image.resize((SIZE, SIZE), Image.Resampling.NEAREST)


def render_graphite(kind, style):
    image = Image.new("RGBA", (SIZE, SIZE))
    filled = style.endswith("Filled")
    opened = style.startswith("Open")
    graphite = "#333333"
    keyline = (231, 233, 234, 180)
    back = [
        (5, 17), *curve((5, 17), (5, 14), (7, 12), (10, 12)), (23, 12),
        *curve((23, 12), (25, 12), (26, 13), (27, 14)), (33, 19),
        *curve((33, 19), (34, 20), (35, 20), (37, 20)), (54, 20),
        *curve((54, 20), (57, 20), (59, 22), (59, 25)), (59, 48),
        *curve((59, 48), (59, 51), (57, 53), (54, 53)), (10, 53),
        *curve((10, 53), (7, 53), (5, 51), (5, 48)),
    ]

    def paint(points):
        draw = ImageDraw.Draw(image)
        polygon = [px(point) for point in points]
        if filled:
            draw.polygon(polygon, fill=graphite)
            draw.line([*polygon, polygon[0]], fill=keyline, width=round(0.85 * SCALE), joint="curve")
        else:
            draw.line([*polygon, polygon[0]], fill=keyline, width=round(4.3 * SCALE), joint="curve")
            draw.line([*polygon, polygon[0]], fill=graphite, width=round(2.6 * SCALE), joint="curve")

    paint(back)
    if opened:
        front = [
            (6, 46), (12, 27), *curve((12, 27), (13, 25), (15, 24), (18, 24)), (57, 24),
            *curve((57, 24), (61, 24), (63, 27), (62, 30)), (56, 49),
            *curve((56, 49), (55, 52), (53, 53), (50, 53)), (10, 53),
            *curve((10, 53), (6, 53), (5, 50), (6, 46)),
        ]
        mask = Image.new("L", image.size)
        ImageDraw.Draw(mask).polygon([px(point) for point in front], fill=255)
        mask = mask.filter(ImageFilter.MaxFilter(13))
        ImageDraw.Draw(mask).rectangle(px((12, 24, 64, 64)), fill=255)
        image.putalpha(ImageChops.subtract(image.getchannel("A"), mask))
        paint(front)

    glyph = symbol(kind, "#FAFAFA" if filled else graphite, 27 * SCALE, 16 * SCALE)
    position = (34 * SCALE - glyph.width // 2, (39 if opened else 36) * SCALE - glyph.height // 2)
    if not filled:
        halo = Image.new("RGBA", image.size, (231, 233, 234, 0))
        mask = Image.new("L", image.size)
        mask.paste(glyph.getchannel("A"), position)
        halo.putalpha(mask.filter(ImageFilter.MaxFilter(7)))
        image.alpha_composite(halo)
    image.alpha_composite(glyph, position)
    return image


def render(kind, color, style):
    if style == "Modern":
        return render_modern(kind, color)
    if style == "Retro":
        return render_retro(kind, color)
    if style.startswith(("Open", "Closed")):
        return render_graphite(kind, style)
    image = Image.new("RGBA", (SIZE, SIZE))
    shadow = Image.new("L", image.size)
    ImageDraw.Draw(shadow).rounded_rectangle(px((4, 24, 60, 55)), radius=7 * SCALE, fill=104)
    image.paste("#3A220C", (0, 0), shadow.filter(ImageFilter.GaussianBlur(2 * SCALE)))

    back = Image.new("L", image.size)
    draw = ImageDraw.Draw(back)
    draw.rounded_rectangle(px((5, 18, 59, 52)), radius=5 * SCALE, fill=255)
    tab = 31 if style == "Outline" else 5
    draw.rounded_rectangle(px((tab, 12, tab + 24.5, 31)), radius=4 * SCALE, fill=255)
    draw.polygon([px(point) for point in ((tab + 19, 12), (tab + 23, 13), (tab + 29, 19), (tab + 19, 22))], fill=255)
    back_colors = ("#F5B947", "#B66B27") if style == "Classic" else (color, mix(color, "#131823", 0.46))
    gradient(image, back, *back_colors, 12, 53)

    front = Image.new("L", image.size)
    ImageDraw.Draw(front).rounded_rectangle(px((5, 23, 59, 53)), radius=5 * SCALE, fill=255)
    if style == "Colored":
        top, bottom, edge = mix(color, "#FFFFFF", 0.30), color, mix(color, "#131823", 0.38)
    elif style == "Outline":
        top, bottom, edge = "#FFFEFC", mix(color, "#FFFFFF", 0.88), color
    else:
        top, bottom, edge = "#FFE294", "#EDA63D", "#CA812A"
    gradient(image, front, top, bottom, 23, 53)
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle(px((5.5, 23.5, 58.5, 52.5)), radius=4.5 * SCALE, outline=edge,
                           width=(2 if style == "Outline" else 1) * SCALE)
    if style == "Classic":
        draw.line(px((11, 25, 53, 25)), fill="#FFEDB2", width=SCALE)
        draw.rounded_rectangle(px((14, 26.5, 50, 51)), radius=6 * SCALE, fill=color)

    glyph = symbol(kind, color if style == "Outline" else "#FFF8E8", 29 * SCALE, 19 * SCALE)
    image.alpha_composite(glyph, (32 * SCALE - glyph.width // 2, 39 * SCALE - glyph.height // 2))
    return image


if __name__ == "__main__":
    ASSETS.mkdir(parents=True, exist_ok=True)
    for kind, color in PALETTE.items():
        for style in ("Classic", "Colored", "Outline", "Modern", "Retro",
                      "ClosedOutline", "ClosedFilled", "OpenOutline", "OpenFilled"):
            name = kind if style == "Classic" else f"{kind}-{style}"
            icon = ASSETS / f"{name}.ico"
            render(kind, color, style).save(icon, sizes=[(size, size) for size in SIZES])
            data = icon.read_bytes()
            # ICO directory entries store frame length and offset after the 8-byte dimensions/format header.
            entry = next(offset for offset in range(6, 6 + 16 * len(SIZES), 16) if data[offset] == 48)
            length, offset = unpack_from("<II", data, entry + 8)
            (ASSETS / f"{name}.png").write_bytes(data[offset:offset + length])
