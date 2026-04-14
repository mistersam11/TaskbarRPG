from __future__ import annotations

import math
from pathlib import Path
from typing import Iterable

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ASSET_DIR = ROOT / "Assets" / "Boss"

TRANSPARENT = (0, 0, 0, 0)
PALETTE = {
    "outline": (18, 14, 18, 255),
    "armor_dark": (42, 44, 52, 255),
    "armor_mid": (92, 97, 110, 255),
    "armor_light": (168, 174, 186, 255),
    "ember_dark": (112, 34, 16, 255),
    "ember_mid": (220, 82, 30, 255),
    "ember_light": (255, 186, 76, 255),
    "ember_core": (255, 239, 169, 255),
    "lava": (255, 116, 34, 255),
    "lava_glow": (255, 191, 108, 210),
    "snow_dark": (132, 166, 201, 255),
    "snow_mid": (208, 228, 246, 255),
    "snow_light": (246, 251, 255, 255),
    "warning": (255, 214, 110, 255),
    "warning_glow": (255, 150, 66, 180),
    "spike_dark": (78, 55, 62, 255),
    "spike_mid": (142, 70, 44, 255),
    "spike_light": (255, 158, 74, 255),
}

BODY_SIZE = (120, 148)
HEAD_SIZE = (48, 48)
SPIKE_SIZE = (32, 64)
SNOWBALL_SIZE = (28, 28)


def canvas(size: tuple[int, int]) -> Image.Image:
    return Image.new("RGBA", size, TRANSPARENT)


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
        "fallen_knight_walk*.png",
        "fallen_knight_spike_field_telegraph*.png",
        "fallen_knight_spike_field_attack*.png",
        "fallen_knight_snowball_heave_telegraph*.png",
        "fallen_knight_snowball_heave_attack*.png",
        "fallen_knight_fire_head_telegraph*.png",
        "fallen_knight_fire_head_attack*.png",
        "fallen_knight_fire_head_cooldown*.png",
        "fallen_knight_head_walk*.png",
        "fallen_knight_head_attack*.png",
        "fallen_knight_spike_rise*.png",
        "fallen_knight_spike_sink*.png",
        "fallen_knight_snowball_spin*.png",
    ):
        for path in ASSET_DIR.glob(pattern):
            path.unlink(missing_ok=True)


def draw_crack(draw: ImageDraw.ImageDraw, points: list[tuple[int, int]]) -> None:
    line(draw, points, PALETTE["lava_glow"], width=3)
    line(draw, points, PALETTE["lava"], width=1)


