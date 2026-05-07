from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "Resources" / "PresentationPolish"


def rgba(hex_value, alpha=255):
    value = hex_value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4)) + (alpha,)


def ensure_parent(path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)


def rounded(draw, rect, radius, fill, outline=None, width=1):
    draw.rounded_rectangle(rect, radius=radius, fill=fill, outline=outline, width=width)


def plate(path: Path, size, fill, border, glow=None, stripe=None, notch=False):
    ensure_parent(path)
    w, h = size
    image = Image.new("RGBA", size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    if glow:
      rounded(draw, (10, 10, w - 10, h - 10), 36, glow)

    rounded(draw, (16, 16, w - 16, h - 16), 30, fill, border, 3)
    rounded(draw, (28, 28, w - 28, h - 28), 22, (255, 255, 255, 8))

    if stripe:
        draw.rectangle((24, 24, 48, h - 24), fill=stripe)
        draw.rectangle((58, 28, 64, h - 28), fill=(255, 255, 255, 18))

    if notch:
        draw.polygon([(w - 92, 16), (w - 16, 16), (w - 16, 92)], fill=border)
        draw.polygon([(w - 82, 26), (w - 28, 26), (w - 28, 80)], fill=fill)

    image.save(path)


def visit_button(path: Path, fill, border, stripe, badge_fill):
    ensure_parent(path)
    w, h = 960, 192
    image = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    rounded(draw, (10, 14, w - 10, h - 14), 34, stripe)
    rounded(draw, (22, 22, w - 22, h - 22), 28, fill, border, 4)
    rounded(draw, (30, 30, 176, h - 30), 22, badge_fill)
    draw.rectangle((200, 34, 204, h - 34), fill=(255, 255, 255, 18))
    draw.rectangle((214, 34, 218, h - 34), fill=(255, 255, 255, 10))
    image.save(path)


def node_plate(path: Path, fill, border, accent):
    ensure_parent(path)
    w, h = 256, 256
    image = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    rounded(draw, (12, 12, w - 12, h - 12), 50, accent)
    rounded(draw, (24, 24, w - 24, h - 24), 40, fill, border, 4)
    rounded(draw, (40, 40, w - 40, h - 96), 30, (255, 255, 255, 8))
    draw.rectangle((48, h - 72, w - 48, h - 56), fill=(255, 255, 255, 14))
    draw.rectangle((72, h - 48, w - 72, h - 38), fill=(255, 255, 255, 10))
    image.save(path)


def ring_icon(path: Path, fill, border, kind):
    ensure_parent(path)
    w, h = 128, 128
    image = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    draw.ellipse((12, 12, w - 12, h - 12), fill=fill, outline=border, width=6)
    if kind == "current":
        draw.polygon([(64, 24), (98, 64), (64, 104), (30, 64)], fill=border)
    elif kind == "available":
        draw.ellipse((34, 34, 94, 94), outline=border, width=10)
        draw.ellipse((50, 50, 78, 78), fill=border)
    elif kind == "completed":
        draw.line((30, 70, 52, 92, 98, 38), fill=border, width=12)
    elif kind == "locked":
        rounded(draw, (34, 56, 94, 98), 12, border)
        draw.arc((38, 26, 90, 74), start=180, end=360, fill=border, width=10)
    elif kind == "visited":
        draw.arc((28, 28, 100, 100), start=45, end=315, fill=border, width=10)
        draw.polygon([(92, 32), (104, 38), (94, 50)], fill=border)
    elif kind == "question":
        draw.rounded_rectangle((30, 34, 98, 84), radius=18, fill=border)
        draw.polygon([(48, 84), (60, 102), (70, 84)], fill=border)
        draw.ellipse((54, 48, 66, 60), fill=fill)
        draw.line((60, 62, 60, 72), fill=fill, width=6)

    image.save(path)


def main():
    panels = {
        "Panels/current_location_plate.png": ("#241A14", "#D7A24A", "#3B2919", "#D7A24A", True),
        "Panels/route_summary_plate.png": ("#121824", "#486B8A", "#1B2736", "#486B8A", False),
        "Panels/visit_list_plate.png": ("#111723", "#273344", "#0D131D", "#2B3A4E", False),
        "Panels/suspect_presence_plate.png": ("#171422", "#8B5C6A", "#241A2B", "#8B5C6A", True),
        "Panels/solve_gate_plate.png": ("#142028", "#4E8E8A", "#18252D", "#4E8E8A", False),
        "Panels/scene_hint_plate.png": ("#16181F", "#4A5460", "#11141A", "#3E4752", False),
        "Panels/lead_action_plate.png": ("#2A170E", "#D78539", "#190F0A", "#D78539", True),
        "Panels/map_selection_plate.png": ("#131A24", "#5F7F92", "#0E141C", "#5F7F92", False),
        "Panels/map_legend_plate.png": ("#11161E", "#455768", "#0D1117", "#455768", False),
        "Panels/dossier_card_plate.png": ("#131724", "#5A6C84", "#0D111A", "#5A6C84", False),
        "Panels/dossier_header_plate.png": ("#1A1E2B", "#D7A24A", "#121722", "#D7A24A", True),
        "Panels/dossier_detail_plate.png": ("#0F1521", "#39495A", "#0A0E16", "#39495A", False),
        "Panels/dossier_summary_plate.png": ("#151926", "#8B5C6A", "#0F1420", "#8B5C6A", False),
        "Panels/portrait_frame_plate.png": ("#1B2130", "#D7A24A", "#111621", "#D7A24A", False),
        "Panels/interrogation_prompt_plate.png": ("#131A24", "#7B8E4E", "#0D1117", "#7B8E4E", False),
        "Panels/interrogation_feedback_plate.png": ("#101721", "#4E8E8A", "#0C1118", "#4E8E8A", False),
        "Panels/interrogation_trigger_plate.png": ("#24160E", "#D78539", "#130D08", "#D78539", True),
        "Panels/interrogation_response_plate.png": ("#172032", "#4E6C95", "#101726", "#4E6C95", False),
        "Panels/interrogation_response_success.png": ("#132419", "#4E9A65", "#0E1711", "#4E9A65", False),
        "Panels/interrogation_response_failure.png": ("#2A1419", "#B45862", "#190C10", "#B45862", False),
    }

    for relative_path, (fill, border, glow, stripe, notch) in panels.items():
        plate(
            OUT / relative_path,
            (1024, 256) if "dossier_card" not in relative_path and "map_selection" not in relative_path else (1024, 320),
            rgba(fill, 240),
            rgba(border),
            rgba(glow, 72),
            rgba(stripe, 204),
            notch=notch,
        )

    visit_button(OUT / "VisitState/visit_current_plate.png", rgba("#2B2117", 244), rgba("#E3AD53"), rgba("#7A5A22", 164), rgba("#E3AD53", 220))
    visit_button(OUT / "VisitState/visit_available_plate.png", rgba("#172334", 244), rgba("#5F8CB7"), rgba("#35506F", 164), rgba("#5F8CB7", 220))
    visit_button(OUT / "VisitState/visit_completed_plate.png", rgba("#16261A", 244), rgba("#5CA16E"), rgba("#32583D", 164), rgba("#5CA16E", 220))
    visit_button(OUT / "VisitState/visit_locked_plate.png", rgba("#1B1C22", 244), rgba("#616777"), rgba("#343844", 164), rgba("#616777", 220))
    visit_button(OUT / "VisitState/visit_visited_plate.png", rgba("#20222C", 244), rgba("#838AA0"), rgba("#444A5C", 164), rgba("#838AA0", 220))

    ring_icon(OUT / "VisitState/state_current.png", rgba("#2B2117", 240), rgba("#E3AD53"), "current")
    ring_icon(OUT / "VisitState/state_available.png", rgba("#172334", 240), rgba("#5F8CB7"), "available")
    ring_icon(OUT / "VisitState/state_completed.png", rgba("#16261A", 240), rgba("#5CA16E"), "completed")
    ring_icon(OUT / "VisitState/state_locked.png", rgba("#1B1C22", 240), rgba("#9A7380"), "locked")
    ring_icon(OUT / "VisitState/state_visited.png", rgba("#20222C", 240), rgba("#A2A9BE"), "visited")
    ring_icon(OUT / "Interrogation/interrogation_ready_badge.png", rgba("#24160E", 240), rgba("#E7B15A"), "question")

    node_plate(OUT / "Map/node_unlocked_plate.png", rgba("#141C28", 244), rgba("#516B86"), rgba("#10161F", 92))
    node_plate(OUT / "Map/node_locked_plate.png", rgba("#18171D", 244), rgba("#6A5964"), rgba("#101014", 92))
    node_plate(OUT / "Map/node_selected_plate.png", rgba("#1F1820", 248), rgba("#E3AD53"), rgba("#7A5A22", 112))

    print("Presentation polish assets built.")


if __name__ == "__main__":
    main()
