from __future__ import annotations

from pathlib import Path
from typing import Iterable

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ASSET_DIR = ROOT / "Assets" / "Player"

TRANSPARENT = (0, 0, 0, 0)
PALETTE = {
    "outline": (26, 22, 28, 255),
    "fur_dark": (82, 76, 88, 255),
    "fur_mid": (122, 116, 129, 255),
    "fur_light": (199, 188, 173, 255),
    "mask": (38, 34, 42, 255),
    "ear_inner": (201, 160, 168, 255),
    "eye": (244, 239, 226, 255),
    "nose": (47, 37, 40, 255),
    "tunic": (84, 122, 91, 255),
    "tunic_shadow": (63, 92, 70, 255),
    "belt": (124, 84, 53, 255),
    "boot": (74, 53, 37, 255),
    "glove": (94, 88, 98, 255),
    "metal": (208, 214, 221, 255),
    "metal_shadow": (145, 152, 160, 255),
    "wood": (123, 86, 52, 255),
    "string": (220, 210, 183, 255),
    "arrow_shaft": (166, 123, 76, 255),
    "arrow_tip": (224, 231, 236, 255),
    "fletching": (172, 79, 62, 255),
    "fire_purple_outer": (88, 41, 173, 255),
    "fire_purple_mid": (143, 72, 233, 255),
    "fire_purple_core": (226, 154, 255, 255),
    "fire_red": (208, 64, 38, 255),
    "fire_orange": (246, 139, 40, 255),
    "fire_yellow": (255, 214, 89, 255),
    "hurt_red": (210, 82, 77, 255),
}


def canvas(width: int, height: int) -> Image.Image:
    return Image.new("RGBA", (width, height), TRANSPARENT)


def rect(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], color: tuple[int, int, int, int]) -> None:
    draw.rectangle(box, fill=color)


def ellipse(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], color: tuple[int, int, int, int]) -> None:
    draw.ellipse(box, fill=color)


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
    patterns = [
        "player_idle*.png",
        "player_walk*.png",
        "player_jump*.png",
        "player_attack*.png",
        "player_damaged*.png",
        "player_bow_*.png",
        "player_arrow*.png",
        "arrow.png",
    ]
    for pattern in patterns:
        for path in ASSET_DIR.glob(pattern):
            path.unlink(missing_ok=True)


def draw_tail(draw: ImageDraw.ImageDraw, x: int, y: int, sway: int) -> None:
    segments = [
        (x + 6 + sway, y + 17, x + 11 + sway, y + 20),
        (x + 5 + sway, y + 20, x + 11 + sway, y + 24),
        (x + 5 + sway, y + 24, x + 11 + sway, y + 28),
    ]
    for segment in segments:
        ellipse(draw, segment, PALETTE["fur_mid"])
    rect(draw, (x + 5 + sway, y + 20, x + 10 + sway, y + 20), PALETTE["mask"])
    rect(draw, (x + 5 + sway, y + 24, x + 10 + sway, y + 24), PALETTE["mask"])
    rect(draw, (x + 6 + sway, y + 27, x + 10 + sway, y + 27), PALETTE["mask"])


def draw_leg(draw: ImageDraw.ImageDraw, x: int, y: int, offset_x: int, offset_y: int, front: bool) -> None:
    leg_color = PALETTE["fur_mid"] if front else PALETTE["fur_dark"]
    rect(draw, (x + offset_x, y + 20 + offset_y, x + offset_x + 2, y + 26), leg_color)
    rect(draw, (x + offset_x, y + 26, x + offset_x + 3, y + 29), PALETTE["boot"])


def draw_torso(draw: ImageDraw.ImageDraw, x: int, y: int, bob: int) -> None:
    rect(draw, (x + 12, y + 12 + bob, x + 19, y + 19 + bob), PALETTE["tunic"])
    rect(draw, (x + 12, y + 18 + bob, x + 19, y + 19 + bob), PALETTE["belt"])
    rect(draw, (x + 12, y + 12 + bob, x + 13, y + 19 + bob), PALETTE["tunic_shadow"])


