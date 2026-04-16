from __future__ import annotations

from pathlib import Path
from typing import Iterable

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ASSET_DIR = ROOT / "Assets" / "Enemy"

TRANSPARENT = (0, 0, 0, 0)
PALETTE = {
    "outline": (26, 16, 18, 255),
    "coal_dark": (44, 34, 38, 255),
    "coal_mid": (76, 60, 54, 255),
    "coal_light": (112, 86, 68, 255),
    "ember_dark": (176, 56, 18, 255),
    "ember_mid": (255, 118, 34, 255),
    "ember_light": (255, 182, 84, 255),
    "ember_core": (255, 238, 166, 255),
    "smoke": (116, 92, 80, 130),
    "eye": (255, 228, 156, 255),
    "eye_hot": (255, 250, 206, 255),
}

SIZE = (40, 44)


def canvas() -> Image.Image:
    return Image.new("RGBA", SIZE, TRANSPARENT)


def rect(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], color: tuple[int, int, int, int]) -> None:
    draw.rectangle(box, fill=color)


def ellipse(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], color: tuple[int, int, int, int]) -> None:
    draw.ellipse(box, fill=color)


def polygon(draw: ImageDraw.ImageDraw, points: Iterable[tuple[int, int]], color: tuple[int, int, int, int]) -> None:
    draw.polygon(list(points), fill=color)


def line(
    draw: ImageDraw.ImageDraw,
    points: Iterable[tuple[int, int]],
    color: tuple[int, int, int, int],
    *,
    width: int = 1,
) -> None:
    draw.line(list(points), fill=color, width=width)


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
        "emberling_walk*.png",
        "emberling_dash_strike_telegraph*.png",
        "emberling_dash_strike_attack*.png",
        "emberling_hop_contact_attack*.png",
    ):
        for path in ASSET_DIR.glob(pattern):
            path.unlink(missing_ok=True)


def draw_flame(
    draw: ImageDraw.ImageDraw,
    *,
    center_x: int,
    base_y: int,
    height: int,
    lean: int = 0,
    brighten: bool = False,
) -> None:
    outer = [
        (center_x - 8, base_y),
        (center_x - 11 + lean, base_y - 8),
        (center_x - 6 + lean, base_y - 18),
        (center_x - 2 + lean, base_y - 12),
        (center_x + 1 + lean, base_y - height),
        (center_x + 4 + lean, base_y - 14),
        (center_x + 9 + lean, base_y - 20),
        (center_x + 10, base_y - 7),
        (center_x + 7, base_y),
    ]
    mid = [
        (center_x - 5, base_y),
        (center_x - 7 + lean, base_y - 8),
        (center_x - 2 + lean, base_y - 16),
        (center_x + 1 + lean, base_y - height + 5),
        (center_x + 4 + lean, base_y - 13),
        (center_x + 7, base_y - 6),
        (center_x + 5, base_y),
    ]
    core = [
        (center_x - 2, base_y - 1),
        (center_x - 3 + lean, base_y - 7),
        (center_x + lean, base_y - 12),
        (center_x + 2 + lean, base_y - height + 9),
        (center_x + 4, base_y - 8),
        (center_x + 2, base_y - 1),
    ]
    polygon(draw, outer, PALETTE["ember_dark"])
    polygon(draw, mid, PALETTE["ember_mid"] if not brighten else PALETTE["ember_light"])
    polygon(draw, core, PALETTE["ember_light"])
    ellipse(draw, (center_x - 2, base_y - 10, center_x + 3, base_y - 5), PALETTE["ember_core"])


