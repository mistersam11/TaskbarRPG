# TaskbarRPG

Small WPF taskbar RPG with stage-based progression, data-driven enemies/items, and editable config/save files.

## Data files

All runtime-editable files are copied to output and loaded from the executable directory:

- `TaskbarRPG/gameconfig.json`
- `TaskbarRPG/enemy_definitions.txt`
- `TaskbarRPG/item_definitions.txt`
- `TaskbarRPG/save_state.txt`

## Config (`gameconfig.json`)

Common keys:

- `Debug` - show debug hitboxes/overlays.
- `AttackPosition` - melee hitbox forward offset.
- `PlayerHitboxWidth`, `PlayerHitboxHeight` - player collision size.
- `MoveSpeed`, `Gravity`, `JumpStrength` - movement tuning.
- `ArrowHitboxWidth`, `ArrowHitboxHeight` - projectile collision size.
- `ArrowSpeed` - projectile travel speed.
- `ArrowDurationFrames` - projectile lifetime in frames.

## Enemy definitions (`enemy_definitions.txt`)

One enemy per line:

```txt
name;health;attackdamage;movespeed;level;biomes;stages
```

- Required: first 5 columns.
- Optional:
  - `biomes`: comma-separated (`plains,cave,forest,tundra`) or `*`
  - `stages`: comma/range list (`1-4,8,12-15`) or `*`

## Item definitions (`item_definitions.txt`)

One item per line:

```txt
name;damage;cooldown
```

- Category is inferred from name (`bow` => bow shop, otherwise sword shop).
- Shops improve every 5-stage cycle and prices scale up with tier.

## Sprite guide

| Type | Folder | Naming | Suggested Resolution |
|---|---|---|---|
| Player idle | `Assets/Player` | `player_idle.png` | `32x32` |
| Player walk | `Assets/Player` | `player_walk1.png`, `player_walk2.png` | `32x32` |
| Player attack | `Assets/Player` | `player_attack.png` | `64x32` (or wider, 32 high) |
| Player arrow (optional) | `Assets/Player` | `arrow.png` | `16x16` |
| Enemy walk frames | `Assets/Enemy` | `<name>_walk1.png`, `<name>_walk2.png`, ... | `~18x24` visual target |
| Enemy attack frames | `Assets/Enemy` | `<name>_attack1.png`, `<name>_attack2.png`, ... | width renders at 2x walking width while attacking |
| Item sprite (optional metadata) | `Assets/Item` | `<item_name_normalized>.png` (e.g. `iron_sword.png`) | `32x32` recommended |
| Town/NPC/shop | `Assets/Town` | existing built-in names | existing project assets |

## Controls

- `F8` toggle game on/off.
- `ESC` opens system menu:
  - `1` Save
  - `2` Save + Exit
  - `3` Reset progress (with confirmation)