def draw_head(draw: ImageDraw.ImageDraw, x: int, y: int, bob: int, blink: bool = False) -> None:
    ear_y = y + bob
    rect(draw, (x + 13, ear_y + 2, x + 15, ear_y + 5), PALETTE["fur_dark"])
    rect(draw, (x + 17, ear_y + 2, x + 19, ear_y + 5), PALETTE["fur_dark"])
    rect(draw, (x + 14, ear_y + 3, x + 14, ear_y + 4), PALETTE["ear_inner"])
    rect(draw, (x + 18, ear_y + 3, x + 18, ear_y + 4), PALETTE["ear_inner"])
    ellipse(draw, (x + 12, y + 4 + bob, x + 20, y + 12 + bob), PALETTE["fur_mid"])
    rect(draw, (x + 13, y + 6 + bob, x + 18, y + 8 + bob), PALETTE["mask"])
    ellipse(draw, (x + 17, y + 8 + bob, x + 21, y + 11 + bob), PALETTE["fur_light"])
    rect(draw, (x + 20, y + 9 + bob, x + 20, y + 9 + bob), PALETTE["nose"])
    if blink:
        rect(draw, (x + 18, y + 7 + bob, x + 18, y + 7 + bob), PALETTE["nose"])
    else:
        rect(draw, (x + 18, y + 7 + bob, x + 18, y + 7 + bob), PALETTE["eye"])


def draw_idle_arms(draw: ImageDraw.ImageDraw, x: int, y: int, bob: int, swing: int = 0) -> None:
    rect(draw, (x + 11, y + 14 + bob, x + 12, y + 20 + bob + swing), PALETTE["glove"])
    rect(draw, (x + 18, y + 14 + bob - swing, x + 19, y + 20 + bob), PALETTE["glove"])


def draw_character_base(
    width: int,
    *,
    bob: int = 0,
    tail_sway: int = 0,
    blink: bool = False,
    back_leg: tuple[int, int] = (13, 0),
    front_leg: tuple[int, int] = (17, 0),
    arm_swing: int = 0,
) -> Image.Image:
    image = canvas(width, 32)
    draw = ImageDraw.Draw(image)
    draw_tail(draw, 0, bob, tail_sway)
    draw_leg(draw, 0, bob, back_leg[0], back_leg[1], front=False)
    draw_torso(draw, 0, 0, bob)
    draw_head(draw, 0, 0, bob, blink=blink)
    draw_leg(draw, 0, bob, front_leg[0], front_leg[1], front=True)
    draw_idle_arms(draw, 0, 0, bob, swing=arm_swing)
    return image


def add_sword_pose(base: Image.Image, frame: int) -> Image.Image:
    image = base.copy()
    draw = ImageDraw.Draw(image)
    arm_sets = [
        ((18, 14), (23, 12), (28, 10)),
        ((18, 14), (24, 13), (31, 13)),
        ((18, 15), (25, 15), (35, 16)),
        ((18, 15), (23, 17), (29, 19)),
    ]
    sword_sets = [
        ((28, 10), (39, 6)),
        ((31, 13), (45, 12)),
        ((35, 16), (56, 16)),
        ((29, 19), (44, 24)),
    ]
    hilt_start, elbow, hand = arm_sets[frame]
    blade_start, blade_end = sword_sets[frame]
    draw.line([hilt_start, elbow, hand], fill=PALETTE["glove"], width=2)
    draw.line([hand, blade_start], fill=PALETTE["wood"], width=2)
    draw.line([blade_start, blade_end], fill=PALETTE["metal"], width=2)
    draw.line([blade_start[0], blade_start[1] + 1, blade_end[0], blade_end[1] + 1], fill=PALETTE["metal_shadow"], width=1)
    rect(draw, (blade_start[0] - 1, blade_start[1] - 1, blade_start[0] + 1, blade_start[1]), PALETTE["belt"])
    return image


