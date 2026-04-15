from __future__ import annotations

import math
from pathlib import Path
from typing import Iterable

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ASSET_DIR = ROOT / "Assets" / "Enemy"

TRANSPARENT = (0, 0, 0, 0)
PALETTE = {
    "outline": (20, 28, 40, 255),
    "fur_dark": (110, 134, 156, 255),
    "fur_mid": (190, 210, 230, 255),
    "fur_light": (244, 250, 255, 255),
    "fur_blue": (168, 201, 232, 255),
    "snout": (156, 182, 206, 255),
    "shadow": (64, 82, 102, 145),
    "eye": (110, 228, 255, 255),
    "snowball_dark": (150, 188, 218, 255),
    "snowball_mid": (218, 236, 250, 255),
    "snowball_light": (246, 252, 255, 255),
    "snowball_glow": (188, 232, 255, 170),
    "icicle_glow": (86, 186, 255, 200),
    "ice_dark": (46, 120, 220, 255),
    "ice_mid": (108, 196, 255, 255),
    "ice_light": (214, 242, 255, 255),
}

ENEMY_SIZE = (72, 96)
BODY_EXPORT_SIZE = (60, 96)
ICICLE_SIZE = (36, 96)


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
    points: Iterable[tuple[float, float]],
    color: tuple[int, int, int, int],
    *,
    width: int,
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