def draw_emberling_frame(
    *,
    bob: int,
    stride: int,
    flame_height: int,
    arm_reach: int,
    eye_glow: bool = False,
    lunge: int = 0,
    trail: bool = False,
) -> Image.Image:
    image = canvas()
    draw = ImageDraw.Draw(image)

    floor_y = 39 + max(0, bob // 2)
    center_x = 20 + lunge
    body_top = 11 + bob
    body_bottom = 34 + bob

    ellipse(draw, (center_x - 10, floor_y - 2, center_x + 10, floor_y + 2), PALETTE["smoke"])
    if trail:
        ellipse(draw, (center_x - 18, floor_y - 3, center_x - 7, floor_y + 1), PALETTE["smoke"])
        draw_flame(draw, center_x=center_x - 13, base_y=33 + bob, height=12, lean=-3, brighten=False)

    polygon(
        draw,
        [
            (center_x - 11, body_bottom),
            (center_x - 13, body_top + 8),
            (center_x - 8, body_top + 1),
            (center_x - 1, body_top - 2),
            (center_x + 8, body_top + 3),
            (center_x + 11, body_top + 12),
            (center_x + 10, body_bottom),
        ],
        PALETTE["coal_dark"],
    )
    polygon(
        draw,
        [
            (center_x - 8, body_bottom - 1),
            (center_x - 9, body_top + 10),
            (center_x - 5, body_top + 3),
            (center_x + 2, body_top + 1),
            (center_x + 8, body_top + 8),
            (center_x + 7, body_bottom - 1),
        ],
        PALETTE["coal_mid"],
    )
    polygon(
        draw,
        [
            (center_x - 4, body_bottom - 2),
            (center_x - 4, body_top + 10),
            (center_x, body_top + 5),
            (center_x + 4, body_top + 10),
            (center_x + 3, body_bottom - 2),
        ],
        PALETTE["coal_light"],
    )

    draw_flame(draw, center_x=center_x, base_y=31 + bob, height=flame_height, lean=lunge // 2, brighten=eye_glow)

    eye_color = PALETTE["eye_hot"] if eye_glow else PALETTE["eye"]
    rect(draw, (center_x - 4, body_top + 12, center_x - 2, body_top + 14), eye_color)
    rect(draw, (center_x + 2, body_top + 12, center_x + 4, body_top + 14), eye_color)

    line(draw, [(center_x - 8, body_top + 16), (center_x - 12 - arm_reach, body_top + 18), (center_x - 10 - arm_reach, body_top + 23)], PALETTE["coal_dark"], width=2)
    line(draw, [(center_x + 8, body_top + 16), (center_x + 12 + arm_reach, body_top + 18), (center_x + 10 + arm_reach, body_top + 23)], PALETTE["coal_dark"], width=2)
    line(draw, [(center_x - 6, body_bottom), (center_x - 8 - stride, floor_y)], PALETTE["coal_dark"], width=2)
    line(draw, [(center_x + 4, body_bottom), (center_x + 6 + stride, floor_y)], PALETTE["coal_dark"], width=2)
    rect(draw, (center_x - 10 - stride, floor_y - 1, center_x - 5 - stride, floor_y), PALETTE["coal_mid"])
    rect(draw, (center_x + 4 + stride, floor_y - 1, center_x + 9 + stride, floor_y), PALETTE["coal_mid"])

    polygon(draw, [(center_x - 7, body_top + 2), (center_x - 9, body_top - 4), (center_x - 3, body_top + 4)], PALETTE["coal_dark"])
    polygon(draw, [(center_x + 5, body_top + 3), (center_x + 8, body_top - 3), (center_x + 2, body_top + 5)], PALETTE["coal_dark"])
    return image


def make_walk_frames() -> list[Image.Image]:
    return [
        draw_emberling_frame(bob=0, stride=-2, flame_height=20, arm_reach=0),
        draw_emberling_frame(bob=1, stride=1, flame_height=18, arm_reach=1),
        draw_emberling_frame(bob=0, stride=2, flame_height=19, arm_reach=0),
        draw_emberling_frame(bob=-1, stride=-1, flame_height=21, arm_reach=-1),
        draw_emberling_frame(bob=0, stride=-2, flame_height=20, arm_reach=0),
        draw_emberling_frame(bob=1, stride=1, flame_height=18, arm_reach=1),
    ]


def make_telegraph_frames() -> list[Image.Image]:
    return [
        draw_emberling_frame(bob=0, stride=0, flame_height=21 + i, arm_reach=1 + i, eye_glow=True)
        for i in (0, 1, 2, 3, 4)
    ]


def make_attack_frames() -> list[Image.Image]:
    return [
        draw_emberling_frame(bob=0, stride=0, flame_height=23, arm_reach=3, eye_glow=True, lunge=1),
        draw_emberling_frame(bob=-1, stride=1, flame_height=24, arm_reach=4, eye_glow=True, lunge=3, trail=True),
        draw_emberling_frame(bob=-1, stride=2, flame_height=24, arm_reach=4, eye_glow=True, lunge=5, trail=True),
        draw_emberling_frame(bob=0, stride=2, flame_height=22, arm_reach=3, eye_glow=True, lunge=6, trail=True),
        draw_emberling_frame(bob=1, stride=1, flame_height=20, arm_reach=1, eye_glow=False, lunge=3),
        draw_emberling_frame(bob=1, stride=0, flame_height=18, arm_reach=0, eye_glow=False),
    ]


def make_hop_attack_frames() -> list[Image.Image]:
    return [
        draw_emberling_frame(bob=0, stride=0, flame_height=22, arm_reach=2, eye_glow=True),
        draw_emberling_frame(bob=-2, stride=1, flame_height=25, arm_reach=2, eye_glow=True, lunge=1),
        draw_emberling_frame(bob=-4, stride=2, flame_height=27, arm_reach=1, eye_glow=True, lunge=2, trail=True),
        draw_emberling_frame(bob=-2, stride=2, flame_height=24, arm_reach=0, eye_glow=True, lunge=2, trail=True),
        draw_emberling_frame(bob=0, stride=1, flame_height=21, arm_reach=0, eye_glow=False, lunge=1),
    ]


def write_frames(prefix: str, frames: list[Image.Image]) -> None:
    for index, frame in enumerate(frames, start=1):
        save(frame, f"{prefix}{index}.png")


def main() -> None:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    clear_existing_outputs()
    write_frames("emberling_walk", make_walk_frames())
    write_frames("emberling_dash_strike_telegraph", make_telegraph_frames())
    write_frames("emberling_dash_strike_attack", make_attack_frames())
    write_frames("emberling_hop_contact_attack", make_hop_attack_frames())


if __name__ == "__main__":
    main()
