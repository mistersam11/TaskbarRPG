from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ASSET_DIR = ROOT / "Assets" / "Boss"

TRANSPARENT = (0, 0, 0, 0)
PALETTE = {
    "outline": (18, 22, 30, 255),
    "metal_dark": (34, 40, 54, 255),
    "metal_mid": (78, 90, 110, 255),
    "metal_light": (168, 182, 205, 255),
    "metal_bronze": (154, 122, 84, 255),
    "metal_warning": (224, 176, 88, 255),
    "charge_dark": (54, 82, 170, 255),
    "charge_mid": (86, 200, 255, 255),
    "charge_light": (188, 246, 255, 255),
    "charge_core": (255, 250, 204, 255),
    "plasma": (178, 132, 255, 255),
    "plasma_light": (236, 216, 255, 255),
    "smoke": (110, 126, 148, 138),
    "shadow": (26, 20, 34, 112),
}

ROBOT_SIZE = (208, 176)
HELICOPTER_SIZE = (172, 70)
PACKAGE_SIZE = (36, 28)
VENT_SIZE = (40, 84)
STOMP_LEG_SIZE = (56, 128)
STOMP_SPLASH_SIZE = (236, 38)
MINE_SIZE = (30, 30)
BALL_SIZE = (20, 20)


def canvas(size: tuple[int, int]) -> Image.Image:
    return Image.new("RGBA", size, TRANSPARENT)


def rect(draw: ImageDraw.ImageDraw, box: tuple[float, float, float, float], color: tuple[int, int, int, int]) -> None:
    draw.rectangle(box, fill=color)


def ellipse(draw: ImageDraw.ImageDraw, box: tuple[float, float, float, float], color: tuple[int, int, int, int]) -> None:
    draw.ellipse(box, fill=color)


def ellipse_outline(
    draw: ImageDraw.ImageDraw,
    box: tuple[float, float, float, float],
    color: tuple[int, int, int, int],
    *,
    width: int = 1,
) -> None:
    draw.ellipse(box, outline=color, width=width)


def polygon(draw: ImageDraw.ImageDraw, points: list[tuple[float, float]], color: tuple[int, int, int, int]) -> None:
    draw.polygon(points, fill=color)


