from __future__ import annotations

from pathlib import Path
from typing import Iterable

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ASSET_DIR = ROOT / "Assets" / "Enemy"

TRANSPARENT = (0, 0, 0, 0)
PALETTE = {
    "outline": (20, 18, 28, 255),
    "fur_dark": (29, 24, 39, 255),
    "fur_mid": (53, 46, 68, 255),
    "fur_light": (93, 84, 118, 255),
    "wing_membrane": (66, 56, 87, 255),
    "wing_glow": (115, 102, 150, 255),
    "eye": (233, 110, 116, 255),
    "fang": (232, 225, 236, 255),
    "telegraph": (175, 98, 110, 255),
}


def canvas() -> Image.Image:
    return Image.new("RGBA", (32, 32), TRANSPARENT)


def rect(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], color: tuple[int, int, int, int]) -> None:
    draw.rectangle(box, fill=color)


def ellipse(draw: ImageDraw.ImageDraw, box: tuple[int, int, int, int], color: tuple[int, int, int, int]) -> None:
    draw.ellipse(box, fill=color)


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
        "bat_walk*.png",
        "bat_swoop_dive_telegraph*.png",
        "bat_swoop_dive_attack*.png",
    ):
        for path in ASSET_DIR.glob(pattern):
            path.unlink(missing_ok=True)


