"""Rebuild the SideMenu tray asset (requires Pillow)."""
from pathlib import Path

from PIL import Image, ImageDraw

ASSETS = Path(__file__).resolve().parents[1] / "src/SideWidget.App/Assets/Icons"
ASSETS.mkdir(parents=True, exist_ok=True)

SVG = '''<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 16 16">
  <rect x="1" y="2" width="14" height="12" rx="3" fill="#181a1c"/>
  <rect x="2" y="3" width="12" height="10" rx="2" fill="#f2f4f5"/>
  <rect x="3" y="4" width="6" height="8" rx="1" fill="#181a1c"/>
  <rect x="10" y="4" width="3" height="8" rx="1" fill="#4285f4"/>
</svg>
'''
(ASSETS / "sidemenu.svg").write_text(SVG, encoding="utf-8")


def render(size):
    scale = 8
    unit = size * scale / 16
    image = Image.new("RGBA", (size * scale, size * scale))
    draw = ImageDraw.Draw(image)
    for box, radius, color in (
        ((1, 2, 15, 14), 3, "#181a1c"),
        ((2, 3, 14, 13), 2, "#f2f4f5"),
        ((3, 4, 9, 12), 1, "#181a1c"),
        ((10, 4, 13, 12), 1, "#4285f4"),
    ):
        # Pillow boxes include their final pixel; SVG rectangles do not.
        bounds = tuple(round(v * unit) - (1 if i > 1 else 0) for i, v in enumerate(box))
        draw.rounded_rectangle(bounds, radius=round(radius * unit), fill=color)
    return image.resize((size, size), Image.Resampling.LANCZOS)


sizes = (16, 20, 24, 32, 40, 48, 64, 128, 256)
frames = [render(size) for size in sizes]
frames[-1].save(ASSETS / "sidemenu.png")
frames[-1].save(ASSETS / "sidemenu.ico", sizes=[(s, s) for s in sizes], append_images=frames[:-1])

# Contact sheet: actual tray sizes against light and dark taskbars.
preview = Image.new("RGB", (360, 144), "#181a1c")
draw = ImageDraw.Draw(preview)
draw.rectangle((0, 72, 360, 144), fill="#eeeeee")
for y in (0, 72):
    for index, size in enumerate(sizes[:6]):
        icon = render(size)
        preview.paste(icon, (index * 60 + (60 - size) // 2, y + (72 - size) // 2), icon)
preview_path = ASSETS.parents[3] / "output/tray-icon-preview.png"
preview_path.parent.mkdir(parents=True, exist_ok=True)
preview.save(preview_path)
