from __future__ import annotations

import math
from pathlib import Path
from typing import Iterable

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ASSET_DIR = ROOT / "Assets" / "Enemy"

TRANSPARENT = (0, 0, 0, 0)
PALETTE = {
    "outline": (22, 16, 18, 255),
    "skin_dark": (87, 56, 54, 255),
    "skin_mid": (150, 108, 94, 255),
    "skin_light": (208, 171, 142, 255),
    "bruise": (101, 74, 118, 255),
    "nail": (224, 214, 189, 255),
    "shadow": (60, 38, 34, 255),
    "dirt_dark": (74, 58, 42, 255),
    "dirt_mid": (108, 84, 57, 255),
    "dirt_light": (145, 116, 78, 255),
    "dust": (182, 150, 109, 200),
}

CANVAS_SIZE = 48


def canvas() -> Image.Image:
    return Image.new("RGBA", (CANVAS_SIZE, CANVAS_SIZE), TRANSPARENT)


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


def point_from(origin: tuple[float, float], angle_deg: float, length: float) -> tuple[float, float]:
    radians = math.radians(angle_deg)
    return (origin[0] + (math.cos(radians) * length), origin[1] + (math.sin(radians) * length))


def draw_shadow(draw: ImageDraw.ImageDraw, *, left: int, top: int, right: int, bottom: int) -> None:
    ellipse(draw, (left, top, right, bottom), PALETTE["shadow"])


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
        "crawler_walk*.png",
        "crawler_burrow_ambush_telegraph*.png",
        "crawler_burrow_ambush_underground*.png",
        "crawler_burrow_ambush_attack*.png",
        "crawler_burrow_ambush_float*.png",
    ):
        for path in ASSET_DIR.glob(pattern):
            path.unlink(missing_ok=True)


def draw_side_digit(
    draw: ImageDraw.ImageDraw,
    *,
    origin: tuple[float, float],
    upper_angle: float,
    lower_angle: float,
    upper_len: float,
    lower_len: float,
    thickness: int = 4,
    nail_size: int = 2,
) -> None:
    joint = point_from(origin, upper_angle, upper_len)
    tip = point_from(joint, lower_angle, lower_len)
    line(draw, [origin, joint, tip], PALETTE["skin_dark"], width=thickness + 2)
    line(draw, [origin, joint, tip], PALETTE["skin_mid"], width=thickness)
    line(draw, [origin, ((origin[0] + joint[0]) / 2.0, (origin[1] + joint[1]) / 2.0)], PALETTE["skin_light"], width=max(1, thickness - 2))
    for px, py, size in (
        (origin[0], origin[1], thickness + 1),
        (joint[0], joint[1], thickness),
    ):
        ellipse(
            draw,
            (int(round(px - (size / 2))), int(round(py - (size / 2))), int(round(px + (size / 2))), int(round(py + (size / 2)))),
            PALETTE["skin_mid"],
        )
    nail_x = int(round(tip[0]))
    nail_y = int(round(tip[1]))
    polygon(
        draw,
        [
            (nail_x - nail_size, nail_y - 1),
            (nail_x + nail_size, nail_y),
            (nail_x - nail_size, nail_y + 1),
        ],
        PALETTE["nail"],
    )