def add_down_sword_pose(base: Image.Image, frame: int) -> Image.Image:
    image = base.copy()
    draw = ImageDraw.Draw(image)
    arm_sets = [
        ((18, 14), (23, 12), (28, 10)),
        ((18, 14), (23, 14), (27, 17)),
        ((18, 15), (21, 17), (24, 20)),
        ((18, 15), (20, 18), (21, 21)),
    ]
    sword_sets = [
        ((28, 10), (39, 20)),
        ((27, 17), (34, 31)),
        ((24, 20), (28, 31)),
        ((21, 21), (19, 31)),
    ]
    hilt_start, elbow, hand = arm_sets[frame]
    blade_start, blade_end = sword_sets[frame]
    draw.line([hilt_start, elbow, hand], fill=PALETTE["glove"], width=2)
    draw.line([hand, blade_start], fill=PALETTE["wood"], width=2)
    draw.line([blade_start, blade_end], fill=PALETTE["metal"], width=2)
    draw.line([blade_start[0] + 1, blade_start[1], blade_end[0] + 1, blade_end[1]], fill=PALETTE["metal_shadow"], width=1)
    rect(draw, (blade_start[0] - 1, blade_start[1] - 1, blade_start[0] + 1, blade_start[1]), PALETTE["belt"])
    return image


def draw_bow(draw: ImageDraw.ImageDraw, grip_x: int, center_y: int) -> tuple[tuple[int, int], tuple[int, int], tuple[int, int]]:
    top_tip = (grip_x - 3, center_y - 10)
    upper_curve = (grip_x - 1, center_y - 5)
    grip = (grip_x, center_y)
    lower_curve = (grip_x - 1, center_y + 5)
    bottom_tip = (grip_x - 3, center_y + 10)
    draw.line([top_tip, upper_curve, grip, lower_curve, bottom_tip], fill=PALETTE["wood"], width=2)
    rect(draw, (grip_x - 1, center_y - 1, grip_x, center_y + 1), PALETTE["belt"])
    return top_tip, grip, bottom_tip


def draw_arrow(draw: ImageDraw.ImageDraw, tail: tuple[int, int], head: tuple[int, int], flaming: bool, flame_frame: int = 0) -> None:
    draw.line([tail, head], fill=PALETTE["arrow_shaft"], width=1)
    rect(draw, (head[0], head[1] - 1, head[0] + 1, head[1] + 1), PALETTE["arrow_tip"])
    rect(draw, (tail[0] - 1, tail[1] - 1, tail[0], tail[1]), PALETTE["fletching"])
    if flaming:
        flame_offsets = [
            [(2, 0, "fire_purple_core"), (3, -1, "fire_purple_core"), (3, 1, "fire_purple_core"), (4, -2, "fire_purple_core"), (4, 0, "fire_purple_core"), (4, 2, "fire_purple_core"), (5, -3, "fire_purple_mid"), (5, -1, "fire_purple_mid"), (5, 1, "fire_purple_mid"), (5, 3, "fire_purple_mid"), (6, -4, "fire_purple_mid"), (6, -2, "fire_purple_mid"), (6, 0, "fire_purple_mid"), (6, 2, "fire_purple_mid"), (6, 4, "fire_purple_mid"), (7, -3, "fire_purple_outer"), (7, -1, "fire_purple_outer"), (7, 1, "fire_purple_outer"), (7, 3, "fire_purple_outer"), (8, -2, "fire_purple_outer"), (8, 0, "fire_purple_outer"), (8, 2, "fire_purple_outer"), (9, -1, "fire_purple_outer"), (9, 1, "fire_purple_outer")],
            [(2, 0, "fire_purple_core"), (3, -1, "fire_purple_core"), (3, 1, "fire_purple_core"), (4, -2, "fire_purple_core"), (4, 0, "fire_purple_core"), (4, 2, "fire_purple_core"), (5, -4, "fire_purple_mid"), (5, -2, "fire_purple_mid"), (5, 0, "fire_purple_mid"), (5, 2, "fire_purple_mid"), (5, 4, "fire_purple_mid"), (6, -3, "fire_purple_mid"), (6, -1, "fire_purple_mid"), (6, 1, "fire_purple_mid"), (6, 3, "fire_purple_mid"), (7, -4, "fire_purple_outer"), (7, -2, "fire_purple_outer"), (7, 0, "fire_purple_outer"), (7, 2, "fire_purple_outer"), (7, 4, "fire_purple_outer"), (8, -1, "fire_purple_outer"), (8, 1, "fire_purple_outer"), (9, 0, "fire_purple_outer")],
            [(2, 0, "fire_purple_core"), (3, 0, "fire_purple_core"), (4, -1, "fire_purple_core"), (4, 1, "fire_purple_core"), (5, -3, "fire_purple_mid"), (5, -1, "fire_purple_mid"), (5, 1, "fire_purple_mid"), (5, 3, "fire_purple_mid"), (6, -4, "fire_purple_mid"), (6, -2, "fire_purple_mid"), (6, 0, "fire_purple_mid"), (6, 2, "fire_purple_mid"), (6, 4, "fire_purple_mid"), (7, -3, "fire_purple_outer"), (7, -1, "fire_purple_outer"), (7, 1, "fire_purple_outer"), (7, 3, "fire_purple_outer"), (8, -2, "fire_purple_outer"), (8, 0, "fire_purple_outer"), (8, 2, "fire_purple_outer"), (9, -1, "fire_purple_outer"), (9, 1, "fire_purple_outer"), (10, 0, "fire_purple_outer")],
            [(2, 0, "fire_purple_core"), (3, -1, "fire_purple_core"), (3, 1, "fire_purple_core"), (4, -2, "fire_purple_core"), (4, 0, "fire_purple_core"), (4, 2, "fire_purple_core"), (5, -3, "fire_purple_mid"), (5, -1, "fire_purple_mid"), (5, 1, "fire_purple_mid"), (5, 3, "fire_purple_mid"), (6, -4, "fire_purple_mid"), (6, -2, "fire_purple_mid"), (6, 0, "fire_purple_mid"), (6, 2, "fire_purple_mid"), (6, 4, "fire_purple_mid"), (7, -2, "fire_purple_outer"), (7, 0, "fire_purple_outer"), (7, 2, "fire_purple_outer"), (8, -3, "fire_purple_outer"), (8, -1, "fire_purple_outer"), (8, 1, "fire_purple_outer"), (8, 3, "fire_purple_outer"), (9, -2, "fire_purple_outer"), (9, 0, "fire_purple_outer"), (9, 2, "fire_purple_outer")],
        ][flame_frame % 4]
        for offset_x, offset_y, color_name in flame_offsets:
            rect(draw, (head[0] + offset_x, head[1] - 1 + offset_y, head[0] + offset_x, head[1] + 1 + offset_y), PALETTE[color_name])


