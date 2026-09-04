"""Generate the Shelly Cloud Control plugin icons.

Draws a power glyph (broken ring plus vertical bar) on a rounded dark tile.
Everything is rendered supersampled and then downsampled so the curves stay
clean at 16px as well as 256px. The small variant uses heavier strokes and a
larger glyph, because thin strokes disappear at that size.
"""

from PIL import Image, ImageDraw

BACKGROUND = (30, 33, 40, 255)      # dark slate tile
GLYPH = (255, 141, 31, 255)         # warm amber
SUPERSAMPLE = 16


def render(size, glyph_scale, stroke_scale, corner_ratio=0.22):
    px = size * SUPERSAMPLE
    image = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    # Rounded background tile
    draw.rounded_rectangle(
        [(0, 0), (px - 1, px - 1)],
        radius=int(px * corner_ratio),
        fill=BACKGROUND,
    )

    stroke = max(1, int(px * stroke_scale))
    radius = px * glyph_scale / 2
    cx = cy = px / 2

    # Ring with a gap at the top, centred slightly low to balance the bar
    ring_cy = cy + px * 0.045
    box = [cx - radius, ring_cy - radius, cx + radius, ring_cy + radius]
    # PIL angles: 0 = east, increasing clockwise; 270 = north.
    # Drawing 305 -> 235 wraps the long way round, leaving a gap at the top.
    draw.arc(box, start=305, end=235, fill=GLYPH, width=stroke)

    # Vertical bar through the gap
    bar_top = ring_cy - radius - px * 0.085
    bar_bottom = ring_cy - radius * 0.30
    draw.line(
        [(cx, bar_top), (cx, bar_bottom)],
        fill=GLYPH,
        width=stroke,
    )
    # Round the bar's ends so they match the arc's stroke
    r = stroke / 2
    for y in (bar_top, bar_bottom):
        draw.ellipse([cx - r, y - r, cx + r, y + r], fill=GLYPH)

    return image.resize((size, size), Image.LANCZOS)


if __name__ == "__main__":
    # 256px: refined proportions.
    render(256, glyph_scale=0.52, stroke_scale=0.075).save(
        "metadata/Icon256x256.png"
    )
    # 16px: bolder and larger so the glyph survives the downscale.
    render(16, glyph_scale=0.60, stroke_scale=0.105, corner_ratio=0.20).save(
        "metadata/Icon16x16.png"
    )
    print("wrote metadata/Icon256x256.png and metadata/Icon16x16.png")