def draw_side_hand(
    draw: ImageDraw.ImageDraw,
    *,
    palm_x: int,
    palm_y: int,
    stride: float,
    sink: int = 0,
    curl: float = 0.0,
    dust: int = 0,
) -> None:
    palm_top = palm_y + sink
    draw_shadow(draw, left=palm_x - 6, top=36 + min(6, sink // 2), right=palm_x + 18, bottom=41 + min(6, sink // 2))

    ellipse(draw, (palm_x - 4, palm_top - 2, palm_x + 15, palm_top + 11), PALETTE["skin_dark"])
    ellipse(draw, (palm_x - 2, palm_top - 1, palm_x + 13, palm_top + 10), PALETTE["skin_mid"])
    ellipse(draw, (palm_x + 2, palm_top + 1, palm_x + 10, palm_top + 5), PALETTE["skin_light"])
    ellipse(draw, (palm_x + 1, palm_top + 5, palm_x + 6, palm_top + 8), PALETTE["bruise"])
    rect(draw, (palm_x - 9, palm_top + 2, palm_x - 1, palm_top + 8), PALETTE["skin_dark"])
    rect(draw, (palm_x - 7, palm_top + 3, palm_x, palm_top + 7), PALETTE["skin_mid"])

    finger_origins = [
        (palm_x + 10, palm_top + 9),
        (palm_x + 6, palm_top + 10),
        (palm_x + 2, palm_top + 10),
        (palm_x - 2, palm_top + 9),
    ]
    base_angles = [58, 68, 79, 90]
    lower_angles = [92, 102, 112, 123]
    upper_lengths = [10, 10, 9, 8]
    lower_lengths = [9, 9, 8, 8]
    gait = [1.0, -0.9, 0.8, -0.7]

    for index, origin in enumerate(finger_origins):
        stride_bias = stride * gait[index]
        bend = curl * (14 + (index * 1.5))
        draw_side_digit(
            draw,
            origin=(origin[0], origin[1] + (curl * 3)),
            upper_angle=base_angles[index] - (stride_bias * 7) + bend,
            lower_angle=lower_angles[index] + (stride_bias * 10) + bend,
            upper_len=upper_lengths[index] - (curl * 1.5),
            lower_len=lower_lengths[index] - (curl * 2.0),
        )

    thumb_origin = (palm_x + 12, palm_top + 3)
    draw_side_digit(
        draw,
        origin=thumb_origin,
        upper_angle=18 - (stride * 7) + (curl * 10),
        lower_angle=58 + (curl * 10),
        upper_len=7,
        lower_len=6,
        thickness=3,
        nail_size=1,
    )

    if dust > 0:
        for offset in range(dust):
            ellipse(draw, (palm_x + 10 + (offset * 3), 35 - offset, palm_x + 14 + (offset * 3), 39 - offset), PALETTE["dust"])
            ellipse(draw, (palm_x - 2 - (offset * 2), 36 - (offset // 2), palm_x + 2 - (offset * 2), 39 - (offset // 2)), PALETTE["dirt_light"])


def draw_dirt_mound(
    draw: ImageDraw.ImageDraw,
    *,
    center_x: int,
    width: int,
    height: int,
    skew: int = 0,
    spray: bool = False,
) -> None:
    left = center_x - (width // 2)
    right = center_x + (width // 2)
    top = 40 - height
    ellipse(draw, (left + skew, top, right + skew, 42), PALETTE["dirt_dark"])
    ellipse(draw, (left + 3 + skew, top + 2, right - 2 + skew, 41), PALETTE["dirt_mid"])
    ellipse(draw, (left + 8 + skew, top + 4, left + 17 + skew, top + 9), PALETTE["dirt_light"])
    ellipse(draw, (right - 18 + skew, top + 5, right - 9 + skew, top + 10), PALETTE["dirt_light"])
    ellipse(draw, (left - 3 + skew, 39, right + 4 + skew, 43), PALETTE["shadow"])
    if spray:
        for index, dx in enumerate((-12, -7, -1, 5, 11)):
            size = 3 + (index % 2)
            ellipse(draw, (center_x + dx, top - 4 - (index % 3), center_x + dx + size, top - 1 - (index % 3)), PALETTE["dirt_light"])


def draw_open_digit(
    draw: ImageDraw.ImageDraw,
    *,
    origin: tuple[float, float],
    angle: float,
    spread: float,
    lift: int,
    thickness: int = 4,
    thumb: bool = False,
) -> None:
    upper_len = 9 if thumb else 11
    lower_len = 8 if thumb else 10
    joint = point_from(origin, angle, upper_len)
    tip = point_from(joint, angle - spread, lower_len)
    line(draw, [origin, joint, tip], PALETTE["skin_dark"], width=thickness + 2)
    line(draw, [origin, joint, tip], PALETTE["skin_mid"], width=thickness)
    line(draw, [origin, ((origin[0] + joint[0]) / 2.0, (origin[1] + joint[1]) / 2.0)], PALETTE["skin_light"], width=max(1, thickness - 2))
    ellipse(draw, (int(origin[0] - 2), int(origin[1] - 2), int(origin[0] + 3), int(origin[1] + 3)), PALETTE["skin_mid"])
    tip_x = int(round(tip[0]))
    tip_y = int(round(tip[1]))
    polygon(
        draw,
        [
            (tip_x - 1, tip_y - 2),
            (tip_x + 2, tip_y - 1),
            (tip_x, tip_y + 1),
        ],
        PALETTE["nail"],
    )
    if lift > 0:
        ellipse(draw, (tip_x - 2, tip_y + 2, tip_x + 1, tip_y + 5), PALETTE["dust"])


def draw_open_hand(
    draw: ImageDraw.ImageDraw,
    *,
    center_x: int,
    center_y: int,
    spread: float,
    rise: int = 0,
    dirt_burst: int = 0,
    shadow_strength: int = 10,
) -> None:
    shadow_left = center_x - shadow_strength
    shadow_right = center_x + shadow_strength + 2
    draw_shadow(draw, left=shadow_left, top=38 - min(rise, 6), right=shadow_right, bottom=42 - min(rise, 6))

    palm_top = center_y - 6 - rise
    palm_bottom = center_y + 7 - rise
    ellipse(draw, (center_x - 8, palm_top, center_x + 8, palm_bottom), PALETTE["skin_dark"])
    ellipse(draw, (center_x - 6, palm_top + 1, center_x + 7, palm_bottom - 1), PALETTE["skin_mid"])
    ellipse(draw, (center_x - 2, palm_top + 2, center_x + 3, palm_top + 6), PALETTE["skin_light"])
    ellipse(draw, (center_x - 5, palm_top + 5, center_x - 1, palm_top + 8), PALETTE["bruise"])
    rect(draw, (center_x - 4, palm_bottom - 1, center_x + 4, palm_bottom + 5), PALETTE["skin_dark"])
    rect(draw, (center_x - 3, palm_bottom, center_x + 3, palm_bottom + 4), PALETTE["skin_mid"])

    origins = [
        (center_x - 7, center_y - 1 - rise),
        (center_x - 3, center_y - 5 - rise),
        (center_x + 1, center_y - 6 - rise),
        (center_x + 5, center_y - 4 - rise),
    ]
    angles = [158, 132, 104, 75]
    spreads = [20, 16, 12, 10]

    for index, origin in enumerate(origins):
        draw_open_digit(
            draw,
            origin=origin,
            angle=angles[index] - (spread * 0.2 * (2 - index)),
            spread=spreads[index] + spread,
            lift=dirt_burst,
        )

    draw_open_digit(
        draw,
        origin=(center_x + 7, center_y + 1 - rise),
        angle=28 + (spread * 0.4),
        spread=18 + (spread * 0.6),
        lift=dirt_burst,
        thickness=3,
        thumb=True,
    )

    if dirt_burst > 0:
        for index, dx in enumerate((-13, -8, -3, 4, 10, 15)):
            size = 3 + (index % 3)
            ellipse(
                draw,
                (center_x + dx, 37 - rise - (index % 2), center_x + dx + size, 41 - rise - (index % 2)),
                PALETTE["dirt_light"] if index % 2 == 0 else PALETTE["dust"],
            )


def make_walk_frames() -> list[Image.Image]:
    specs = [
        dict(palm_x=18, palm_y=18, stride=-1.0),
        dict(palm_x=19, palm_y=19, stride=-0.35),
        dict(palm_x=20, palm_y=18, stride=0.55),
        dict(palm_x=19, palm_y=19, stride=1.0),
    ]
    frames: list[Image.Image] = []
    for spec in specs:
        image = canvas()
        draw = ImageDraw.Draw(image)
        draw_side_hand(draw, palm_x=spec["palm_x"], palm_y=spec["palm_y"], stride=spec["stride"])
        frames.append(image)
    return frames


def make_burrow_frames() -> list[Image.Image]:
    specs = [
        dict(palm_x=19, palm_y=18, stride=-0.2, sink=0, curl=0.05, dust=0),
        dict(palm_x=19, palm_y=19, stride=0.1, sink=1, curl=0.12, dust=1),
        dict(palm_x=19, palm_y=20, stride=0.2, sink=2, curl=0.2, dust=1),
        dict(palm_x=20, palm_y=20, stride=0.0, sink=3, curl=0.3, dust=2),
        dict(palm_x=20, palm_y=21, stride=-0.1, sink=5, curl=0.45, dust=2),
        dict(palm_x=20, palm_y=22, stride=0.0, sink=7, curl=0.6, dust=3),
        dict(palm_x=21, palm_y=22, stride=0.0, sink=9, curl=0.78, dust=3),
        dict(palm_x=21, palm_y=23, stride=0.0, sink=11, curl=0.95, dust=4),
    ]
    frames: list[Image.Image] = []
    for spec in specs:
        image = canvas()
        draw = ImageDraw.Draw(image)
        draw_dirt_mound(draw, center_x=25, width=26 + spec["dust"], height=6 + spec["dust"], skew=0, spray=spec["dust"] >= 2)
        draw_side_hand(
            draw,
            palm_x=spec["palm_x"],
            palm_y=spec["palm_y"],
            stride=spec["stride"],
            sink=spec["sink"],
            curl=spec["curl"],
            dust=spec["dust"],
        )
        frames.append(image)
    return frames


def make_underground_frames() -> list[Image.Image]:
    specs = [
        dict(center_x=19, width=24, height=7, skew=-3, spray=False),
        dict(center_x=23, width=26, height=8, skew=-1, spray=True),
        dict(center_x=27, width=24, height=7, skew=2, spray=False),
        dict(center_x=24, width=27, height=8, skew=0, spray=True),
    ]
    frames: list[Image.Image] = []
    for spec in specs:
        image = canvas()
        draw = ImageDraw.Draw(image)
        draw_dirt_mound(
            draw,
            center_x=spec["center_x"],
            width=spec["width"],
            height=spec["height"],
            skew=spec["skew"],
            spray=spec["spray"],
        )
        ellipse(draw, (spec["center_x"] - 4, 34, spec["center_x"], 36), PALETTE["dust"])
        ellipse(draw, (spec["center_x"] + 4, 35, spec["center_x"] + 9, 37), PALETTE["dust"])
        frames.append(image)
    return frames


def make_attack_frames() -> list[Image.Image]:
    specs = [
        dict(center_x=24, center_y=30, spread=12, rise=0, dirt=4, shadow=12),
        dict(center_x=24, center_y=28, spread=16, rise=2, dirt=5, shadow=11),
        dict(center_x=24, center_y=26, spread=20, rise=4, dirt=5, shadow=10),
        dict(center_x=24, center_y=24, spread=24, rise=6, dirt=6, shadow=9),
        dict(center_x=24, center_y=23, spread=27, rise=7, dirt=5, shadow=8),
        dict(center_x=24, center_y=22, spread=29, rise=8, dirt=4, shadow=7),
        dict(center_x=24, center_y=22, spread=30, rise=8, dirt=3, shadow=6),
        dict(center_x=24, center_y=23, spread=31, rise=7, dirt=2, shadow=5),
    ]
    frames: list[Image.Image] = []
    for spec in specs:
        image = canvas()
        draw = ImageDraw.Draw(image)
        draw_open_hand(
            draw,
            center_x=spec["center_x"],
            center_y=spec["center_y"],
            spread=spec["spread"],
            rise=spec["rise"],
            dirt_burst=spec["dirt"],
            shadow_strength=spec["shadow"],
        )
        if spec["dirt"] >= 4:
            draw_dirt_mound(draw, center_x=24, width=22, height=6, skew=0, spray=True)
        frames.append(image)
    return frames


def make_float_frames() -> list[Image.Image]:
    specs = [
        dict(center_x=24, center_y=21, spread=23, rise=8, dirt=0, shadow=5),
        dict(center_x=24, center_y=22, spread=19, rise=7, dirt=0, shadow=5),
    ]
    frames: list[Image.Image] = []
    for spec in specs:
        image = canvas()
        draw = ImageDraw.Draw(image)
        draw_open_hand(
            draw,
            center_x=spec["center_x"],
            center_y=spec["center_y"],
            spread=spec["spread"],
            rise=spec["rise"],
            dirt_burst=spec["dirt"],
            shadow_strength=spec["shadow"],
        )
        ellipse(draw, (18, 39, 22, 41), PALETTE["dust"])
        ellipse(draw, (27, 38, 31, 40), PALETTE["dust"])
        frames.append(image)
    return frames


def write_frames(prefix: str, frames: Iterable[Image.Image]) -> None:
    for index, frame in enumerate(frames, start=1):
        save(frame, f"{prefix}{index}.png")


def main() -> None:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    clear_existing_outputs()
    write_frames("crawler_walk", make_walk_frames())
    write_frames("crawler_burrow_ambush_telegraph", make_burrow_frames())
    write_frames("crawler_burrow_ambush_underground", make_underground_frames())
    write_frames("crawler_burrow_ambush_attack", make_attack_frames())
    write_frames("crawler_burrow_ambush_float", make_float_frames())


if __name__ == "__main__":
    main()