def draw_body(draw: ImageDraw.ImageDraw, *, center_x: int, center_y: int, body_h: int, eyes_glow: bool = False) -> None:
    ellipse(draw, (center_x - 5, center_y - body_h // 2, center_x + 5, center_y + body_h // 2), PALETTE["fur_dark"])
    ellipse(draw, (center_x - 4, center_y - body_h // 2 + 2, center_x + 4, center_y + body_h // 2 - 1), PALETTE["fur_mid"])
    ellipse(draw, (center_x - 2, center_y - body_h // 2 + 3, center_x + 2, center_y - body_h // 2 + 6), PALETTE["fur_light"])

    rect(draw, (center_x - 4, center_y - body_h // 2 - 2, center_x - 2, center_y - body_h // 2 + 2), PALETTE["fur_dark"])
    rect(draw, (center_x + 2, center_y - body_h // 2 - 2, center_x + 4, center_y - body_h // 2 + 2), PALETTE["fur_dark"])

    eye_color = PALETTE["telegraph"] if eyes_glow else PALETTE["eye"]
    rect(draw, (center_x - 3, center_y - 2, center_x - 2, center_y - 1), eye_color)
    rect(draw, (center_x + 2, center_y - 2, center_x + 3, center_y - 1), eye_color)
    rect(draw, (center_x - 1, center_y + 4, center_x - 1, center_y + 5), PALETTE["fang"])
    rect(draw, (center_x + 1, center_y + 4, center_x + 1, center_y + 5), PALETTE["fang"])


def draw_wing(
    draw: ImageDraw.ImageDraw,
    *,
    shoulder: tuple[int, int],
    upper: tuple[int, int],
    tip: tuple[int, int],
    lower: tuple[int, int],
    highlight: bool = False,
) -> None:
    membrane = PALETTE["wing_glow"] if highlight else PALETTE["wing_membrane"]
    draw.polygon([shoulder, upper, tip, lower], fill=membrane)
    line(draw, [shoulder, upper, tip], PALETTE["fur_dark"], width=1)
    line(draw, [shoulder, lower, tip], PALETTE["fur_dark"], width=1)


def draw_bat_frame(
    *,
    body_y: int,
    body_h: int,
    left_wing: tuple[tuple[int, int], tuple[int, int], tuple[int, int], tuple[int, int]],
    right_wing: tuple[tuple[int, int], tuple[int, int], tuple[int, int], tuple[int, int]],
    eyes_glow: bool = False,
    accent_lines: bool = False,
) -> Image.Image:
    image = canvas()
    draw = ImageDraw.Draw(image)
    draw_wing(draw, shoulder=left_wing[0], upper=left_wing[1], tip=left_wing[2], lower=left_wing[3], highlight=eyes_glow)
    draw_wing(draw, shoulder=right_wing[0], upper=right_wing[1], tip=right_wing[2], lower=right_wing[3], highlight=eyes_glow)
    draw_body(draw, center_x=16, center_y=body_y, body_h=body_h, eyes_glow=eyes_glow)
    if accent_lines:
        line(draw, [(7, body_y - 1), (4, body_y + 1), (2, body_y + 5)], PALETTE["telegraph"], width=1)
        line(draw, [(25, body_y - 1), (28, body_y + 1), (30, body_y + 5)], PALETTE["telegraph"], width=1)
    return image


def make_walk_frames() -> list[Image.Image]:
    specs = [
        dict(
            body_y=14,
            body_h=12,
            left=((12, 14), (8, 9), (2, 4), (5, 16)),
            right=((20, 14), (24, 9), (30, 4), (27, 16)),
        ),
        dict(
            body_y=15,
            body_h=11,
            left=((12, 15), (8, 12), (2, 11), (6, 17)),
            right=((20, 15), (24, 12), (30, 11), (26, 17)),
        ),
        dict(
            body_y=14,
            body_h=12,
            left=((12, 14), (9, 7), (5, 2), (7, 16)),
            right=((20, 14), (23, 7), (27, 2), (25, 16)),
        ),
        dict(
            body_y=15,
            body_h=11,
            left=((12, 15), (8, 11), (2, 9), (6, 18)),
            right=((20, 15), (24, 11), (30, 9), (26, 18)),
        ),
    ]
    frames: list[Image.Image] = []
    for spec in specs:
        frames.append(
            draw_bat_frame(
                body_y=spec["body_y"],
                body_h=spec["body_h"],
                left_wing=spec["left"],
                right_wing=spec["right"],
            )
        )
    return frames


def make_telegraph_frames() -> list[Image.Image]:
    specs = [
        dict(
            body_y=15,
            body_h=13,
            left=((12, 15), (8, 12), (4, 10), (8, 18)),
            right=((20, 15), (24, 12), (28, 10), (24, 18)),
        ),
        dict(
            body_y=14,
            body_h=14,
            left=((12, 14), (7, 10), (3, 8), (8, 18)),
            right=((20, 14), (25, 10), (29, 8), (24, 18)),
        ),
        dict(
            body_y=13,
            body_h=15,
            left=((12, 13), (7, 8), (4, 6), (9, 18)),
            right=((20, 13), (25, 8), (28, 6), (23, 18)),
        ),
        dict(
            body_y=14,
            body_h=14,
            left=((12, 14), (8, 10), (5, 8), (9, 18)),
            right=((20, 14), (24, 10), (27, 8), (23, 18)),
        ),
    ]
    frames: list[Image.Image] = []
    for spec in specs:
        frames.append(
            draw_bat_frame(
                body_y=spec["body_y"],
                body_h=spec["body_h"],
                left_wing=spec["left"],
                right_wing=spec["right"],
                eyes_glow=True,
                accent_lines=True,
            )
        )
    return frames


def make_attack_frames() -> list[Image.Image]:
    specs = [
        dict(
            body_y=16,
            body_h=13,
            left=((12, 16), (8, 13), (4, 16), (7, 22)),
            right=((20, 16), (24, 13), (28, 16), (25, 22)),
        ),
        dict(
            body_y=18,
            body_h=14,
            left=((12, 18), (8, 15), (4, 19), (7, 25)),
            right=((20, 18), (24, 15), (28, 19), (25, 25)),
        ),
        dict(
            body_y=20,
            body_h=14,
            left=((12, 20), (8, 17), (5, 21), (7, 27)),
            right=((20, 20), (24, 17), (27, 21), (25, 27)),
        ),
        dict(
            body_y=17,
            body_h=13,
            left=((12, 17), (8, 14), (3, 18), (7, 23)),
            right=((20, 17), (24, 14), (29, 18), (25, 23)),
        ),
    ]
    frames: list[Image.Image] = []
    for spec in specs:
        frames.append(
            draw_bat_frame(
                body_y=spec["body_y"],
                body_h=spec["body_h"],
                left_wing=spec["left"],
                right_wing=spec["right"],
                eyes_glow=True,
            )
        )
    return frames


def write_frames(prefix: str, frames: Iterable[Image.Image]) -> None:
    for index, frame in enumerate(frames, start=1):
        save(frame, f"{prefix}{index}.png")


def main() -> None:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    clear_existing_outputs()
    write_frames("bat_walk", make_walk_frames())
    write_frames("bat_swoop_dive_telegraph", make_telegraph_frames())
    write_frames("bat_swoop_dive_attack", make_attack_frames())


if __name__ == "__main__":
    main()