def line(
    draw: ImageDraw.ImageDraw,
    points: list[tuple[float, float]],
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


def clamp01(value: float) -> float:
    return max(0.0, min(1.0, value))


def lerp(a: float, b: float, t: float) -> float:
    return a + ((b - a) * t)


def pulse(progress: float, cycles: float = 1.0) -> float:
    return 0.5 + (math.sin(progress * math.pi * cycles) * 0.5)


def draw_orb(draw: ImageDraw.ImageDraw, center_x: float, center_y: float, radius: float, glow: float = 0.0) -> None:
    ellipse(draw, (center_x - radius, center_y - radius, center_x + radius, center_y + radius), PALETTE["charge_dark"])
    ellipse(draw, (center_x - radius + 2, center_y - radius + 2, center_x + radius - 2, center_y + radius - 2), PALETTE["charge_mid"])
    if radius >= 6:
        ellipse(draw, (center_x - radius + 4, center_y - radius + 4, center_x + radius - 4, center_y + radius - 4), PALETTE["charge_light"])
    ellipse(draw, (center_x - 2, center_y - 2, center_x + 2, center_y + 2), PALETTE["charge_core"])
    if glow > 0:
        ellipse_outline(
            draw,
            (center_x - radius - glow, center_y - radius - glow, center_x + radius + glow, center_y + radius + glow),
            PALETTE["plasma_light"],
            width=2,
        )


def draw_package(draw: ImageDraw.ImageDraw, left: float, top: float, width: float, height: float, glow: float) -> None:
    rect(draw, (left, top, left + width, top + height), PALETTE["metal_dark"])
    rect(draw, (left + 2, top + 2, left + width - 2, top + height - 2), PALETTE["metal_mid"])
    rect(draw, (left + 6, top + 4, left + width - 6, top + 9), PALETTE["metal_bronze"])
    rect(draw, (left + width * 0.48, top + 2, left + width * 0.52, top + height - 2), PALETTE["metal_light"])
    orb_radius = 3 + glow
    draw_orb(draw, left + (width * 0.28), top + (height * 0.42), orb_radius, glow=1 if glow > 0.3 else 0)


def draw_segmented_limb(
    draw: ImageDraw.ImageDraw,
    points: list[tuple[float, float]],
    *,
    dark_width: int,
    light_width: int,
) -> None:
    line(draw, points, PALETTE["metal_dark"], width=dark_width)
    line(draw, points, PALETTE["metal_mid"], width=light_width)


def draw_robot(
    draw: ImageDraw.ImageDraw,
    *,
    body_bob: float,
    front_step: float,
    back_step: float,
    front_leg_lift: float,
    arm_raise: float,
    cannon_raise: float,
    flex_amount: float,
    core_glow: float,
    torso_lean: float,
    shockwave: float = 0.0,
    charge_bars: float = 0.0,
) -> None:
    floor_y = 162
    torso_x = 76 + torso_lean
    torso_top = 34 + body_bob
    torso_bottom = 112 + body_bob
    shoulder_y = torso_top + 18
    hip_y = torso_bottom - 2
    rear_hip_x = torso_x + 26
    front_hip_x = torso_x + 74

    rear_foot_x = torso_x + 18 + back_step
    rear_foot_y = floor_y
    rear_knee_x = rear_hip_x - 8 + (back_step * 0.4)
    rear_knee_y = torso_bottom + 26

    front_foot_x = torso_x + 86 + front_step
    front_foot_y = floor_y - front_leg_lift
    front_knee_x = front_hip_x + 10 + (front_step * 0.35)
    front_knee_y = torso_bottom + 22 - (front_leg_lift * 0.3)
    front_ankle_x = front_foot_x - 6
    front_ankle_y = floor_y - max(0, front_leg_lift * 0.18)

    ellipse(draw, (rear_foot_x - 18, floor_y - 5, rear_foot_x + 18, floor_y + 3), PALETTE["shadow"])
    ellipse(draw, (front_foot_x - 22, floor_y - 5, front_foot_x + 22, floor_y + 3), PALETTE["shadow"])

    draw_segmented_limb(
        draw,
        [(rear_hip_x, hip_y), (rear_knee_x, rear_knee_y), (rear_foot_x, rear_foot_y - 4)],
        dark_width=18,
        light_width=11,
    )
    rect(draw, (rear_foot_x - 18, rear_foot_y - 8, rear_foot_x + 5, rear_foot_y), PALETTE["metal_bronze"])
    rect(draw, (rear_foot_x - 14, rear_foot_y - 6, rear_foot_x + 1, rear_foot_y - 1), PALETTE["metal_light"])

    draw_segmented_limb(
        draw,
        [(front_hip_x, hip_y), (front_knee_x, front_knee_y), (front_ankle_x, front_ankle_y - 2), (front_foot_x, front_foot_y - 4)],
        dark_width=20,
        light_width=12,
    )
    rect(draw, (front_foot_x - 16, front_foot_y - 9, front_foot_x + 9, front_foot_y), PALETTE["metal_bronze"])
    rect(draw, (front_foot_x - 12, front_foot_y - 7, front_foot_x + 5, front_foot_y - 1), PALETTE["metal_light"])

    rect(draw, (torso_x + 2, torso_top + 12, torso_x + 84, torso_bottom), PALETTE["metal_dark"])
    rect(draw, (torso_x + 6, torso_top + 16, torso_x + 80, torso_bottom - 4), PALETTE["metal_mid"])
    rect(draw, (torso_x + 18, torso_top + 24, torso_x + 62, torso_bottom - 16), PALETTE["metal_bronze"])
    polygon(
        draw,
        [
            (torso_x + 58, torso_top + 8),
            (torso_x + 92, torso_top + 18),
            (torso_x + 92, torso_top + 54),
            (torso_x + 68, torso_top + 60),
            (torso_x + 50, torso_top + 44),
        ],
        PALETTE["metal_dark"],
    )
    rect(draw, (torso_x + 60, torso_top + 18, torso_x + 86, torso_top + 52), PALETTE["metal_mid"])
    rect(draw, (torso_x + 68, torso_top + 24, torso_x + 82, torso_top + 30), PALETTE["metal_light"])
    rect(draw, (torso_x + 70, torso_top + 38, torso_x + 84, torso_top + 44), PALETTE["metal_light"])

    draw_orb(draw, torso_x + 44, torso_top + 44, 11 + (core_glow * 2), glow=1 + core_glow)
    rect(draw, (torso_x + 14, torso_bottom - 12, torso_x + 30, torso_bottom - 4), PALETTE["charge_mid" if charge_bars > 0.4 else "plasma"])
    rect(draw, (torso_x + 54, torso_bottom - 12, torso_x + 70, torso_bottom - 4), PALETTE["charge_mid" if charge_bars > 0.4 else "plasma"])

    head_x = torso_x + 92
    head_y = torso_top + 8
    rect(draw, (head_x, head_y + 8, head_x + 30, head_y + 30), PALETTE["metal_dark"])
    rect(draw, (head_x + 3, head_y + 11, head_x + 27, head_y + 27), PALETTE["metal_mid"])
    rect(draw, (head_x + 18, head_y + 15, head_x + 32, head_y + 27), PALETTE["metal_bronze"])
    rect(draw, (head_x + 10, head_y + 18, head_x + 24, head_y + 20), PALETTE["charge_light"])
    rect(draw, (head_x + 25, head_y + 13, head_x + 28, head_y + 18), PALETTE["metal_warning"])
    line(draw, [(head_x + 12, head_y + 8), (head_x + 8, head_y - 8)], PALETTE["metal_light"], width=3)
    draw_orb(draw, head_x + 8, head_y - 8, 3, glow=0)

    cannon_shoulder = (torso_x + 12, shoulder_y + 8)
    cannon_elbow = (torso_x - 8, shoulder_y - 2 - cannon_raise)
    cannon_wrist = (torso_x - 26, shoulder_y + 2 - cannon_raise)
    draw_segmented_limb(draw, [cannon_shoulder, cannon_elbow, cannon_wrist], dark_width=14, light_width=9)
    rect(draw, (cannon_wrist[0] - 22, cannon_wrist[1] - 9, cannon_wrist[0] + 2, cannon_wrist[1] + 9), PALETTE["metal_dark"])
    rect(draw, (cannon_wrist[0] - 18, cannon_wrist[1] - 6, cannon_wrist[0], cannon_wrist[1] + 6), PALETTE["metal_mid"])
    draw_orb(draw, cannon_wrist[0] - 18, cannon_wrist[1], 5 + charge_bars, glow=1 if charge_bars > 0.25 else 0)

    front_shoulder = (torso_x + 72, shoulder_y + 2)
    front_elbow = (torso_x + 102, shoulder_y - arm_raise)
    front_wrist = (torso_x + 122, shoulder_y + 4 - arm_raise)
    draw_segmented_limb(draw, [front_shoulder, front_elbow, front_wrist], dark_width=16, light_width=10)
    bicep_size = 10 + (flex_amount * 4)
    ellipse(draw, (front_elbow[0] - bicep_size, front_elbow[1] - bicep_size, front_elbow[0] + bicep_size, front_elbow[1] + bicep_size), PALETTE["metal_bronze"])
    rect(draw, (front_wrist[0] - 2, front_wrist[1] - 8, front_wrist[0] + 14, front_wrist[1] + 8), PALETTE["metal_light"])
    rect(draw, (front_wrist[0] + 12, front_wrist[1] - 6, front_wrist[0] + 18, front_wrist[1] + 6), PALETTE["metal_warning"])

    if shockwave > 0:
        wave_width = 70 + (shockwave * 110)
        wave_height = 12 + (shockwave * 10)
        ellipse_outline(
            draw,
            (front_foot_x - wave_width, floor_y - wave_height, front_foot_x + wave_width, floor_y + 2),
            PALETTE["plasma_light"],
            width=3,
        )


def draw_helicopter_frame(index: int, frame_count: int) -> Image.Image:
    image = canvas(HELICOPTER_SIZE)
    draw = ImageDraw.Draw(image)
    progress = index / max(1, frame_count - 1)
    bob = math.sin(progress * math.pi * 2.0) * 2.0
    rotor_blur = 0.4 + (pulse(progress, 4.0) * 0.6)

    body_top = 26 + bob
    rect(draw, (24, body_top, 124, body_top + 26), PALETTE["metal_dark"])
    rect(draw, (28, body_top + 3, 120, body_top + 22), PALETTE["metal_mid"])
    polygon(draw, [(124, body_top + 4), (158, body_top + 14), (124, body_top + 24)], PALETTE["metal_dark"])
    polygon(draw, [(124, body_top + 7), (150, body_top + 14), (124, body_top + 21)], PALETTE["metal_light"])
    rect(draw, (38, body_top + 8, 58, body_top + 18), PALETTE["charge_light"])
    rect(draw, (64, body_top + 8, 84, body_top + 18), PALETTE["charge_light"])
    rect(draw, (90, body_top + 8, 110, body_top + 18), PALETTE["charge_light"])

    line(draw, [(62, body_top), (72, body_top - 16), (98, body_top - 16), (108, body_top)], PALETTE["metal_light"], width=3)
    rotor_width = 76 + int(rotor_blur * 42)
    rect(draw, (84 - rotor_width, body_top - 20, 84 + rotor_width, body_top - 16), PALETTE["plasma_light"])

    line(draw, [(34, body_top + 28), (22, body_top + 40), (12, body_top + 40)], PALETTE["metal_light"], width=3)
    line(draw, [(106, body_top + 28), (118, body_top + 40), (146, body_top + 40)], PALETTE["metal_light"], width=3)
    line(draw, [(18, body_top + 14), (2, body_top + 10)], PALETTE["metal_light"], width=3)
    rect(draw, (0, body_top + 8, 12, body_top + 12), PALETTE["metal_warning"])
    return image


def draw_package_idle_frame(index: int, frame_count: int) -> Image.Image:
    image = canvas(PACKAGE_SIZE)
    draw = ImageDraw.Draw(image)
    progress = index / max(1, frame_count - 1)
    glow = 0.5 + (pulse(progress, 2.0) * 1.4)
    draw_package(draw, 2, 2, PACKAGE_SIZE[0] - 4, PACKAGE_SIZE[1] - 4, glow)
    return image


def draw_package_transform_frame(index: int, frame_count: int) -> Image.Image:
    image = canvas(ROBOT_SIZE)
    draw = ImageDraw.Draw(image)
    progress = index / max(1, frame_count - 1)
    floor_y = 162

    package_scale = 1.0 - (progress * 0.42)
    pkg_w = PACKAGE_SIZE[0] * package_scale
    pkg_h = PACKAGE_SIZE[1] * package_scale
    draw_package(draw, (ROBOT_SIZE[0] - pkg_w) / 2.0, floor_y - pkg_h, pkg_w, pkg_h, 0.8 + (progress * 1.8))

    robot_layer = canvas(ROBOT_SIZE)
    robot_draw = ImageDraw.Draw(robot_layer)
    reveal = clamp01((progress - 0.12) / 0.88)
    draw_robot(
        robot_draw,
        body_bob=4 - (reveal * 4),
        front_step=6 * (1.0 - reveal),
        back_step=-6 * (1.0 - reveal),
        front_leg_lift=16 * (1.0 - reveal),
        arm_raise=6 * (1.0 - reveal),
        cannon_raise=4 * (1.0 - reveal),
        flex_amount=reveal * 0.7,
        core_glow=reveal * 1.2,
        torso_lean=6 * (1.0 - reveal),
        charge_bars=reveal,
    )
    mask = Image.new("L", ROBOT_SIZE, 0)
    mask_draw = ImageDraw.Draw(mask)
    reveal_height = int(16 + (reveal * 136))
    mask_draw.rectangle((0, floor_y - reveal_height, ROBOT_SIZE[0], floor_y + 8), fill=255)
    image.paste(robot_layer, (0, 0), mask)
    return image


def draw_walk_frame(index: int, frame_count: int) -> Image.Image:
    image = canvas(ROBOT_SIZE)
    draw = ImageDraw.Draw(image)
    angle = (index / frame_count) * math.pi * 2.0
    draw_robot(
        draw,
        body_bob=math.sin(angle * 2.0) * 2.2,
        front_step=math.sin(angle) * 12.0,
        back_step=math.sin(angle + math.pi) * 10.0,
        front_leg_lift=max(0.0, math.sin(angle) * 12.0),
        arm_raise=math.sin(angle + math.pi) * 8.0,
        cannon_raise=math.sin(angle) * 5.0,
        flex_amount=0.3,
        core_glow=0.5 + (pulse(index / max(1, frame_count - 1), 2.0) * 0.5),
        torso_lean=math.sin(angle) * 2.0,
        charge_bars=0.35,
    )
    return image


def draw_pose_frame(index: int, frame_count: int) -> Image.Image:
    image = canvas(ROBOT_SIZE)
    draw = ImageDraw.Draw(image)
    progress = index / max(1, frame_count - 1)
    draw_robot(
        draw,
        body_bob=math.sin(progress * math.pi * 2.0) * 1.5,
        front_step=0,
        back_step=0,
        front_leg_lift=0,
        arm_raise=18 + (math.sin(progress * math.pi * 2.0) * 4.0),
        cannon_raise=10 + (math.cos(progress * math.pi * 2.0) * 3.0),
        flex_amount=1.0,
        core_glow=1.3,
        torso_lean=0,
        charge_bars=0.9,
    )
    return image


def draw_stomp_telegraph_frame(index: int, frame_count: int) -> Image.Image:
    image = canvas(ROBOT_SIZE)
    draw = ImageDraw.Draw(image)
    progress = index / max(1, frame_count - 1)
    draw_robot(
        draw,
        body_bob=-progress * 3.0,
        front_step=2 + (progress * 10.0),
        back_step=-progress * 6.0,
        front_leg_lift=progress * 36.0,
        arm_raise=progress * 10.0,
        cannon_raise=progress * 4.0,
        flex_amount=0.45 + (progress * 0.4),
        core_glow=0.6 + (progress * 0.8),
        torso_lean=-progress * 4.0,
        charge_bars=0.45 + (progress * 0.4),
    )
    return image


def draw_stomp_attack_frame(index: int, frame_count: int) -> Image.Image:
    image = canvas(ROBOT_SIZE)
    draw = ImageDraw.Draw(image)
    progress = index / max(1, frame_count - 1)
    if progress < 0.4:
        slam = progress / 0.4
        front_leg_lift = 36 - (slam * 8)
        shockwave = 0
    else:
        slam = (progress - 0.4) / 0.6
        front_leg_lift = max(0.0, 28 - (slam * 44))
        shockwave = slam
    draw_robot(
        draw,
        body_bob=-2 + (shockwave * 2.0),
        front_step=8 + (progress * 6.0),
        back_step=-4,
        front_leg_lift=front_leg_lift,
        arm_raise=8 - (progress * 4.0),
        cannon_raise=6 - (progress * 2.0),
        flex_amount=0.7,
        core_glow=1.0,
        torso_lean=2.0,
        shockwave=shockwave,
        charge_bars=0.75,
    )
    return image


def draw_mine_attack_telegraph_frame(index: int, frame_count: int) -> Image.Image:
    image = canvas(ROBOT_SIZE)
    draw = ImageDraw.Draw(image)
    progress = index / max(1, frame_count - 1)
    draw_robot(
        draw,
        body_bob=0,
        front_step=2,
        back_step=-2,
        front_leg_lift=0,
        arm_raise=2 + (progress * 6.0),
        cannon_raise=10 + (progress * 12.0),
        flex_amount=0.35,
        core_glow=0.8 + (progress * 1.2),
        torso_lean=-progress * 5.0,
        charge_bars=0.5 + (progress * 0.5),
    )
    marker_y = 148 - (progress * 10)
    for marker_index in range(3):
        center_x = 46 + (marker_index * 20)
        draw_orb(draw, center_x, marker_y + (marker_index * 4), 5 + (progress * 2), glow=1)
    return image


def draw_mine_attack_frame(index: int, frame_count: int) -> Image.Image:
    image = canvas(ROBOT_SIZE)
    draw = ImageDraw.Draw(image)
    progress = index / max(1, frame_count - 1)
    launch_push = math.sin(progress * math.pi)
    draw_robot(
        draw,
        body_bob=launch_push * 1.5,
        front_step=4,
        back_step=-2,
        front_leg_lift=0,
        arm_raise=4 - (launch_push * 3.0),
        cannon_raise=20 - (launch_push * 12.0),
        flex_amount=0.45,
        core_glow=1.2,
        torso_lean=-4 + (launch_push * 6.0),
        charge_bars=1.0,
    )
    for orb_index in range(3):
        center_x = 42 - (progress * 28) + (orb_index * 6)
        center_y = 118 - (progress * (18 + (orb_index * 6)))
        draw_orb(draw, center_x, center_y, 5 + (orb_index * 0.6), glow=1)
    return image


def draw_vent_frame(progress: float) -> Image.Image:
    image = canvas(VENT_SIZE)
    draw = ImageDraw.Draw(image)
    floor_y = VENT_SIZE[1] - 2
    plume_height = 18 + (progress * 58)
    plume_top = floor_y - plume_height
    ellipse(draw, (2, floor_y - 12, VENT_SIZE[0] - 2, floor_y), PALETTE["shadow"])
    polygon(
        draw,
        [
            (8, floor_y - 4),
            (14, plume_top + 24),
            (20, plume_top + 8),
            (26, plume_top + 20),
            (32, plume_top),
            (34, floor_y - 8),
        ],
        PALETTE["metal_warning"],
    )
    polygon(
        draw,
        [
            (12, floor_y - 6),
            (18, plume_top + 26),
            (22, plume_top + 12),
            (28, plume_top + 22),
            (31, plume_top + 8),
            (30, floor_y - 8),
        ],
        PALETTE["charge_mid"],
    )
    return image


def draw_stomp_leg_frame(progress: float) -> Image.Image:
    image = canvas(STOMP_LEG_SIZE)
    draw = ImageDraw.Draw(image)
    floor_y = STOMP_LEG_SIZE[1] - 4
    height = 18 + (progress * 100)
    rect(draw, (16, floor_y - height, 40, floor_y), PALETTE["metal_dark"])
    rect(draw, (19, floor_y - height + 3, 37, floor_y - 3), PALETTE["metal_mid"])
    rect(draw, (10, floor_y - 10, 46, floor_y), PALETTE["metal_bronze"])
    rect(draw, (16, floor_y - 16, 40, floor_y - 10), PALETTE["charge_mid"])
    draw_orb(draw, 28, floor_y - height + 20, 4 + (progress * 2), glow=1 if progress > 0.55 else 0)
    return image


def draw_stomp_splash_frame(progress: float) -> Image.Image:
    image = canvas(STOMP_SPLASH_SIZE)
    draw = ImageDraw.Draw(image)
    base_y = STOMP_SPLASH_SIZE[1] - 4
    width = 28 + (progress * 196)
    height = 6 + (progress * 18)
    ellipse(draw, (STOMP_SPLASH_SIZE[0] / 2 - width / 2, base_y - height, STOMP_SPLASH_SIZE[0] / 2 + width / 2, base_y), PALETTE["plasma"])
    ellipse(draw, (STOMP_SPLASH_SIZE[0] / 2 - width * 0.42, base_y - height * 0.72, STOMP_SPLASH_SIZE[0] / 2 + width * 0.42, base_y - 2), PALETTE["plasma_light"])
    ellipse_outline(draw, (STOMP_SPLASH_SIZE[0] / 2 - width / 2, base_y - height, STOMP_SPLASH_SIZE[0] / 2 + width / 2, base_y), PALETTE["charge_light"], width=2)
    return image


def draw_electro_mine_frame(index: int, frame_count: int) -> Image.Image:
    image = canvas(MINE_SIZE)
    draw = ImageDraw.Draw(image)
    progress = index / max(1, frame_count - 1)
    radius = 8 + (pulse(progress, 2.0) * 2)
    draw_orb(draw, MINE_SIZE[0] / 2, MINE_SIZE[1] / 2, radius, glow=1)
    line(draw, [(4, MINE_SIZE[1] / 2), (10, MINE_SIZE[1] / 2)], PALETTE["metal_light"], width=2)
    line(draw, [(MINE_SIZE[0] - 10, MINE_SIZE[1] / 2), (MINE_SIZE[0] - 4, MINE_SIZE[1] / 2)], PALETTE["metal_light"], width=2)
    line(draw, [(MINE_SIZE[0] / 2, 4), (MINE_SIZE[0] / 2, 10)], PALETTE["metal_light"], width=2)
    line(draw, [(MINE_SIZE[0] / 2, MINE_SIZE[1] - 10), (MINE_SIZE[0] / 2, MINE_SIZE[1] - 4)], PALETTE["metal_light"], width=2)
    return image


def draw_electro_ball_frame(index: int, frame_count: int) -> Image.Image:
    image = canvas(BALL_SIZE)
    draw = ImageDraw.Draw(image)
    progress = index / max(1, frame_count - 1)
    radius = 5 + (pulse(progress, 3.0) * 1.6)
    draw_orb(draw, BALL_SIZE[0] / 2, BALL_SIZE[1] / 2, radius, glow=1)
    line(draw, [(3, BALL_SIZE[1] / 2), (BALL_SIZE[0] - 3, BALL_SIZE[1] / 2)], PALETTE["plasma_light"], width=2)
    line(draw, [(BALL_SIZE[0] / 2, 3), (BALL_SIZE[0] / 2, BALL_SIZE[1] - 3)], PALETTE["charge_light"], width=1)
    return image


def clear_existing_outputs() -> None:
    for path in ASSET_DIR.glob("db-5000*.png"):
        path.unlink(missing_ok=True)


def generate_sequence(prefix: str, count: int, drawer) -> None:
    for index in range(count):
        save(drawer(index, count), f"{prefix}{index + 1}.png")


def generate_rise_sink(prefix: str, rise_count: int, sink_count: int, drawer) -> None:
    for index in range(rise_count):
        progress = (index + 1) / max(1, rise_count)
        save(drawer(progress), f"{prefix}_rise{index + 1}.png")
    for index in range(sink_count):
        progress = 1.0 - ((index + 1) / max(1, sink_count))
        save(drawer(progress), f"{prefix}_sink{index + 1}.png")


def main() -> None:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    clear_existing_outputs()

    generate_sequence("db-5000_walk", 6, draw_walk_frame)
    generate_sequence("db-5000_pose", 6, draw_pose_frame)
    generate_sequence("db-5000_stomp_telegraph", 6, draw_stomp_telegraph_frame)
    generate_sequence("db-5000_stomp_attack", 6, draw_stomp_attack_frame)
    generate_sequence("db-5000_electro_mines_telegraph", 6, draw_mine_attack_telegraph_frame)
    generate_sequence("db-5000_electro_mines_attack", 6, draw_mine_attack_frame)

    generate_sequence("db-5000_helicopter_fly", 4, draw_helicopter_frame)
    generate_sequence("db-5000_package_idle", 4, draw_package_idle_frame)
    generate_sequence("db-5000_package_transform", 8, draw_package_transform_frame)

    generate_rise_sink("db-5000_vent", 5, 4, draw_vent_frame)
    generate_rise_sink("db-5000_stomp_leg", 5, 4, draw_stomp_leg_frame)
    generate_rise_sink("db-5000_stomp_splash", 5, 4, draw_stomp_splash_frame)

    generate_sequence("db-5000_electro_mine_spin", 4, draw_electro_mine_frame)
    generate_sequence("db-5000_electro_ball_spin", 4, draw_electro_ball_frame)

    print("Generated DB-5000 boss sprites.")


if __name__ == "__main__":
    main()
