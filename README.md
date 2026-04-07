# TaskbarRPG

Small WPF taskbar RPG with stage-based progression, data-driven enemies/items, and editable config/save files.

## Data files

All runtime-editable files are copied to output and loaded from the executable directory:

- `TaskbarRPG/gameconfig.json`
- `TaskbarRPG/enemy_definitions.txt`
- `TaskbarRPG/boss_definitions.txt`
- `TaskbarRPG/area_definitions.txt`
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

## Boss definitions (`boss_definitions.txt`)

One boss per line, in progression order (line 1 = stage 5 boss, line 2 = stage 10 boss, etc):

```txt
name;health;attackdamage;movespeed;width(optional);height(optional)
```

- Required: first 4 columns.
- Optional width/height default to `64`.
- If the player reaches a boss milestone with no corresponding line, progression ends and they win for now.

## Area definitions (`area_definitions.txt`)

One area profile per line:

```txt
name;stages;enemies;colorHex(optional)
```

- `name`: display name of area.
- `stages`: where this area can appear (`1-4,16-19` etc).
- `enemies`: comma-separated enemy names from `enemy_definitions.txt`.
- `colorHex`: optional ground color (`RRGGBB`).

## Item definitions (`item_definitions.txt`)

One item per line:

```txt
name;damage;cooldown
```

- Category is inferred from name (`bow` => bow shop, otherwise sword shop).
- Shops improve every 5-stage cycle with meaningful damage jumps and higher prices per tier.

## Balancing model (current)

- **Static monster identity, stage-scaled stats**: enemies do not auto-match player stats; area templates choose which enemies appear, while stage adds controlled HP/DMG multipliers.
- **XP curve**: required XP per level increases super-linearly (`20 + 12L + 2L²`) to avoid runaway early levelling.
- **XP anti-farm scaling**: XP gained is scaled by enemy level vs player level so trivial enemies give heavily reduced XP and harder enemies give a modest bonus.
- **Shop progression**: every 5-stage cycle unlocks stronger template items and raises prices.

## Sprite guide

Player sprites are loaded from `Assets/Player` in the executable directory first, then fall back to built-in resources if missing.  
Player animations support any number of frames using `player_<action><index>.png` naming (for example `player_idle1.png`, `player_idle2.png`, ...).

| Type | Folder | Naming | Suggested Resolution |
|---|---|---|---|
| Player idle | `Assets/Player` | `player_idle1.png`, `player_idle2.png`, ... (fallback: `player_idle.png`) | `32x32` |
| Player walk | `Assets/Player` | `player_walk1.png`, `player_walk2.png`, ... | `32x32` |
| Player attack | `Assets/Player` | `player_attack1.png`, `player_attack2.png`, ... (fallback: `player_attack.png`) | `64x32` (or wider, 32 high) |
| Player jump (optional) | `Assets/Player` | `player_jump1.png`, `player_jump2.png`, ... | `32x32` |
| Player damaged (optional) | `Assets/Player` | `player_damaged1.png`, `player_damaged2.png`, ... | `32x32` |
| Player arrow (optional) | `Assets/Player` | `arrow.png` | `16x16` |
| Enemy walk frames | `Assets/Enemy` | `<name>_walk1.png`, `<name>_walk2.png`, ... | `~18x24` visual target |
| Enemy attack frames | `Assets/Enemy` | `<name>_attack1.png`, `<name>_attack2.png`, ... | any size; in-game width follows frame aspect ratio |
| Boss walk frames | `Assets/Boss` | `<boss_name>_walk1.png`, `<boss_name>_walk2.png`, ... | `64x64` typical (varying sizes supported) |
| Boss attack frames | `Assets/Boss` | `<boss_name>_attack1.png`, `<boss_name>_attack2.png`, ... | extra-wide frames supported; width follows frame aspect ratio |
| Item sprite (optional metadata) | `Assets/Item` | `<item_name_normalized>.png` (e.g. `iron_sword.png`) | `32x32` recommended |
| Town/NPC/shop | `Assets/Town` | existing built-in names | existing project assets |

## Controls

- `F8` toggle game on/off.
- `ESC` opens system menu:
  - `1` Save
  - `2` Save + Exit
  - `3` Reset progress (with confirmation)
