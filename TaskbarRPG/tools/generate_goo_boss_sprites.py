from __future__ import annotations

from pathlib import Path
from typing import Iterable

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ASSET_DIR = ROOT / "Assets" / "Boss"

TRANSPARENT = (0, 0, 0, 0)
PALETTE = {
    "outline": (22, 18, 24, 255),
    "goo_black": (12, 12, 16, 255),
    "goo_shadow": (6, 6, 9, 255),
    "goo_mid": (28, 28, 34, 255),
    "goo_highlight": (66, 66, 78, 255),
    "goo_gloss": (126, 126, 142, 255),
    "telegraph_glow": (138, 86, 212, 255),
    "telegraph_core": (214, 175, 255, 255),
}


def canvas() -> Image.Image:
    return Image.new("RGBA", (64, 64), TRANSPARENT)


def rect(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], color: tuple[int, int, int, int]) -> None:
    draw.rectangle(box, fill=color)


def ellipse(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], color: tuple[int, int, int, int]) -> None:
    draw.ellipse(box, fill=color)


def line(
    draw: ImageDraw.ImageDraw,
    points: list[tuple[int, int]] | tuple[tuple[int, int], ...],
    color: tuple[int, int, int, int],
    *,
    width: int = 1,
) -> None:
    draw.line(points, fill=color, width=width)


def outline_image(image: Image.Image) -> Image.Image:
    source = image.load()
    outlined = image.copy()
    target = outlined.load()
    width, height = image.size
    for y in range(height):
        for x in range(width):
            if source[x, y][3] != 0:
                continue
            for oy in (-1, 0, 1):
                for ox in (-1, 0, 1):
                    if ox == 0 and oy == 0:
                        continue
                    nx = x + ox
                    ny = y + oy
                    if nx < 0 or ny < 0 or nx >= width or ny >= height:
                        continue
                    if source[nx, ny][3] != 0:
                        target[x, y] = PALETTE["outline"]
                        ox = oy = 2
                        break
                else:
                    continue
                break
    return outlined


def save(image: Image.Image, name: str) -> None:
    outline_image(image).save(ASSET_DIR / name)


def clear_existing_outputs() -> None:
    for pattern in (
        "the_goo_walk*.png",
        "the_goo_dash_strike_telegraph*.png",
        "the_goo_dash_strike_attack*.png",
        "the_goo_dash_strike_cooldown*.png",
        "the_goo_hop_contact_attack*.png",
    ):
        for path in ASSET_DIR.glob(pattern):
            path.unlink(missing_ok=True)


def draw_blob(
    draw: ImageDraw.ImageDraw,
    *,
    offset_x: int = 0,
    offset_y: int = 0,
    squash: int = 0,
    stretch: int = 0,
    lean: int = 0,
    telegraph: bool = False,
) -> None:
    left = 10 + offset_x + lean
    top = 16 + offset_y - stretch
    right = 54 + offset_x + lean
    bottom = 54 + offset_y + squash
    ellipse(draw, (left, top, right, bottom), PALETTE["goo_black"])
    ellipse(draw, (left + 6, top - 4, left + 22, top + 8), PALETTE["goo_black"])
    ellipse(draw, (right - 20, top - 3, right - 4, top + 7), PALETTE["goo_black"])
    ellipse(draw, (left + 4, top + 6, right - 5, bottom - 2), PALETTE["goo_mid"])
    ellipse(draw, (left + 10, top + 10, left + 26, top + 21), PALETTE["goo_highlight"])
    ellipse(draw, (left + 24, top + 11, left + 31, top + 17), PALETTE["goo_gloss"])
    ellipse(draw, (left + 18, bottom - 12, right - 15, bottom - 4), PALETTE["goo_shadow"])
    rect(draw, (left + 14, bottom - 1, left + 17, bottom + 3), PALETTE["goo_shadow"])
    rect(draw, (left + 29, bottom, left + 32, bottom + 4), PALETTE["goo_shadow"])
    if telegraph:
        ellipse(draw, (left + 6, top + 4, right - 8, bottom - 4), PALETTE["telegraph_glow"])
        ellipse(draw, (left + 12, top + 10, right - 14, bottom - 10), PALETTE["goo_mid"])
        ellipse(draw, (left + 22, top + 12, left + 30, top + 20), PALETTE["telegraph_core"])


