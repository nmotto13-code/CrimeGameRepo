import json
import math
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "scripts" / "image-gen" / "presentation-prompts.json"


def hex_to_rgba(value, alpha=255):
    value = value.lstrip("#")
    return tuple(int(value[i:i+2], 16) for i in (0, 2, 4)) + (alpha,)


def load_font(size):
    candidates = [
        Path("C:/Windows/Fonts/segoeuib.ttf"),
        Path("C:/Windows/Fonts/segoeui.ttf"),
        Path("C:/Windows/Fonts/arialbd.ttf"),
        Path("C:/Windows/Fonts/arial.ttf"),
    ]
    for candidate in candidates:
        if candidate.exists():
            return ImageFont.truetype(str(candidate), size=size)
    return ImageFont.load_default()


def ensure_parent(path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)


def lerp(a, b, t):
    return a + (b - a) * t


def blend(color_a, color_b, t):
    return tuple(int(lerp(a, b, t)) for a, b in zip(color_a, color_b))


def draw_badge(path: Path, color: str, accent: str, code: str, size: int):
    ensure_parent(path)
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    outer = hex_to_rgba(accent)
    fill = hex_to_rgba(color)
    glow = hex_to_rgba(color, 56)
    draw.ellipse((12, 12, size - 12, size - 12), fill=glow)
    draw.rounded_rectangle((28, 28, size - 28, size - 28), radius=44, fill=outer)
    draw.rounded_rectangle((40, 40, size - 40, size - 40), radius=36, fill=fill)
    draw.line((size * 0.24, size * 0.34, size * 0.76, size * 0.34), fill=outer, width=6)
    draw.line((size * 0.24, size * 0.66, size * 0.76, size * 0.66), fill=outer, width=6)
    font = load_font(int(size * 0.20))
    bbox = draw.textbbox((0, 0), code, font=font)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    draw.text(((size - tw) / 2, (size - th) / 2 - 4), code, font=font, fill=outer)
    image.save(path)


def draw_node_icon(path: Path, color: str, accent: str, code: str, size: int):
    ensure_parent(path)
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    outer = hex_to_rgba(accent)
    fill = hex_to_rgba(color)
    draw.rounded_rectangle((8, 8, size - 8, size - 8), radius=28, fill=outer)
    draw.rounded_rectangle((18, 18, size - 18, size - 18), radius=22, fill=fill)
    draw.rectangle((size * 0.28, size * 0.26, size * 0.72, size * 0.34), fill=outer)
    draw.rectangle((size * 0.28, size * 0.66, size * 0.72, size * 0.74), fill=outer)
    font = load_font(int(size * 0.18))
    bbox = draw.textbbox((0, 0), code, font=font)
    tw = bbox[2] - bbox[0]
    th = bbox[3] - bbox[1]
    draw.text(((size - tw) / 2, (size - th) / 2 - 2), code, font=font, fill=outer)
    image.save(path)