def add_bow_pose(
    base: Image.Image,
    pull: int,
    *,
    flaming: bool = False,
    flame_frame: int = 0,
    moving: bool = False,
    bow_bob: int = 0,
) -> Image.Image:
    image = base.copy()
    draw = ImageDraw.Draw(image)
    center_y = 15 + bow_bob
    bow_shoulder = (18, 14 + bow_bob)
    bow_elbow = (22, 14 + bow_bob)
    bow_hand = (27, 15 + bow_bob)
    pull_shoulder = (16, 15 + bow_bob)
    pull_elbow = (20, 13 + bow_bob)
    pull_hand = (25 - pull, 15 + bow_bob)
    if moving:
        center_y = 16 + bow_bob
        bow_shoulder = (18, 15 + bow_bob)
        bow_elbow = (22, 15 + bow_bob)
        bow_hand = (27, 16 + bow_bob)
        pull_shoulder = (16, 16 + bow_bob)
        pull_elbow = (20, 14 + bow_bob)
        pull_hand = (25 - pull, 16 + bow_bob)
    draw.line([bow_shoulder, bow_elbow, bow_hand], fill=PALETTE["glove"], width=2)
    draw.line([pull_shoulder, pull_elbow, pull_hand], fill=PALETTE["glove"], width=2)
    top_tip, grip, bottom_tip = draw_bow(draw, 30, center_y)
    draw.line([top_tip, pull_hand, bottom_tip], fill=PALETTE["string"], width=1)
    arrow_tail = (pull_hand[0] + 1, center_y)
    arrow_head = (36, center_y)
    draw_arrow(draw, arrow_tail, arrow_head, flaming=flaming, flame_frame=flame_frame)
    rect(draw, (grip[0] - 1, center_y - 1, grip[0], center_y + 1), PALETTE["glove"])
    return image