def draw_droplets(draw: ImageDraw.ImageDraw, droplets: Iterable[tuple[int, int, int, int]]) -> None:
    for x, y, size, shade in droplets:
        color = PALETTE["goo_highlight"] if shade > 0 else PALETTE["goo_mid"]
        ellipse(draw, (x, y, x + size, y + size), color)
        ellipse(draw, (x + 1, y + 1, x + size - 1, y + size - 1), PALETTE["goo_black"])


def smoothstep(value: float) -> float:
    value = max(0.0, min(1.0, value))
    return value * value * (3.0 - (2.0 * value))


def draw_telegraph_aura(draw: ImageDraw.ImageDraw, frame: int) -> None:
    pulse = [0, 1, 2, 3, 2, 1, 0, 1][frame % 8]
    arc_shift = [-1, 0, 1, 2, 1, 0, -1, -2][frame % 8]
    line(
        draw,
        [(46 + arc_shift, 14 - pulse), (54 + arc_shift, 22), (56 + arc_shift, 34), (51 + arc_shift, 47 + pulse)],
        PALETTE["telegraph_glow"],
        width=4,
    )
    line(
        draw,
        [(46 + arc_shift, 14 - pulse), (54 + arc_shift, 22), (56 + arc_shift, 34), (51 + arc_shift, 47 + pulse)],
        PALETTE["telegraph_core"],
        width=1,
    )
    ellipse(draw, (18 - pulse, 12 - pulse, 46 + pulse, 54 + pulse), PALETTE["telegraph_glow"])
    ellipse(draw, (21, 16, 43, 50), PALETTE["goo_shadow"])


def draw_telegraph_charge(draw: ImageDraw.ImageDraw, frame: int) -> None:
    center_shift = [-1, 0, 1, 2, 1, 0, -1, -2][frame % 8]
    pulse = [0, 1, 2, 3, 2, 1, 0, 1][frame % 8]
    charge_path = [
        (31 + center_shift, 18),
        (29 + center_shift, 24),
        (33 + center_shift, 30),
        (30 + center_shift, 36),
        (32 + center_shift, 43),
    ]
    line(draw, charge_path, PALETTE["telegraph_glow"], width=3)
    line(draw, charge_path, PALETTE["telegraph_core"], width=1)
    ellipse(draw, (27 + center_shift - pulse, 24 - pulse, 35 + center_shift + pulse, 38 + pulse), PALETTE["telegraph_glow"])
    ellipse(draw, (30 + center_shift, 28, 33 + center_shift, 33), PALETTE["telegraph_core"])
    ellipse(draw, (16, 43, 23, 50), PALETTE["telegraph_glow"])
    ellipse(draw, (18, 45, 21, 48), PALETTE["telegraph_core"])