def draw_flame(draw: ImageDraw.ImageDraw, *, center_x: int, base_y: int, flicker: int, surge: int = 0) -> None:
    outer = [
        (center_x - 12, base_y),
        (center_x - 20, base_y - 12 - surge),
        (center_x - 11, base_y - 28 - flicker - surge),
        (center_x - 7, base_y - 20 - flicker),
        (center_x - 2, base_y - 40 - flicker - surge),
        (center_x + 2, base_y - 22 - flicker),
        (center_x + 8, base_y - 50 - (flicker // 2) - surge),
        (center_x + 12, base_y - 28 - flicker),
        (center_x + 18, base_y - 42 - surge),
        (center_x + 18, base_y - 12),
        (center_x + 12, base_y),
    ]
    mid = [
        (center_x - 8, base_y),
        (center_x - 12, base_y - 12 - surge),
        (center_x - 6, base_y - 24 - flicker),
        (center_x - 1, base_y - 17),
        (center_x + 4, base_y - 34 - surge),
        (center_x + 7, base_y - 19 - flicker),
        (center_x + 11, base_y - 28 - surge),
        (center_x + 12, base_y - 10),
        (center_x + 8, base_y),
    ]
    core = [
        (center_x - 4, base_y),
        (center_x - 6, base_y - 10),
        (center_x - 2, base_y - 20 - flicker),
        (center_x + 1, base_y - 12),
        (center_x + 4, base_y - 26 - surge),
        (center_x + 7, base_y - 12),
        (center_x + 5, base_y),
    ]
    polygon(draw, outer, PALETTE["ember_dark"])
    polygon(draw, mid, PALETTE["ember_mid"])
    polygon(draw, core, PALETTE["ember_light"])
    ellipse(draw, (center_x - 3, base_y - 17, center_x + 3, base_y - 9), PALETTE["ember_core"])


def draw_knight(
    *,
    crouch: int,
    lean: int,
    front_arm: tuple[tuple[int, int], tuple[int, int], tuple[int, int]],
    back_arm: tuple[tuple[int, int], tuple[int, int], tuple[int, int]],
    front_leg_shift: int,
    back_leg_shift: int,
    flame_flicker: int,
    crack_glow: int,
) -> Image.Image:
    image = canvas(BODY_SIZE)
    draw = ImageDraw.Draw(image)

    center_x = 74 + lean
    floor_y = 144
    hip_y = 96 + crouch
    torso_top = 40 + crouch
    torso_bottom = 104 + crouch

    back_leg = [
        (center_x - 10, hip_y),
        (center_x - 16 + back_leg_shift, 119 + crouch),
        (center_x - 17 + back_leg_shift, floor_y),
    ]
    front_leg = [
        (center_x + 9, hip_y),
        (center_x + 15 + front_leg_shift, 118 + crouch),
        (center_x + 17 + front_leg_shift, floor_y),
    ]

    for points, light in ((back_leg, PALETTE["armor_mid"]), (front_leg, PALETTE["armor_light"])):
        line(draw, points, PALETTE["armor_dark"], width=10)
        line(draw, points, light, width=7)
        rect(draw, (points[-1][0] - 9, floor_y - 4, points[-1][0] + 8, floor_y), PALETTE["armor_dark"])
        rect(draw, (points[-1][0] - 7, floor_y - 4, points[-1][0] + 7, floor_y - 1), light)

    polygon(
        draw,
        [
            (center_x - 22, torso_bottom),
            (center_x - 30, torso_top + 18),
            (center_x - 18, torso_top),
            (center_x + 8, torso_top - 2),
            (center_x + 21, torso_top + 12),
            (center_x + 16, torso_bottom),
        ],
        PALETTE["armor_dark"],
    )
    polygon(
        draw,
        [
            (center_x - 18, torso_bottom - 2),
            (center_x - 24, torso_top + 20),
            (center_x - 13, torso_top + 4),
            (center_x + 4, torso_top + 3),
            (center_x + 14, torso_top + 16),
            (center_x + 10, torso_bottom - 3),
        ],
        PALETTE["armor_mid"],
    )
    polygon(draw, [(center_x - 11, torso_top + 17), (center_x - 4, torso_top + 8), (center_x + 5, torso_top + 16)], PALETTE["armor_light"])
    ellipse(draw, (center_x - 34, torso_top + 8, center_x - 8, torso_top + 28), PALETTE["armor_dark"])
    ellipse(draw, (center_x + 2, torso_top + 6, center_x + 30, torso_top + 30), PALETTE["armor_dark"])
    ellipse(draw, (center_x - 31, torso_top + 10, center_x - 11, torso_top + 25), PALETTE["armor_mid"])
    ellipse(draw, (center_x + 5, torso_top + 9, center_x + 26, torso_top + 26), PALETTE["armor_mid"])
    rect(draw, (center_x - 7, torso_top + 7, center_x + 2, torso_top + 32), PALETTE["armor_light"])
    rect(draw, (center_x - 13, torso_bottom - 4, center_x + 7, torso_bottom + 4), PALETTE["armor_dark"])
    rect(draw, (center_x - 10, torso_bottom - 2, center_x + 4, torso_bottom + 3), PALETTE["armor_mid"])

    for arm, light, gauntlet in (
        (back_arm, PALETTE["armor_mid"], PALETTE["armor_mid"]),
        (front_arm, PALETTE["armor_light"], PALETTE["armor_light"]),
    ):
        line(draw, arm, PALETTE["armor_dark"], width=9)
        line(draw, arm, light, width=6)
        hand_x, hand_y = arm[-1]
        ellipse(draw, (hand_x - 7, hand_y - 7, hand_x + 7, hand_y + 7), PALETTE["armor_dark"])
        ellipse(draw, (hand_x - 5, hand_y - 5, hand_x + 5, hand_y + 5), gauntlet)

    helmet_base_x = center_x - 2
    rect(draw, (helmet_base_x - 10, torso_top - 8, helmet_base_x + 8, torso_top + 11), PALETTE["armor_dark"])
    rect(draw, (helmet_base_x - 7, torso_top - 5, helmet_base_x + 6, torso_top + 10), PALETTE["armor_mid"])
    draw_flame(draw, center_x=helmet_base_x, base_y=torso_top - 6, flicker=flame_flicker, surge=crack_glow)

    draw_crack(draw, [(center_x - 5, torso_top + 15), (center_x - 1, torso_top + 28), (center_x - 8, torso_top + 38)])
    draw_crack(draw, [(center_x + 6, torso_top + 20), (center_x + 3, torso_top + 34), (center_x + 10, torso_top + 47)])
    if crack_glow > 0:
        draw_crack(draw, [(center_x - 22, torso_top + 19), (center_x - 18, torso_top + 29), (center_x - 24, torso_top + 42)])
        draw_crack(draw, [(center_x + 18, torso_top + 21), (center_x + 13, torso_top + 32), (center_x + 21, torso_top + 45)])

    return image


def draw_rubble_frame(index: int) -> Image.Image:
    image = canvas(BODY_SIZE)
    draw = ImageDraw.Draw(image)
    floor_y = 144
    wobble = [0, 1, -1, 1][index % 4]
    pieces = [
        [(18, floor_y - 10), (36, floor_y - 24 + wobble), (48, floor_y - 9), (35, floor_y)],
        [(42, floor_y - 8), (62, floor_y - 21 - wobble), (74, floor_y - 10), (60, floor_y)],
        [(73, floor_y - 10), (92, floor_y - 26 + wobble), (107, floor_y - 8), (92, floor_y)],
        [(52, floor_y - 31), (66, floor_y - 41 + wobble), (79, floor_y - 30), (69, floor_y - 18)],
    ]
    for piece in pieces:
        polygon(draw, piece, PALETTE["armor_dark"])
        inner = [(x + (1 if i % 2 == 0 else -1), y - 1) for i, (x, y) in enumerate(piece)]
        polygon(draw, inner, PALETTE["armor_mid"])
    draw_crack(draw, [(29, floor_y - 19), (35, floor_y - 11), (28, floor_y - 3)])
    draw_crack(draw, [(82, floor_y - 20), (88, floor_y - 11), (82, floor_y - 1)])
    ellipse(draw, (54, floor_y - 45, 74, floor_y - 27), PALETTE["lava_glow"])
    return image


def draw_head_frame(*, flare: int, lean: int, attack: bool) -> Image.Image:
    image = canvas(HEAD_SIZE)
    draw = ImageDraw.Draw(image)
    center_x = 23 + lean
    base_y = 40
    draw_flame(draw, center_x=center_x, base_y=base_y, flicker=flare, surge=3 if attack else 0)
    rect(draw, (center_x - 6, base_y - 15, center_x + 6, base_y - 8), PALETTE["ember_dark"])
    rect(draw, (center_x - 5, base_y - 14, center_x + 4, base_y - 9), PALETTE["ember_mid"])
    rect(draw, (center_x - 5, base_y - 17, center_x - 1, base_y - 15), PALETTE["outline"])
    rect(draw, (center_x + 1, base_y - 17, center_x + 5, base_y - 15), PALETTE["outline"])
    if attack:
        ellipse(draw, (center_x - 18, base_y - 19, center_x - 10, base_y - 9), PALETTE["lava_glow"])
        ellipse(draw, (center_x + 11, base_y - 14, center_x + 19, base_y - 5), PALETTE["lava_glow"])
    return image


def draw_spike_frame(height: int, *, warning: bool, sink: int = 0) -> Image.Image:
    image = canvas(SPIKE_SIZE)
    draw = ImageDraw.Draw(image)
    center_x = SPIKE_SIZE[0] // 2
    base_y = SPIKE_SIZE[1] - 2 + sink
    tip_y = max(4, base_y - height)
    half_width = max(5, 5 + (height // 10))
    if warning:
        ellipse(draw, (center_x - 12, base_y - 7, center_x + 12, base_y + 1), PALETTE["warning_glow"])
        ellipse(draw, (center_x - 8, base_y - 5, center_x + 8, base_y - 1), PALETTE["warning"])
        return image

    polygon(draw, [(center_x, tip_y), (center_x - half_width, base_y), (center_x + half_width, base_y)], PALETTE["spike_dark"])
    polygon(draw, [(center_x, tip_y + 2), (center_x - half_width + 2, base_y - 1), (center_x + half_width - 2, base_y - 1)], PALETTE["spike_mid"])
    polygon(draw, [(center_x, tip_y + 4), (center_x - 3, base_y - 7), (center_x + 3, base_y - 7)], PALETTE["spike_light"])
    ellipse(draw, (center_x - 12, base_y - 7, center_x + 12, base_y - 1), PALETTE["warning_glow"])
    return image


def draw_snowball_frame(index: int) -> Image.Image:
    image = canvas(SNOWBALL_SIZE)
    draw = ImageDraw.Draw(image)
    radius = 11
    center_x = 14
    center_y = 14
    ellipse(draw, (center_x - radius, center_y - radius, center_x + radius, center_y + radius), PALETTE["snow_dark"])
    ellipse(draw, (center_x - radius + 2, center_y - radius + 2, center_x + radius - 2, center_y + radius - 2), PALETTE["snow_mid"])
    ellipse(draw, (center_x - 6, center_y - 7, center_x + 3, center_y), PALETTE["snow_light"])
    spin_shift = [-2, 0, 2, 0][index % 4]
    line(draw, [(center_x - 8, center_y - 4), (center_x - 1 + spin_shift, center_y + 1), (center_x + 7, center_y + 7)], PALETTE["snow_dark"], width=2)
    line(draw, [(center_x - 1, center_y - 9), (center_x + 5, center_y - 3 + spin_shift), (center_x + 8, center_y + 4)], PALETTE["snow_dark"], width=2)
    return image


def make_walk_frames() -> list[Image.Image]:
    frames: list[Image.Image] = []
    for index in range(8):
        phase = (index / 8.0) * math.tau
        front_leg = int(round(math.sin(phase) * 4))
        back_leg = int(round(math.sin(phase + math.pi) * 3))
        front_arm = ((84, 56), (94, 82), (98, 110 + (front_leg // 2)))
        back_arm = ((55, 55), (49, 82), (45, 108 + (back_leg // 2)))
        frames.append(
            draw_knight(
                crouch=int(round(math.cos(phase * 2) * 1)),
                lean=int(round(math.sin(phase) * 2)),
                front_arm=front_arm,
                back_arm=back_arm,
                front_leg_shift=front_leg,
                back_leg_shift=back_leg,
                flame_flicker=[0, 2, 4, 3, 1, 3, 4, 2][index],
                crack_glow=0,
            )
        )
    return frames


def make_spike_telegraph_frames() -> list[Image.Image]:
    specs = [
        dict(crouch=0, front=((84, 56), (93, 36), (86, 19)), back=((55, 55), (48, 38), (52, 24))),
        dict(crouch=-2, front=((84, 54), (95, 31), (88, 14)), back=((55, 54), (48, 34), (52, 18))),
        dict(crouch=-4, front=((84, 52), (96, 26), (89, 8)), back=((55, 52), (48, 29), (53, 12))),
        dict(crouch=-4, front=((84, 52), (96, 26), (89, 8)), back=((55, 52), (48, 29), (53, 12))),
        dict(crouch=-3, front=((84, 53), (95, 28), (88, 11)), back=((55, 53), (48, 31), (53, 15))),
        dict(crouch=-1, front=((84, 55), (94, 34), (87, 17)), back=((55, 55), (49, 36), (52, 21))),
    ]
    return [
        draw_knight(
            crouch=spec["crouch"],
            lean=0,
            front_arm=spec["front"],
            back_arm=spec["back"],
            front_leg_shift=0,
            back_leg_shift=0,
            flame_flicker=3 + idx,
            crack_glow=1,
        )
        for idx, spec in enumerate(specs)
    ]


def make_spike_attack_frames() -> list[Image.Image]:
    specs = [
        dict(crouch=-1, front=((84, 57), (94, 41), (90, 24)), back=((55, 56), (48, 42), (52, 29))),
        dict(crouch=2, front=((84, 61), (92, 84), (88, 111)), back=((55, 60), (49, 84), (46, 109))),
        dict(crouch=5, front=((84, 64), (91, 98), (89, 127)), back=((55, 62), (49, 96), (47, 123))),
        dict(crouch=6, front=((84, 65), (91, 102), (89, 130)), back=((55, 63), (49, 99), (47, 126))),
        dict(crouch=3, front=((84, 60), (92, 88), (88, 114)), back=((55, 59), (49, 86), (47, 111))),
        dict(crouch=1, front=((84, 58), (93, 72), (90, 98)), back=((55, 57), (49, 70), (47, 96))),
    ]
    return [
        draw_knight(
            crouch=spec["crouch"],
            lean=0,
            front_arm=spec["front"],
            back_arm=spec["back"],
            front_leg_shift=0,
            back_leg_shift=0,
            flame_flicker=4,
            crack_glow=2 if idx in (2, 3) else 1,
        )
        for idx, spec in enumerate(specs)
    ]


def make_snowball_telegraph_frames() -> list[Image.Image]:
    specs = [
        dict(crouch=0, front=((84, 58), (95, 48), (105, 40)), back=((55, 56), (48, 74), (43, 92))),
        dict(crouch=-1, front=((84, 57), (99, 45), (111, 39)), back=((55, 55), (48, 73), (43, 91))),
        dict(crouch=-1, front=((84, 57), (103, 42), (116, 37)), back=((55, 55), (48, 72), (43, 90))),
        dict(crouch=0, front=((84, 58), (104, 43), (117, 38)), back=((55, 56), (48, 72), (43, 90))),
        dict(crouch=1, front=((84, 59), (101, 46), (113, 42)), back=((55, 57), (48, 74), (43, 92))),
        dict(crouch=1, front=((84, 59), (97, 49), (107, 43)), back=((55, 57), (48, 75), (43, 93))),
    ]
    frames: list[Image.Image] = []
    for idx, spec in enumerate(specs):
        image = draw_knight(
            crouch=spec["crouch"],
            lean=0,
            front_arm=spec["front"],
            back_arm=spec["back"],
            front_leg_shift=0,
            back_leg_shift=0,
            flame_flicker=2 + idx,
            crack_glow=1,
        )
        draw = ImageDraw.Draw(image)
        size = 12 + idx
        ellipse(draw, (100 - size // 2, 36 - size // 2, 100 + size // 2, 36 + size // 2), PALETTE["snow_dark"])
        ellipse(draw, (102 - size // 3, 35 - size // 3, 102 + size // 3, 35 + size // 3), PALETTE["snow_mid"])
        frames.append(image)
    return frames


def make_snowball_attack_frames() -> list[Image.Image]:
    specs = [
        dict(crouch=0, front=((84, 58), (96, 48), (108, 39))),
        dict(crouch=-1, front=((84, 56), (100, 42), (114, 34))),
        dict(crouch=1, front=((84, 57), (105, 48), (118, 52))),
        dict(crouch=2, front=((84, 58), (104, 54), (117, 60))),
        dict(crouch=1, front=((84, 58), (100, 51), (113, 54))),
        dict(crouch=0, front=((84, 58), (96, 50), (108, 47))),
        dict(crouch=0, front=((84, 57), (94, 49), (105, 44))),
        dict(crouch=0, front=((84, 57), (92, 50), (101, 45))),
    ]
    return [
        draw_knight(
            crouch=spec["crouch"],
            lean=0,
            front_arm=spec["front"],
            back_arm=((55, 56), (48, 74), (43, 92)),
            front_leg_shift=0,
            back_leg_shift=0,
            flame_flicker=3,
            crack_glow=0,
        )
        for spec in specs
    ]


def make_fire_head_telegraph_frames() -> list[Image.Image]:
    specs = [
        dict(crouch=0, lean=0, front=((84, 59), (92, 82), (94, 109)), back=((55, 57), (49, 83), (47, 109))),
        dict(crouch=2, lean=-1, front=((83, 61), (90, 86), (90, 114)), back=((54, 59), (48, 86), (45, 113))),
        dict(crouch=4, lean=-2, front=((82, 64), (88, 90), (86, 118)), back=((53, 61), (46, 89), (42, 116))),
        dict(crouch=6, lean=-3, front=((81, 68), (87, 95), (84, 123)), back=((52, 63), (45, 92), (40, 121))),
        dict(crouch=7, lean=-3, front=((80, 70), (86, 97), (83, 126)), back=((51, 65), (44, 95), (39, 124))),
        dict(crouch=8, lean=-3, front=((80, 71), (85, 100), (82, 128)), back=((50, 66), (43, 98), (38, 126))),
    ]
    return [
        draw_knight(
            crouch=spec["crouch"],
            lean=spec["lean"],
            front_arm=spec["front"],
            back_arm=spec["back"],
            front_leg_shift=0,
            back_leg_shift=0,
            flame_flicker=5,
            crack_glow=2,
        )
        for spec in specs
    ]


def make_fire_head_attack_frames() -> list[Image.Image]:
    specs = [
        dict(crouch=7, lean=-3, front=((80, 71), (85, 100), (82, 128)), back=((50, 66), (43, 98), (38, 126))),
        dict(crouch=10, lean=-5, front=((78, 78), (82, 106), (79, 132)), back=((47, 69), (40, 101), (34, 129))),
        dict(crouch=14, lean=-7, front=((76, 88), (79, 114), (75, 137)), back=((43, 74), (35, 107), (28, 134))),
        dict(crouch=18, lean=-8, front=((74, 100), (75, 124), (70, 142)), back=((39, 82), (30, 114), (21, 140))),
        dict(crouch=20, lean=-8, front=((73, 104), (74, 128), (68, 144)), back=((37, 86), (28, 118), (18, 143))),
        dict(crouch=22, lean=-8, front=((72, 108), (73, 132), (66, 145)), back=((35, 91), (26, 123), (16, 144))),
    ]
    return [
        draw_knight(
            crouch=spec["crouch"],
            lean=spec["lean"],
            front_arm=spec["front"],
            back_arm=spec["back"],
            front_leg_shift=0,
            back_leg_shift=0,
            flame_flicker=6,
            crack_glow=3,
        )
        for spec in specs
    ]


def make_fire_head_cooldown_frames() -> list[Image.Image]:
    return [draw_rubble_frame(index) for index in range(8)]


def make_head_walk_frames() -> list[Image.Image]:
    return [
        draw_head_frame(flare=flare, lean=lean, attack=False)
        for flare, lean in ((0, -1), (2, 0), (4, 1), (3, 1), (1, 0), (3, -1), (4, -1), (2, 0))
    ]


def make_head_attack_frames() -> list[Image.Image]:
    return [
        draw_head_frame(flare=flare, lean=lean, attack=True)
        for flare, lean in ((4, -1), (6, 0), (8, 1), (5, 0))
    ]


def make_spike_rise_frames() -> list[Image.Image]:
    return [
        draw_spike_frame(0, warning=True),
        draw_spike_frame(18, warning=False),
        draw_spike_frame(32, warning=False),
        draw_spike_frame(46, warning=False),
        draw_spike_frame(58, warning=False),
    ]


def make_spike_sink_frames() -> list[Image.Image]:
    return [
        draw_spike_frame(42, warning=False, sink=4),
        draw_spike_frame(28, warning=False, sink=10),
        draw_spike_frame(14, warning=False, sink=16),
        draw_spike_frame(0, warning=True, sink=0),
    ]


def make_snowball_frames() -> list[Image.Image]:
    return [draw_snowball_frame(index) for index in range(4)]


def write_frames(prefix: str, frames: Iterable[Image.Image]) -> None:
    for index, frame in enumerate(frames, start=1):
        save(frame, f"{prefix}{index}.png")


def main() -> None:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    clear_existing_outputs()

    write_frames("fallen_knight_walk", make_walk_frames())
    write_frames("fallen_knight_spike_field_telegraph", make_spike_telegraph_frames())
    write_frames("fallen_knight_spike_field_attack", make_spike_attack_frames())
    write_frames("fallen_knight_snowball_heave_telegraph", make_snowball_telegraph_frames())
    write_frames("fallen_knight_snowball_heave_attack", make_snowball_attack_frames())
    write_frames("fallen_knight_fire_head_telegraph", make_fire_head_telegraph_frames())
    write_frames("fallen_knight_fire_head_attack", make_fire_head_attack_frames())
    write_frames("fallen_knight_fire_head_cooldown", make_fire_head_cooldown_frames())
    write_frames("fallen_knight_head_walk", make_head_walk_frames())
    write_frames("fallen_knight_head_attack", make_head_attack_frames())
    write_frames("fallen_knight_spike_rise", make_spike_rise_frames())
    write_frames("fallen_knight_spike_sink", make_spike_sink_frames())
    write_frames("fallen_knight_snowball_spin", make_snowball_frames())


if __name__ == "__main__":
    main()