def tint_hurt(base: Image.Image, blink: bool) -> Image.Image:
    image = base.copy()
    pixels = image.load()
    for y in range(image.height):
        for x in range(image.width):
            pixel = pixels[x, y]
            if pixel[3] == 0:
                continue
            if blink:
                pixels[x, y] = PALETTE["hurt_red"]
            else:
                pixels[x, y] = (
                    min(255, pixel[0] + 45),
                    max(0, pixel[1] - 20),
                    max(0, pixel[2] - 20),
                    pixel[3],
                )
    return image


def make_idle_frames() -> list[Image.Image]:
    return [
        draw_character_base(32, bob=0, tail_sway=0, blink=False, back_leg=(13, 0), front_leg=(17, 0), arm_swing=0),
        draw_character_base(32, bob=0, tail_sway=1, blink=True, back_leg=(13, 0), front_leg=(17, 0), arm_swing=0),
    ]


def make_walk_frames() -> list[Image.Image]:
    poses = [
        dict(bob=0, tail_sway=-1, back_leg=(12, 0), front_leg=(18, 1), arm_swing=1),
        dict(bob=-1, tail_sway=0, back_leg=(13, 1), front_leg=(17, 0), arm_swing=0),
        dict(bob=0, tail_sway=1, back_leg=(14, 1), front_leg=(16, 0), arm_swing=-1),
        dict(bob=-1, tail_sway=0, back_leg=(13, 0), front_leg=(17, 1), arm_swing=0),
    ]
    return [draw_character_base(32, **pose) for pose in poses]


def make_jump_frames() -> list[Image.Image]:
    poses = [
        dict(bob=1, tail_sway=0, back_leg=(13, 1), front_leg=(17, 1), arm_swing=1),
        dict(bob=0, tail_sway=1, back_leg=(14, -1), front_leg=(16, -1), arm_swing=0),
        dict(bob=-1, tail_sway=1, back_leg=(14, -2), front_leg=(16, -2), arm_swing=-1),
        dict(bob=0, tail_sway=-1, back_leg=(13, -1), front_leg=(17, -1), arm_swing=0),
    ]
    return [draw_character_base(32, **pose) for pose in poses]


def make_attack_frames() -> list[Image.Image]:
    bases = [
        draw_character_base(64, bob=0, tail_sway=0, back_leg=(13, 0), front_leg=(17, 0), arm_swing=0),
        draw_character_base(64, bob=-1, tail_sway=1, back_leg=(13, 0), front_leg=(17, 1), arm_swing=0),
        draw_character_base(64, bob=0, tail_sway=-1, back_leg=(12, 1), front_leg=(18, 0), arm_swing=0),
        draw_character_base(64, bob=0, tail_sway=0, back_leg=(13, 0), front_leg=(17, 0), arm_swing=0),
    ]
    return [add_sword_pose(base, index) for index, base in enumerate(bases)]


def make_down_attack_frames() -> list[Image.Image]:
    bases = [
        draw_character_base(64, bob=0, tail_sway=0, back_leg=(13, -1), front_leg=(17, 0), arm_swing=0),
        draw_character_base(64, bob=1, tail_sway=1, back_leg=(13, 0), front_leg=(17, 1), arm_swing=0),
        draw_character_base(64, bob=1, tail_sway=-1, back_leg=(12, 1), front_leg=(18, 1), arm_swing=0),
        draw_character_base(64, bob=0, tail_sway=0, back_leg=(13, 0), front_leg=(17, 1), arm_swing=0),
    ]
    return [add_down_sword_pose(base, index) for index, base in enumerate(bases)]


def make_bow_walk_frames(pull: int) -> list[Image.Image]:
    poses = [
        dict(bob=0, tail_sway=-1, back_leg=(12, 0), front_leg=(18, 1), arm_swing=0),
        dict(bob=-1, tail_sway=0, back_leg=(13, 1), front_leg=(17, 0), arm_swing=0),
        dict(bob=0, tail_sway=1, back_leg=(14, 1), front_leg=(16, 0), arm_swing=0),
        dict(bob=-1, tail_sway=0, back_leg=(13, 0), front_leg=(17, 1), arm_swing=0),
    ]
    bow_bob_offsets = [0, -1, 1, 0]
    frames: list[Image.Image] = []
    for index, pose in enumerate(poses):
        base = draw_character_base(64, **pose)
        frames.append(add_bow_pose(base, pull, moving=True, bow_bob=bow_bob_offsets[index]))
    return frames