def draw_dash_blob(
    draw: ImageDraw.ImageDraw,
    *,
    offset_x: int = 0,
    offset_y: int = 0,
    squash: int = 0,
    stretch: int = 0,
    smear: int = 0,
    crest: int = 0,
) -> None:
    left = 6 + offset_x
    top = 18 + offset_y - stretch
    right = 41 + offset_x
    bottom = 51 + offset_y + squash
    nose = min(60, right + 10 + smear)

    ellipse(draw, (left, top, right, bottom), PALETTE["goo_black"])
    ellipse(draw, (right - 6, top + 3, nose, bottom - 3), PALETTE["goo_black"])
    ellipse(draw, (left - 5, top + 8, left + 12, bottom - 2), PALETTE["goo_black"])
    ellipse(draw, (right - 8, top - 2 - crest, right + 6, top + 7), PALETTE["goo_black"])

    ellipse(draw, (left + 3, top + 6, right - 4, bottom - 1), PALETTE["goo_mid"])
    ellipse(draw, (right - 4, top + 8, nose - 1, bottom - 5), PALETTE["goo_mid"])
    ellipse(draw, (left + 10, top + 8, left + 25, top + 18), PALETTE["goo_highlight"])
    ellipse(draw, (right - 1, top + 10, nose - 3, top + 19), PALETTE["goo_gloss"])
    ellipse(draw, (left + 13, bottom - 10, nose - 11, bottom - 4), PALETTE["goo_shadow"])
    ellipse(draw, (left - 7, bottom - 5, left + 5, bottom - 2), PALETTE["goo_shadow"])
    rect(draw, (left + 13, bottom - 1, left + 16, bottom + 2), PALETTE["goo_shadow"])
    rect(draw, (nose - 12, bottom - 1, nose - 9, bottom + 2), PALETTE["goo_shadow"])