def draw_city_map_base(path: Path, manifest: dict):
    ensure_parent(path)
    width, height = 1024, 1792
    image = Image.new("RGBA", (width, height), (9, 14, 20, 255))
    draw = ImageDraw.Draw(image)

    for y in range(height):
        t = y / max(1, height - 1)
        row = blend((10, 15, 21), (22, 30, 38), t)
        draw.line((0, y, width, y), fill=row)

    frame = (56, 188, width - 56, height - 160)
    draw.rounded_rectangle(frame, radius=40, fill=(16, 24, 32, 255), outline=(118, 132, 144, 255), width=4)

    map_left, map_top, map_right, map_bottom = frame[0] + 24, frame[1] + 24, frame[2] - 24, frame[3] - 24
    map_w = map_right - map_left
    map_h = map_bottom - map_top

    water_color = (28, 67, 90, 255)
    water_highlight = (58, 117, 152, 140)
    river = [
        (map_left + map_w * 0.00, map_top + map_h * 0.66),
        (map_left + map_w * 0.12, map_top + map_h * 0.60),
        (map_left + map_w * 0.28, map_top + map_h * 0.55),
        (map_left + map_w * 0.48, map_top + map_h * 0.54),
        (map_left + map_w * 0.72, map_top + map_h * 0.60),
        (map_left + map_w * 1.00, map_top + map_h * 0.70),
        (map_left + map_w * 1.00, map_bottom),
        (map_left + map_w * 0.00, map_bottom),
    ]
    harbor = [
        (map_left + map_w * 0.68, map_top + map_h * 0.76),
        (map_left + map_w * 0.80, map_top + map_h * 0.74),
        (map_left + map_w * 0.93, map_top + map_h * 0.82),
        (map_left + map_w * 0.90, map_bottom),
        (map_left + map_w * 0.66, map_bottom),
    ]
    draw.polygon(river, fill=water_color)
    draw.polygon(harbor, fill=water_color)
    draw.line(river[:6], fill=water_highlight, width=6)

    district_entries = manifest["districtMarkers"]
    district_colors = {entry["districtId"]: hex_to_rgba(entry["color"], 110) for entry in district_entries}
    district_accents = {entry["districtId"]: hex_to_rgba(entry["accent"], 180) for entry in district_entries}
    district_positions = {}

    for location in manifest["locations"]:
        district_positions.setdefault(location["districtId"], []).append(location["mapPosition"])

    for district_id, points in district_positions.items():
        xs = [map_left + point["x"] * map_w for point in points]
        ys = [map_top + point["y"] * map_h for point in points]
        if not xs or not ys:
            continue
        padding_x = 92
        padding_y = 82
        rect = (
            max(map_left + 12, min(xs) - padding_x),
            max(map_top + 12, min(ys) - padding_y),
            min(map_right - 12, max(xs) + padding_x),
            min(map_bottom - 12, max(ys) + padding_y),
        )
        draw.rounded_rectangle(rect, radius=44, fill=district_colors[district_id], outline=district_accents[district_id], width=3)

    road_color = (214, 191, 146, 120)
    road_edge = (248, 226, 180, 92)
    primary_roads = [
        [(0.08, 0.22), (0.24, 0.34), (0.46, 0.44), (0.72, 0.49), (0.92, 0.54)],
        [(0.14, 0.74), (0.34, 0.64), (0.55, 0.58), (0.77, 0.49), (0.92, 0.32)],
        [(0.38, 0.10), (0.44, 0.28), (0.50, 0.46), (0.57, 0.68), (0.66, 0.84)],
    ]
    for road in primary_roads:
        points = [(map_left + x * map_w, map_top + y * map_h) for x, y in road]
        draw.line(points, fill=road_color, width=18, joint="curve")
        draw.line(points, fill=road_edge, width=4, joint="curve")

    for offset in range(8):
        y = map_top + map_h * (0.12 + offset * 0.08)
        draw.line((map_left + 36, y, map_right - 36, y), fill=(58, 73, 86, 80), width=2)
    for offset in range(7):
        x = map_left + map_w * (0.10 + offset * 0.11)
        draw.line((x, map_top + 36, x, map_bottom - 36), fill=(58, 73, 86, 80), width=2)

    for location in manifest["locations"]:
        x = map_left + location["mapPosition"]["x"] * map_w
        y = map_top + location["mapPosition"]["y"] * map_h
        district_id = location["districtId"]
        glow = hex_to_rgba(next(entry["color"] for entry in district_entries if entry["districtId"] == district_id), 78)
        draw.ellipse((x - 18, y - 18, x + 18, y + 18), fill=glow)
        draw.ellipse((x - 8, y - 8, x + 8, y + 8), fill=(245, 226, 176, 255), outline=district_accents[district_id], width=2)

    for i in range(12):
        x = map_left + 40 + i * 70
        y = map_bottom - 84 + (i % 3) * 12
        draw.rectangle((x, y, x + 46, y + 12), fill=(40, 47, 56, 255), outline=(90, 104, 116, 255), width=1)

    image.save(path)


def main():
    manifest = json.loads(MANIFEST.read_text(encoding="utf-8"))

    for entry in manifest["departmentIcons"]:
        draw_badge(ROOT / entry["output"], entry["color"], entry["accent"], entry["iconCode"], 256)

    for entry in manifest["districtMarkers"]:
        draw_badge(ROOT / entry["output"], entry["color"], entry["accent"], entry["iconCode"], 256)

    for entry in manifest["locationNodeIcons"]:
        draw_node_icon(ROOT / entry["output"], entry["color"], entry["accent"], entry["iconCode"], 192)

    city_map_entry = next((entry for entry in manifest["artEntries"] if entry["id"] == "city_map_base"), None)
    if city_map_entry:
        draw_city_map_base(ROOT / city_map_entry["output"], manifest)

    print("Presentation icons built.")


if __name__ == "__main__":
    main()