def make_bow_charge_frame(pull: int) -> Image.Image:
    base = draw_character_base(64, bob=0, tail_sway=0, back_leg=(13, 0), front_leg=(17, 0), arm_swing=0)
    return add_bow_pose(base, pull, moving=False)


def make_bow_full_frames() -> list[Image.Image]:
    frames: list[Image.Image] = []
    for flame_frame in range(4):
        base = draw_character_base(64, bob=0, tail_sway=(flame_frame % 2), back_leg=(13, 0), front_leg=(17, 0), arm_swing=0)
        frames.append(add_bow_pose(base, 10, flaming=True, flame_frame=flame_frame, moving=False))
    return frames


def make_bow_full_walk_frames() -> list[Image.Image]:
    poses = [
        dict(bob=0, tail_sway=-1, back_leg=(12, 0), front_leg=(18, 1), arm_swing=0),
        dict(bob=-1, tail_sway=0, back_leg=(13, 1), front_leg=(17, 0), arm_swing=0),
        dict(bob=0, tail_sway=1, back_leg=(14, 1), front_leg=(16, 0), arm_swing=0),
        dict(bob=-1, tail_sway=0, back_leg=(13, 0), front_leg=(17, 1), arm_swing=0),
    ]
    bow_bob_offsets = [0, -1, 1, 0]
    frames: list[Image.Image] = []
    for flame_frame, pose in enumerate(poses):
        base = draw_character_base(64, **pose)
        frames.append(
            add_bow_pose(
                base,
                10,
                flaming=True,
                flame_frame=flame_frame,
                moving=True,
                bow_bob=bow_bob_offsets[flame_frame % len(bow_bob_offsets)],
            )
        )
    return frames


def make_arrow_frames() -> list[Image.Image]:
    frames: list[Image.Image] = []
    for flick in (0, 1):
        image = canvas(32, 32)
        draw = ImageDraw.Draw(image)
        draw_arrow(draw, (9, 16 + flick), (22, 16), flaming=False)
        frames.append(image)
    return frames


def make_arrow_max_frames() -> list[Image.Image]:
    frames: list[Image.Image] = []
    for flame_frame in range(4):
        image = canvas(32, 32)
        draw = ImageDraw.Draw(image)
        draw_arrow(draw, (9, 16), (22, 16), flaming=True, flame_frame=flame_frame)
        frames.append(image)
    return frames


def write_frames(prefix: str, frames: Iterable[Image.Image]) -> None:
    for index, frame in enumerate(frames, start=1):
        save(frame, f"{prefix}{index}.png")


def main() -> None:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    clear_existing_outputs()

    write_frames("player_idle", make_idle_frames())
    write_frames("player_walk", make_walk_frames())
    write_frames("player_jump", make_jump_frames())
    write_frames("player_attack", make_attack_frames())
    write_frames("player_attack_down", make_down_attack_frames())
    write_frames("player_damaged", [tint_hurt(frame, index % 2 == 0) for index, frame in enumerate(make_idle_frames())])

    save(make_bow_charge_frame(2), "player_bow_charge1.png")
    save(make_bow_charge_frame(6), "player_bow_charge2.png")
    save(make_bow_charge_frame(10), "player_bow_charge3.png")
    write_frames("player_bow_charge1_walk", make_bow_walk_frames(2))
    write_frames("player_bow_charge2_walk", make_bow_walk_frames(6))
    write_frames("player_bow_charge3_walk", make_bow_walk_frames(10))
    write_frames("player_bow_full", make_bow_full_frames())
    write_frames("player_bow_full_walk", make_bow_full_walk_frames())

    normal_arrow_frames = make_arrow_frames()
    max_arrow_frames = make_arrow_max_frames()
    write_frames("player_arrow", normal_arrow_frames)
    write_frames("player_arrow_max", max_arrow_frames)
    save(normal_arrow_frames[0], "arrow.png")


if __name__ == "__main__":
    main()