def draw_reforming_blob(
    draw: ImageDraw.ImageDraw,
    *,
    center_x: int,
    bottom: int,
    width: int,
    height: int,
    lean: int = 0,
    ripple: int = 0,
) -> None:
    left = center_x - (width // 2) + lean
    right = center_x + (width // 2) + lean
    top = bottom - height

    ellipse(draw, (left, top, right, bottom), PALETTE["goo_black"])
    if height >= 18:
        lobe_top = max(3, top - max(2, height // 8))
        ellipse(draw, (left + 4, lobe_top, left + 18, top + 8), PALETTE["goo_black"])
        ellipse(draw, (right - 18, lobe_top + 1, right - 4, top + 9), PALETTE["goo_black"])

    inner_top = top + max(1, height // 12)
    inner_bottom = bottom - 1
    inner_inset = 2 if height <= 12 else 4
    ellipse(draw, (left + inner_inset, inner_top, right - inner_inset, inner_bottom), PALETTE["goo_mid"])

    if 13 <= height <= 18:
        ellipse(draw, (center_x - 9 + lean, bottom - height + 4, center_x + 9 + lean, bottom - 2), PALETTE["goo_mid"])
        ellipse(draw, (center_x - 4 + lean, bottom - height + 6, center_x + 4 + lean, bottom - 4), PALETTE["goo_highlight"])

    if height > 12:
        highlight_bottom = top + max(8, min(height - 2, 12))
        ellipse(draw, (left + 10, top + 4, left + 24, highlight_bottom), PALETTE["goo_highlight"])
    if height > 18:
        gloss_bottom = top + max(9, min(height - 2, 14))
        ellipse(draw, (left + 24, top + 6, left + 32, gloss_bottom), PALETTE["goo_gloss"])

    if height <= 18:
        ellipse(draw, (left - 4 + ripple, bottom - 2, right + 4 - ripple, bottom + 3), PALETTE["goo_shadow"])
    else:
        ellipse(draw, (left + 15, bottom - 9, right - 13, bottom - 3), PALETTE["goo_shadow"])
        rect(draw, (left + 13, bottom - 1, left + 16, bottom + 2), PALETTE["goo_shadow"])
        rect(draw, (right - 16, bottom - 1, right - 13, bottom + 2), PALETTE["goo_shadow"])


def draw_cooldown_recovery_blob(
    draw: ImageDraw.ImageDraw,
    *,
    center_x: int,
    bottom: int,
    width: int,
    height: int,
    growth: float,
    ripple: int = 0,
) -> None:
    left = center_x - (width // 2)
    right = center_x + (width // 2)
    top = bottom - height

    ellipse(draw, (left, top, right, bottom), PALETTE["goo_black"])

    if growth > 0.45:
        lobe_raise = int(round(((growth - 0.45) / 0.55) * 5))
        ellipse(draw, (left + 5, top - 1 - lobe_raise, left + 18, top + 7), PALETTE["goo_black"])
        ellipse(draw, (right - 18, top - lobe_raise, right - 5, top + 8), PALETTE["goo_black"])

    inner_inset = 2 + int(round(growth * 2))
    inner_top = top + 1
    ellipse(draw, (left + inner_inset, inner_top, right - inner_inset, bottom - 1), PALETTE["goo_mid"])

    highlight_width = max(5, int(round(width * (0.18 + (growth * 0.08))))
    )
    highlight_height = max(3, int(round(height * (0.18 + (growth * 0.12))))
    )
    highlight_left = center_x - int(round(width * 0.18))
    highlight_top = top + max(1, int(round(height * 0.18)))
    ellipse(
        draw,
        (highlight_left, highlight_top, highlight_left + highlight_width, highlight_top + highlight_height),
        PALETTE["goo_highlight"],
    )

    if growth > 0.7:
        gloss_width = max(4, int(round(width * 0.12)))
        gloss_height = max(3, int(round(height * 0.12)))
        gloss_left = center_x + int(round(width * 0.05))
        gloss_top = top + max(2, int(round(height * 0.2)))
        ellipse(
            draw,
            (gloss_left, gloss_top, gloss_left + gloss_width, gloss_top + gloss_height),
            PALETTE["goo_gloss"],
        )

    if growth < 0.35:
        ellipse(draw, (left - 4 + ripple, bottom - 2, right + 4 - ripple, bottom + 3), PALETTE["goo_shadow"])
    else:
        shadow_left = left + int(round(width * 0.22))
        shadow_right = right - int(round(width * 0.22))
        ellipse(draw, (shadow_left, bottom - 8, shadow_right, bottom - 3), PALETTE["goo_shadow"])
        if growth > 0.85:
            rect(draw, (left + 13, bottom - 1, left + 16, bottom + 2), PALETTE["goo_shadow"])
            rect(draw, (right - 16, bottom - 1, right - 13, bottom + 2), PALETTE["goo_shadow"])


def make_walk_frames() -> list[Image.Image]:
    specs = [
        dict(offset_x=-3, offset_y=2, squash=4, stretch=0, lean=-3, droplets=[(12, 13, 5, 0), (48, 10, 4, 1)]),
        dict(offset_x=-2, offset_y=1, squash=2, stretch=2, lean=-2, droplets=[(15, 8, 4, 1), (45, 11, 3, 0)]),
        dict(offset_x=0, offset_y=0, squash=0, stretch=4, lean=-1, droplets=[(18, 6, 4, 1)]),
        dict(offset_x=2, offset_y=1, squash=1, stretch=2, lean=1, droplets=[(49, 9, 4, 0), (14, 12, 3, 1)]),
        dict(offset_x=3, offset_y=2, squash=4, stretch=0, lean=3, droplets=[(47, 14, 5, 0), (16, 10, 4, 1)]),
        dict(offset_x=2, offset_y=1, squash=2, stretch=2, lean=2, droplets=[(46, 8, 4, 1), (18, 12, 3, 0)]),
        dict(offset_x=0, offset_y=0, squash=0, stretch=4, lean=1, droplets=[(20, 5, 4, 1)]),
        dict(offset_x=-2, offset_y=1, squash=2, stretch=2, lean=-1, droplets=[(13, 9, 4, 0), (46, 12, 3, 1)]),
    ]
    frames: list[Image.Image] = []
    for spec in specs:
        image = canvas()
        draw = ImageDraw.Draw(image)
        draw_blob(draw, offset_x=spec["offset_x"], offset_y=spec["offset_y"], squash=spec["squash"], stretch=spec["stretch"], lean=spec["lean"])
        draw_droplets(draw, spec["droplets"])
        frames.append(image)
    return frames


def make_dash_telegraph_frames() -> list[Image.Image]:
    specs = [
        dict(offset_x=-2, offset_y=2, squash=5, stretch=0, lean=-3, droplets=[(11, 11, 4, 1)]),
        dict(offset_x=-3, offset_y=3, squash=7, stretch=0, lean=-4, droplets=[(9, 9, 5, 1)]),
        dict(offset_x=-2, offset_y=4, squash=9, stretch=0, lean=-3, droplets=[(8, 7, 5, 1)]),
        dict(offset_x=-1, offset_y=4, squash=10, stretch=0, lean=-2, droplets=[(9, 6, 5, 1)]),
        dict(offset_x=0, offset_y=3, squash=8, stretch=1, lean=-1, droplets=[(11, 7, 4, 1)]),
        dict(offset_x=1, offset_y=2, squash=6, stretch=2, lean=0, droplets=[(13, 8, 4, 1)]),
        dict(offset_x=0, offset_y=1, squash=4, stretch=3, lean=-1, droplets=[(15, 9, 4, 1)]),
        dict(offset_x=-1, offset_y=2, squash=5, stretch=2, lean=-2, droplets=[(13, 10, 4, 1)]),
    ]
    frames: list[Image.Image] = []
    for frame_index, spec in enumerate(specs):
        image = canvas()
        draw = ImageDraw.Draw(image)
        draw_telegraph_aura(draw, frame_index)
        draw_blob(draw, offset_x=spec["offset_x"], offset_y=spec["offset_y"], squash=spec["squash"], stretch=spec["stretch"], lean=spec["lean"])
        draw_telegraph_charge(draw, frame_index)
        draw_droplets(draw, spec["droplets"])
        frames.append(image)
    return frames


def make_dash_attack_frames() -> list[Image.Image]:
    specs = [
        dict(offset_x=0, offset_y=0, squash=3, stretch=1, smear=2, crest=0, droplets=[(12, 22, 4, 0), (18, 11, 4, 1)]),
        dict(offset_x=2, offset_y=-1, squash=2, stretch=2, smear=4, crest=1, droplets=[(10, 25, 5, 0), (15, 14, 4, 1)]),
        dict(offset_x=4, offset_y=-1, squash=1, stretch=3, smear=6, crest=2, droplets=[(8, 27, 6, 0), (13, 16, 4, 1)]),
        dict(offset_x=5, offset_y=0, squash=2, stretch=2, smear=7, crest=2, droplets=[(6, 29, 6, 0), (12, 18, 4, 1)]),
        dict(offset_x=5, offset_y=1, squash=3, stretch=1, smear=6, crest=1, droplets=[(5, 30, 6, 0), (12, 20, 4, 1)]),
        dict(offset_x=4, offset_y=1, squash=4, stretch=1, smear=5, crest=0, droplets=[(7, 29, 5, 0), (14, 19, 4, 1)]),
        dict(offset_x=2, offset_y=0, squash=3, stretch=1, smear=3, crest=0, droplets=[(10, 26, 5, 0), (16, 16, 4, 1)]),
        dict(offset_x=1, offset_y=0, squash=2, stretch=1, smear=2, crest=0, droplets=[(12, 23, 4, 0), (19, 12, 4, 1)]),
    ]
    frames: list[Image.Image] = []
    for spec in specs:
        image = canvas()
        draw = ImageDraw.Draw(image)
        draw_dash_blob(
            draw,
            offset_x=spec["offset_x"],
            offset_y=spec["offset_y"],
            squash=spec["squash"],
            stretch=spec["stretch"],
            smear=spec["smear"],
            crest=spec["crest"],
        )
        draw_droplets(draw, spec["droplets"])
        ellipse(draw, (3, 45, 14, 49), PALETTE["goo_shadow"])
        frames.append(image)
    return frames


def make_hop_attack_frames() -> list[Image.Image]:
    specs = [
        dict(offset_x=0, offset_y=-2, squash=0, stretch=3, lean=0, droplets=[(20, 10, 4, 1), (44, 12, 4, 0)]),
        dict(offset_x=-1, offset_y=-6, squash=0, stretch=6, lean=-1, droplets=[(17, 5, 5, 1), (47, 8, 4, 0)]),
        dict(offset_x=-2, offset_y=-10, squash=0, stretch=8, lean=-2, droplets=[(14, 2, 5, 1), (50, 4, 4, 0)]),
        dict(offset_x=0, offset_y=-13, squash=0, stretch=10, lean=0, droplets=[(11, 1, 5, 0), (52, 1, 5, 1)]),
        dict(offset_x=2, offset_y=-11, squash=0, stretch=9, lean=2, droplets=[(13, 3, 4, 0), (50, 0, 5, 1)]),
        dict(offset_x=2, offset_y=-8, squash=0, stretch=7, lean=2, droplets=[(16, 6, 4, 1), (49, 4, 5, 0)]),
        dict(offset_x=1, offset_y=-5, squash=0, stretch=5, lean=1, droplets=[(18, 8, 4, 1), (47, 9, 4, 0)]),
        dict(offset_x=0, offset_y=-1, squash=1, stretch=2, lean=0, droplets=[(20, 12, 4, 1), (45, 14, 4, 0)]),
    ]
    frames: list[Image.Image] = []
    for spec in specs:
        image = canvas()
        draw = ImageDraw.Draw(image)
        draw_blob(draw, offset_x=spec["offset_x"], offset_y=spec["offset_y"], squash=spec["squash"], stretch=spec["stretch"], lean=spec["lean"])
        draw_droplets(draw, spec["droplets"])
        frames.append(image)
    return frames


def make_dash_cooldown_frames() -> list[Image.Image]:
    frames: list[Image.Image] = []
    puddle_wobble = [0, 1, 2, 1, 0, -1, -2, -1]
    for index in range(60):
        image = canvas()
        draw = ImageDraw.Draw(image)

        if index < 10:
            wobble = puddle_wobble[index % len(puddle_wobble)]
            width = 58 + wobble
            height = 7 + (1 if index % 4 == 0 else 0)
            growth = 0.0
            ripple = wobble
            droplets = [(14 + (index % 4), 47, 3, 1), (43 - (index % 3), 46, 3, 0)]
        elif index < 52:
            growth = smoothstep((index - 10) / 41.0)
            width = 58 - int(round(growth * 18))
            height = 7 + int(round(growth * 31))
            ripple = 0
            droplet_y = max(14, 30 - int(round(growth * 11)))
            droplets = [(18, droplet_y, 4, 1), (45, droplet_y + 3, 4, 0)] if growth > 0.75 else []
        else:
            settle_offsets = [0, 1, 0, -1, 0, 1, 0, 0]
            settle_index = index - 52
            growth = 1.0
            width = 40
            height = 38 + settle_offsets[settle_index]
            ripple = 0
            droplets = [(18, 18, 4, 1), (45, 21, 4, 0)] if settle_index % 2 == 0 else []

        draw_cooldown_recovery_blob(draw, center_x=32, bottom=54, width=width, height=height, growth=growth, ripple=ripple)
        draw_droplets(draw, droplets)
        frames.append(image)
    return frames


def write_frames(prefix: str, frames: Iterable[Image.Image]) -> None:
    for index, frame in enumerate(frames, start=1):
        save(frame, f"{prefix}{index}.png")


def main() -> None:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    clear_existing_outputs()
    write_frames("the_goo_walk", make_walk_frames())
    write_frames("the_goo_dash_strike_telegraph", make_dash_telegraph_frames())
    write_frames("the_goo_dash_strike_attack", make_dash_attack_frames())
    write_frames("the_goo_dash_strike_cooldown", make_dash_cooldown_frames())
    write_frames("the_goo_hop_contact_attack", make_hop_attack_frames())


if __name__ == "__main__":
    main()