def normalize_to_canvas(image: Image.Image, target_size: tuple[int, int]) -> Image.Image:
    bbox = image.getbbox()
    if bbox is None:
        return canvas(target_size)

    cropped = image.crop(bbox)
    target_width = max(target_size[0], cropped.width)
    target_height = max(target_size[1], cropped.height)
    target = canvas((target_width, target_height))
    paste_x = max(0, (target_width - cropped.width) // 2)
    paste_y = max(0, target_height - cropped.height)
    target.alpha_composite(cropped, (paste_x, paste_y))
    return target


def clear_existing_outputs() -> None:
    for pattern in (
        "frostling_walk*.png",
        "frostling_ice_slam_telegraph*.png",
        "frostling_ice_slam_attack*.png",
        "frostling_ice_slam_cooldown*.png",
        "frostling_icicle_rise*.png",
        "frostling_icicle_sink*.png",
    ):
        for path in ASSET_DIR.glob(pattern):
            path.unlink(missing_ok=True)


def draw_shadow(draw: ImageDraw.ImageDraw, *, center_x: int, top: int, width: int, height: int) -> None:
    ellipse(draw, (center_x - width // 2, top, center_x + width // 2, top + height), PALETTE["shadow"])


def draw_jointed_limb(
    draw: ImageDraw.ImageDraw,
    points: list[tuple[float, float]],
    *,
    thickness: int,
    dark: tuple[int, int, int, int],
    mid: tuple[int, int, int, int],
    light: tuple[int, int, int, int],
) -> None:
    line(draw, points, dark, width=thickness + 3)
    line(draw, points, mid, width=thickness)
    line(draw, points, light, width=max(1, thickness - 3))
    for px, py in points[:-1]:
        ellipse(
            draw,
            (
                int(round(px - thickness / 2)),
                int(round(py - thickness / 2)),
                int(round(px + thickness / 2)),
                int(round(py + thickness / 2)),
            ),
            mid,
        )


def draw_foot(draw: ImageDraw.ImageDraw, *, x: int, y: int, length: int, lift: int = 0) -> None:
    ellipse(draw, (x - 2, y - 2 - lift, x + length, y + 4 - lift), PALETTE["fur_dark"])
    ellipse(draw, (x, y - 2 - lift, x + length - 1, y + 3 - lift), PALETTE["fur_mid"])
    rect(draw, (x + 5, y - 3 - lift, x + 8, y - 1 - lift), PALETTE["fur_light"])
    rect(draw, (x + 11, y - 3 - lift, x + 14, y - 1 - lift), PALETTE["fur_light"])
    rect(draw, (x + 17, y - 3 - lift, x + 20, y - 1 - lift), PALETTE["fur_light"])


def draw_fur_tuft(draw: ImageDraw.ImageDraw, *, x: int, y: int, size: int, facing: int = 1) -> None:
    polygon(
        draw,
        [
            (x, y),
            (x + (size * facing), y + max(2, size // 2)),
            (x + ((size // 2) * facing), y + size),
        ],
        PALETTE["fur_light"],
    )


def draw_side_giant_frame(
    *,
    body_shift: int,
    bob: int,
    front_stride: int,
    back_stride: int,
    front_arm: tuple[tuple[int, int], tuple[int, int], tuple[int, int]],
    back_arm: tuple[tuple[int, int], tuple[int, int], tuple[int, int]],
    crouch: int = 0,
    head_shake: int = 0,
    head_dy: int = 0,
    eye_glow: bool = False,
    impact: bool = False,
) -> Image.Image:
    image = canvas(ENEMY_SIZE)
    draw = ImageDraw.Draw(image)

    center_x = 34 + body_shift
    center_y = 92
    base_y = center_y - 1 + bob + crouch
    hip_y = 57 + bob + crouch
    torso_top = 21 + bob + crouch
    torso_bottom = 75 + bob + crouch

    draw_shadow(draw, center_x=center_x + 1, top=92 + min(2, crouch), width=34, height=3)

    back_leg = [
        (center_x - 2, hip_y - 1),
        (center_x - 6 + back_stride, 77 + bob),
        (center_x - 7 + back_stride, base_y),
    ]
    front_leg = [
        (center_x + 7, hip_y),
        (center_x + 11 + front_stride, 76 + bob),
        (center_x + 13 + front_stride, base_y - 1),
    ]
    draw_jointed_limb(draw, back_leg, thickness=6, dark=PALETTE["fur_dark"], mid=PALETTE["fur_blue"], light=PALETTE["fur_mid"])
    draw_jointed_limb(draw, front_leg, thickness=6, dark=PALETTE["fur_dark"], mid=PALETTE["fur_mid"], light=PALETTE["fur_light"])
    draw_foot(draw, x=center_x - 16 + back_stride, y=base_y + 1, length=16, lift=max(0, back_stride // 5))
    draw_foot(draw, x=center_x + 1 + front_stride, y=base_y, length=18, lift=max(0, -front_stride // 4))

    ellipse(draw, (center_x - 15, torso_top + 3, center_x + 5, torso_bottom - 1), PALETTE["fur_dark"])
    ellipse(draw, (center_x - 13, torso_top + 5, center_x + 4, torso_bottom - 2), PALETTE["fur_mid"])
    ellipse(draw, (center_x - 7, torso_top + 13, center_x + 5, torso_bottom - 5), PALETTE["fur_light"])
    ellipse(draw, (center_x - 1, torso_top + 24, center_x + 8, torso_bottom - 5), PALETTE["fur_blue"])
    polygon(draw, [(center_x - 8, torso_top + 12), (center_x - 3, torso_top + 5), (center_x + 2, torso_top + 12)], PALETTE["fur_dark"])
    draw_fur_tuft(draw, x=center_x - 13, y=torso_top + 18, size=8, facing=-1)
    draw_fur_tuft(draw, x=center_x - 6, y=torso_bottom - 6, size=8, facing=-1)
    draw_fur_tuft(draw, x=center_x + 5, y=torso_bottom - 8, size=7, facing=1)

    back_points = [(back_arm[0][0] + body_shift, back_arm[0][1] + bob), (back_arm[1][0] + body_shift, back_arm[1][1] + bob), (back_arm[2][0] + body_shift, back_arm[2][1] + bob)]
    front_points = [(front_arm[0][0] + body_shift, front_arm[0][1] + bob), (front_arm[1][0] + body_shift, front_arm[1][1] + bob), (front_arm[2][0] + body_shift, front_arm[2][1] + bob)]
    draw_jointed_limb(draw, back_points, thickness=5, dark=PALETTE["fur_dark"], mid=PALETTE["fur_blue"], light=PALETTE["fur_mid"])
    draw_jointed_limb(draw, front_points, thickness=5, dark=PALETTE["fur_dark"], mid=PALETTE["fur_mid"], light=PALETTE["fur_light"])
    ellipse(draw, (int(front_points[-1][0] - 4), int(front_points[-1][1] - 4), int(front_points[-1][0] + 4), int(front_points[-1][1] + 4)), PALETTE["fur_mid"])
    ellipse(draw, (int(back_points[-1][0] - 4), int(back_points[-1][1] - 4), int(back_points[-1][0] + 4), int(back_points[-1][1] + 4)), PALETTE["fur_blue"])

    head_left = center_x + 2 + head_shake
    head_top = 11 + bob + crouch + head_dy
    head_right = center_x + 18 + head_shake
    head_bottom = 30 + bob + crouch + head_dy
    ellipse(draw, (head_left, head_top, head_right, head_bottom), PALETTE["fur_dark"])
    ellipse(draw, (head_left + 2, head_top + 3, head_right - 1, head_bottom - 1), PALETTE["fur_mid"])
    ellipse(draw, (head_left + 4, head_top + 6, head_right - 3, head_bottom - 3), PALETTE["fur_light"])
    ellipse(draw, (head_right - 1, head_top + 10, head_right + 9, head_bottom - 3), PALETTE["snout"])
    rect(draw, (head_left + 5, head_top + 3, head_left + 8, head_top + 7), PALETTE["fur_dark"])
    polygon(draw, [(head_left + 2, head_top + 4), (head_left - 4, head_top - 4), (head_left + 5, head_top)], PALETTE["fur_dark"])
    draw_fur_tuft(draw, x=head_left + 4, y=head_top - 2, size=7, facing=-1)
    draw_fur_tuft(draw, x=head_left + 10, y=head_top - 4, size=8, facing=1)
    draw_fur_tuft(draw, x=head_right + 1, y=head_top + 10, size=5, facing=1)

    eye_color = PALETTE["eye"] if eye_glow else PALETTE["outline"]
    rect(draw, (head_left + 9, head_top + 10, head_left + 12, head_top + 12), eye_color)
    rect(draw, (head_right + 3, head_top + 17, head_right + 4, head_top + 18), PALETTE["outline"])

    if impact:
        polygon(draw, [(center_x + 9, 84), (center_x + 16, 73), (center_x + 22, 84)], PALETTE["snowball_glow"])
        polygon(draw, [(center_x - 11, 84), (center_x - 4, 75), (center_x + 2, 84)], PALETTE["snowball_glow"])

    return image


def draw_snowball_frame(*, bob: int, spin: int, sparkle: int) -> Image.Image:
    image = canvas(ENEMY_SIZE)
    draw = ImageDraw.Draw(image)
    center_x = 38
    center_y = 26 + bob
    radius = 20

    ellipse(draw, (center_x - radius - 3, center_y - radius - 1, center_x + radius + 3, center_y + radius + 3), PALETTE["snowball_glow"])
    ellipse(draw, (center_x - radius, center_y - radius, center_x + radius, center_y + radius), PALETTE["snowball_dark"])
    ellipse(draw, (center_x - radius + 3, center_y - radius + 3, center_x + radius - 3, center_y + radius - 2), PALETTE["snowball_mid"])
    ellipse(draw, (center_x - 9, center_y - 12, center_x + 8, center_y + 1), PALETTE["snowball_light"])
    ellipse(draw, (center_x + 6, center_y - 2, center_x + 12, center_y + 5), PALETTE["snowball_light"])

    line(draw, [(center_x - 12, center_y - 5), (center_x - 3, center_y + spin), (center_x + 8, center_y + 8)], PALETTE["ice_dark"], width=2)
    line(draw, [(center_x - 4, center_y - 14), (center_x + 5 + spin, center_y - 5), (center_x + 13, center_y + 5)], PALETTE["ice_mid"], width=2)

    sparkle_points = [
        (center_x - 24 + sparkle, center_y - 10),
        (center_x + 24 - sparkle, center_y - 2),
        (center_x - 17, center_y + 14),
        (center_x + 17, center_y + 11),
    ]
    for sx, sy in sparkle_points:
        rect(draw, (sx, sy - 1, sx + 1, sy + 1), PALETTE["ice_light"])
        rect(draw, (sx - 1, sy, sx + 2, sy), PALETTE["ice_light"])

    return image


def make_walk_frames() -> list[Image.Image]:
    frames: list[Image.Image] = []
    for index in range(16):
        phase = (index / 16.0) * math.tau
        front = int(round(math.sin(phase) * 10))
        back = int(round(math.sin(phase + math.pi * 0.92) * 6))
        bob = int(round(math.cos(phase * 2.0) * 1.4))
        arm_swing = int(round(math.sin(phase + math.pi * 0.2) * 5))
        front_arm = ((39, 42), (45 + arm_swing // 2, 58), (49 + arm_swing, 78))
        back_arm = ((30, 41), (24 - arm_swing // 3, 57), (21 - arm_swing // 2, 75))
        frames.append(
            draw_side_giant_frame(
                body_shift=0,
                bob=bob,
                front_stride=front,
                back_stride=back,
                front_arm=front_arm,
                back_arm=back_arm,
            )
        )
    return frames


def make_telegraph_frames() -> list[Image.Image]:
    specs = [
        dict(crouch=0, head_dy=0, front=((41, 43), (47, 29), (43, 12)), back=((29, 42), (25, 30), (27, 16))),
        dict(crouch=-2, head_dy=-1, front=((41, 41), (49, 23), (44, 5)), back=((29, 40), (24, 26), (26, 9))),
        dict(crouch=-4, head_dy=-2, front=((41, 39), (50, 18), (45, 0)), back=((29, 38), (23, 20), (25, 3))),
        dict(crouch=-5, head_dy=-3, front=((41, 38), (50, 14), (46, -3)), back=((29, 37), (23, 16), (26, 0))),
        dict(crouch=-4, head_dy=-2, front=((41, 39), (49, 17), (45, -1)), back=((29, 38), (23, 18), (26, 2))),
        dict(crouch=-3, head_dy=-2, front=((41, 40), (48, 20), (44, 2)), back=((29, 39), (24, 22), (26, 5))),
    ]
    return [
        draw_side_giant_frame(
            body_shift=0,
            bob=-1,
            front_stride=0,
            back_stride=0,
            front_arm=spec["front"],
            back_arm=spec["back"],
            crouch=spec["crouch"],
            head_dy=spec["head_dy"],
            eye_glow=True,
        )
        for spec in specs
    ]


def make_attack_frames() -> list[Image.Image]:
    specs = [
        dict(crouch=-3, front=((41, 38), (49, 17), (45, 1)), back=((29, 37), (23, 18), (26, 4)), impact=False),
        dict(crouch=0, front=((41, 45), (48, 55), (50, 71)), back=((29, 44), (23, 55), (22, 71)), impact=False),
        dict(crouch=6, front=((41, 48), (47, 68), (50, 89)), back=((29, 47), (23, 66), (22, 88)), impact=True),
        dict(crouch=7, front=((41, 48), (46, 70), (49, 90)), back=((29, 47), (23, 68), (22, 89)), impact=True),
        dict(crouch=4, front=((41, 46), (47, 61), (49, 79)), back=((29, 45), (23, 60), (22, 78)), impact=False),
        dict(crouch=2, front=((41, 44), (47, 56), (49, 70)), back=((29, 43), (23, 55), (22, 68)), impact=False),
    ]
    return [
        draw_side_giant_frame(
            body_shift=0,
            bob=0,
            front_stride=0,
            back_stride=0,
            front_arm=spec["front"],
            back_arm=spec["back"],
            crouch=spec["crouch"],
            head_dy=1,
            impact=spec["impact"],
        )
        for spec in specs
    ]


def make_cooldown_frames() -> list[Image.Image]:
    frames: list[Image.Image] = []

    transform_in = [
        draw_side_giant_frame(body_shift=0, bob=0, front_stride=0, back_stride=0, front_arm=((41, 44), (47, 55), (49, 71)), back_arm=((29, 43), (23, 55), (22, 70)), crouch=1, head_dy=0),
        draw_side_giant_frame(body_shift=0, bob=1, front_stride=0, back_stride=0, front_arm=((40, 45), (45, 60), (42, 74)), back_arm=((29, 44), (24, 58), (23, 72)), crouch=4, head_dy=4),
        draw_side_giant_frame(body_shift=0, bob=1, front_stride=0, back_stride=0, front_arm=((39, 40), (42, 47), (48, 56)), back_arm=((31, 42), (28, 48), (24, 57)), crouch=8, head_dy=10),
        draw_snowball_frame(bob=0, spin=-2, sparkle=0),
    ]
    hover = [
        draw_snowball_frame(bob=0, spin=-2, sparkle=0),
        draw_snowball_frame(bob=-1, spin=1, sparkle=1),
        draw_snowball_frame(bob=0, spin=2, sparkle=2),
        draw_snowball_frame(bob=1, spin=-1, sparkle=1),
    ]
    transform_out = [
        draw_snowball_frame(bob=0, spin=-2, sparkle=0),
        draw_side_giant_frame(body_shift=0, bob=1, front_stride=0, back_stride=0, front_arm=((39, 40), (42, 47), (48, 56)), back_arm=((31, 42), (28, 48), (24, 57)), crouch=8, head_dy=10),
        draw_side_giant_frame(body_shift=0, bob=1, front_stride=0, back_stride=0, front_arm=((40, 45), (45, 60), (42, 74)), back_arm=((29, 44), (24, 58), (23, 72)), crouch=4, head_dy=4),
        draw_side_giant_frame(body_shift=0, bob=0, front_stride=0, back_stride=0, front_arm=((41, 44), (47, 55), (49, 71)), back_arm=((29, 43), (23, 55), (22, 70)), crouch=1, head_dy=0),
    ]

    frames.extend(transform_in)
    frames.extend(hover)
    frames.extend(transform_out)
    return frames


def draw_icicle_frame(height: int, sink: int = 0) -> Image.Image:
    image = canvas(ICICLE_SIZE)
    draw = ImageDraw.Draw(image)
    width, canvas_height = ICICLE_SIZE
    center_x = width // 2
    base_y = canvas_height - 2 + sink
    tip_y = max(2, base_y - height)
    half_width = max(5, 4 + height // 7)

    ellipse(draw, (center_x - 11, canvas_height - 7, center_x + 11, canvas_height - 1), PALETTE["icicle_glow"])
    polygon(draw, [(center_x, tip_y), (center_x - half_width, base_y), (center_x + half_width, base_y)], PALETTE["ice_dark"])
    polygon(draw, [(center_x, tip_y + 1), (center_x - half_width + 2, base_y - 1), (center_x + half_width - 2, base_y - 1)], PALETTE["ice_mid"])
    polygon(draw, [(center_x, tip_y + 3), (center_x - 3, base_y - 6), (center_x + 3, base_y - 6)], PALETTE["ice_light"])
    line(draw, [(center_x, tip_y + 2), (center_x, base_y - 4)], PALETTE["ice_light"], width=1)
    return image


def make_icicle_rise_frames() -> list[Image.Image]:
    heights = [24, 44, 68, 90]
    return [draw_icicle_frame(height=h) for h in heights]


def make_icicle_sink_frames() -> list[Image.Image]:
    specs = [(76, 4), (54, 10), (32, 16), (14, 20)]
    return [draw_icicle_frame(height=h, sink=sink) for h, sink in specs]


def main() -> None:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    clear_existing_outputs()

    for index, frame in enumerate(make_walk_frames(), start=1):
        save(normalize_to_canvas(frame, BODY_EXPORT_SIZE), f"frostling_walk{index}.png")

    for index, frame in enumerate(make_telegraph_frames(), start=1):
        save(normalize_to_canvas(frame, BODY_EXPORT_SIZE), f"frostling_ice_slam_telegraph{index}.png")

    for index, frame in enumerate(make_attack_frames(), start=1):
        save(normalize_to_canvas(frame, BODY_EXPORT_SIZE), f"frostling_ice_slam_attack{index}.png")

    for index, frame in enumerate(make_cooldown_frames(), start=1):
        save(normalize_to_canvas(frame, BODY_EXPORT_SIZE), f"frostling_ice_slam_cooldown{index}.png")

    for index, frame in enumerate(make_icicle_rise_frames(), start=1):
        save(frame, f"frostling_icicle_rise{index}.png")

    for index, frame in enumerate(make_icicle_sink_frames(), start=1):
        save(frame, f"frostling_icicle_sink{index}.png")


if __name__ == "__main__":
    main()
