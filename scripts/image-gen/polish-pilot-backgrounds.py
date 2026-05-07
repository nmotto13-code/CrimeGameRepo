from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
BACKGROUND_DIR = ROOT / "Assets" / "Sprites" / "Backgrounds"


def add_region_shadow(image, rect, blur_radius=28, overlay_alpha=180):
    mask = Image.new("L", image.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle(rect, radius=28, fill=255)
    mask = mask.filter(ImageFilter.GaussianBlur(blur_radius))
    shadow = Image.new("RGBA", image.size, (6, 8, 12, overlay_alpha))
    return Image.composite(shadow, image, mask)


def soften_region(image, rect, blur_radius=22):
    crop = image.crop(rect)
    crop = crop.filter(ImageFilter.GaussianBlur(blur_radius))
    mask = Image.new("L", image.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle(rect, radius=24, fill=220)
    mask = mask.filter(ImageFilter.GaussianBlur(18))
    layer = image.copy()
    layer.paste(crop, rect)
    return Image.composite(layer, image, mask)


def polish_case003():
    path = BACKGROUND_DIR / "case003_harwick_gallery_side_entrance.jpg"
    image = Image.open(path).convert("RGBA")
    for rect in [
        (0, 240, 390, 760),
        (780, 1030, 1024, 1540),
        (0, 1260, 250, 1600),
    ]:
        image = soften_region(image, rect)
        image = add_region_shadow(image, rect, overlay_alpha=210)
    image = add_region_shadow(image, (0, 0, 180, image.size[1]), blur_radius=90, overlay_alpha=88)
    image = add_region_shadow(image, (844, 0, image.size[0], image.size[1]), blur_radius=90, overlay_alpha=74)
    image.convert("RGB").save(path, quality=92)


def polish_case023():
    path = BACKGROUND_DIR / "case023_marina_ramp.jpg"
    image = Image.open(path).convert("RGBA")
    for rect in [
        (0, 280, 1024, 760),
        (680, 620, 1024, 1080),
        (140, 1370, 470, 1660),
        (420, 1240, 1024, 1792),
    ]:
        image = soften_region(image, rect)
        image = add_region_shadow(image, rect, overlay_alpha=190)
    image = add_region_shadow(image, (0, 0, image.size[0], 260), blur_radius=120, overlay_alpha=84)
    image = add_region_shadow(image, (0, 1440, image.size[0], image.size[1]), blur_radius=120, overlay_alpha=92)
    image.convert("RGB").save(path, quality=92)


if __name__ == "__main__":
    polish_case003()
    polish_case023()
    print("Pilot background polish complete.")
