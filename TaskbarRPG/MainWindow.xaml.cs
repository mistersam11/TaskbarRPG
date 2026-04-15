using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using IOPath = System.IO.Path;

namespace TaskbarRPG
{
    public enum AreaType
    {
        Town,
        Adventure,
    }

    public enum BiomeType
    {
        Plains,
        Cave,
        Forest,
        Firelands
    }

    public enum TransitionDirection
    {
        Left,
        Right
    }

    public enum CrawlerPhase
    {
        SurfaceWalk,
        Burrowing,
        Underground,
        LeapTelegraph,
        LeapAttack,
        FloatDescent
    }

    public enum EnemyDeathAnimationType
    {
        FadeOut,
        SlimeBurst,
        WolfCollapse,
        BatPlummet,
        CrawlerBackflip
    }

    public enum EnemyHazardPhase
    {
        Delay,
        Rise,
        Hold,
        Sink
    }

    public enum ZoneContentType
    {
        None,
        Shop
    }

    public enum ShopType
    {
        Sword,
        Bow,
        Healing
    }

    public enum PanelMode
    {
        None,
        SystemMenu,
        ResetConfirm,
        FastTravel,
        Stats,
        Map,
        ShopMain,
        ShopBuy,
        ShopSell,
        EquipSword,
        EquipBow,
        DebugCheatMenu,
        DebugCheatSetLevel,
        DebugCheatSetItems,
    }

    public enum ItemKind
    {
        Weapon,
        Consumable,
        Ammo,
        Misc
    }

    public enum WeaponCategory
    {
        Sword,
        Bow
    }

    public abstract class ItemBase
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "";
        public ItemKind Kind { get; protected set; }
        public int BasePrice { get; set; }
        public bool Stackable { get; protected set; }

        public virtual string GetDisplayText() => Name;
    }

    public class WeaponItem : ItemBase
    {
        public WeaponCategory WeaponCategory { get; set; }
        public int Level { get; set; } = 1;
        public int CooldownFrames { get; set; } = 10;
        public string? SpritePath { get; set; } = null;

        public WeaponItem()
        {
            Kind = ItemKind.Weapon;
            Stackable = false;
        }

        public override string GetDisplayText() => $"{Name} (Lv {Level}, x{Level}, CD {CooldownFrames})";
    }

    public class ItemTemplate
    {
        public string Name { get; set; } = "";
        public int Level { get; set; } = 1;
        public int CooldownFrames { get; set; } = 10;
        public string? SpritePath { get; set; } = null;

        public WeaponCategory Category =>
            Name.Contains("bow", StringComparison.OrdinalIgnoreCase) ? WeaponCategory.Bow : WeaponCategory.Sword;
    }

    public class ConsumableItem : ItemBase
    {
        public int HealAmount { get; set; }

        public ConsumableItem()
        {
            Kind = ItemKind.Consumable;
            Stackable = true;
        }

        public override string GetDisplayText() => $"{Name} (+{HealAmount} HP)";
    }

    public class AmmoItem : ItemBase
    {
        public string AmmoType { get; set; } = "Arrow";

        public AmmoItem()
        {
            Kind = ItemKind.Ammo;
            Stackable = true;
        }

        public override string GetDisplayText() => Name;
    }

    public class InventoryEntry
    {
        public ItemBase Item { get; set; } = null!;
        public int Quantity { get; set; }
    }

    public class ShopListing
    {
        public ItemBase Item { get; set; } = null!;
        public int Quantity { get; set; } = 1;
        public int Price { get; set; }

        public string GetDisplayText()
        {
            if (Quantity > 1)
                return $"{Item.GetDisplayText()} x{Quantity} - {Price}g";
            return $"{Item.GetDisplayText()} - {Price}g";
        }
    }

    public class PlayerData
    {
        public int Level { get; set; } = 1;
        public int Experience { get; set; } = 0;
        public int Gold { get; set; } = 10;
        public int Health { get; set; } = 100;
        public int MaxHealth { get; set; } = 100;

        public WeaponItem? EquippedSword { get; set; } = null;
        public WeaponItem? EquippedBow { get; set; } = null;

        public List<InventoryEntry> Inventory { get; set; } = new();

        public int NextLevelXp => 20 + (Level * 12) + (Level * Level * 2);
        public int BaseDamage => Level;

        public int GetArrowCount()
        {
            var arrows = Inventory.FirstOrDefault(i => i.Item is AmmoItem ammo && ammo.AmmoType == "Arrow");
            return arrows?.Quantity ?? 0;
        }

        public bool RemoveArrows(int amount)
        {
            var arrows = Inventory.FirstOrDefault(i => i.Item is AmmoItem ammo && ammo.AmmoType == "Arrow");
            if (arrows == null || arrows.Quantity < amount)
                return false;

            arrows.Quantity -= amount;
            if (arrows.Quantity <= 0)
                Inventory.Remove(arrows);

            return true;
        }
    }

    public class GameConfig
    {
        public bool Debug { get; set; } = false;
        public int AttackPosition { get; set; } = 8;
        public double PlayerHitboxWidth { get; set; } = 24;
        public double PlayerHitboxHeight { get; set; } = 28;
        public double MoveSpeed { get; set; } = 4.4;
        public double Gravity { get; set; } = 0.8;
        public double JumpStrength { get; set; } = -7.4;
        public int StatusFrames { get; set; } = 60;
    }

    public class VariableZone
    {
        public int SlotIndex { get; set; }
        public double X { get; set; }
        public ZoneContent? Content { get; set; }
    }

    public abstract class ZoneContent
    {
        public ZoneContentType ContentType { get; protected set; }
        public string DisplayName { get; set; } = "";
        public string NpcName { get; set; } = "";
        public Color BuildingColor { get; set; } = Colors.Gray;
        public Color NpcColor { get; set; } = Colors.White;
    }

    public class ShopZoneContent : ZoneContent
    {
        public ShopType ShopType { get; set; }
        public List<ShopListing> Stock { get; set; } = new();

        public ShopZoneContent()
        {
            ContentType = ZoneContentType.Shop;
        }
    }

    public class EnemyDefinition
    {
        public string Name { get; set; } = "Enemy";
        public List<string> BehaviorIds { get; set; } = new() { "melee_chaser" };
        public int BehaviorIntervalFrames { get; set; } = 42;
        public int PowerLevel { get; set; } = 1;
        public double X { get; set; }
        public double PatrolRange { get; set; } = 80;
        public double AggroRange { get; set; } = 180;
        public double Speed { get; set; } = 1.0;
        public int MaxHealth { get; set; } = 5;
        public int ContactDamage { get; set; } = 5;
        public int XpReward { get; set; } = 4;
        public int GoldMin { get; set; } = 1;
        public int GoldMax { get; set; } = 2;
        public Color Color { get; set; } = Colors.Purple;
        public double Width { get; set; } = 18;
        public double Height { get; set; } = 24;
        public double AttackHitboxWidth { get; set; } = 24;
        public double AttackHitboxHeight { get; set; } = 14;
        public double? CollisionHitboxWidth { get; set; }
        public double? CollisionHitboxHeight { get; set; }
        public double? CollisionHitboxOffsetX { get; set; }
        public double? CollisionHitboxOffsetY { get; set; }
    }

    public class EnemyTemplate
    {
        public string Name { get; set; } = "Enemy";
        public List<string> BehaviorIds { get; set; } = new() { "melee_chaser" };
        public int BehaviorIntervalFrames { get; set; } = 42;
        public int Health { get; set; } = 10;
        public int AttackDamage { get; set; } = 4;
        public double MoveSpeed { get; set; } = 1.0;
        public int Level { get; set; } = 1;
        public double Width { get; set; } = 20;
        public double Height { get; set; } = 24;
        public double AttackHitboxWidth { get; set; } = 24;
        public double AttackHitboxHeight { get; set; } = 14;
        public double? CollisionHitboxWidth { get; set; }
        public double? CollisionHitboxHeight { get; set; }
        public double? CollisionHitboxOffsetX { get; set; }
        public double? CollisionHitboxOffsetY { get; set; }
        public HashSet<BiomeType>? AllowedBiomes { get; set; } = null;
        public List<(int Min, int Max)> StageRanges { get; set; } = new();

        public bool CanSpawnIn(int stage, BiomeType biome)
        {
            bool biomeAllowed = AllowedBiomes == null || AllowedBiomes.Count == 0 || AllowedBiomes.Contains(biome);
            if (!biomeAllowed) return false;

            if (StageRanges.Count > 0)
                return StageRanges.Any(r => stage >= r.Min && stage <= r.Max);

            int blockStart = ((Math.Max(1, Level) - 1) / 5) * 5 + 1;
            return stage >= blockStart && stage <= blockStart + 3;
        }
    }

    public class BossTemplate
    {
        public string Name { get; set; } = "Boss";
        public int Health { get; set; } = 80;
        public int AttackDamage { get; set; } = 18;
        public double MoveSpeed { get; set; } = 1.15;
        public double Width { get; set; } = 64;
        public double Height { get; set; } = 64;
        public List<string> BehaviorIds { get; set; } = new() { "melee_chaser" };
        public int BehaviorIntervalFrames { get; set; } = 40;
    }

    public class AreaTemplate
    {
        public string Name { get; set; } = "Wilderness";
        public List<(int Min, int Max)> StageRanges { get; set; } = new();
        public List<string> EnemyNames { get; set; } = new();
        public Color GroundColor { get; set; } = Color.FromRgb(90, 170, 80);

        public bool CanAppearAtStage(int stage)
        {
            if (StageRanges.Count == 0) return true;
            return StageRanges.Any(r => stage >= r.Min && stage <= r.Max);
        }
    }

    public class Area
    {
        public AreaType Type { get; set; }
        public string Name { get; set; } = "";
        public Color GroundColor { get; set; }
        public BiomeType? Biome { get; set; }
        public int StageNumber { get; set; }
        public bool IsBossArea { get; set; }
        public VariableZone[] Zones { get; set; } = new VariableZone[6];
        public List<EnemyDefinition> EnemySpawns { get; set; } = new();
    }

    public static class AreaDefinitions
    {
        private static readonly List<BossTemplate> defaultBossTemplates = new()
        {
            new BossTemplate { Name = "The Goo", Health = 80, AttackDamage = 20, MoveSpeed = 1.05, Width = 64, Height = 64, BehaviorIds = new() { "hop_contact", "dash_strike" }, BehaviorIntervalFrames = 42 },
            new BossTemplate { Name = "Fallen Knight", Health = 190, AttackDamage = 22, MoveSpeed = 0.0, Width = 96, Height = 180, BehaviorIds = new() { "spike_field", "snowball_heave", "fire_head" }, BehaviorIntervalFrames = 44 },
            new BossTemplate { Name = "DB-5000", Health = 130, AttackDamage = 24, MoveSpeed = 1.1, Width = 72, Height = 64, BehaviorIds = new() { "melee_chaser" }, BehaviorIntervalFrames = 30 },
        };

        private static readonly List<BossTemplate> bossTemplates = new();

        static AreaDefinitions()
        {
            SetBossTemplates(defaultBossTemplates);
        }

        public static void SetBossTemplates(IEnumerable<BossTemplate> templates)
        {
            bossTemplates.Clear();
            bossTemplates.AddRange(templates.Where(t => !string.IsNullOrWhiteSpace(t.Name)));
        }

        public static bool CanGenerateStage(int stage)
        {
            if (stage <= 0) return true;
            if (bossTemplates.Count == 0) return stage < 5;
            if (stage > bossTemplates.Count * 5) return false;
            if (stage % 5 != 0) return true;
            int bossIndex = (stage / 5) - 1;
            return bossIndex >= 0 && bossIndex < bossTemplates.Count;
        }

        public static int GetHighestConfiguredStage()
        {
            if (bossTemplates.Count == 0)
                return 4;

            return bossTemplates.Count * 5;
        }

        public static bool IsFinalBossStage(int stage)
        {
            if (stage <= 0 || stage % 5 != 0) return false;
            int bossIndex = (stage / 5) - 1;
            return bossIndex >= 0 && bossIndex == bossTemplates.Count - 1;
        }

        public static Area GetTown()
        {
            return new Area
            {
                Type = AreaType.Town,
                Name = "Town",
                StageNumber = 0,
                IsBossArea = false,
                GroundColor = Color.FromRgb(194, 154, 108),
                Zones = new VariableZone[]
                {
                    new VariableZone
                    {
                        SlotIndex = 0,
                        X = 180,
                        Content = new ShopZoneContent
                        {
                            ShopType = ShopType.Sword,
                            DisplayName = "Sword Shop",
                            NpcName = "Blacksmith",
                            BuildingColor = Color.FromRgb(110, 110, 120),
                            NpcColor = Color.FromRgb(220, 220, 220),
                        }
                    },
                    new VariableZone { SlotIndex = 1, X = 430, Content = null },
                    new VariableZone
                    {
                        SlotIndex = 2,
                        X = 680,
                        Content = new ShopZoneContent
                        {
                            ShopType = ShopType.Bow,
                            DisplayName = "Bow Shop",
                            NpcName = "Fletcher",
                            BuildingColor = Color.FromRgb(120, 85, 55),
                            NpcColor = Color.FromRgb(210, 190, 130),
                        }
                    },
                    new VariableZone { SlotIndex = 3, X = 930, Content = null },
                    new VariableZone
                    {
                        SlotIndex = 4,
                        X = 1180,
                        Content = new ShopZoneContent
                        {
                            ShopType = ShopType.Healing,
                            DisplayName = "Healing Shop",
                            NpcName = "Healer",
                            BuildingColor = Color.FromRgb(140, 180, 150),
                            NpcColor = Color.FromRgb(255, 220, 240),
                        }
                    },
                    new VariableZone { SlotIndex = 5, X = 1430, Content = null },
                },
                EnemySpawns = new List<EnemyDefinition>()
            };
        }

        public static Area CreateStageArea(int stage, Random rng, IReadOnlyList<AreaTemplate> areaTemplates, IReadOnlyList<EnemyTemplate> enemyTemplates)
        {
            if (stage % 5 == 0)
                return CreateBossArea(stage);

            var eligibleAreas = areaTemplates.Where(a => a.CanAppearAtStage(stage)).ToList();
            var chosenArea = eligibleAreas.Count > 0
                ? eligibleAreas[rng.Next(eligibleAreas.Count)]
                : new AreaTemplate
                {
                    Name = "Wilderness",
                    StageRanges = new List<(int, int)> { (1, int.MaxValue) },
                    EnemyNames = enemyTemplates.Select(e => e.Name).ToList(),
                    GroundColor = Color.FromRgb(90, 170, 80)
                };

            int count = rng.Next(4, 9);
            var spawns = new List<EnemyDefinition>();
            const double leftSpawnPadding = 260.0;
            double spacing = 1300.0 / count;
            var enemyMap = enemyTemplates.ToDictionary(e => e.Name, e => e, StringComparer.OrdinalIgnoreCase);
            var allowedEnemies = chosenArea.EnemyNames
                .Where(name => enemyMap.ContainsKey(name))
                .Select(name => enemyMap[name])
                .ToList();

            for (int i = 0; i < count; i++)
            {
                EnemyTemplate chosen = allowedEnemies.Count > 0
                    ? allowedEnemies[rng.Next(allowedEnemies.Count)]
                    : new EnemyTemplate { Name = "Wisp", Health = 8, AttackDamage = 4, MoveSpeed = 1.0, Level = stage };

                double hpScale = 1.0 + (stage * 0.06);
                double dmgScale = 1.0 + (stage * 0.04);
                if (stage >= 6)
                {
                    hpScale *= 1.10 + ((stage - 5) * 0.03);
                    dmgScale *= 1.05 + ((stage - 5) * 0.015);
                }
                int enemyLevel = Math.Max(1, chosen.Level + (stage / 5));
                spawns.Add(new EnemyDefinition
                {
                    Name = chosen.Name,
                    BehaviorIds = chosen.BehaviorIds.Count > 0 ? chosen.BehaviorIds.ToList() : new List<string> { "melee_chaser" },
                    BehaviorIntervalFrames = chosen.BehaviorIntervalFrames,
                    PowerLevel = enemyLevel,
                    X = leftSpawnPadding + (i * spacing) + rng.Next(-25, 26),
                    PatrolRange = 90 + rng.Next(0, 70),
                    AggroRange = 160 + rng.Next(0, 70),
                    Speed = Math.Max(0.4, chosen.MoveSpeed),
                    MaxHealth = Math.Max(2, (int)Math.Round(chosen.Health * hpScale)),
                    ContactDamage = Math.Max(1, (int)Math.Round(chosen.AttackDamage * dmgScale)),
                    XpReward = Math.Max(4, 6 + (enemyLevel * 2)),
                    GoldMin = Math.Max(1, enemyLevel / 2),
                    GoldMax = Math.Max(2, enemyLevel + 2),
                    Width = Math.Max(10, chosen.Width),
                    Height = Math.Max(10, chosen.Height),
                    AttackHitboxWidth = Math.Max(8, chosen.AttackHitboxWidth),
                    AttackHitboxHeight = Math.Max(6, chosen.AttackHitboxHeight),
                    CollisionHitboxWidth = chosen.CollisionHitboxWidth,
                    CollisionHitboxHeight = chosen.CollisionHitboxHeight,
                    CollisionHitboxOffsetX = chosen.CollisionHitboxOffsetX,
                    CollisionHitboxOffsetY = chosen.CollisionHitboxOffsetY,
                    Color = Color.FromRgb(
                        (byte)rng.Next(70, 210),
                        (byte)rng.Next(70, 210),
                        (byte)rng.Next(70, 210)),
                });
            }

            return new Area
            {
                Type = AreaType.Adventure,
                Name = $"Stage {stage} - {chosenArea.Name}",
                GroundColor = chosenArea.GroundColor,
                Biome = ParseBiomeName(chosenArea.Name),
                StageNumber = stage,
                IsBossArea = false,
                Zones = CreateEmptyZones(),
                EnemySpawns = spawns
            };
        }

        private static BiomeType? ParseBiomeName(string areaName)
        {
            return Enum.TryParse<BiomeType>(areaName, true, out var biome)
                ? biome
                : null;
        }

        private static Area CreateBossArea(int stage)
        {
            int bossIndex = Math.Max(0, (stage / 5) - 1);
            if (bossIndex >= bossTemplates.Count)
            {
                return new Area
                {
                    Type = AreaType.Adventure,
                    Name = $"Stage {stage} - Victory",
                    GroundColor = Color.FromRgb(50, 72, 62),
                    Biome = null,
                    StageNumber = stage,
                    IsBossArea = false,
                    Zones = CreateEmptyZones(),
                    EnemySpawns = new List<EnemyDefinition>()
                };
            }

            BossTemplate template = bossTemplates[bossIndex];

            int power = Math.Max(3, stage);
            double bossHpScale = 1.0;
            double bossDamageScale = 1.0;
            if (stage >= 10)
            {
                bossHpScale = 1.10 + ((stage - 5) * 0.03);
                bossDamageScale = 1.05 + ((stage - 5) * 0.015);
            }
            var boss = new EnemyDefinition
            {
                Name = template.Name,
                BehaviorIds = template.BehaviorIds.Count > 0 ? template.BehaviorIds.ToList() : new List<string> { "melee_chaser" },
                BehaviorIntervalFrames = template.BehaviorIntervalFrames,
                PowerLevel = Math.Max(1, stage + 2),
                X = 1180,
                PatrolRange = 170,
                AggroRange = 260,
                Speed = Math.Max(0.7, template.MoveSpeed + Math.Min(0.35, power * 0.01)),
                MaxHealth = Math.Max(20, (int)Math.Round((template.Health + (power * 6)) * bossHpScale)),
                ContactDamage = Math.Max(3, (int)Math.Round((template.AttackDamage + (power / 2.0)) * bossDamageScale)),
                XpReward = 20 + power * 2,
                GoldMin = 12 + power,
                GoldMax = 20 + power * 2,
                Width = Math.Max(24, template.Width),
                Height = Math.Max(24, template.Height),
                AttackHitboxWidth = Math.Max(20, template.Width * 0.85),
                AttackHitboxHeight = Math.Max(16, template.Height * 0.45),
                Color = Color.FromRgb(180, 60, 70),
            };

            if (template.Name.Equals("The Goo", StringComparison.OrdinalIgnoreCase))
            {
                boss.X = 820;
                boss.CollisionHitboxHeight = Math.Min(boss.Height, (boss.Height * 0.76) + 10);
                boss.CollisionHitboxOffsetY = boss.Height - boss.CollisionHitboxHeight.Value;
            }
            else if (template.Name.Equals("Fallen Knight", StringComparison.OrdinalIgnoreCase))
            {
                boss.X = 1180;
                boss.PatrolRange = 0;
                boss.AggroRange = 2400;
                boss.Speed = 0;
                boss.AttackHitboxWidth = Math.Max(44, template.Width * 0.58);
                boss.AttackHitboxHeight = Math.Max(84, template.Height * 0.8);
                boss.CollisionHitboxWidth = Math.Max(56, template.Width * 0.5);
                boss.CollisionHitboxHeight = Math.Max(116, Math.Min(130, template.Height * 0.88));
                boss.CollisionHitboxOffsetX = (template.Width - boss.CollisionHitboxWidth.Value) / 2.0;
                boss.CollisionHitboxOffsetY = Math.Max(0, template.Height - boss.CollisionHitboxHeight.Value);
            }

            return new Area
            {
                Type = AreaType.Adventure,
                Name = $"Stage {stage} - Boss",
                GroundColor = Color.FromRgb(70, 65, 75),
                Biome = null,
                StageNumber = stage,
                IsBossArea = true,
                Zones = CreateEmptyZones(),
                EnemySpawns = new List<EnemyDefinition> { boss }
            };
        }

        private static VariableZone[] CreateEmptyZones()
        {
            return new VariableZone[]
            {
                new VariableZone { SlotIndex = 0, X = 180,  Content = null },
                new VariableZone { SlotIndex = 1, X = 430,  Content = null },
                new VariableZone { SlotIndex = 2, X = 680,  Content = null },
                new VariableZone { SlotIndex = 3, X = 930,  Content = null },
                new VariableZone { SlotIndex = 4, X = 1180, Content = null },
                new VariableZone { SlotIndex = 5, X = 1430, Content = null },
            };
        }
    }

    public static class ItemFactory
    {
        public static int ClampWeaponLevel(int level) => Math.Clamp(level, 1, 5);

        public static int GetWeaponBasePrice(WeaponCategory category, int level, int cooldownFrames)
        {
            level = ClampWeaponLevel(level);

            int levelPrice = level switch
            {
                1 => 12,
                2 => 48,
                3 => 110,
                4 => 240,
                5 => 425,
                _ => 425 + ((level - 5) * 160)
            };

            int cooldownAdjustment = Math.Clamp((20 - cooldownFrames) * 3, -18, 18);
            int rangedAdjustment = category == WeaponCategory.Bow ? 8 : 0;
            return Math.Max(1, levelPrice + cooldownAdjustment + rangedAdjustment);
        }

        public static WeaponItem CreateWeapon(
            string name,
            WeaponCategory category,
            int level,
            int cooldownFrames,
            string? spritePath = null,
            int? basePrice = null)
        {
            int clampedLevel = ClampWeaponLevel(level);
            int clampedCooldown = Math.Max(1, cooldownFrames);

            return new WeaponItem
            {
                Name = name,
                WeaponCategory = category,
                Level = clampedLevel,
                CooldownFrames = clampedCooldown,
                BasePrice = basePrice ?? GetWeaponBasePrice(category, clampedLevel, clampedCooldown),
                SpritePath = spritePath,
            };
        }

        public static WeaponItem CreateOldSword()
        {
            return CreateWeapon("Old Sword", WeaponCategory.Sword, 1, 12, basePrice: 50);
        }

        public static WeaponItem CreateStarterBow()
        {
            return CreateWeapon("Simple Bow", WeaponCategory.Bow, 1, 14, basePrice: 50);
        }

        public static ConsumableItem CreatePotion()
        {
            return new ConsumableItem
            {
                Name = "Potion",
                HealAmount = 25,
                BasePrice = 8,
            };
        }

        public static AmmoItem CreateArrowItem()
        {
            return new AmmoItem
            {
                Name = "Arrow",
                AmmoType = "Arrow",
                BasePrice = 1,
            };
        }

        public static WeaponItem CreateRandomSword(Random rng, int minLevel = 2, int maxLevel = 5)
        {
            string[] prefixes = { "Bronze", "Iron", "Steel", "Hunter's", "Knight's" };
            string[] suffixes = { "Sword", "Blade", "Sabre" };
            int level = rng.Next(Math.Max(1, minLevel), Math.Max(minLevel, maxLevel) + 1);
            int cooldown = rng.Next(8, 15);

            return CreateWeapon(
                $"{prefixes[rng.Next(prefixes.Length)]} {suffixes[rng.Next(suffixes.Length)]}",
                WeaponCategory.Sword,
                level,
                cooldown);
        }

        public static WeaponItem CreateRandomBow(Random rng, int minLevel = 2, int maxLevel = 5)
        {
            string[] prefixes = { "Oak", "Recurve", "Hunter's", "Elm", "Long" };
            string[] suffixes = { "Bow", "Shortbow", "Longbow" };
            int level = rng.Next(Math.Max(1, minLevel), Math.Max(minLevel, maxLevel) + 1);
            int cooldown = rng.Next(10, 17);

            return CreateWeapon(
                $"{prefixes[rng.Next(prefixes.Length)]} {suffixes[rng.Next(suffixes.Length)]}",
                WeaponCategory.Bow,
                level,
                cooldown);
        }
    }

    // ---------------------------------------------------------------------------
    // Area transition fade overlay
    // ---------------------------------------------------------------------------
    public class AreaTransition
    {
        private readonly Rectangle overlay;
        private readonly Canvas canvas;
        private readonly DispatcherTimer timer;
        private readonly Action<int, TransitionDirection> onMidpoint;

        private bool fadingOut = true;
        private int pendingStage;
        private TransitionDirection pendingDir;
        private double opacity = 0;

        public bool IsActive { get; private set; }

        public AreaTransition(Canvas canvas, Action<int, TransitionDirection> onMidpoint)
        {
            this.canvas = canvas;
            this.onMidpoint = onMidpoint;

            overlay = new Rectangle
            {
                Fill = Brushes.Black,
                Opacity = 0,
                Visibility = Visibility.Hidden,
            };

            canvas.Children.Add(overlay);
            Panel.SetZIndex(overlay, 100);

            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += Tick;
        }

        public void Start(int targetStage, TransitionDirection dir)
        {
            if (IsActive) return;

            pendingStage = targetStage;
            pendingDir = dir;
            fadingOut = true;
            opacity = 0;
            IsActive = true;

            overlay.Width = canvas.ActualWidth > 0 ? canvas.ActualWidth : 2560;
            overlay.Height = canvas.ActualHeight > 0 ? canvas.ActualHeight : 200;
            overlay.Visibility = Visibility.Visible;
            Canvas.SetLeft(overlay, 0);
            Canvas.SetTop(overlay, 0);

            timer.Start();
        }

        private void Tick(object? sender, EventArgs e)
        {
            if (fadingOut)
            {
                opacity += 0.1;
                if (opacity >= 1.0)
                {
                    opacity = 1.0;
                    fadingOut = false;
                    onMidpoint(pendingStage, pendingDir);
                }
            }
            else
            {
                opacity -= 0.1;
                if (opacity <= 0)
                {
                    opacity = 0;
                    IsActive = false;
                    overlay.Visibility = Visibility.Hidden;
                    timer.Stop();
                }
            }

            overlay.Opacity = opacity;
        }
    }

    // ---------------------------------------------------------------------------
    // Scene object holders
    // ---------------------------------------------------------------------------
    public class SpawnedZoneVisual
    {
        public VariableZone Zone = null!;
        public FrameworkElement Building = null!;
        public TextBlock? BuildingLabel = null;

        public Image Npc = null!;
        public TextBlock NpcLabel = null!;

        public BitmapImage? NpcIdle1 = null!;
        public BitmapImage? NpcIdle2 = null!;
        public double NpcBaseY;
    }

    public class SpawnedGroundDecoration
    {
        public FrameworkElement Visual = null!;
        public double X;
        public double Height;
        public double GroundSink;
    }

    public class SpawnedEnemy
    {
        public EnemyDefinition Definition = null!;
        public FrameworkElement Body = null!;
        public Image? BodySprite = null;
        public List<BitmapImage> WalkFrames { get; set; } = new();
        public List<BitmapImage> AttackFrames { get; set; } = new();
        public Dictionary<string, List<BitmapImage>> BehaviorAttackFrames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<BitmapImage>> BehaviorTelegraphFrames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<BitmapImage>> BehaviorCooldownFrames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<BitmapImage>> BehaviorUndergroundFrames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, List<BitmapImage>> BehaviorFloatFrames { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<BitmapImage> HazardRiseFrames { get; set; } = new();
        public List<BitmapImage> HazardSinkFrames { get; set; } = new();
        public Rectangle AttackHitbox = null!;
        public bool IsAttacking = false;
        public int AttackFramesRemaining = 0;
        public int AttackAnimationTick = 0;
        public int AttackAnimationDuration = 0;
        public bool AttackEffectTriggered = false;
        public int AttackCooldownFrames = 0;
        public bool AttackDamageApplied = false;
        public bool IsTelegraphing = false;
        public int TelegraphFramesRemaining = 0;
        public int TelegraphAnimationTick = 0;
        public int TelegraphAnimationDuration = 0;
        public bool HasLockedAttackDirection = false;
        public int LockedAttackDirection = 1;
        public string CurrentBehaviorId = "melee_chaser";
        public int BehaviorCycleFrames = 0;
        public int BehaviorTimerFrames = 0;
        public int GooHopJumpsRemaining = 0;
        public bool IsRecovering = false;
        public int RecoveryPauseFrames = 0;
        public int RecoveryAnimationTick = 0;
        public int RecoveryAnimationDuration = 0;
        public double FlightHomeY = double.NaN;
        public double FlightAnchorX = double.NaN;
        public double FlightTargetX = double.NaN;
        public double FlightTargetY = double.NaN;
        public int FlightRetargetFrames = 0;
        public int FlightAttackPhase = 0;
        public int FlightAttackPhaseFrames = 0;
        public double HorizontalVelocity = 0;
        public double VerticalVelocity = 0;
        public bool IsGrounded = true;
        public TextBlock Label = null!;
        public Rectangle HealthBg = null!;
        public Rectangle HealthFill = null!;
        public double X;
        public double Y;
        public double LeftBound;
        public double RightBound;
        public double Speed;
        public int Direction = 1;
        public bool IsAlive = true;
        public int CurrentHealth;
        public double CurrentSpriteWidth;
        public double SpriteGroundOffsetY = 0;
        public bool IsAggroLocked = false;
        public CrawlerPhase CrawlerPhase = CrawlerPhase.SurfaceWalk;
        public int CrawlerPhaseTick = 0;
        public int CrawlerPhaseDuration = 0;
        public double SpriteRotationDegrees = 0;
        public double SpriteRotationVelocityDegrees = 0;
        public bool IsDying = false;
        public int DeathAnimationTick = 0;
        public int DeathAnimationDuration = 0;
        public EnemyDeathAnimationType DeathAnimationType = EnemyDeathAnimationType.FadeOut;
        public double DeathOpacity = 1.0;
        public double DeathScaleX = 1.0;
        public double DeathScaleY = 1.0;
        public double DeathOffsetY = 0;
        public double DeathRotationDegrees = 0;
        public SpawnedEnemy? LinkedEnemy = null;
        public bool IsReturningToOwner = false;
        public bool SuppressRewards = false;
        public bool IsInvulnerable = false;
        public bool SuppressContactDamage = false;
        public int SpecialActionStep = 0;
        public int SpecialActionCounter = 0;
        public int FallenKnightSpikeAttacksCompleted = 0;
        public int FallenKnightSnowballAttacksCompleted = 0;
        public double HomeX = double.NaN;
        public double HomeY = double.NaN;
        public int ReturnAnimationTick = 0;
        public int ReturnAnimationDuration = 0;
        public List<Point> TrailHistory { get; set; } = new();
        public List<FrameworkElement> TrailBodies { get; set; } = new();
    }

    public class ArrowProjectile
    {
        public FrameworkElement Body = null!;
        public List<BitmapImage> Frames { get; set; } = new();
        public int AnimationFrameCounter;
        public double X;
        public double Y;
        public int Direction;
        public double Speed;
        public double VerticalVelocity;
        public double GravityPerFrame;
        public double InitialGravityMultiplier;
        public int GravityRampFrames;
        public int GravityDelayFrames;
        public int AgeFrames;
        public int Damage;
        public bool IsAlive = true;
    }

    public class SpawnedEnemyProjectile
    {
        public SpawnedEnemy Owner = null!;
        public FrameworkElement Body = null!;
        public Image? BodySprite = null;
        public List<BitmapImage> Frames { get; set; } = new();
        public double X;
        public double Y;
        public int Direction = 1;
        public double HorizontalVelocity;
        public double VerticalVelocity;
        public double GravityPerFrame;
        public double HitboxWidth;
        public double HitboxHeight;
        public int Damage;
        public int AnimationFrameCounter;
        public bool IsAlive = true;
        public bool HasAppliedDamage = false;
    }

    public class SpawnedEnemyHazard
    {
        public SpawnedEnemy Owner = null!;
        public FrameworkElement Body = null!;
        public Image? BodySprite = null;
        public Rectangle Hitbox = null!;
        public List<BitmapImage> RiseFrames { get; set; } = new();
        public List<BitmapImage> SinkFrames { get; set; } = new();
        public double X;
        public double Y;
        public double Width;
        public double Height;
        public double HitboxWidth;
        public double HitboxHeight;
        public double HitboxOffsetX;
        public double HitboxOffsetY;
        public int Damage;
        public int DelayFrames;
        public int PhaseTick;
        public int HoldFrames;
        public int RiseDurationFrames = 7;
        public int SinkDurationFrames = 8;
        public int Direction = 1;
        public EnemyHazardPhase Phase = EnemyHazardPhase.Delay;
        public bool IsAlive = true;
        public bool HasAppliedDamage = false;
    }

    // ---------------------------------------------------------------------------
    // Main window
    // ---------------------------------------------------------------------------
    public partial class MainWindow : Window
    {
        // Win32 constants
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x80;

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int VK_BACK = 0x08;
        private const int VK_RETURN = 0x0D;
        private const int VK_LEFT = 0x25;
        private const int VK_UP = 0x26;
        private const int VK_RIGHT = 0x27;
        private const int VK_DOWN = 0x28;
        private const int VK_SPACE = 0x20;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_0 = 0x30;
        private const int VK_A = 0x41;
        private const int VK_C = 0x43;
        private const int VK_D = 0x44;
        private const int VK_E = 0x45;
        private const int VK_F = 0x46;
        private const int VK_M = 0x4D;
        private const int VK_S = 0x53;
        private const int VK_W = 0x57;
        private const int VK_X = 0x58;
        private const int VK_Z = 0x5A;
        private const int VK_F8 = 0x77;
        private const int VK_OEM_2 = 0xBF; // / ?
        private const int VK_1 = 0x31;
        private const int VK_2 = 0x32;
        private const int VK_3 = 0x33;
        private const int VK_4 = 0x34;
        private const int VK_5 = 0x35;
        private const int VK_6 = 0x36;
        private const int VK_7 = 0x37;
        private const int VK_8 = 0x38;
        private const int VK_9 = 0x39;
        private static readonly int[] BossSkipSequence = { VK_UP, VK_DOWN, VK_UP, VK_DOWN, VK_LEFT, VK_RIGHT, VK_LEFT, VK_RIGHT };
        private static readonly int[] DebugCheatSequence = { VK_W, VK_S, VK_W, VK_S, VK_A, VK_D, VK_A, VK_D };

        // P/Invoke
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("shell32.dll")]
        private static extern int SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        private const uint ABM_GETTASKBARPOS = 0x00000005;

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int left, top, right, bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public int lParam;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // -----------------------------------------------------------------------
        // Fields
        // -----------------------------------------------------------------------
        private readonly Random rng = new Random();

        private IntPtr keyboardHook = IntPtr.Zero;
        private LowLevelKeyboardProc? keyboardProc;

        // Input state
        private bool leftHeld = false;
        private bool rightHeld = false;
        private bool jumpHeld = false;
        private bool downHeld = false;
        private bool meleeHeld = false;
        private bool fireHeld = false;
        private bool closeHeld = false;
        private bool escapeHeld = false;
        private bool fastTravelHeld = false;
        private bool statsHeld = false;
        private bool mapHeld = false;
        private bool potionHeld = false;
        private bool zeroHeld = false;
        private bool enterHeld = false;
        private bool backspaceHeld = false;

        private readonly bool[] numberHeld = new bool[9];
        private readonly bool[] numberHeldLastFrame = new bool[9];

        private bool jumpHeldLastFrame = false;
        private bool meleeHeldLastFrame = false;
        private bool fireHeldLastFrame = false;
        private bool closeHeldLastFrame = false;
        private bool escapeHeldLastFrame = false;
        private bool fastTravelHeldLastFrame = false;
        private bool statsHeldLastFrame = false;
        private bool mapHeldLastFrame = false;
        private bool potionHeldLastFrame = false;
        private bool zeroHeldLastFrame = false;
        private bool enterHeldLastFrame = false;
        private bool backspaceHeldLastFrame = false;
        private int bossSkipSequenceIndex = 0;
        private int debugCheatSequenceIndex = 0;
        private int fastTravelPageIndex = 0;
        private bool fastTravelCheatMode = false;
        private string debugCheatLevelInput = "";

        // UI elements
        private DispatcherTimer timer = null!;
        private Image player = null!;
        private Rectangle playerLevelUpPulseOverlay = null!;
        private Rectangle attackHitbox = null!;
        private Rectangle playerHitboxDebug = null!;
        private Rectangle groundRect = null!;

        private Rectangle playerHealthBg = null!;
        private Rectangle playerHealthFill = null!;
        private TextBlock playerHealthText = null!;
        private TextBlock playerArrowText = null!;

        private Border panelBorder = null!;
        private TextBlock panelText = null!;

        private TextBlock leftExitText = null!;
        private TextBlock rightExitText = null!;
        private TextBlock statusText = null!;

        private List<BitmapImage> playerIdleFrames = new();
        private List<BitmapImage> playerWalkFrames = new();
        private List<BitmapImage> playerAttackFrames = new();
        private List<BitmapImage> playerDownAttackFrames = new();
        private List<BitmapImage> playerJumpFrames = new();
        private List<BitmapImage> playerDamagedFrames = new();
        private List<BitmapImage> playerBowCharge1Frames = new();
        private List<BitmapImage> playerBowCharge1WalkFrames = new();
        private List<BitmapImage> playerBowCharge2Frames = new();
        private List<BitmapImage> playerBowCharge2WalkFrames = new();
        private List<BitmapImage> playerBowCharge3Frames = new();
        private List<BitmapImage> playerBowCharge3WalkFrames = new();
        private List<BitmapImage> playerBowFullFrames = new();
        private List<BitmapImage> playerBowFullWalkFrames = new();
        private List<BitmapImage> playerArrowFrames = new();
        private List<BitmapImage> playerArrowMaxFrames = new();
        private BitmapImage? playerArrowSprite = null;

        private void UpdateNpcAnimations()
        {
            foreach (var visual in activeZoneVisuals)
            {
                if (visual.NpcIdle1 == null || visual.NpcIdle2 == null)
                    continue;

                bool useFirstFrame = ((animationFrameCounter / 20) % 2 == 0);
                visual.Npc.Source = useFirstFrame ? visual.NpcIdle1 : visual.NpcIdle2;

                double bounceOffset = useFirstFrame ? 0 : -1;
                Canvas.SetTop(visual.Npc, visual.NpcBaseY + bounceOffset);
            }
        }

        private BitmapImage? GetShopBuildingSprite(ZoneContent content)
        {
            if (content is not ShopZoneContent shop)
                return null;

            return shop.ShopType switch
            {
                ShopType.Sword => LoadSprite("Assets/Town/sword_shop.png"),
                ShopType.Bow => LoadSprite("Assets/Town/bow_shop.png"),
                ShopType.Healing => LoadSprite("Assets/Town/healing_shop.png"),
                _ => null
            };
        }

        private BitmapImage? GetShopNpcIdle1Sprite(ZoneContent content)
        {
            if (content is not ShopZoneContent shop)
                return null;

            return shop.ShopType switch
            {
                ShopType.Sword => LoadSprite("Assets/Town/blacksmith_idle1.png"),
                ShopType.Bow => LoadSprite("Assets/Town/fletcher_idle1.png"),
                ShopType.Healing => LoadSprite("Assets/Town/healer_idle1.png"),
                _ => null
            };
        }

        private BitmapImage? GetShopNpcIdle2Sprite(ZoneContent content)
        {
            if (content is not ShopZoneContent shop)
                return null;

            return shop.ShopType switch
            {
                ShopType.Sword => LoadSprite("Assets/Town/blacksmith_idle2.png"),
                ShopType.Bow => LoadSprite("Assets/Town/fletcher_idle2.png"),
                ShopType.Healing => LoadSprite("Assets/Town/healer_idle2.png"),
                _ => null
            };
        }

        private int animationFrameCounter = 0;

        private BitmapImage LoadSprite(string relativePath)
        {
            return new BitmapImage(new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute));
        }

        private List<BitmapImage> LoadEnemyFrames(string enemyName, string action)
        {
            string normalized = enemyName.Trim().ToLowerInvariant().Replace(" ", "_");
            var bossFrames = LoadFramesFromDirectory(IOPath.Combine(AppContext.BaseDirectory, "Assets", "Boss"), normalized, action);
            if (bossFrames.Count > 0)
                return bossFrames;

            return LoadFramesFromDirectory(IOPath.Combine(AppContext.BaseDirectory, "Assets", "Enemy"), normalized, action);
        }

        private List<BitmapImage> LoadEnemyBehaviorFrames(string enemyName, string behaviorId, string phase)
        {
            string normalizedBehavior = behaviorId.Trim().ToLowerInvariant();
            var behaviorFrames = LoadEnemyFrames(enemyName, $"{normalizedBehavior}_{phase}");
            if (behaviorFrames.Count > 0)
                return behaviorFrames;

            // Some shipped sprite sheets use "dask" instead of "dash" in the filename.
            if (normalizedBehavior.Contains("dash", StringComparison.OrdinalIgnoreCase))
            {
                string typoBehavior = normalizedBehavior.Replace("dash", "dask", StringComparison.OrdinalIgnoreCase);
                behaviorFrames = LoadEnemyFrames(enemyName, $"{typoBehavior}_{phase}");
                if (behaviorFrames.Count > 0)
                    return behaviorFrames;
            }

            if (normalizedBehavior.EndsWith("_strike", StringComparison.OrdinalIgnoreCase))
            {
                string shortBehavior = normalizedBehavior[..^"_strike".Length];
                behaviorFrames = LoadEnemyFrames(enemyName, $"{shortBehavior}_{phase}");
                if (behaviorFrames.Count > 0)
                    return behaviorFrames;

                if (shortBehavior.Contains("dash", StringComparison.OrdinalIgnoreCase))
                {
                    string typoShortBehavior = shortBehavior.Replace("dash", "dask", StringComparison.OrdinalIgnoreCase);
                    behaviorFrames = LoadEnemyFrames(enemyName, $"{typoShortBehavior}_{phase}");
                    if (behaviorFrames.Count > 0)
                        return behaviorFrames;
                }
            }

            return LoadEnemyFrames(enemyName, phase);
        }

        private List<BitmapImage> LoadEnemyBehaviorAttackFrames(string enemyName, string behaviorId)
        {
            if (!UsesDedicatedEnemyAttackFrames(enemyName, behaviorId))
                return new List<BitmapImage>();
            return LoadEnemyBehaviorFrames(enemyName, behaviorId, "attack");
        }

        private List<BitmapImage> LoadEnemyBehaviorTelegraphFrames(string enemyName, string behaviorId)
        {
            return LoadEnemyBehaviorFrames(enemyName, behaviorId, "telegraph");
        }

        private List<BitmapImage> LoadEnemyBehaviorCooldownFrames(string enemyName, string behaviorId)
        {
            return LoadEnemyBehaviorFrames(enemyName, behaviorId, "cooldown");
        }

        private List<BitmapImage> LoadEnemyBehaviorUndergroundFrames(string enemyName, string behaviorId)
        {
            return LoadEnemyBehaviorFrames(enemyName, behaviorId, "underground");
        }

        private List<BitmapImage> LoadEnemyBehaviorFloatFrames(string enemyName, string behaviorId)
        {
            return LoadEnemyBehaviorFrames(enemyName, behaviorId, "float");
        }

        private static bool UsesDedicatedEnemyAttackFrames(string enemyName, string? behaviorId = null)
        {
            if (enemyName.Equals("Fallen Knight Head", StringComparison.OrdinalIgnoreCase))
                return behaviorId != null && behaviorId.Equals("fire_tower", StringComparison.OrdinalIgnoreCase);

            if (enemyName.Equals("slime", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        private List<BitmapImage> LoadFramesFromDirectory(string dir, string normalizedName, string action)
        {
            var frames = new List<BitmapImage>();
            if (!System.IO.Directory.Exists(dir))
                return frames;

            string pattern = $"{normalizedName}_{action}*.png";
            foreach (string file in System.IO.Directory
                .GetFiles(dir, pattern)
                .OrderBy(path => GetAnimationFrameSortKey(path).BaseName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(path => GetAnimationFrameSortKey(path).FrameNumber)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(file, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                frames.Add(bitmap);
            }

            return frames;
        }

        private static (string BaseName, int FrameNumber) GetAnimationFrameSortKey(string path)
        {
            string fileName = IOPath.GetFileNameWithoutExtension(path);
            int suffixStart = fileName.Length;
            while (suffixStart > 0 && char.IsDigit(fileName[suffixStart - 1]))
                suffixStart--;

            if (suffixStart < fileName.Length &&
                int.TryParse(fileName[suffixStart..], out int frameNumber))
            {
                return (fileName[..suffixStart], frameNumber);
            }

            return (fileName, 0);
        }

        private double GetSpriteWidthForHeight(BitmapSource frame, double targetHeight)
        {
            if (frame.PixelHeight <= 0 || targetHeight <= 0)
                return targetHeight;

            return targetHeight * ((double)frame.PixelWidth / frame.PixelHeight);
        }

        private static int GetHeldAnimationFrameIndex(int frameCount, int tick, int duration)
        {
            if (frameCount <= 1)
                return 0;

            int safeDuration = Math.Max(1, duration);
            int clampedTick = Math.Clamp(tick, 0, safeDuration - 1);
            return Math.Min(frameCount - 1, (clampedTick * frameCount) / safeDuration);
        }

        private void StartEnemyTelegraph(SpawnedEnemy enemy, int durationFrames)
        {
            enemy.IsTelegraphing = true;
            enemy.TelegraphFramesRemaining = Math.Max(1, durationFrames);
            enemy.TelegraphAnimationDuration = enemy.TelegraphFramesRemaining;
            enemy.TelegraphAnimationTick = 0;
        }

        private void StopEnemyTelegraph(SpawnedEnemy enemy)
        {
            enemy.IsTelegraphing = false;
            enemy.TelegraphFramesRemaining = 0;
            enemy.TelegraphAnimationTick = 0;
            enemy.TelegraphAnimationDuration = 0;
        }

        private void StartEnemyAttack(SpawnedEnemy enemy, int durationFrames)
        {
            enemy.IsAttacking = true;
            enemy.AttackFramesRemaining = Math.Max(1, durationFrames);
            enemy.AttackAnimationDuration = enemy.AttackFramesRemaining;
            enemy.AttackAnimationTick = 0;
            enemy.AttackEffectTriggered = false;
            enemy.FlightAttackPhase = 0;
            enemy.FlightAttackPhaseFrames = 0;
        }

        private void StopEnemyAttack(SpawnedEnemy enemy)
        {
            enemy.IsAttacking = false;
            enemy.AttackFramesRemaining = 0;
            enemy.AttackAnimationTick = 0;
            enemy.AttackAnimationDuration = 0;
            enemy.AttackEffectTriggered = false;
            enemy.FlightAttackPhase = 0;
            enemy.FlightAttackPhaseFrames = 0;
        }

        private void StartEnemyRecoveryPause(SpawnedEnemy enemy, int durationFrames)
        {
            enemy.IsRecovering = true;
            enemy.RecoveryPauseFrames = Math.Max(1, durationFrames);
            enemy.RecoveryAnimationTick = 0;
            enemy.RecoveryAnimationDuration = enemy.RecoveryPauseFrames;
            enemy.HorizontalVelocity = 0;
            enemy.VerticalVelocity = 0;
        }

        private void StopEnemyRecoveryPause(SpawnedEnemy enemy)
        {
            enemy.IsRecovering = false;
            enemy.RecoveryPauseFrames = 0;
            enemy.RecoveryAnimationTick = 0;
            enemy.RecoveryAnimationDuration = 0;
        }

        private string ChooseRandomGooAttackBehavior(SpawnedEnemy enemy)
        {
            var gooBehaviors = enemy.Definition.BehaviorIds
                .Where(id =>
                    id.Equals("hop_contact", StringComparison.OrdinalIgnoreCase) ||
                    id.Equals("dash_strike", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (gooBehaviors.Count == 0)
                return enemy.CurrentBehaviorId;

            return gooBehaviors[rng.Next(gooBehaviors.Count)];
        }

        private void SetEnemyBehavior(SpawnedEnemy enemy, string behaviorId)
        {
            bool changed = !string.Equals(enemy.CurrentBehaviorId, behaviorId, StringComparison.OrdinalIgnoreCase);
            enemy.CurrentBehaviorId = behaviorId;

            if (!enemy.Definition.Name.Equals("The Goo", StringComparison.OrdinalIgnoreCase))
            {
                if (enemy.Definition.Name.Equals("Fallen Knight", StringComparison.OrdinalIgnoreCase))
                {
                    enemy.SpecialActionStep = 0;
                    enemy.SpecialActionCounter = 0;
                }
                return;
            }

            if (behaviorId.Equals("hop_contact", StringComparison.OrdinalIgnoreCase))
            {
                if (changed || enemy.GooHopJumpsRemaining <= 0)
                    enemy.GooHopJumpsRemaining = rng.Next(4, 11);
            }
            else
            {
                enemy.GooHopJumpsRemaining = 0;
            }
        }

        private bool HandleGooRecoveryState(SpawnedEnemy enemy)
        {
            if (!enemy.Definition.Name.Equals("The Goo", StringComparison.OrdinalIgnoreCase))
                return false;

            if (enemy.IsRecovering && enemy.RecoveryPauseFrames <= 0)
            {
                StopEnemyRecoveryPause(enemy);
                SetEnemyBehavior(enemy, ChooseRandomGooAttackBehavior(enemy));
                enemy.BehaviorTimerFrames = 0;
                enemy.HasLockedAttackDirection = false;
                enemy.HorizontalVelocity = 0;
                enemy.VerticalVelocity = 0;
                return true;
            }

            if (enemy.IsRecovering)
            {
                enemy.RecoveryAnimationTick++;
                enemy.RecoveryPauseFrames--;
                enemy.HorizontalVelocity = 0;
                enemy.VerticalVelocity = 0;
                return true;
            }

            return false;
        }

        private double GetEnemySpriteGroundOffsetY(EnemyDefinition definition)
        {
            if (definition.Name.Equals("wolf", StringComparison.OrdinalIgnoreCase))
                return 12;
            if (definition.Name.Equals("frostling", StringComparison.OrdinalIgnoreCase))
                return 2;

            return 0;
        }

        private static double GetStepTowards(double current, double target, double maxStep)
        {
            if (maxStep <= 0)
                return 0;

            return Math.Clamp(target - current, -maxStep, maxStep);
        }

        private double GetRandomDouble(double min, double max)
        {
            if (max <= min)
                return min;

            return min + (rng.NextDouble() * (max - min));
        }

        private void SetBatFlightTarget(
            SpawnedEnemy enemy,
            double desiredX,
            double desiredY,
            double xJitter,
            double yJitter,
            double minX,
            double maxX,
            double minY,
            double maxY,
            int minRetargetFrames,
            int maxRetargetFrames)
        {
            enemy.FlightTargetX = Math.Clamp(
                desiredX + GetRandomDouble(-xJitter, xJitter),
                minX,
                maxX);
            enemy.FlightTargetY = Math.Clamp(
                desiredY + GetRandomDouble(-yJitter, yJitter),
                minY,
                maxY);
            enemy.FlightRetargetFrames = rng.Next(
                Math.Max(1, minRetargetFrames),
                Math.Max(minRetargetFrames, maxRetargetFrames) + 1);
        }

        private BitmapImage? LoadOptionalPlayerSpriteFromDisk(string fileName)
        {
            string path = IOPath.Combine(AppContext.BaseDirectory, "Assets", "Player", fileName);
            if (!System.IO.File.Exists(path))
                return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private BitmapImage LoadPlayerSpriteFromDiskOrPack(string fileName, string fallbackPackPath)
            => LoadOptionalPlayerSpriteFromDisk(fileName) ?? LoadSprite(fallbackPackPath);

        private BitmapImage LoadBitmapImageFromPath(string path)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private List<BitmapImage> LoadPlayerAnimationFrames(string actionName, string? fallbackPackPath = null)
        {
            var frames = new List<BitmapImage>();
            string dir = IOPath.Combine(AppContext.BaseDirectory, "Assets", "Player");
            string pattern = $"player_{actionName}*.png";

            if (System.IO.Directory.Exists(dir))
            {
                foreach (string file in System.IO.Directory
                    .GetFiles(dir, pattern)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
                {
                    frames.Add(LoadBitmapImageFromPath(file));
                }
            }

            if (frames.Count == 0 && fallbackPackPath != null)
                frames.Add(LoadSprite(fallbackPackPath));

            return frames;
        }

        private List<BitmapImage> LoadPlayerAnimationFramesByPrefix(string filePrefix, Func<string, bool>? includeFile = null)
        {
            var frames = new List<BitmapImage>();
            string dir = IOPath.Combine(AppContext.BaseDirectory, "Assets", "Player");
            if (!System.IO.Directory.Exists(dir))
                return frames;

            foreach (string file in System.IO.Directory
                .GetFiles(dir, $"{filePrefix}*.png")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string fileName = IOPath.GetFileName(file);
                if (includeFile != null && !includeFile(fileName))
                    continue;

                frames.Add(LoadBitmapImageFromPath(file));
            }

            return frames;
        }

        // Game state
        private Area currentArea = null!;
        private Area? previousArea = null;
        private AreaTransition transition = null!;
        private readonly Dictionary<int, Area> stageAreas = new();
        private readonly Dictionary<BitmapSource, ImageBrush> playerLevelUpPulseMasks = new();
        private int currentStageNumber = 0;
        private int highestUnlockedStage = 1;

        private readonly List<SpawnedGroundDecoration> activeGroundDecorations = new();
        private readonly List<SpawnedZoneVisual> activeZoneVisuals = new();
        private readonly List<SpawnedEnemy> activeEnemies = new();
        private readonly List<SpawnedEnemyHazard> activeEnemyHazards = new();
        private readonly List<SpawnedEnemyProjectile> activeEnemyProjectiles = new();
        private readonly List<ArrowProjectile> activeProjectiles = new();
        private readonly List<AreaTemplate> areaTemplates = new();
        private readonly List<EnemyTemplate> enemyTemplates = new();
        private readonly List<BossTemplate> bossTemplates = new();
        private readonly List<ItemTemplate> itemTemplates = new();

        private readonly PlayerData playerData = new();
        private GameConfig gameConfig = new();

        // Physics / layout
        private double playAreaHeight = 240;
        private double playerX = 100;
        private double playerY = 0;
        private double playerWidth = 32;
        private double playerHeight = 32;
        private double velocityX = 0;
        private double velocityY = 0;
        private double moveSpeed = 4.4;
        private double gravity = 0.8;
        private double jumpStrength = -7.4;
        private double playerHitboxWidth = 24;
        private double playerHitboxHeight = 28;
        private const double ArrowHitboxWidth = 10;
        private const double ArrowHitboxHeight = 6;
        private const double ArrowSpeed = 8.5;
        private double groundY = 0;
        private bool isOnGround = false;
        private bool facingRight = true;
        private double groundStripHeight = 14;
        private const double OutOfCombatMoveSpeedMultiplier = 1.75;

        // Game state flags
        private bool controlsEnabled = true;
        private PanelMode panelMode = PanelMode.None;
        private bool stageTookDamage = false;

        private bool isAttacking = false;
        private bool isDownAttack = false;
        private int attackFramesRemaining = 0;
        private int attackDurationFrames = 8;
        private const int PlayerAttackFrameTicks = 2;
        private const int GooDashAttackDurationFrames = 90;
        private const int GooDashRecoveryDurationFrames = 125;
        private const double GooDashSpeed = 8.4;
        private const double SwordKnockbackSpeed = 1.65;
        private const double SwordKnockbackLift = -0.45;
        private const int CrawlerBurrowDurationFrames = 24;
        private const int CrawlerUndergroundDurationFrames = 108;
        private const int CrawlerLeapTelegraphDurationFrames = 20;
        private const int CrawlerLeapAnimationDurationFrames = 18;
        private const double CrawlerBurrowTriggerDistance = 104;
        private const double CrawlerUndergroundPlayerSpeedLead = 0.35;
        private const double CrawlerLeapVerticalSpeed = -11.2;
        private const double CrawlerLeapGravityMultiplier = 1.0;
        private const double CrawlerLeapMaxRisePixels = 96;
        private const double CrawlerFloatGravityMultiplier = 0.18;
        private const double CrawlerFloatMaxFallSpeed = 1.2;
        private const double CrawlerSpinDegreesPerFrame = 40;
        private const double CrawlerFloatSpinDamping = 0.88;
        private const double CrawlerFloatHorizontalDamping = 0.94;
        private const int FrostlingTelegraphDurationFrames = 28;
        private const int FrostlingAttackDurationFrames = 34;
        private const int FrostlingRecoveryDurationFrames = 210;
        private const int FrostlingAttackRange = 56;
        private const int FrostlingMinimumAttackRange = 10;
        private const int FrostlingIcicleRiseFrames = 7;
        private const int FrostlingIcicleHoldFrames = 8;
        private const int FrostlingIcicleSinkFrames = 8;
        private const int FrostlingIcicleDelayStepFrames = 4;
        private const int FrostlingIcicleBurstCount = 14;
        private const int FrostlingIcicleDamage = 11;
        private const int FallenKnightSpikeTelegraphFrames = 26;
        private const int FallenKnightSpikeRiseFrames = 9;
        private const int FallenKnightSpikeHoldFrames = 10;
        private const int FallenKnightSpikeSinkFrames = 8;
        private const int FallenKnightSpikeDamage = 18;
        private const int FallenKnightSnowballTelegraphDurationFrames = 24;
        private const int FallenKnightSnowballAttackBaseFrames = 56;
        private const int FallenKnightSnowballVolleyCount = 6;
        private const int FallenKnightFireHeadTelegraphDurationFrames = 24;
        private const int FallenKnightFireHeadAttackDurationFrames = 34;
        private const int FallenKnightAttackCooldownFrames = 54;
        private const int FallenKnightHeadFireTowerChargeFrames = 120;
        private const int FallenKnightHeadFireTowerBaseAttackFrames = 48;
        private const int FallenKnightHeadFireTowerMinJumpCount = 5;
        private const int FallenKnightHeadFireTowerMaxJumpCount = 7;
        private const double FallenKnightFixedX = 1180;
        private const int FallenKnightHeadReturnDurationFrames = 18;
        private const double FallenKnightSnowballGravity = 0.18;
        private const double FallenKnightSnowballBaseTravelFrames = 28;
        private const double FallenKnightSnowballTravelFrameStep = 4;
        private const double FallenKnightSnowballLandingSpreadBase = 52;
        private const double FallenKnightSnowballLandingSpreadStep = 18;
        private const int FallenKnightHeadJumpMinAirFrames = 20;
        private const int FallenKnightHeadJumpMaxAirFrames = 36;
        private const double FallenKnightHeadJumpMinDistance = 84;
        private const double FallenKnightHeadJumpLandingSpread = 164;
        private const double FallenKnightHeadTargetedJumpMaxSpeed = 10.4;
        private const double FallenKnightHeadTrailSampleMinDistance = 10;
        private const int FallenKnightHeadTrailLength = 4;
        private const double FallenKnightHeadSplashMinImpactSpeed = 4.5;
        private const int FallenKnightHeadSplashHoldFrames = 5;
        private const int FallenKnightHeadSplashRiseFrames = 5;
        private const int FallenKnightHeadSplashSinkFrames = 6;
        private const double FallenKnightHeadSplashHitboxWidth = 84;
        private const double FallenKnightHeadSplashHitboxHeight = 28;
        private const double FallenKnightHeadFireTowerScaleX = 0.58;
        private const double FallenKnightHeadFireTowerScaleY = 4.25;
        private const double FallenKnightReassemblyStartScaleX = 0.86;
        private const double FallenKnightReassemblyStartScaleY = 0.48;
        private const double FallenKnightReassemblyLiftPixels = 36;
        private const double FallenKnightCollapsedHitboxHeight = 26;
        private const double FallenKnightCollapsedHitboxWidth = 92;
        private const double EnemyProjectileDefaultHitboxScale = 0.64;
        private const int EnemyDeathDefaultDurationFrames = 24;
        private const int SlimeDeathDurationFrames = 20;
        private const int WolfDeathDurationFrames = 22;
        private const int BatDeathDurationFrames = 34;
        private const int CrawlerDeathDurationFrames = 28;
        private readonly HashSet<SpawnedEnemy> currentAttackVictims = new();
        private int meleeCooldownFrames = 0;
        private int bowCooldownFrames = 0;
        private bool isBowCharging = false;
        private int bowChargeFrames = 0;
        private bool bowChargeFullNotified = false;
        private const int BowChargeMaxFrames = 72;
        private const int PerfectClearGoldBonus = 50;
        private const int LevelUpPulseDurationFrames = 42;
        private int playerJumpAnimationTick = 0;
        private int levelUpPulseFramesRemaining = 0;

        private SpawnedZoneVisual? currentInteractableZone = null;
        private ShopZoneContent? activeShop = null;

        private double mapPulseTime = 0;
        private int playerDamageCooldownFrames = 0;
        private const int PlayerDamageCooldownMax = 35;
        private int statusFramesRemaining = 0;

        // -----------------------------------------------------------------------
        // Constructor
        // -----------------------------------------------------------------------
        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            Closed += MainWindow_Closed;
        }

        // -----------------------------------------------------------------------
        // Startup
        // -----------------------------------------------------------------------
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfig();
            LoadAreaTemplates();
            LoadEnemyTemplates();
            LoadBossTemplates();
            LoadItemTemplates();
            InitializePlayerData();
            int startStage = LoadSaveState();
            PositionAboveTaskbar();
            MakeClickThrough();
            CreateBackground();
            CreateHud();
            CreateMainPanel();
            CreateEdgeTexts();

            playerIdleFrames = LoadPlayerAnimationFrames("idle", "Assets/Player/player_idle.png");
            playerWalkFrames = LoadPlayerAnimationFrames("walk", "Assets/Player/player_walk1.png");
            var walkFallbackFrame = LoadPlayerSpriteFromDiskOrPack("player_walk2.png", "Assets/Player/player_walk2.png");
            if (playerWalkFrames.Count == 1)
                playerWalkFrames.Add(walkFallbackFrame);
            playerAttackFrames = LoadPlayerAnimationFrames("attack", "Assets/Player/player_attack.png");
            playerDownAttackFrames = LoadPlayerAnimationFrames("attack_down");
            if (playerDownAttackFrames.Count == 0)
                playerDownAttackFrames = playerAttackFrames.ToList();
            playerJumpFrames = LoadPlayerAnimationFrames("jump");
            playerDamagedFrames = LoadPlayerAnimationFrames("damaged");
            playerBowCharge1Frames = LoadOptionalPlayerSpriteFromDisk("player_bow_charge1.png") is BitmapImage bowCharge1
                ? new List<BitmapImage> { bowCharge1 }
                : LoadPlayerAnimationFrames("bow_charge1");
            playerBowCharge1WalkFrames = LoadPlayerAnimationFramesByPrefix("player_bow_charge1_walk");
            playerBowCharge2Frames = LoadOptionalPlayerSpriteFromDisk("player_bow_charge2.png") is BitmapImage bowCharge2
                ? new List<BitmapImage> { bowCharge2 }
                : LoadPlayerAnimationFrames("bow_charge2");
            playerBowCharge2WalkFrames = LoadPlayerAnimationFramesByPrefix("player_bow_charge2_walk");
            playerBowCharge3Frames = LoadOptionalPlayerSpriteFromDisk("player_bow_charge3.png") is BitmapImage bowCharge3
                ? new List<BitmapImage> { bowCharge3 }
                : LoadPlayerAnimationFrames("bow_charge3");
            playerBowCharge3WalkFrames = LoadPlayerAnimationFramesByPrefix("player_bow_charge3_walk");
            playerBowFullFrames = LoadPlayerAnimationFramesByPrefix(
                "player_bow_full",
                fileName => !fileName.Contains("_walk", StringComparison.OrdinalIgnoreCase));
            if (playerBowFullFrames.Count == 0)
                playerBowFullFrames = LoadPlayerAnimationFrames("bow_full");
            playerBowFullWalkFrames = LoadPlayerAnimationFramesByPrefix("player_bow_full_walk");
            playerArrowFrames = LoadPlayerAnimationFrames("arrow");
            playerArrowMaxFrames = LoadPlayerAnimationFrames("arrow_max");
            playerArrowSprite = LoadOptionalPlayerSpriteFromDisk("arrow.png");
            if (playerArrowFrames.Count == 0 && playerArrowSprite != null)
                playerArrowFrames.Add(playerArrowSprite);
            if (playerArrowMaxFrames.Count == 0)
                playerArrowMaxFrames = playerArrowFrames.ToList();

            CreatePlayer();
            SetupTransition();
            InstallKeyboardHook();

            LoadArea(startStage, TransitionDirection.Right, animate: false);
            StartGameLoop();

        }

        private void LoadConfig()
        {
            string configPath = IOPath.Combine(AppContext.BaseDirectory, "gameconfig.json");
            var options = new JsonSerializerOptions { WriteIndented = true };

            try
            {
                if (!System.IO.File.Exists(configPath))
                {
                    System.IO.File.WriteAllText(configPath, JsonSerializer.Serialize(new GameConfig(), options));
                }

                var loaded = JsonSerializer.Deserialize<GameConfig>(System.IO.File.ReadAllText(configPath));
                gameConfig = loaded ?? new GameConfig();
            }
            catch
            {
                gameConfig = new GameConfig();
            }

            moveSpeed = gameConfig.MoveSpeed;
            gravity = gameConfig.Gravity;
            jumpStrength = gameConfig.JumpStrength;
            playerHitboxWidth = Math.Max(6, Math.Min(playerWidth, gameConfig.PlayerHitboxWidth));
            playerHitboxHeight = Math.Max(6, Math.Min(playerHeight, gameConfig.PlayerHitboxHeight));
        }

        private void LoadAreaTemplates()
        {
            areaTemplates.Clear();
            string path = IOPath.Combine(AppContext.BaseDirectory, "area_definitions.txt");

            if (!System.IO.File.Exists(path))
            {
                string seed =
                    "# name;stages;enemies;colorHex(optional)\n" +
                    "Plains;1-2;slime;5AAA50\n" +
                    "Plains;3-4;slime,wolf;5AAA50\n" +
                    "Cave;6-7;bat,crawler;5F5F69\n" +
                    "Cave;8-9;bat,crawler,frostling;5F5F69\n" +
                    "Firelands;11-14;bat;B85423";
                System.IO.File.WriteAllText(path, seed);
            }

            foreach (var raw in System.IO.File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var parts = line.Split(';');
                if (parts.Length < 3) continue;

                var template = new AreaTemplate
                {
                    Name = parts[0].Trim(),
                    StageRanges = ParseStageRanges(parts[1]),
                    EnemyNames = parts[2]
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .ToList(),
                    GroundColor = parts.Length >= 4 ? ParseColorHex(parts[3], Color.FromRgb(90, 170, 80)) : Color.FromRgb(90, 170, 80)
                };

                areaTemplates.Add(template);
            }
        }

        private static List<(int Min, int Max)> ParseStageRanges(string value)
        {
            var ranges = new List<(int Min, int Max)>();
            foreach (var token in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (token == "*")
                {
                    ranges.Clear();
                    return ranges;
                }

                var split = token.Split('-', StringSplitOptions.TrimEntries);
                if (split.Length == 1 && int.TryParse(split[0], out int exact))
                {
                    ranges.Add((exact, exact));
                }
                else if (split.Length == 2 &&
                         int.TryParse(split[0], out int min) &&
                         int.TryParse(split[1], out int max))
                {
                    ranges.Add((Math.Min(min, max), Math.Max(min, max)));
                }
            }
            return ranges;
        }

        private static Color ParseColorHex(string raw, Color fallback)
        {
            string value = raw.Trim().TrimStart('#');
            if (value.Length != 6)
                return fallback;
            try
            {
                byte r = Convert.ToByte(value[..2], 16);
                byte g = Convert.ToByte(value.Substring(2, 2), 16);
                byte b = Convert.ToByte(value.Substring(4, 2), 16);
                return Color.FromRgb(r, g, b);
            }
            catch
            {
                return fallback;
            }
        }

        private void LoadEnemyTemplates()
        {
            enemyTemplates.Clear();
            enemyTemplates.AddRange(new[]
            {
                new EnemyTemplate
                {
                    Name = "slime",
                    Health = 8,
                    AttackDamage = 4,
                    MoveSpeed = 0.9,
                    Level = 1,
                    AllowedBiomes = new HashSet<BiomeType> { BiomeType.Plains },
                    Width = 16,
                    Height = 28,
                    AttackHitboxWidth = 18,
                    AttackHitboxHeight = 16,
                    BehaviorIds = new List<string> { "hop_contact" },
                    BehaviorIntervalFrames = 135,
                    CollisionHitboxWidth = 21,
                    CollisionHitboxHeight = 24,
                    CollisionHitboxOffsetY = 0,
                },
                new EnemyTemplate
                {
                    Name = "bat",
                    Health = 9,
                    AttackDamage = 5,
                    MoveSpeed = 1.4,
                    Level = 2,
                    AllowedBiomes = new HashSet<BiomeType> { BiomeType.Cave },
                    Width = 32,
                    Height = 32,
                    AttackHitboxWidth = 20,
                    AttackHitboxHeight = 14,
                    BehaviorIds = new List<string> { "swoop_dive" },
                    BehaviorIntervalFrames = 42,
                    CollisionHitboxWidth = 22,
                    CollisionHitboxHeight = 14,
                    CollisionHitboxOffsetY = 9,
                },
                new EnemyTemplate
                {
                    Name = "wolf",
                    Health = 15,
                    AttackDamage = 6,
                    MoveSpeed = 1.35,
                    Level = 4,
                    AllowedBiomes = new HashSet<BiomeType> { BiomeType.Forest },
                    Width = 48,
                    Height = 46,
                    AttackHitboxWidth = 34,
                    AttackHitboxHeight = 16,
                    BehaviorIds = new List<string> { "dash_strike" },
                    BehaviorIntervalFrames = 32,
                    CollisionHitboxWidth = 32,
                    CollisionHitboxHeight = 22,
                    CollisionHitboxOffsetX = 4,
                    CollisionHitboxOffsetY = 20,
                },
                new EnemyTemplate
                {
                    Name = "crawler",
                    Health = 18,
                    AttackDamage = 16,
                    MoveSpeed = 1.1,
                    Level = 6,
                    AllowedBiomes = new HashSet<BiomeType> { BiomeType.Cave },
                    StageRanges = new List<(int Min, int Max)> { (6, 9) },
                    Width = 42,
                    Height = 42,
                    AttackHitboxWidth = 26,
                    AttackHitboxHeight = 17,
                    BehaviorIds = new List<string> { "burrow_ambush" },
                    BehaviorIntervalFrames = 42,
                    CollisionHitboxWidth = 29,
                    CollisionHitboxHeight = 20,
                    CollisionHitboxOffsetX = 7,
                    CollisionHitboxOffsetY = 20,
                },
                new EnemyTemplate
                {
                    Name = "frostling",
                    Health = 132,
                    AttackDamage = 10,
                    MoveSpeed = 0.72,
                    Level = 8,
                    AllowedBiomes = new HashSet<BiomeType> { BiomeType.Cave },
                    StageRanges = new List<(int Min, int Max)> { (8, 9) },
                    Width = 68,
                    Height = 116,
                    AttackHitboxWidth = 48,
                    AttackHitboxHeight = 92,
                    BehaviorIds = new List<string> { "ice_slam" },
                    BehaviorIntervalFrames = 46,
                    CollisionHitboxWidth = 48,
                    CollisionHitboxHeight = 106,
                    CollisionHitboxOffsetX = 10,
                    CollisionHitboxOffsetY = 8,
                },
            });
        }

        private void LoadBossTemplates()
        {
            bossTemplates.Clear();
            string path = IOPath.Combine(AppContext.BaseDirectory, "boss_definitions.txt");

            if (!System.IO.File.Exists(path))
            {
                string seed =
                    "# name;health;attackdamage;movespeed;width(optional);height(optional);behaviors(optional);behaviorintervalframes(optional)\n" +
                    "The Goo;80;20;1.05;64;64;hop_contact,dash_strike;42\n" +
                    "Fallen Knight;190;22;0.00;96;180;spike_field,snowball_heave,fire_head;44\n" +
                    "DB-5000;130;24;1.10;72;64;melee_chaser;30";
                System.IO.File.WriteAllText(path, seed);
            }

            foreach (var raw in System.IO.File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                string[] parts = line.Split(';');
                if (parts.Length < 4) continue;
                if (!int.TryParse(parts[1], out int health)) continue;
                if (!int.TryParse(parts[2], out int attackDamage)) continue;
                if (!double.TryParse(parts[3], out double moveSpeed)) continue;

                double width = 64;
                double height = 64;
                if (parts.Length >= 5 && double.TryParse(parts[4], out double parsedWidth))
                    width = parsedWidth;
                if (parts.Length >= 6 && double.TryParse(parts[5], out double parsedHeight))
                    height = parsedHeight;

                var behaviorIds = new List<string> { "melee_chaser" };
                if (parts.Length >= 7 && !string.IsNullOrWhiteSpace(parts[6]))
                {
                    var parsedBehaviors = parts[6]
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(b => b.Trim().ToLowerInvariant())
                        .Where(b => b.Length > 0)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (parsedBehaviors.Count > 0)
                        behaviorIds = parsedBehaviors;
                }

                int behaviorIntervalFrames = 40;
                if (parts.Length >= 8 && int.TryParse(parts[7], out int parsedInterval))
                    behaviorIntervalFrames = Math.Max(8, parsedInterval);

                bossTemplates.Add(new BossTemplate
                {
                    Name = parts[0].Trim(),
                    Health = Math.Max(20, health),
                    AttackDamage = Math.Max(2, attackDamage),
                    MoveSpeed = Math.Max(0.4, moveSpeed),
                    Width = Math.Max(24, width),
                    Height = Math.Max(24, height),
                    BehaviorIds = behaviorIds,
                    BehaviorIntervalFrames = behaviorIntervalFrames
                });
            }

            AreaDefinitions.SetBossTemplates(bossTemplates);
        }

        private void LoadItemTemplates()
        {
            itemTemplates.Clear();
            string itemPath = IOPath.Combine(AppContext.BaseDirectory, "item_definitions.txt");

            if (!System.IO.File.Exists(itemPath))
            {
                string seed =
                    "# name;level;cooldown\n" +
                    "Rusty Sword;2;20\n" +
                    "Copper Sword;3;18\n" +
                    "Iron Sword;4;22\n" +
                    "Steel Sword;5;18\n" +
                    "Oak Bow;2;30\n" +
                    "Hunter Bow;3;25\n" +
                    "War Bow;4;25\n" +
                    "Long Bow;5;30";
                System.IO.File.WriteAllText(itemPath, seed);
            }

            foreach (var raw in System.IO.File.ReadAllLines(itemPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                string[] parts = line.Split(';');
                if (parts.Length < 3) continue;
                if (!int.TryParse(parts[1], out int level)) continue;
                if (!int.TryParse(parts[2], out int cooldown)) continue;

                string name = parts[0].Trim();
                itemTemplates.Add(new ItemTemplate
                {
                    Name = name,
                    Level = ItemFactory.ClampWeaponLevel(level),
                    CooldownFrames = Math.Max(1, cooldown),
                    SpritePath = ResolveItemSpritePath(name)
                });
            }
        }

        private string? ResolveItemSpritePath(string itemName)
        {
            string normalized = itemName.Trim().ToLowerInvariant().Replace(" ", "_");
            string dir = IOPath.Combine(AppContext.BaseDirectory, "Assets", "Item");
            if (!System.IO.Directory.Exists(dir))
                return null;

            string direct = IOPath.Combine(dir, $"{normalized}.png");
            if (System.IO.File.Exists(direct))
                return direct;

            string firstMatch = System.IO.Directory.GetFiles(dir, $"{normalized}*.png")
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault() ?? "";
            return firstMatch.Length > 0 ? firstMatch : null;
        }

        private int LoadSaveState()
        {
            string savePath = IOPath.Combine(AppContext.BaseDirectory, "save_state.txt");
            if (!System.IO.File.Exists(savePath))
                return 0;

            try
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var raw in System.IO.File.ReadAllLines(savePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#")) continue;
                    int split = line.IndexOf('=');
                    if (split <= 0) continue;
                    values[line[..split].Trim()] = line[(split + 1)..].Trim();
                }

                playerData.Level = GetInt(values, "Level", playerData.Level);
                playerData.Experience = GetInt(values, "Experience", playerData.Experience);
                playerData.Gold = GetInt(values, "Gold", playerData.Gold);
                playerData.Health = GetInt(values, "Health", playerData.Health);
                playerData.MaxHealth = GetInt(values, "MaxHealth", playerData.MaxHealth);
                highestUnlockedStage = Math.Max(1, GetInt(values, "HighestUnlockedStage", highestUnlockedStage));

                playerData.Inventory.Clear();
                int inventoryCount = Math.Max(0, GetInt(values, "InventoryCount", 0));
                for (int i = 0; i < inventoryCount; i++)
                {
                    if (!values.TryGetValue($"Inventory{i}", out var line)) continue;
                    var entry = ParseInventoryEntry(line);
                    if (entry != null)
                        playerData.Inventory.Add(entry);
                }

                int swordIndex = GetInt(values, "EquippedSwordIndex", -1);
                int bowIndex = GetInt(values, "EquippedBowIndex", -1);
                playerData.EquippedSword = (swordIndex >= 0 && swordIndex < playerData.Inventory.Count && playerData.Inventory[swordIndex].Item is WeaponItem sw && sw.WeaponCategory == WeaponCategory.Sword)
                    ? sw : null;
                playerData.EquippedBow = (bowIndex >= 0 && bowIndex < playerData.Inventory.Count && playerData.Inventory[bowIndex].Item is WeaponItem bw && bw.WeaponCategory == WeaponCategory.Bow)
                    ? bw : null;

                int stage = Math.Max(0, GetInt(values, "CurrentStage", 0));
                return stage;
            }
            catch
            {
                return 0;
            }
        }

        private void SaveGameState()
        {
            string savePath = IOPath.Combine(AppContext.BaseDirectory, "save_state.txt");
            var lines = new List<string>
            {
                "# TaskbarRPG Save State",
                "# Editable for testing",
                $"CurrentStage={currentStageNumber}",
                $"HighestUnlockedStage={highestUnlockedStage}",
                $"Level={playerData.Level}",
                $"Experience={playerData.Experience}",
                $"Gold={playerData.Gold}",
                $"Health={playerData.Health}",
                $"MaxHealth={playerData.MaxHealth}",
                $"InventoryCount={playerData.Inventory.Count}",
            };

            for (int i = 0; i < playerData.Inventory.Count; i++)
                lines.Add($"Inventory{i}={SerializeInventoryEntry(playerData.Inventory[i])}");

            int equippedSwordIndex = playerData.Inventory.FindIndex(e => ReferenceEquals(e.Item, playerData.EquippedSword));
            int equippedBowIndex = playerData.Inventory.FindIndex(e => ReferenceEquals(e.Item, playerData.EquippedBow));
            lines.Add($"EquippedSwordIndex={equippedSwordIndex}");
            lines.Add($"EquippedBowIndex={equippedBowIndex}");

            System.IO.File.WriteAllLines(savePath, lines);
        }

        private void ResetProgress()
        {
            stageAreas.Clear();
            previousArea = null;
            highestUnlockedStage = 1;

            playerData.Level = 1;
            playerData.Experience = 0;
            playerData.Gold = 10;
            playerData.MaxHealth = 100;
            playerData.Health = 100;
            playerData.Inventory.Clear();
            playerData.EquippedSword = null;
            playerData.EquippedBow = null;
            InitializePlayerData();

            CloseAllPanels();
            LoadArea(0, TransitionDirection.Right, animate: false);
            SaveGameState();
            ShowStatus("Progress reset.", 90);
        }

        private static int GetInt(Dictionary<string, string> values, string key, int fallback)
        {
            return values.TryGetValue(key, out var raw) && int.TryParse(raw, out int parsed)
                ? parsed
                : fallback;
        }

        private static int NormalizeLoadedWeaponLevel(string name, int storedValue)
        {
            if (name.Equals("Old Sword", StringComparison.OrdinalIgnoreCase))
                return 1;

            return ItemFactory.ClampWeaponLevel(storedValue);
        }

        private static int NormalizeLoadedBasePrice(ItemKind kind, string name, int storedValue)
        {
            if (kind == ItemKind.Weapon)
            {
                if (name.Equals("Old Sword", StringComparison.OrdinalIgnoreCase) ||
                    name.Equals("Simple Bow", StringComparison.OrdinalIgnoreCase))
                    return 50;
            }

            return Math.Max(1, storedValue);
        }

        private static string SerializeInventoryEntry(InventoryEntry entry)
        {
            string basePart = $"{entry.Item.Kind}|{entry.Item.Name.Replace("|", "")}|{entry.Quantity}|{entry.Item.BasePrice}";
            return entry.Item switch
            {
                WeaponItem w => $"{basePart}|{w.WeaponCategory}|{w.Level}|{w.CooldownFrames}",
                ConsumableItem c => $"{basePart}|{c.HealAmount}",
                AmmoItem a => $"{basePart}|{a.AmmoType.Replace("|", "")}",
                _ => basePart
            };
        }

        private static InventoryEntry? ParseInventoryEntry(string line)
        {
            string[] parts = line.Split('|');
            if (parts.Length < 4) return null;

            if (!Enum.TryParse<ItemKind>(parts[0], out var kind)) return null;
            string name = parts[1];
            if (!int.TryParse(parts[2], out int qty)) return null;
            if (!int.TryParse(parts[3], out int basePrice)) return null;
            int normalizedBasePrice = NormalizeLoadedBasePrice(kind, name, basePrice);

            ItemBase item = kind switch
            {
                ItemKind.Weapon when parts.Length >= 6 &&
                    Enum.TryParse<WeaponCategory>(parts[4], out var category) &&
                    int.TryParse(parts[5], out int level) =>
                    new WeaponItem
                    {
                        Name = name,
                        BasePrice = normalizedBasePrice,
                        WeaponCategory = category,
                        Level = NormalizeLoadedWeaponLevel(name, level),
                        CooldownFrames = parts.Length >= 7 && int.TryParse(parts[6], out int cd) ? cd : 10
                    },
                ItemKind.Consumable when parts.Length >= 5 &&
                    int.TryParse(parts[4], out int heal) =>
                    new ConsumableItem { Name = name, BasePrice = normalizedBasePrice, HealAmount = heal },
                ItemKind.Ammo when parts.Length >= 5 =>
                    new AmmoItem { Name = name, BasePrice = normalizedBasePrice, AmmoType = parts[4] },
                _ => new AmmoItem { Name = name, BasePrice = normalizedBasePrice, AmmoType = "Arrow" }
            };

            return new InventoryEntry { Item = item, Quantity = Math.Max(1, qty) };
        }

        private void InitializePlayerData()
        {
            var oldSword = ItemFactory.CreateOldSword();
            var starterBow = ItemFactory.CreateStarterBow();

            playerData.EquippedSword = oldSword;
            playerData.EquippedBow = starterBow;

            AddItemToInventory(oldSword, 1);
            AddItemToInventory(starterBow, 1);
            AddItemToInventory(ItemFactory.CreatePotion(), 2);
            AddItemToInventory(ItemFactory.CreateArrowItem(), 10);
        }

        private void MainWindow_Closed(object? sender, EventArgs e)
        {
            // Stop the game loop first so no further callbacks fire
            timer?.Stop();
            SaveGameState();

            if (keyboardHook != IntPtr.Zero)
            {
                UnhookWindowsHookEx(keyboardHook);
                keyboardHook = IntPtr.Zero;
            }
        }

        // -----------------------------------------------------------------------
        // Keyboard hook
        // -----------------------------------------------------------------------
        private void InstallKeyboardHook()
        {
            keyboardProc = KeyboardHookCallback;

            using Process curProcess = Process.GetCurrentProcess();
            using ProcessModule? curModule = curProcess.MainModule;
            IntPtr moduleHandle = GetModuleHandle(curModule?.ModuleName);

            keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, keyboardProc, moduleHandle, 0);
        }

        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msg = wParam.ToInt32();
                bool isKeyDown = msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN;
                bool isKeyUp = msg == WM_KEYUP || msg == WM_SYSKEYUP;

                if (isKeyDown || isKeyUp)
                {
                    KBDLLHOOKSTRUCT kb = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    int vk = (int)kb.vkCode;
                    bool down = isKeyDown;

                    // F8 toggles the game regardless of controlsEnabled
                    if (isKeyDown && vk == VK_F8)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            controlsEnabled = !controlsEnabled;
                            ShowStatus(controlsEnabled ? "GAME ON" : "GAME OFF", 50);

                            if (!controlsEnabled)
                            {
                                CloseAllPanels();
                                ResetHeldInputs();
                            }
                        }));

                        return (IntPtr)1; // block F8 from reaching other apps
                    }

                    if (controlsEnabled)
                    {
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            SetHeldKeyState(vk, down);
                            if (isKeyDown)
                            {
                                HandleBossSkipSequenceInput(vk);
                                HandleDebugCheatSequenceInput(vk);
                            }
                        }));

                        if (ShouldBlockKey(vk))
                            return (IntPtr)1;
                    }
                }
            }

            return CallNextHookEx(keyboardHook, nCode, wParam, lParam);
        }

        private void SetHeldKeyState(int vk, bool isDown)
        {
            switch (vk)
            {
                case VK_LEFT: case VK_A: leftHeld = isDown; break;
                case VK_RIGHT: case VK_D: rightHeld = isDown; break;
                case VK_UP:
                case VK_W:
                case VK_SPACE:
                    jumpHeld = isDown; break;
                case VK_DOWN:
                    downHeld = isDown; break;
                case VK_Z: meleeHeld = isDown; break;
                case VK_X: fireHeld = isDown; break;
                case VK_C: closeHeld = isDown; break;
                case VK_ESCAPE: escapeHeld = isDown; break;
                case VK_RETURN: enterHeld = isDown; break;
                case VK_BACK: backspaceHeld = isDown; break;
                case VK_F: fastTravelHeld = isDown; break;
                case VK_E: statsHeld = isDown; break;
                case VK_M: mapHeld = isDown; break;
                case VK_OEM_2: potionHeld = isDown; break;
                case VK_0: zeroHeld = isDown; break;
                case VK_1: numberHeld[0] = isDown; break;
                case VK_2: numberHeld[1] = isDown; break;
                case VK_3: numberHeld[2] = isDown; break;
                case VK_4: numberHeld[3] = isDown; break;
                case VK_5: numberHeld[4] = isDown; break;
                case VK_6: numberHeld[5] = isDown; break;
                case VK_7: numberHeld[6] = isDown; break;
                case VK_8: numberHeld[7] = isDown; break;
                case VK_9: numberHeld[8] = isDown; break;
            }
        }

        private bool ShouldBlockKey(int vk)
        {
            switch (vk)
            {
                case VK_BACK:
                case VK_RETURN:
                case VK_LEFT:
                case VK_UP:
                case VK_RIGHT:
                case VK_DOWN:
                case VK_SPACE:
                case VK_0:
                case VK_A:
                case VK_C:
                case VK_ESCAPE:
                case VK_D:
                case VK_E:
                case VK_F:
                case VK_M:
                case VK_OEM_2:
                case VK_S:
                case VK_W:
                case VK_X:
                case VK_Z:
                case VK_1:
                case VK_2:
                case VK_3:
                case VK_4:
                case VK_5:
                case VK_6:
                case VK_7:
                case VK_8:
                case VK_9:
                    return true;
                default:
                    return false;
            }
        }

        private void ResetHeldInputs()
        {
            CancelBowCharge();
            bossSkipSequenceIndex = 0;
            debugCheatSequenceIndex = 0;
            leftHeld = rightHeld = jumpHeld = meleeHeld = fireHeld =
            downHeld = closeHeld = escapeHeld = fastTravelHeld = statsHeld = mapHeld = potionHeld =
            zeroHeld = enterHeld = backspaceHeld = false;

            jumpHeldLastFrame = meleeHeldLastFrame = fireHeldLastFrame =
            closeHeldLastFrame = escapeHeldLastFrame = fastTravelHeldLastFrame = statsHeldLastFrame =
            mapHeldLastFrame = potionHeldLastFrame = zeroHeldLastFrame =
            enterHeldLastFrame = backspaceHeldLastFrame = false;

            for (int i = 0; i < numberHeld.Length; i++)
            {
                numberHeld[i] = false;
                numberHeldLastFrame[i] = false;
            }
        }

        private void HandleBossSkipSequenceInput(int vk)
        {
            if (vk != VK_UP && vk != VK_DOWN && vk != VK_LEFT && vk != VK_RIGHT)
                return;

            if (vk == BossSkipSequence[bossSkipSequenceIndex])
            {
                bossSkipSequenceIndex++;
                if (bossSkipSequenceIndex >= BossSkipSequence.Length)
                {
                    bossSkipSequenceIndex = 0;
                    OpenSecretStageSelector();
                }
            }
            else
            {
                bossSkipSequenceIndex = vk == BossSkipSequence[0] ? 1 : 0;
            }
        }

        private void HandleDebugCheatSequenceInput(int vk)
        {
            if (vk != VK_W && vk != VK_S && vk != VK_A && vk != VK_D)
                return;

            if (vk == DebugCheatSequence[debugCheatSequenceIndex])
            {
                debugCheatSequenceIndex++;
                if (debugCheatSequenceIndex >= DebugCheatSequence.Length)
                {
                    debugCheatSequenceIndex = 0;
                    OpenDebugCheatMenu();
                }
            }
            else
            {
                debugCheatSequenceIndex = vk == DebugCheatSequence[0] ? 1 : 0;
            }
        }

        private bool HasOutOfCombatMoveSpeedBoost()
            => currentArea.Type == AreaType.Town || (currentStageNumber > 0 && activeEnemies.Count == 0);

        private double GetCurrentPlayerMoveSpeed()
            => moveSpeed * (HasOutOfCombatMoveSpeedBoost() ? OutOfCombatMoveSpeedMultiplier : 1.0);

        private List<int> GetUnlockedFastTravelStages()
        {
            return Enumerable.Range(1, Math.Max(0, highestUnlockedStage))
                .Where(AreaDefinitions.CanGenerateStage)
                .ToList();
        }

        private List<int> GetCheatFastTravelStages()
        {
            return Enumerable.Range(1, Math.Max(0, AreaDefinitions.GetHighestConfiguredStage()))
                .Where(AreaDefinitions.CanGenerateStage)
                .ToList();
        }

        private List<int> GetCurrentFastTravelStages()
            => fastTravelCheatMode ? GetCheatFastTravelStages() : GetUnlockedFastTravelStages();

        private int GetFastTravelPageCount()
        {
            const int stagesPerPage = 7;
            int totalStages = GetCurrentFastTravelStages().Count;
            return Math.Max(1, (int)Math.Ceiling(totalStages / (double)stagesPerPage));
        }

        private void PrepareFastTravelSelectorPage(int? preferredStage = null)
        {
            const int stagesPerPage = 7;
            var stages = GetCurrentFastTravelStages();
            if (stages.Count == 0)
            {
                fastTravelPageIndex = 0;
                return;
            }

            int stageToFocus = preferredStage ?? (currentStageNumber > 0 ? currentStageNumber : highestUnlockedStage);
            int stageIndex = stages.FindIndex(stage => stage == stageToFocus);
            if (stageIndex < 0)
                stageIndex = Math.Max(0, stages.Count - 1);

            fastTravelPageIndex = stageIndex / stagesPerPage;
            fastTravelPageIndex = Math.Clamp(fastTravelPageIndex, 0, GetFastTravelPageCount() - 1);
        }

        private void OpenSecretStageSelector()
        {
            if (GetCheatFastTravelStages().Count == 0)
            {
                ShowStatus("No stages are configured for cheat travel.", 120);
                return;
            }

            CloseAllPanels();
            ResetHeldInputs();
            velocityX = 0;
            velocityY = 0;
            fastTravelCheatMode = true;
            panelMode = PanelMode.FastTravel;
            panelBorder.Visibility = Visibility.Visible;
            activeShop = null;
            PrepareFastTravelSelectorPage();
            RenderCurrentPanel();
            ShowStatus("Cheat stage selector opened.", 100);
        }

        private void OpenDebugCheatMenu()
        {
            CloseAllPanels();
            ResetHeldInputs();
            velocityX = 0;
            velocityY = 0;
            panelMode = PanelMode.DebugCheatMenu;
            panelBorder.Visibility = Visibility.Visible;
            activeShop = null;
            debugCheatLevelInput = "";
            RenderCurrentPanel();
            ShowStatus("Testing cheat menu opened.", 100);
        }

        // -----------------------------------------------------------------------
        // Window positioning — DPI-aware fix
        // -----------------------------------------------------------------------
        private void PositionAboveTaskbar()
        {
            APPBARDATA data = new APPBARDATA();
            data.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
            int result = SHAppBarMessage(ABM_GETTASKBARPOS, ref data);

            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;

            // Convert physical RECT pixels → logical (WPF) pixels using DPI scale
            var source = PresentationSource.FromVisual(this);
            double dpiScaleX = source?.CompositionTarget?.TransformFromDevice.M11 ?? 1.0;
            double dpiScaleY = source?.CompositionTarget?.TransformFromDevice.M22 ?? 1.0;

            if (result != 0)
            {
                double left = data.rc.left * dpiScaleX;
                double top = data.rc.top * dpiScaleY;
                double right = data.rc.right * dpiScaleX;
                double bottom = data.rc.bottom * dpiScaleY;

                double taskbarWidth = right - left;
                double taskbarHeight = bottom - top;

                if (taskbarWidth >= screenWidth * 0.5) // Horizontal taskbar
                {
                    if (top >= screenHeight * 0.5) // Bottom (most common)
                    {
                        Left = 0;
                        Top = top - playAreaHeight;
                        Width = screenWidth;
                        Height = playAreaHeight;
                    }
                    else // Top taskbar
                    {
                        Left = 0;
                        Top = bottom;
                        Width = screenWidth;
                        Height = playAreaHeight;
                    }
                }
                else // Vertical taskbar
                {
                    if (left < screenWidth * 0.5) // Left side
                    {
                        Left = right;
                        Top = 0;
                        Width = screenWidth - right;
                        Height = screenHeight;
                    }
                    else // Right side
                    {
                        Left = 0;
                        Top = 0;
                        Width = left;
                        Height = screenHeight;
                    }
                }
            }
            else // Fallback: assume bottom taskbar at default height
            {
                Left = 0;
                Top = screenHeight - 80 - playAreaHeight;
                Width = screenWidth;
                Height = playAreaHeight;
            }
        }

        private void MakeClickThrough()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int extStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
        }

        // -----------------------------------------------------------------------
        // UI creation
        // -----------------------------------------------------------------------
        private void CreateBackground()
        {
            groundRect = new Rectangle
            {
                Width = Width,
                Height = groundStripHeight,
                Fill = Brushes.Transparent,
            };

            GameCanvas.Children.Add(groundRect);
            Panel.SetZIndex(groundRect, 0);

            Canvas.SetLeft(groundRect, 0);
            Canvas.SetTop(groundRect, Height - groundStripHeight);
        }

        private void ApplyReadableTextStyle(TextBlock textBlock)
        {
            textBlock.Background = Brushes.Transparent;
            textBlock.Padding = new Thickness(0);
            textBlock.FontWeight = FontWeights.Bold;
            textBlock.Effect = new DropShadowEffect
            {
                Color = Colors.Black,
                ShadowDepth = 0,
                BlurRadius = 3,
                Opacity = 1.0
            };
            textBlock.FontSize += 3;
        }

        private void CreateHud()
        {
            playerHealthBg = new Rectangle
            {
                Width = 44,
                Height = 6,
                Fill = Brushes.Black,
                Opacity = 0.7
            };

            playerHealthFill = new Rectangle
            {
                Width = 44,
                Height = 6,
                Fill = Brushes.LimeGreen
            };

            playerHealthText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 9,
                FontFamily = new FontFamily("Consolas"),
                Width = 60,
                TextAlignment = TextAlignment.Center
            };
            ApplyReadableTextStyle(playerHealthText);

            playerArrowText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 9,
                FontFamily = new FontFamily("Consolas"),
                Width = 60,
                TextAlignment = TextAlignment.Center
            };
            ApplyReadableTextStyle(playerArrowText);

            statusText = new TextBlock
            {
                Foreground = Brushes.Gold,
                FontSize = 12,
                FontFamily = new FontFamily("Consolas"),
                Width = 420,
                TextAlignment = TextAlignment.Center,
            };
            ApplyReadableTextStyle(statusText);

            GameCanvas.Children.Add(playerHealthBg);
            GameCanvas.Children.Add(playerHealthFill);
            GameCanvas.Children.Add(playerHealthText);
            GameCanvas.Children.Add(playerArrowText);
            GameCanvas.Children.Add(statusText);

            Panel.SetZIndex(playerHealthBg, 40);
            Panel.SetZIndex(playerHealthFill, 41);
            Panel.SetZIndex(playerHealthText, 42);
            Panel.SetZIndex(playerArrowText, 42);
            Panel.SetZIndex(statusText, 70);
        }

        private void CreateMainPanel()
        {
            panelText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                TextWrapping = TextWrapping.NoWrap,
                Margin = new Thickness(5, 3, 5, 3),
            };
            ApplyReadableTextStyle(panelText);

            panelBorder = new Border
            {
                Width = 720,
                Background = new SolidColorBrush(Color.FromArgb(228, 18, 18, 18)),
                BorderBrush = Brushes.Gold,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Child = panelText,
                Visibility = Visibility.Hidden,
            };

            GameCanvas.Children.Add(panelBorder);
            Panel.SetZIndex(panelBorder, 60);

            Canvas.SetLeft(panelBorder, 300);
            Canvas.SetTop(panelBorder, 2);
        }

        // Shrink font until all lines fit within the window height.
        // Must be called after panelText.Text is set so WPF can measure real content.
        private void FitPanelText()
        {
            double maxH = Height - 6;
            double maxW = panelBorder.Width - 12;

            for (double size = 12.0; size >= 7.5; size -= 0.5)
            {
                panelText.FontSize = size;
                panelText.Measure(new Size(maxW, double.PositiveInfinity));
                if (panelText.DesiredSize.Height <= maxH)
                    break;
            }
        }

        private void CreateEdgeTexts()
        {
            leftExitText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Width = 180,
                TextAlignment = TextAlignment.Left
            };
            ApplyReadableTextStyle(leftExitText);

            rightExitText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Width = 180,
                TextAlignment = TextAlignment.Right
            };
            ApplyReadableTextStyle(rightExitText);

            GameCanvas.Children.Add(leftExitText);
            GameCanvas.Children.Add(rightExitText);

            Panel.SetZIndex(leftExitText, 30);
            Panel.SetZIndex(rightExitText, 30);
        }

        private void UpdateExitTexts()
        {
            if (currentStageNumber == 0)
            {
                leftExitText.Text = "";
                int displayStage = highestUnlockedStage;
                while (displayStage > 1 && !AreaDefinitions.CanGenerateStage(displayStage))
                    displayStage--;

                if (AreaDefinitions.CanGenerateStage(displayStage))
                {
                    rightExitText.Text = $"Stage {displayStage} >";
                    rightExitText.Foreground = Brushes.White;
                }
                else
                {
                    rightExitText.Text = "Victory!";
                    rightExitText.Foreground = Brushes.Gold;
                }
            }
            else
            {
                leftExitText.Text = "< Town";
                leftExitText.Foreground = Brushes.White;

                if (activeEnemies.Count > 0)
                {
                    rightExitText.Text = currentArea.IsBossArea ? "Boss alive >" : "Clear area >";
                    rightExitText.Foreground = Brushes.OrangeRed;
                }
                else
                {
                    bool canAdvance = AreaDefinitions.CanGenerateStage(currentStageNumber + 1);
                    rightExitText.Text = currentArea.IsBossArea
                        ? "Town >"
                        : canAdvance ? $"Stage {currentStageNumber + 1} >" : "Victory!";
                    rightExitText.Foreground = canAdvance || currentArea.IsBossArea ? Brushes.White : Brushes.Gold;
                }
            }
        }

        private void CreatePlayer()
        {
            player = new Image
            {
                Width = playerWidth,
                Height = playerHeight,
                Stretch = Stretch.Fill,
                Source = playerIdleFrames[0],
            };

            RenderOptions.SetBitmapScalingMode(player, BitmapScalingMode.NearestNeighbor);

            playerLevelUpPulseOverlay = new Rectangle
            {
                Width = playerWidth,
                Height = playerHeight,
                Fill = new SolidColorBrush(Color.FromRgb(92, 255, 122)),
                Opacity = 0,
                Visibility = Visibility.Hidden,
                IsHitTestVisible = false
            };
            RenderOptions.SetBitmapScalingMode(playerLevelUpPulseOverlay, BitmapScalingMode.NearestNeighbor);

            attackHitbox = new Rectangle
            {
                Width = 20,
                Height = 12,
                Fill = gameConfig.Debug ? new SolidColorBrush(Color.FromArgb(70, 255, 0, 0)) : Brushes.Transparent,
                Visibility = gameConfig.Debug ? Visibility.Visible : Visibility.Hidden,
                Stroke = gameConfig.Debug ? Brushes.Red : null,
                StrokeThickness = gameConfig.Debug ? 1 : 0,
            };

            playerHitboxDebug = new Rectangle
            {
                Width = playerHitboxWidth,
                Height = playerHitboxHeight,
                Fill = Brushes.Transparent,
                Stroke = Brushes.DeepSkyBlue,
                StrokeThickness = 1,
                Visibility = gameConfig.Debug ? Visibility.Visible : Visibility.Hidden,
            };

            GameCanvas.Children.Add(player);
            GameCanvas.Children.Add(playerLevelUpPulseOverlay);
            GameCanvas.Children.Add(playerHitboxDebug);
            GameCanvas.Children.Add(attackHitbox);

            Panel.SetZIndex(player, 20);
            Panel.SetZIndex(playerLevelUpPulseOverlay, 21);
            Panel.SetZIndex(playerHitboxDebug, 22);
            Panel.SetZIndex(attackHitbox, 23);

            groundY = Height - groundStripHeight - playerHeight;
            playerY = groundY;
            isOnGround = true;

            DrawPlayer();
            DrawAttackHitbox();
        }

        // -----------------------------------------------------------------------
        // Game loop
        // -----------------------------------------------------------------------
        private void SetupTransition()
        {
            transition = new AreaTransition(GameCanvas, OnTransitionMidpoint);
        }

        private void StartGameLoop()
        {
            timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            timer.Tick += GameLoop;
            timer.Start();
        }

        private void GameLoop(object? sender, EventArgs e)
        {
            animationFrameCounter++;
            RefreshLayoutSizedElements();
            HandleInput();

            if (transition.IsActive)
            {
                DrawPlayer();
                DrawAttackHitbox();
                return;
            }

            if (panelMode == PanelMode.None)
            {
                ApplyPhysics();
                ResolveCollisions();
                UpdatePlayerAnimationState();
                UpdateEnemies();
                UpdateEnemyHazards();
                UpdateEnemyProjectiles();
                UpdateProjectiles();
                HandleEnemyContactWithPlayer();
                HandleEnemyHazardContactWithPlayer();
                HandleEnemyProjectileContactWithPlayer();
                CheckAreaTransition();
                UpdateAttack();
            }
            else
            {
                velocityX = 0;
                if (!isAttacking)
                    attackHitbox.Visibility = gameConfig.Debug ? Visibility.Visible : Visibility.Hidden;
            }

            currentInteractableZone = FindInteractableZoneInRange();
            mapPulseTime += 0.08;

            if (playerDamageCooldownFrames > 0)
                playerDamageCooldownFrames--;
            if (meleeCooldownFrames > 0)
                meleeCooldownFrames--;
            if (bowCooldownFrames > 0)
                bowCooldownFrames--;
            if (levelUpPulseFramesRemaining > 0)
                levelUpPulseFramesRemaining--;

            if (statusFramesRemaining > 0)
                statusFramesRemaining--;
            else
                statusText.Text = "";

            if (panelMode != PanelMode.None)
                RenderCurrentPanel();

            if (playerData.Health <= 0)
            {
                HandlePlayerDeath();
                return;
            }

            UpdateExitTexts();
            DrawPlayer();
            DrawAttackHitbox();
            DrawPlayerHud();
            UpdateNpcAnimations();
            DrawInteractionIndicators();
            DrawEnemies();
            DrawEnemyHazards();
            DrawEnemyProjectiles();
            DrawProjectiles();
        }

        private void HandlePlayerDeath()
        {
            int previousGold = playerData.Gold;
            int checkpointStage = currentStageNumber > 0
                ? (((Math.Max(1, currentStageNumber) - 1) / 5) * 5) + 1
                : 1;

            while (checkpointStage > 1 && !AreaDefinitions.CanGenerateStage(checkpointStage))
                checkpointStage--;
            checkpointStage = Math.Max(1, checkpointStage);

            playerData.Gold = Math.Max(0, (int)Math.Floor(previousGold * 0.7));
            playerData.Experience = Math.Max(0, (int)Math.Ceiling(playerData.Experience * 0.5));
            playerData.Health = playerData.MaxHealth;

            highestUnlockedStage = checkpointStage;

            CloseAllPanels();
            ResetHeldInputs();
            LoadArea(0, TransitionDirection.Left, animate: false);
            ShowStatus($"Player died (so sad). Lost 30% gold and 50% XP progress. Frontier reset to Stage {checkpointStage}.", 150);
        }

        private void RefreshLayoutSizedElements()
        {
            groundRect.Width = Width;
            Canvas.SetTop(groundRect, Height - groundStripHeight);
            groundY = Height - groundStripHeight - playerHeight;
            LayoutGroundDecorations();

            double sideMargin = 20;
            double maxPanelWidth = Math.Max(260, Width - (sideMargin * 2));

            panelBorder.Width = Math.Min(720, maxPanelWidth);
            panelBorder.MaxHeight = Height - 4;   // never overflow the window

            Canvas.SetLeft(panelBorder, Math.Max(sideMargin, (Width - panelBorder.Width) / 2));
            Canvas.SetTop(panelBorder, 2);

            Canvas.SetLeft(leftExitText, 10);
            Canvas.SetTop(leftExitText, 12);

            Canvas.SetLeft(rightExitText, Width - 190);
            Canvas.SetTop(rightExitText, 12);

            Canvas.SetLeft(statusText, Math.Max(20, (Width - statusText.Width) / 2));
            Canvas.SetTop(statusText, Math.Max(4, Height - 28));
        }

        // -----------------------------------------------------------------------
        // Input handling
        // -----------------------------------------------------------------------
        private void HandleInput()
        {
            bool jumpPressedThisFrame = jumpHeld && !jumpHeldLastFrame;
            bool meleePressedThisFrame = meleeHeld && !meleeHeldLastFrame;
            bool firePressedThisFrame = fireHeld && !fireHeldLastFrame;
            bool fireReleasedThisFrame = !fireHeld && fireHeldLastFrame;
            bool closePressedThisFrame = closeHeld && !closeHeldLastFrame;
            bool escapePressedThisFrame = escapeHeld && !escapeHeldLastFrame;
            bool fastPressedThisFrame = fastTravelHeld && !fastTravelHeldLastFrame;
            bool statsPressedThisFrame = statsHeld && !statsHeldLastFrame;
            bool mapPressedThisFrame = mapHeld && !mapHeldLastFrame;
            bool potionPressedThisFrame = potionHeld && !potionHeldLastFrame;

            if (!controlsEnabled)
            {
                velocityX = 0;
                CancelBowCharge();
                CacheLastFrameInput();
                return;
            }

            if (escapePressedThisFrame)
            {
                if (panelMode == PanelMode.SystemMenu || panelMode == PanelMode.ResetConfirm)
                {
                    CloseAllPanels();
                }
                else
                {
                    panelMode = PanelMode.SystemMenu;
                    panelBorder.Visibility = Visibility.Visible;
                    RenderCurrentPanel();
                }
                CacheLastFrameInput();
                return;
            }

            if (closePressedThisFrame)
            {
                if (panelMode != PanelMode.None)
                {
                    CloseAllPanels();
                    CacheLastFrameInput();
                    return;
                }
            }

            if (panelMode != PanelMode.None && isBowCharging)
                CancelBowCharge();

            if (fastPressedThisFrame) { TogglePanel(PanelMode.FastTravel); CacheLastFrameInput(); return; }
            if (statsPressedThisFrame) { TogglePanel(PanelMode.Stats); CacheLastFrameInput(); return; }
            if (mapPressedThisFrame) { TogglePanel(PanelMode.Map); CacheLastFrameInput(); return; }

            if (potionPressedThisFrame && panelMode == PanelMode.None)
                UsePotion();

            // Panel-specific input routing
            if (panelMode == PanelMode.FastTravel)
            {
                HandleFastTravelSelection();
                CacheLastFrameInput();
                return;
            }

            if (panelMode == PanelMode.SystemMenu || panelMode == PanelMode.ResetConfirm)
            {
                HandleSystemMenuSelection();
                CacheLastFrameInput();
                return;
            }

            if (panelMode == PanelMode.ShopMain ||
                panelMode == PanelMode.ShopBuy ||
                panelMode == PanelMode.ShopSell)
            {
                HandleShopSelection();
                CacheLastFrameInput();
                return;
            }

            if (panelMode == PanelMode.DebugCheatMenu ||
                panelMode == PanelMode.DebugCheatSetLevel ||
                panelMode == PanelMode.DebugCheatSetItems)
            {
                HandleDebugCheatSelection();
                CacheLastFrameInput();
                return;
            }

            if (panelMode == PanelMode.Stats)
            {
                HandleStatsSelection();
                CacheLastFrameInput();
                return;
            }

            if (panelMode == PanelMode.EquipSword || panelMode == PanelMode.EquipBow)
            {
                HandleEquipSelection();
                CacheLastFrameInput();
                return;
            }

            if (panelMode == PanelMode.Map)
            {
                velocityX = 0;
                CacheLastFrameInput();
                return;
            }

            // Free movement
            double currentMoveSpeed = GetCurrentPlayerMoveSpeed();
            velocityX = 0;
            if (leftHeld && !rightHeld) { velocityX = -currentMoveSpeed; facingRight = false; }
            if (rightHeld && !leftHeld) { velocityX = currentMoveSpeed; facingRight = true; }

            if (jumpPressedThisFrame && isOnGround)
            {
                velocityY = jumpStrength;
                isOnGround = false;
            }

            if (meleePressedThisFrame)
            {
                bool wantsDownAttack = downHeld;
                if (wantsDownAttack)
                {
                    if (!isAttacking && meleeCooldownFrames <= 0)
                        StartMeleeAttack(isDownwardAttack: true);
                }
                else
                {
                    var interactable = FindInteractableZoneInRange();
                    if (interactable != null)
                        InteractWithZone(interactable);
                    else if (!isAttacking && meleeCooldownFrames <= 0)
                        StartMeleeAttack(isDownwardAttack: false);
                }
            }

            if (panelMode == PanelMode.None)
                UpdateBowChargeInput(firePressedThisFrame, fireReleasedThisFrame);
            else if (isBowCharging)
                CancelBowCharge();

            CacheLastFrameInput();
        }

        private void CacheLastFrameInput()
        {
            jumpHeldLastFrame = jumpHeld;
            meleeHeldLastFrame = meleeHeld;
            fireHeldLastFrame = fireHeld;
            closeHeldLastFrame = closeHeld;
            escapeHeldLastFrame = escapeHeld;
            fastTravelHeldLastFrame = fastTravelHeld;
            statsHeldLastFrame = statsHeld;
            mapHeldLastFrame = mapHeld;
            potionHeldLastFrame = potionHeld;
            zeroHeldLastFrame = zeroHeld;
            enterHeldLastFrame = enterHeld;
            backspaceHeldLastFrame = backspaceHeld;

            for (int i = 0; i < numberHeld.Length; i++)
                numberHeldLastFrame[i] = numberHeld[i];
        }

        // -----------------------------------------------------------------------
        // Panel management
        // -----------------------------------------------------------------------
        private void TogglePanel(PanelMode mode)
        {
            if (panelMode == mode)
            {
                CloseAllPanels();
            }
            else
            {
                panelMode = mode;
                panelBorder.Visibility = Visibility.Visible;
                activeShop = null;
                if (mode == PanelMode.FastTravel)
                {
                    fastTravelCheatMode = false;
                    PrepareFastTravelSelectorPage();
                }
                RenderCurrentPanel();
            }
        }

        private void CloseAllPanels()
        {
            panelMode = PanelMode.None;
            panelBorder.Visibility = Visibility.Hidden;
            panelText.Text = "";
            activeShop = null;
            fastTravelCheatMode = false;
            debugCheatLevelInput = "";
        }

        private void RenderCurrentPanel()
        {
            switch (panelMode)
            {
                case PanelMode.SystemMenu: RenderSystemMenuPanel(); break;
                case PanelMode.ResetConfirm: RenderResetConfirmPanel(); break;
                case PanelMode.FastTravel: RenderFastTravelPanel(); break;
                case PanelMode.Stats: RenderStatsPanel(); break;
                case PanelMode.Map: RenderMapPanel(); break;
                case PanelMode.ShopMain: RenderShopMainPanel(); break;
                case PanelMode.ShopBuy: RenderShopBuyPanel(); break;
                case PanelMode.ShopSell: RenderShopSellPanel(); break;
                case PanelMode.EquipSword: RenderEquipSwordPanel(); break;
                case PanelMode.EquipBow: RenderEquipBowPanel(); break;
                case PanelMode.DebugCheatMenu: RenderDebugCheatMenuPanel(); break;
                case PanelMode.DebugCheatSetLevel: RenderDebugCheatSetLevelPanel(); break;
                case PanelMode.DebugCheatSetItems: RenderDebugCheatSetItemsPanel(); break;
            }
        }

        // -----------------------------------------------------------------------
        // Panel renderers
        // -----------------------------------------------------------------------
        private void RenderSystemMenuPanel()
        {
            panelText.Text =
                "SYSTEM MENU\n" +
                "\n" +
                "1. Save\n" +
                "2. Save + Exit\n" +
                "3. Reset Progress\n" +
                "\n" +
                "ESC/C = close";
            FitPanelText();
        }

        private void RenderResetConfirmPanel()
        {
            panelText.Text =
                "RESET PROGRESS?\n" +
                "\n" +
                "1. No (back)\n" +
                "2. Yes (reset)\n" +
                "\n" +
                "ESC/C = cancel";
            FitPanelText();
        }

        private void RenderFastTravelPanel()
        {
            const int stagesPerPage = 7;
            var availableStages = GetCurrentFastTravelStages();
            int pageCount = GetFastTravelPageCount();
            fastTravelPageIndex = Math.Clamp(fastTravelPageIndex, 0, pageCount - 1);
            var pageStages = availableStages
                .Skip(fastTravelPageIndex * stagesPerPage)
                .Take(stagesPerPage)
                .ToList();

            var lines = new List<string>
            {
                fastTravelCheatMode ? "STAGE SELECTOR (CHEAT)" : "FAST TRAVEL",
                ""
            };

            string townMarker = currentStageNumber == 0 ? "  *" : "";
            lines.Add($"1. Town{townMarker}");

            for (int i = 0; i < pageStages.Count; i++)
            {
                int stage = pageStages[i];
                string marker = currentStageNumber == stage
                    ? "  *"
                    : stage == highestUnlockedStage ? "  (frontier)" : "";
                lines.Add($"{i + 2}. Stage {stage}{marker}");
            }

            if (pageCount > 1)
                lines.Add($"9. Next Page  ({fastTravelPageIndex + 1}/{pageCount})");

            lines.Add("");
            lines.Add($"Current: {(currentStageNumber == 0 ? "Town" : $"Stage {currentStageNumber}")}");
            lines.Add("Choose # to travel  C = close");
            if (pageCount > 1)
                lines.Add("9 = cycle pages");

            panelText.Text = string.Join(Environment.NewLine, lines);
            FitPanelText();
        }

        private void RenderStatsPanel()
        {
            string swordText = playerData.EquippedSword?.GetDisplayText() ?? "None";
            string bowText = playerData.EquippedBow?.GetDisplayText() ?? "None";
            string items = GetInventorySummary();

            panelText.Text =
                "PLAYER STATS\n" +
                $"Lv:{playerData.Level}  XP:{playerData.Experience}/{playerData.NextLevelXp}  HP:{playerData.Health}/{playerData.MaxHealth}\n" +
                $"Gold:{playerData.Gold}  Base:{playerData.BaseDamage}  Loc:{currentArea.Name}\n" +
                $"Sword: {swordText}\n" +
                $"Bow:   {bowText}\n" +
                $"Items: {items}\n" +
                "---\n" +
                "1.Equip Sword  2.Equip Bow  C=close";
            FitPanelText();
        }

        private void RenderMapPanel()
        {
            panelText.Text =
                "WORLD MAP\n" +
                "Town -> Stage 1 -> Stage 2 -> Stage 3 -> Stage 4 -> Boss -> Town\n" +
                "\n" +
                $"Current Stage: {(currentStageNumber == 0 ? "Town" : currentStageNumber)}\n" +
                $"Unlocked Frontier: Stage {highestUnlockedStage}\n" +
                $"Area: {currentArea.Name}  C=close";
            FitPanelText();
        }

        private void RenderShopMainPanel()
        {
            if (activeShop == null) { CloseAllPanels(); return; }

            panelText.Text =
                $"{activeShop.NpcName} - {activeShop.DisplayName}\n" +
                $"Gold: {playerData.Gold}\n" +
                "---\n" +
                "1.Buy  2.Sell  3.Leave  C=close";
            FitPanelText();
        }

        private void RenderShopBuyPanel()
        {
            if (activeShop == null) { CloseAllPanels(); return; }

            var lines = new List<string>
            {
                $"{activeShop.DisplayName} - BUY",
                "",
                $"Gold: {playerData.Gold}",
                ""
            };

            for (int i = 0; i < activeShop.Stock.Count && i < 8; i++)
                lines.Add($"{i + 1}. {activeShop.Stock[i].GetDisplayText()}");

            lines.Add("---");
            lines.Add("# to buy  9=Back  C=close");
            panelText.Text = string.Join(Environment.NewLine, lines);
            FitPanelText();
        }

        private void RenderShopSellPanel()
        {
            if (activeShop == null) { CloseAllPanels(); return; }

            var sellable = GetSellableInventory();
            var lines = new List<string>
            {
                $"{activeShop.DisplayName} - SELL",
                "",
                $"Gold: {playerData.Gold}",
                ""
            };

            if (sellable.Count == 0)
            {
                lines.Add("Nothing to sell.");
            }
            else
            {
                for (int i = 0; i < sellable.Count && i < 8; i++)
                {
                    var entry = sellable[i];
                    int sellPrice = Math.Max(1, entry.Item.BasePrice);
                    string qty = entry.Quantity > 1 ? $" x{entry.Quantity}" : "";
                    lines.Add($"{i + 1}. {entry.Item.GetDisplayText()}{qty} - {sellPrice}g");
                }
            }

            lines.Add("---");
            lines.Add("# to sell  9=Back  C=close");
            panelText.Text = string.Join(Environment.NewLine, lines);
            FitPanelText();
        }

        private void RenderEquipSwordPanel()
        {
            var swords = playerData.Inventory
                .Where(i => i.Item is WeaponItem w && w.WeaponCategory == WeaponCategory.Sword)
                .ToList();

            string currentDisplay = playerData.EquippedSword?.GetDisplayText() ?? "None";
            var lines = new List<string>
            {
                $"EQUIP SWORD  (now:{currentDisplay})",
                ""
            };

            if (swords.Count == 0)
                lines.Add("No swords owned.");
            else
                for (int i = 0; i < swords.Count && i < 8; i++)
                {
                    var weapon = (WeaponItem)swords[i].Item;
                    string marker = ReferenceEquals(weapon, playerData.EquippedSword) ? " *" : "";
                    lines.Add($"{i + 1}.{weapon.GetDisplayText()}{marker}");
                }

            lines.Add("---");
            lines.Add("# to equip  9=Back  C=close");
            panelText.Text = string.Join(Environment.NewLine, lines);
            FitPanelText();
        }

        private void RenderEquipBowPanel()
        {
            var bows = playerData.Inventory
                .Where(i => i.Item is WeaponItem w && w.WeaponCategory == WeaponCategory.Bow)
                .ToList();

            string currentDisplay = playerData.EquippedBow?.GetDisplayText() ?? "None";
            var lines = new List<string>
            {
                $"EQUIP BOW  (now:{currentDisplay})",
                ""
            };

            if (bows.Count == 0)
                lines.Add("No bows owned.");
            else
                for (int i = 0; i < bows.Count && i < 8; i++)
                {
                    var weapon = (WeaponItem)bows[i].Item;
                    string marker = ReferenceEquals(weapon, playerData.EquippedBow) ? " *" : "";
                    lines.Add($"{i + 1}.{weapon.GetDisplayText()}{marker}");
                }

            lines.Add("---");
            lines.Add("# to equip  9=Back  C=close");
            panelText.Text = string.Join(Environment.NewLine, lines);
            FitPanelText();
        }

        private void RenderDebugCheatMenuPanel()
        {
            string swordText = playerData.EquippedSword?.GetDisplayText() ?? "None";
            string bowText = playerData.EquippedBow?.GetDisplayText() ?? "None";

            panelText.Text =
                "TESTING CHEATS\n" +
                "\n" +
                $"Player Level: {playerData.Level}\n" +
                $"Sword: {swordText}\n" +
                $"Bow:   {bowText}\n" +
                "\n" +
                "1. Set Player Level\n" +
                "2. Set Sword + Bow Level\n" +
                "\n" +
                "C = close";
            FitPanelText();
        }

        private void RenderDebugCheatSetLevelPanel()
        {
            string displayValue = string.IsNullOrWhiteSpace(debugCheatLevelInput)
                ? "_"
                : debugCheatLevelInput;

            panelText.Text =
                "SET PLAYER LEVEL\n" +
                "\n" +
                $"Input: {displayValue}\n" +
                "\n" +
                "Type 0-9 to enter a level.\n" +
                "ENTER = apply\n" +
                "BACKSPACE = erase\n" +
                "C = close";
            FitPanelText();
        }

        private void RenderDebugCheatSetItemsPanel()
        {
            panelText.Text =
                "SET SWORD + BOW LEVEL\n" +
                "\n" +
                "1. Level 1\n" +
                "2. Level 2\n" +
                "3. Level 3\n" +
                "4. Level 4\n" +
                "5. Level 5\n" +
                "\n" +
                "Choose # to apply  C = close";
            FitPanelText();
        }

        // -----------------------------------------------------------------------
        // Panel input handlers
        // -----------------------------------------------------------------------
        private int GetPressedNumberIndex()
        {
            for (int i = 0; i < numberHeld.Length; i++)
                if (numberHeld[i] && !numberHeldLastFrame[i])
                    return i;
            return -1;
        }

        private int GetPressedDigit()
        {
            if (zeroHeld && !zeroHeldLastFrame)
                return 0;

            int index = GetPressedNumberIndex();
            return index >= 0 ? index + 1 : -1;
        }

        private void HandleSystemMenuSelection()
        {
            int index = GetPressedNumberIndex();
            if (index < 0) return;
            int number = index + 1;

            if (panelMode == PanelMode.SystemMenu)
            {
                if (number == 1)
                {
                    SaveGameState();
                    ShowStatus("Game saved.", 60);
                    CloseAllPanels();
                }
                else if (number == 2)
                {
                    SaveGameState();
                    Close();
                }
                else if (number == 3)
                {
                    panelMode = PanelMode.ResetConfirm;
                    RenderCurrentPanel();
                }
            }
            else if (panelMode == PanelMode.ResetConfirm)
            {
                if (number == 1)
                {
                    panelMode = PanelMode.SystemMenu;
                    RenderCurrentPanel();
                }
                else if (number == 2)
                {
                    ResetProgress();
                }
            }
        }

        private void HandleFastTravelSelection()
        {
            const int stagesPerPage = 7;
            var availableStages = GetCurrentFastTravelStages();
            int pageCount = GetFastTravelPageCount();
            fastTravelPageIndex = Math.Clamp(fastTravelPageIndex, 0, pageCount - 1);
            int index = GetPressedNumberIndex();
            if (index < 0)
                return;
            int number = index + 1;

            if (number == 1)
            {
                if (currentStageNumber == 0) return;
                CloseAllPanels();
                LoadArea(0, TransitionDirection.Left, animate: true);
            }
            else if (number == 9 && pageCount > 1)
            {
                fastTravelPageIndex = (fastTravelPageIndex + 1) % pageCount;
                RenderCurrentPanel();
            }
            else if (number >= 2 && number <= 8)
            {
                int stageIndex = (fastTravelPageIndex * stagesPerPage) + (number - 2);
                if (stageIndex < 0 || stageIndex >= availableStages.Count)
                    return;

                int targetStage = availableStages[stageIndex];
                if (currentStageNumber == targetStage)
                    return;

                TransitionDirection direction = currentStageNumber == 0
                    ? TransitionDirection.Right
                    : targetStage > currentStageNumber ? TransitionDirection.Right : TransitionDirection.Left;

                CloseAllPanels();
                LoadArea(targetStage, direction, animate: true);
            }
        }

        private void HandleStatsSelection()
        {
            int index = GetPressedNumberIndex();
            if (index < 0) return;

            if (index == 0) { panelMode = PanelMode.EquipSword; RenderCurrentPanel(); }
            else if (index == 1) { panelMode = PanelMode.EquipBow; RenderCurrentPanel(); }
        }

        private void HandleEquipSelection()
        {
            int index = GetPressedNumberIndex();
            if (index < 0) return;

            if (index == 8) // key 9 = back
            {
                panelMode = PanelMode.Stats;
                RenderCurrentPanel();
                return;
            }

            if (panelMode == PanelMode.EquipSword)
            {
                var swords = playerData.Inventory
                    .Where(i => i.Item is WeaponItem w && w.WeaponCategory == WeaponCategory.Sword)
                    .ToList();

                if (index < swords.Count)
                {
                    playerData.EquippedSword = (WeaponItem)swords[index].Item;
                    ShowStatus($"Equipped {playerData.EquippedSword.Name}", 60);
                    RenderCurrentPanel();
                }
            }
            else if (panelMode == PanelMode.EquipBow)
            {
                var bows = playerData.Inventory
                    .Where(i => i.Item is WeaponItem w && w.WeaponCategory == WeaponCategory.Bow)
                    .ToList();

                if (index < bows.Count)
                {
                    playerData.EquippedBow = (WeaponItem)bows[index].Item;
                    ShowStatus($"Equipped {playerData.EquippedBow.Name}", 60);
                    RenderCurrentPanel();
                }
            }
        }

        private void HandleShopSelection()
        {
            int index = GetPressedNumberIndex();
            if (index < 0 || activeShop == null) return;

            int number = index + 1;

            if (panelMode == PanelMode.ShopMain)
            {
                if (number == 1) { panelMode = PanelMode.ShopBuy; RenderCurrentPanel(); }
                else if (number == 2) { panelMode = PanelMode.ShopSell; RenderCurrentPanel(); }
                else if (number == 3) { CloseAllPanels(); }
                return;
            }

            if (panelMode == PanelMode.ShopBuy)
            {
                if (number == 9) { panelMode = PanelMode.ShopMain; RenderCurrentPanel(); return; }
                if (index < activeShop.Stock.Count)
                {
                    BuyShopListing(activeShop.Stock[index]);
                    RenderCurrentPanel();
                }
                return;
            }

            if (panelMode == PanelMode.ShopSell)
            {
                if (number == 9) { panelMode = PanelMode.ShopMain; RenderCurrentPanel(); return; }
                var sellable = GetSellableInventory();
                if (index < sellable.Count)
                {
                    SellInventoryEntry(sellable[index]);
                    RenderCurrentPanel();
                }
            }
        }

        // -----------------------------------------------------------------------
        // Area management
        // -----------------------------------------------------------------------
        private void LoadArea(int stageNumber, TransitionDirection entryDir, bool animate = true)
        {
            if (animate)
            {
                transition.Start(stageNumber, entryDir);
            }
            else
            {
                ApplyArea(stageNumber);
                playerX = entryDir == TransitionDirection.Right ? 10 : Width - playerWidth - 10;
                velocityX = 0;
                DrawPlayer();
                DrawAttackHitbox();
            }
        }

        private void OnTransitionMidpoint(int stageNumber, TransitionDirection dir)
        {
            ApplyArea(stageNumber);
            playerX = dir == TransitionDirection.Right ? 10 : Width - playerWidth - 10;
            velocityX = 0;
            CloseAllPanels();
        }

        private void ApplyArea(int stageNumber)
        {
            previousArea = currentArea;
            currentStageNumber = stageNumber;

            if (stageNumber <= 0)
            {
                currentStageNumber = 0;
                currentArea = AreaDefinitions.GetTown();
            }
            else if (!AreaDefinitions.CanGenerateStage(stageNumber))
            {
                currentStageNumber = 0;
                currentArea = AreaDefinitions.GetTown();
                ShowStatus("No boss is configured for that milestone.", 120);
            }
            else
            {
                if (!stageAreas.TryGetValue(stageNumber, out var stageArea))
                {
                    stageArea = AreaDefinitions.CreateStageArea(stageNumber, rng, areaTemplates, enemyTemplates);
                    stageAreas[stageNumber] = stageArea;
                }

                currentArea = stageArea;
            }

            stageTookDamage = false;
            bool enteringTown = currentArea.Type == AreaType.Town &&
                                (previousArea == null || previousArea.Type != AreaType.Town);
            if (enteringTown)
                RefreshTownShops();

            groundRect.Fill = new SolidColorBrush(currentArea.GroundColor);
            ClearGroundDecorations();
            ClearAreaZoneVisuals();
            ClearEnemies();
            ClearEnemyProjectiles();
            ClearProjectiles();
            SpawnGroundDecorations(currentArea);
            SpawnAreaZones(currentArea);
            SpawnEnemies(currentArea);
            UpdateExitTexts();
            SaveGameState();
        }

        private void RefreshTownShops()
        {
            foreach (var zone in currentArea.Zones)
            {
                if (zone.Content is not ShopZoneContent shop)
                    continue;

                shop.Stock.Clear();

                switch (shop.ShopType)
                {
                    case ShopType.Sword:
                        foreach (var sword in GenerateShopWeapons(WeaponCategory.Sword, 3))
                            shop.Stock.Add(new ShopListing { Item = sword, Quantity = 1, Price = sword.BasePrice });
                        break;

                    case ShopType.Bow:
                        foreach (var bow in GenerateShopWeapons(WeaponCategory.Bow, 2))
                            shop.Stock.Add(new ShopListing { Item = bow, Quantity = 1, Price = bow.BasePrice });
                        var arrow = ItemFactory.CreateArrowItem();
                        shop.Stock.Add(new ShopListing { Item = arrow, Quantity = 10, Price = arrow.BasePrice * 10 });
                        break;

                    case ShopType.Healing:
                        var potion = ItemFactory.CreatePotion();
                        shop.Stock.Add(new ShopListing { Item = potion, Quantity = 1, Price = potion.BasePrice });
                        shop.Stock.Add(new ShopListing { Item = potion, Quantity = 2, Price = potion.BasePrice * 2 });
                        break;
                }
            }
        }

        private int GetShopWeaponMaxLevel()
        {
            if (highestUnlockedStage >= 14) return 5;
            if (highestUnlockedStage >= 11) return 4;
            if (highestUnlockedStage >= 6) return 3;
            return 2;
        }

        private WeaponItem CreateWeaponFromTemplate(ItemTemplate template)
        {
            return ItemFactory.CreateWeapon(
                template.Name,
                template.Category,
                template.Level,
                template.CooldownFrames,
                template.SpritePath);
        }

        private List<WeaponItem> GenerateShopWeapons(WeaponCategory category, int count)
        {
            int maxLevel = GetShopWeaponMaxLevel();
            int minLevel = Math.Max(2, maxLevel - 1);
            var pool = itemTemplates
                .Where(i => i.Category == category)
                .Where(i => i.Level >= 2 && i.Level <= maxLevel)
                .OrderByDescending(i => i.Level)
                .ThenBy(i => i.CooldownFrames)
                .ToList();

            if (pool.Count == 0)
            {
                return Enumerable.Range(0, count)
                    .Select(_ => category == WeaponCategory.Sword
                        ? ItemFactory.CreateRandomSword(rng, minLevel, maxLevel)
                        : ItemFactory.CreateRandomBow(rng, minLevel, maxLevel))
                    .ToList();
            }

            var featuredPool = pool.Where(i => i.Level == maxLevel).ToList();
            var rollingPool = pool.Where(i => i.Level >= minLevel).ToList();
            var results = new List<WeaponItem>();

            if (featuredPool.Count > 0)
            {
                var featured = featuredPool[rng.Next(featuredPool.Count)];
                results.Add(CreateWeaponFromTemplate(featured));
            }

            while (results.Count < count)
            {
                var candidatePool = rollingPool
                    .Where(t => !results.Any(r =>
                        r.Name.Equals(t.Name, StringComparison.OrdinalIgnoreCase) &&
                        r.Level == t.Level &&
                        r.CooldownFrames == t.CooldownFrames))
                    .ToList();
                if (candidatePool.Count == 0)
                    candidatePool = rollingPool;

                int targetLevel = maxLevel;
                if (maxLevel > minLevel && rng.NextDouble() < 0.35)
                    targetLevel = maxLevel - 1;

                var targetPool = candidatePool.Where(t => t.Level == targetLevel).ToList();
                if (targetPool.Count == 0)
                    targetPool = candidatePool;

                var template = targetPool[rng.Next(targetPool.Count)];
                results.Add(CreateWeaponFromTemplate(template));
            }

            return results;
        }

        private void CheckAreaTransition()
        {
            if (playerX > Width - playerWidth)
            {
                if (currentStageNumber == 0)
                {
                    int targetStage = highestUnlockedStage;
                    while (targetStage > 1 && !AreaDefinitions.CanGenerateStage(targetStage))
                        targetStage--;

                    if (!AreaDefinitions.CanGenerateStage(targetStage))
                    {
                        ShowStatus("No configured boss for the next milestone.", 120);
                        playerX = Width - playerWidth;
                    }
                    else
                    {
                        LoadArea(targetStage, TransitionDirection.Right);
                    }
                }
                else
                {
                    if (activeEnemies.Count > 0)
                    {
                        playerX = Width - playerWidth;
                        ShowStatus("Defeat all monsters to advance.", 80);
                    }
                    else if (currentArea.IsBossArea)
                    {
                        LoadArea(0, TransitionDirection.Right);
                    }
                    else
                    {
                        int nextStage = currentStageNumber + 1;
                        if (!AreaDefinitions.CanGenerateStage(nextStage))
                        {
                            ShowStatus("No boss is configured for the next milestone.", 120);
                            LoadArea(0, TransitionDirection.Right);
                        }
                        else
                        {
                            LoadArea(nextStage, TransitionDirection.Right);
                        }
                    }
                }
            }
            else if (playerX < 0 && currentStageNumber > 0)
            {
                LoadArea(0, TransitionDirection.Left);
            }
        }

        private static byte ClampColorChannel(int value)
            => (byte)Math.Clamp(value, 0, 255);

        private static Color AdjustColor(Color color, int redDelta, int greenDelta, int blueDelta, int alphaDelta = 0)
        {
            return Color.FromArgb(
                ClampColorChannel(color.A + alphaDelta),
                ClampColorChannel(color.R + redDelta),
                ClampColorChannel(color.G + greenDelta),
                ClampColorChannel(color.B + blueDelta));
        }

        private static double NextDouble(Random random, double min, double max)
        {
            if (max <= min)
                return min;

            return min + (random.NextDouble() * (max - min));
        }

        private void AddGroundDecoration(FrameworkElement visual, double x, double height, double groundSink, int zIndex)
        {
            visual.IsHitTestVisible = false;
            GameCanvas.Children.Add(visual);
            Panel.SetZIndex(visual, zIndex);
            activeGroundDecorations.Add(new SpawnedGroundDecoration
            {
                Visual = visual,
                X = x,
                Height = height,
                GroundSink = groundSink
            });
        }

        private void LayoutGroundDecorations()
        {
            if (activeGroundDecorations.Count == 0)
                return;

            double floorLine = groundY + playerHeight;
            foreach (var decoration in activeGroundDecorations)
            {
                Canvas.SetLeft(decoration.Visual, decoration.X);
                Canvas.SetTop(decoration.Visual, floorLine - decoration.Height + decoration.GroundSink);
            }
        }

        private void ClearGroundDecorations()
        {
            foreach (var decoration in activeGroundDecorations)
                GameCanvas.Children.Remove(decoration.Visual);

            activeGroundDecorations.Clear();
        }

        private void SpawnGroundDecorations(Area area)
        {
            if (area.Type != AreaType.Adventure || area.Biome == null)
                return;

            int seed = unchecked((area.StageNumber * 48611) ^ (((int)area.Biome.Value + 1) * 91939));
            var decorationRng = new Random(seed);

            switch (area.Biome.Value)
            {
                case BiomeType.Plains:
                    SpawnPlainsGroundDecorations(area, decorationRng);
                    break;

                case BiomeType.Cave:
                    SpawnCaveGroundDecorations(area, decorationRng);
                    break;
            }

            LayoutGroundDecorations();
        }

        private void SpawnPlainsGroundDecorations(Area area, Random decorationRng)
        {
            int tuftCount = Math.Max(30, (int)Math.Round(Width / 60.0));
            int flowerCount = Math.Max(7, (int)Math.Round(Width / 175.0));
            Color bladeDark = AdjustColor(area.GroundColor, -28, 24, -16);
            Color bladeMid = AdjustColor(area.GroundColor, -12, 34, -10);
            Color stemColor = AdjustColor(area.GroundColor, -20, 28, -16);
            Color leafColor = AdjustColor(area.GroundColor, -8, 36, -10);
            Color flowerRed = Color.FromRgb(214, 82, 68);
            Color flowerYellow = Color.FromRgb(240, 203, 76);
            Color flowerCenter = Color.FromRgb(114, 78, 34);

            for (int i = 0; i < tuftCount; i++)
            {
                double tuftWidth = NextDouble(decorationRng, 8, 14);
                double tuftHeight = NextDouble(decorationRng, 5, 10);
                double x = NextDouble(decorationRng, 12, Math.Max(18, Width - tuftWidth - 18));
                var tuft = new Canvas
                {
                    Width = tuftWidth,
                    Height = tuftHeight,
                    Opacity = NextDouble(decorationRng, 0.68, 0.9)
                };

                int bladeCount = decorationRng.Next(4, 8);
                for (int bladeIndex = 0; bladeIndex < bladeCount; bladeIndex++)
                {
                    double baseX = NextDouble(decorationRng, 1, Math.Max(1.5, tuftWidth - 1));
                    double tipX = Math.Clamp(baseX + NextDouble(decorationRng, -2.1, 2.1), 0, tuftWidth);
                    double tipY = NextDouble(decorationRng, 0, Math.Max(1.0, tuftHeight - 4));

                    var shadowBlade = new Line
                    {
                        X1 = baseX + 0.35,
                        Y1 = tuftHeight,
                        X2 = tipX + 0.55,
                        Y2 = tipY + 0.2,
                        Stroke = new SolidColorBrush(bladeDark),
                        StrokeThickness = bladeIndex == bladeCount - 1 ? 1.6 : 1.35
                    };
                    var blade = new Line
                    {
                        X1 = baseX,
                        Y1 = tuftHeight,
                        X2 = tipX,
                        Y2 = tipY,
                        Stroke = new SolidColorBrush(bladeMid),
                        StrokeThickness = bladeIndex == bladeCount - 1 ? 1.15 : 1.0
                    };

                    tuft.Children.Add(shadowBlade);
                    tuft.Children.Add(blade);
                }

                AddGroundDecoration(tuft, x, tuftHeight, 1, 2);
            }

            for (int i = 0; i < flowerCount; i++)
            {
                double stemHeight = NextDouble(decorationRng, 6, 11);
                double flowerSize = NextDouble(decorationRng, 4, 6);
                double flowerWidth = flowerSize + 7;
                double flowerHeight = stemHeight + flowerSize + 3;
                double x = NextDouble(decorationRng, 16, Math.Max(20, Width - flowerWidth - 16));
                bool useYellow = decorationRng.NextDouble() < 0.5;
                Color petalColor = useYellow ? flowerYellow : flowerRed;

                var flower = new Canvas
                {
                    Width = flowerWidth,
                    Height = flowerHeight,
                    Opacity = NextDouble(decorationRng, 0.82, 0.97)
                };

                double centerX = flowerWidth / 2.0;
                double bloomY = Math.Max(2.5, flowerHeight - stemHeight - flowerSize + 0.5);

                var stem = new Line
                {
                    X1 = centerX,
                    Y1 = flowerHeight,
                    X2 = centerX,
                    Y2 = bloomY + (flowerSize * 0.55),
                    Stroke = new SolidColorBrush(stemColor),
                    StrokeThickness = 1.1
                };
                var leftLeaf = new Line
                {
                    X1 = centerX,
                    Y1 = flowerHeight - 2.2,
                    X2 = centerX - 2.3,
                    Y2 = flowerHeight - 4.2,
                    Stroke = new SolidColorBrush(leafColor),
                    StrokeThickness = 0.9
                };
                var rightLeaf = new Line
                {
                    X1 = centerX,
                    Y1 = flowerHeight - 3.1,
                    X2 = centerX + 2.4,
                    Y2 = flowerHeight - 5.1,
                    Stroke = new SolidColorBrush(leafColor),
                    StrokeThickness = 0.9
                };

                flower.Children.Add(stem);
                flower.Children.Add(leftLeaf);
                flower.Children.Add(rightLeaf);

                foreach (var petalOffset in new[]
                {
                    new Point(0, -1.5),
                    new Point(-1.8, 0.7),
                    new Point(1.8, 0.7),
                    new Point(0, 2.0),
                })
                {
                    var petal = new Ellipse
                    {
                        Width = flowerSize * 0.66,
                        Height = flowerSize * 0.66,
                        Fill = new SolidColorBrush(petalColor)
                    };
                    Canvas.SetLeft(petal, centerX + petalOffset.X - (petal.Width / 2.0));
                    Canvas.SetTop(petal, bloomY + petalOffset.Y);
                    flower.Children.Add(petal);
                }

                var center = new Ellipse
                {
                    Width = flowerSize * 0.45,
                    Height = flowerSize * 0.45,
                    Fill = new SolidColorBrush(flowerCenter)
                };
                Canvas.SetLeft(center, centerX - (center.Width / 2.0));
                Canvas.SetTop(center, bloomY + 0.75);
                flower.Children.Add(center);

                AddGroundDecoration(flower, x, flowerHeight, 1, 3);
            }
        }

        private void SpawnCaveGroundDecorations(Area area, Random decorationRng)
        {
            int rockCount = Math.Max(10, (int)Math.Round(Width / 135.0));
            int stalagmiteCount = Math.Max(6, (int)Math.Round(Width / 200.0));
            Color rockDark = AdjustColor(area.GroundColor, -24, -20, -14);
            Color rockMid = AdjustColor(area.GroundColor, 6, 7, 12);
            Color rockLight = AdjustColor(area.GroundColor, 22, 24, 30);

            for (int i = 0; i < rockCount; i++)
            {
                double rockWidth = NextDouble(decorationRng, 9, 18);
                double rockHeight = NextDouble(decorationRng, 4, 8);
                double x = NextDouble(decorationRng, 10, Math.Max(16, Width - rockWidth - 10));
                var rock = new Canvas
                {
                    Width = rockWidth,
                    Height = rockHeight,
                    Opacity = NextDouble(decorationRng, 0.78, 0.95)
                };

                var body = new Polygon
                {
                    Fill = new SolidColorBrush(rockMid),
                    Points = new PointCollection
                    {
                        new Point(0, rockHeight),
                        new Point(rockWidth * 0.14, rockHeight * 0.32),
                        new Point(rockWidth * 0.42, 0),
                        new Point(rockWidth * 0.78, rockHeight * 0.18),
                        new Point(rockWidth, rockHeight * 0.74),
                        new Point(rockWidth * 0.85, rockHeight),
                    }
                };
                var shadow = new Polygon
                {
                    Fill = new SolidColorBrush(rockDark),
                    Points = new PointCollection
                    {
                        new Point(rockWidth * 0.06, rockHeight),
                        new Point(rockWidth * 0.3, rockHeight * 0.5),
                        new Point(rockWidth * 0.5, rockHeight * 0.28),
                        new Point(rockWidth * 0.54, rockHeight),
                    }
                };
                var highlight = new Polygon
                {
                    Fill = new SolidColorBrush(rockLight),
                    Opacity = 0.75,
                    Points = new PointCollection
                    {
                        new Point(rockWidth * 0.42, rockHeight * 0.18),
                        new Point(rockWidth * 0.58, rockHeight * 0.08),
                        new Point(rockWidth * 0.7, rockHeight * 0.36),
                        new Point(rockWidth * 0.48, rockHeight * 0.42),
                    }
                };

                rock.Children.Add(body);
                rock.Children.Add(shadow);
                rock.Children.Add(highlight);
                AddGroundDecoration(rock, x, rockHeight, 1, 2);
            }

            for (int i = 0; i < stalagmiteCount; i++)
            {
                double spikeWidth = NextDouble(decorationRng, 7, 15);
                double spikeHeight = NextDouble(decorationRng, 10, 20);
                double x = NextDouble(decorationRng, 14, Math.Max(18, Width - spikeWidth - 14));
                var stalagmite = new Canvas
                {
                    Width = spikeWidth,
                    Height = spikeHeight,
                    Opacity = NextDouble(decorationRng, 0.75, 0.92)
                };

                var spikeBody = new Polygon
                {
                    Fill = new SolidColorBrush(rockDark),
                    Points = new PointCollection
                    {
                        new Point(0, spikeHeight),
                        new Point(spikeWidth * 0.18, spikeHeight * 0.68),
                        new Point(spikeWidth * 0.42, spikeHeight * 0.22),
                        new Point(spikeWidth * 0.6, 0),
                        new Point(spikeWidth * 0.78, spikeHeight * 0.34),
                        new Point(spikeWidth, spikeHeight),
                    }
                };
                var spikeHighlight = new Polygon
                {
                    Fill = new SolidColorBrush(rockLight),
                    Opacity = 0.62,
                    Points = new PointCollection
                    {
                        new Point(spikeWidth * 0.52, spikeHeight * 0.12),
                        new Point(spikeWidth * 0.66, spikeHeight * 0.38),
                        new Point(spikeWidth * 0.58, spikeHeight * 0.84),
                        new Point(spikeWidth * 0.42, spikeHeight * 0.7),
                    }
                };

                stalagmite.Children.Add(spikeBody);
                stalagmite.Children.Add(spikeHighlight);
                AddGroundDecoration(stalagmite, x, spikeHeight, 2, 2);
            }
        }

        // -----------------------------------------------------------------------
        // Zone visuals
        // -----------------------------------------------------------------------
        private void SpawnAreaZones(Area area)
        {
            foreach (var zone in area.Zones)
            {
                if (zone.Content == null)
                    continue;

                FrameworkElement building;

                var buildingSprite = GetShopBuildingSprite(zone.Content);
                if (buildingSprite != null)
                {
                    building = new Image
                    {
                        Width = 96,
                        Height = 64,
                        Stretch = Stretch.Fill,
                        Source = buildingSprite
                    };

                    RenderOptions.SetBitmapScalingMode(building, BitmapScalingMode.NearestNeighbor);
                }
                else
                {
                    building = new Rectangle
                    {
                        Width = 96,
                        Height = 64,
                        Fill = new SolidColorBrush(zone.Content.BuildingColor),
                        Stroke = Brushes.Black,
                        StrokeThickness = 1,
                        RadiusX = 4,
                        RadiusY = 4,
                    };
                }

                var npcIdle1 = GetShopNpcIdle1Sprite(zone.Content);
                var npcIdle2 = GetShopNpcIdle2Sprite(zone.Content);

                var npc = new Image
                {
                    Width = 32,
                    Height = 32,
                    Stretch = Stretch.Fill,
                    Source = npcIdle1
                };

                RenderOptions.SetBitmapScalingMode(npc, BitmapScalingMode.NearestNeighbor);

                var npcLabel = new TextBlock
                {
                    Text = zone.Content.NpcName,
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                    Width = 90,
                    TextAlignment = TextAlignment.Center,
                };
                ApplyReadableTextStyle(npcLabel);

                GameCanvas.Children.Add(building);
                GameCanvas.Children.Add(npc);
                GameCanvas.Children.Add(npcLabel);

                Panel.SetZIndex(building, 3);
                Panel.SetZIndex(npc, 6);
                Panel.SetZIndex(npcLabel, 7);

                double floorLine = groundY + playerHeight;
                double buildingX = zone.X;
                double buildingHeight = building.Height;
                double buildingY = floorLine - buildingHeight;

                double npcX = zone.X + 32;
                double npcY = floorLine - npc.Height;

                Canvas.SetLeft(building, buildingX);
                Canvas.SetTop(building, buildingY);

                Canvas.SetLeft(npc, npcX);
                Canvas.SetTop(npc, npcY);

                Canvas.SetLeft(npcLabel, npcX - 29);
                Canvas.SetTop(npcLabel, npcY - 16);

                activeZoneVisuals.Add(new SpawnedZoneVisual
                {
                    Zone = zone,
                    Building = building,
                    Npc = npc,
                    NpcLabel = npcLabel,
                    NpcIdle1 = npcIdle1,
                    NpcIdle2 = npcIdle2,
                    NpcBaseY = npcY
                });
            }
        }

        private void ClearAreaZoneVisuals()
        {
            foreach (var visual in activeZoneVisuals)
            {
                GameCanvas.Children.Remove(visual.Building);
                if (visual.BuildingLabel != null)
                    GameCanvas.Children.Remove(visual.BuildingLabel);
                GameCanvas.Children.Remove(visual.Npc);
                GameCanvas.Children.Remove(visual.NpcLabel);
            }

            activeZoneVisuals.Clear();
            currentInteractableZone = null;
        }

        private SpawnedZoneVisual? FindInteractableZoneInRange()
        {
            foreach (var visual in activeZoneVisuals)
            {
                double npcLeft = Canvas.GetLeft(visual.Npc);
                double npcTop = Canvas.GetTop(visual.Npc);
                double npcCenterX = npcLeft + visual.Npc.Width / 2;
                double playerCenterX = playerX + playerWidth / 2;

                bool inRange = Math.Abs(playerCenterX - npcCenterX) <= 38 &&
                               Math.Abs(playerY - npcTop) <= 30;
                if (!inRange) continue;

                bool facingNpc =
                    (facingRight && npcCenterX >= playerCenterX - 4) ||
                    (!facingRight && npcCenterX <= playerCenterX + 4);

                if (facingNpc) return visual;
            }

            return null;
        }

        private void InteractWithZone(SpawnedZoneVisual visual)
        {
            if (visual.Zone.Content is ShopZoneContent shop)
            {
                activeShop = shop;
                panelMode = PanelMode.ShopMain;
                panelBorder.Visibility = Visibility.Visible;
                RenderCurrentPanel();
            }
        }

        private void DrawInteractionIndicators()
        {
            foreach (var visual in activeZoneVisuals)
            {
                if (visual == currentInteractableZone)
                {
                    visual.NpcLabel.Text = $"{visual.Zone.Content!.NpcName} [Z]";
                    visual.NpcLabel.Foreground = Brushes.Gold;
                }
                else
                {
                    visual.NpcLabel.Text = visual.Zone.Content!.NpcName;
                    visual.NpcLabel.Foreground = Brushes.White;
                }
            }
        }

        // -----------------------------------------------------------------------
        // Enemy management
        // -----------------------------------------------------------------------
        private SpawnedEnemy CreateSpawnedEnemy(EnemyDefinition def, int initialDirection = 1)
        {
            var walkFrames = LoadEnemyFrames(def.Name, "walk");
            var attackFrames = UsesDedicatedEnemyAttackFrames(def.Name)
                ? LoadEnemyFrames(def.Name, "attack")
                : new List<BitmapImage>();
            var behaviorAttackFrames = new Dictionary<string, List<BitmapImage>>(StringComparer.OrdinalIgnoreCase);
            var behaviorTelegraphFrames = new Dictionary<string, List<BitmapImage>>(StringComparer.OrdinalIgnoreCase);
            var behaviorCooldownFrames = new Dictionary<string, List<BitmapImage>>(StringComparer.OrdinalIgnoreCase);
            var behaviorUndergroundFrames = new Dictionary<string, List<BitmapImage>>(StringComparer.OrdinalIgnoreCase);
            var behaviorFloatFrames = new Dictionary<string, List<BitmapImage>>(StringComparer.OrdinalIgnoreCase);
            var hazardRiseFrames = new List<BitmapImage>();
            var hazardSinkFrames = new List<BitmapImage>();
            foreach (string behaviorId in def.BehaviorIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                behaviorAttackFrames[behaviorId] = LoadEnemyBehaviorAttackFrames(def.Name, behaviorId);
                behaviorTelegraphFrames[behaviorId] = LoadEnemyBehaviorTelegraphFrames(def.Name, behaviorId);
                behaviorCooldownFrames[behaviorId] = LoadEnemyBehaviorCooldownFrames(def.Name, behaviorId);
                behaviorUndergroundFrames[behaviorId] = LoadEnemyBehaviorUndergroundFrames(def.Name, behaviorId);
                behaviorFloatFrames[behaviorId] = LoadEnemyBehaviorFloatFrames(def.Name, behaviorId);
            }

            if (def.Name.Equals("frostling", StringComparison.OrdinalIgnoreCase))
            {
                hazardRiseFrames = LoadEnemyFrames("frostling_icicle", "rise");
                hazardSinkFrames = LoadEnemyFrames("frostling_icicle", "sink");
            }
            else if (def.Name.Equals("Fallen Knight", StringComparison.OrdinalIgnoreCase))
            {
                hazardRiseFrames = LoadEnemyFrames("fallen_knight_spike", "rise");
                hazardSinkFrames = LoadEnemyFrames("fallen_knight_spike", "sink");
            }

            if (hazardSinkFrames.Count == 0 && hazardRiseFrames.Count > 0)
                hazardSinkFrames = hazardRiseFrames.AsEnumerable().Reverse().ToList();

            FrameworkElement body;
            Image? bodySprite = null;

            if (walkFrames.Count > 0 || attackFrames.Count > 0)
            {
                var firstFrame = walkFrames.FirstOrDefault() ?? attackFrames.First();
                double initialWidth = GetSpriteWidthForHeight(firstFrame, def.Height);
                bodySprite = new Image
                {
                    Width = initialWidth,
                    Height = def.Height,
                    Stretch = Stretch.Fill,
                    Source = firstFrame
                };
                RenderOptions.SetBitmapScalingMode(bodySprite, BitmapScalingMode.NearestNeighbor);
                body = bodySprite;
            }
            else
            {
                body = new Rectangle
                {
                    Width = def.Width,
                    Height = def.Height,
                    Fill = new SolidColorBrush(def.Color),
                    Stroke = gameConfig.Debug ? Brushes.Red : null,
                    StrokeThickness = gameConfig.Debug ? 1 : 0,
                    RadiusX = 4,
                    RadiusY = 4,
                };
            }

            var label = new TextBlock
            {
                Text = def.Name,
                Foreground = Brushes.White,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Width = 70,
                TextAlignment = TextAlignment.Center
            };
            ApplyReadableTextStyle(label);

            var healthBg = new Rectangle
            {
                Width = 28,
                Height = 4,
                Fill = Brushes.Black,
                Opacity = 0.7
            };

            var healthFill = new Rectangle
            {
                Width = 28,
                Height = 4,
                Fill = Brushes.LimeGreen
            };

            var attackHitbox = new Rectangle
            {
                Width = def.AttackHitboxWidth,
                Height = def.AttackHitboxHeight,
                Fill = gameConfig.Debug ? new SolidColorBrush(Color.FromArgb(80, 255, 70, 70)) : Brushes.Transparent,
                Stroke = gameConfig.Debug ? Brushes.OrangeRed : null,
                StrokeThickness = gameConfig.Debug ? 1 : 0
            };

            GameCanvas.Children.Add(body);
            GameCanvas.Children.Add(label);
            GameCanvas.Children.Add(healthBg);
            GameCanvas.Children.Add(healthFill);
            GameCanvas.Children.Add(attackHitbox);

            Panel.SetZIndex(body, 12);
            Panel.SetZIndex(label, 13);
            Panel.SetZIndex(healthBg, 14);
            Panel.SetZIndex(healthFill, 15);
            Panel.SetZIndex(attackHitbox, 11);

            double floorLine = groundY + playerHeight;
            bool isBat = def.Name.Equals("bat", StringComparison.OrdinalIgnoreCase);
            double spawnY = floorLine - def.Height;
            double batFlightHomeY = Math.Max(10, spawnY - 44 - rng.Next(0, 10));
            if (isBat)
                spawnY = batFlightHomeY;

            var spawnedEnemy = new SpawnedEnemy
            {
                Definition = def,
                Body = body,
                BodySprite = bodySprite,
                WalkFrames = walkFrames,
                AttackFrames = attackFrames,
                BehaviorAttackFrames = behaviorAttackFrames,
                BehaviorTelegraphFrames = behaviorTelegraphFrames,
                BehaviorCooldownFrames = behaviorCooldownFrames,
                BehaviorUndergroundFrames = behaviorUndergroundFrames,
                BehaviorFloatFrames = behaviorFloatFrames,
                HazardRiseFrames = hazardRiseFrames,
                HazardSinkFrames = hazardSinkFrames,
                AttackHitbox = attackHitbox,
                Label = label,
                HealthBg = healthBg,
                HealthFill = healthFill,
                X = def.X,
                Y = spawnY,
                LeftBound = Math.Max(10, def.X - def.PatrolRange),
                RightBound = Math.Min(Width - def.Width - 10, def.X + def.PatrolRange),
                Speed = def.Speed,
                CurrentBehaviorId = SelectBehavior(def.BehaviorIds),
                BehaviorCycleFrames = rng.Next(90, 181),
                BehaviorTimerFrames = rng.Next(0, Math.Max(1, def.BehaviorIntervalFrames)),
                SpriteGroundOffsetY = GetEnemySpriteGroundOffsetY(def),
                FlightHomeY = isBat ? batFlightHomeY : double.NaN,
                FlightAnchorX = isBat ? def.X + (def.Width / 2.0) : double.NaN,
                FlightTargetX = isBat ? def.X : double.NaN,
                FlightTargetY = isBat ? batFlightHomeY : double.NaN,
                HorizontalVelocity = 0,
                VerticalVelocity = 0,
                IsGrounded = !isBat,
                Direction = initialDirection,
                IsAlive = true,
                CurrentHealth = def.MaxHealth,
                CurrentSpriteWidth = def.Width
            };

            SetEnemyBehavior(spawnedEnemy, SelectBehavior(def.BehaviorIds));
            return spawnedEnemy;
        }

        private void SpawnEnemies(Area area)
        {
            if (area.Type == AreaType.Town) return;

            foreach (var def in area.EnemySpawns)
            {
                int initialDirection = def.Name.Equals("Fallen Knight", StringComparison.OrdinalIgnoreCase) ? -1 : 1;
                activeEnemies.Add(CreateSpawnedEnemy(def, initialDirection));
            }
        }

        private void ClearEnemies()
        {
            ClearEnemyHazards();
            foreach (var enemy in activeEnemies)
            {
                GameCanvas.Children.Remove(enemy.Body);
                GameCanvas.Children.Remove(enemy.Label);
                GameCanvas.Children.Remove(enemy.HealthBg);
                GameCanvas.Children.Remove(enemy.HealthFill);
                GameCanvas.Children.Remove(enemy.AttackHitbox);
            }
            activeEnemies.Clear();
        }

        private void SpawnFrostlingIcicleHazards(SpawnedEnemy enemy)
        {
            if (enemy.HazardRiseFrames.Count == 0 && enemy.HazardSinkFrames.Count == 0)
                return;

            BitmapImage? firstFrame = enemy.HazardRiseFrames.FirstOrDefault() ?? enemy.HazardSinkFrames.FirstOrDefault();
            double spriteWidth = firstFrame?.PixelWidth > 0 ? firstFrame.PixelWidth : 28;
            double spriteHeight = firstFrame?.PixelHeight > 0 ? firstFrame.PixelHeight : 56;
            double floorLineY = groundY + playerHeight;
            double hazardY = floorLineY - spriteHeight;
            double enemyCenterX = enemy.X + (enemy.Definition.Width / 2.0);
            int direction = enemy.Direction == 0 ? 1 : enemy.Direction;
            double firstOffset = Math.Max(12, enemy.Definition.Width * 0.18);
            double spacing = Math.Max(12, spriteWidth * 0.48);
            double[] centerOffsets = Enumerable.Range(0, FrostlingIcicleBurstCount)
                .Select(index => firstOffset + (index * spacing))
                .ToArray();
            double hitboxWidth = Math.Max(16, Math.Min(22, spriteWidth * 0.72));
            double hitboxHeight = Math.Max(32, Math.Min(spriteHeight - 6, spriteHeight * 0.84));

            for (int index = 0; index < centerOffsets.Length; index++)
            {
                FrameworkElement body;
                Image? bodySprite = null;
                if (firstFrame != null)
                {
                    bodySprite = new Image
                    {
                        Width = spriteWidth,
                        Height = spriteHeight,
                        Stretch = Stretch.Fill,
                        Source = firstFrame
                    };
                    RenderOptions.SetBitmapScalingMode(bodySprite, BitmapScalingMode.NearestNeighbor);
                    body = bodySprite;
                }
                else
                {
                    body = new Rectangle
                    {
                        Width = spriteWidth,
                        Height = spriteHeight,
                        Fill = Brushes.LightCyan,
                        RadiusX = 2,
                        RadiusY = 2
                    };
                }

                var hitbox = new Rectangle
                {
                    Width = hitboxWidth,
                    Height = hitboxHeight,
                    Fill = gameConfig.Debug ? new SolidColorBrush(Color.FromArgb(80, 120, 220, 255)) : Brushes.Transparent,
                    Stroke = gameConfig.Debug ? Brushes.DeepSkyBlue : null,
                    StrokeThickness = gameConfig.Debug ? 1 : 0,
                    Visibility = Visibility.Hidden
                };

                GameCanvas.Children.Add(body);
                GameCanvas.Children.Add(hitbox);
                Panel.SetZIndex(body, 18);
                Panel.SetZIndex(hitbox, 19);

                double centerX = enemyCenterX + (direction * centerOffsets[index]);
                double hazardX = Math.Clamp(
                    centerX - (spriteWidth / 2.0),
                    0,
                    Math.Max(0, Width - spriteWidth));

                activeEnemyHazards.Add(new SpawnedEnemyHazard
                {
                    Owner = enemy,
                    Body = body,
                    BodySprite = bodySprite,
                    Hitbox = hitbox,
                    RiseFrames = enemy.HazardRiseFrames,
                    SinkFrames = enemy.HazardSinkFrames,
                    X = hazardX,
                    Y = hazardY,
                    Width = spriteWidth,
                    Height = spriteHeight,
                    HitboxWidth = hitboxWidth,
                    HitboxHeight = hitboxHeight,
                    HitboxOffsetX = Math.Max(0, (spriteWidth - hitboxWidth) / 2.0),
                    HitboxOffsetY = Math.Max(0, spriteHeight - hitboxHeight),
                    Damage = FrostlingIcicleDamage,
                    DelayFrames = index * FrostlingIcicleDelayStepFrames,
                    HoldFrames = FrostlingIcicleHoldFrames,
                    RiseDurationFrames = FrostlingIcicleRiseFrames,
                    SinkDurationFrames = FrostlingIcicleSinkFrames,
                    Direction = direction,
                    Phase = EnemyHazardPhase.Delay,
                    IsAlive = true
                });
            }
        }

        private void SpawnFallenKnightSpikeFieldHazards(SpawnedEnemy enemy)
        {
            if (enemy.HazardRiseFrames.Count == 0 && enemy.HazardSinkFrames.Count == 0)
                return;

            BitmapImage? firstFrame = enemy.HazardRiseFrames.FirstOrDefault() ?? enemy.HazardSinkFrames.FirstOrDefault();
            double spriteWidth = firstFrame?.PixelWidth > 0 ? firstFrame.PixelWidth : 28;
            double spriteHeight = firstFrame?.PixelHeight > 0 ? firstFrame.PixelHeight : 60;
            double floorLineY = groundY + playerHeight;
            double hazardY = floorLineY - spriteHeight;
            double hitboxWidth = Math.Max(16, Math.Min(26, spriteWidth * 0.66));
            double hitboxHeight = Math.Max(30, Math.Min(spriteHeight - 6, spriteHeight * 0.82));
            const int slotCount = 14;
            double leftMargin = 44;
            double rightMargin = Math.Max(leftMargin + 1, Width - 52);
            double spacing = slotCount <= 1 ? 0 : (rightMargin - leftMargin) / (slotCount - 1);
            int safeSlotCount = rng.Next(2, 4);
            var safeSlots = new HashSet<int>();

            while (safeSlots.Count < safeSlotCount)
                safeSlots.Add(rng.Next(0, slotCount));

            for (int slot = 0; slot < slotCount; slot++)
            {
                if (safeSlots.Contains(slot))
                    continue;

                FrameworkElement body;
                Image? bodySprite = null;
                if (firstFrame != null)
                {
                    bodySprite = new Image
                    {
                        Width = spriteWidth,
                        Height = spriteHeight,
                        Stretch = Stretch.Fill,
                        Source = firstFrame
                    };
                    RenderOptions.SetBitmapScalingMode(bodySprite, BitmapScalingMode.NearestNeighbor);
                    body = bodySprite;
                }
                else
                {
                    body = new Rectangle
                    {
                        Width = spriteWidth,
                        Height = spriteHeight,
                        Fill = Brushes.OrangeRed,
                        RadiusX = 2,
                        RadiusY = 2
                    };
                }

                var hitbox = new Rectangle
                {
                    Width = hitboxWidth,
                    Height = hitboxHeight,
                    Fill = gameConfig.Debug ? new SolidColorBrush(Color.FromArgb(80, 255, 170, 70)) : Brushes.Transparent,
                    Stroke = gameConfig.Debug ? Brushes.Gold : null,
                    StrokeThickness = gameConfig.Debug ? 1 : 0,
                    Visibility = Visibility.Hidden
                };

                GameCanvas.Children.Add(body);
                GameCanvas.Children.Add(hitbox);
                Panel.SetZIndex(body, 18);
                Panel.SetZIndex(hitbox, 19);

                double centerX = leftMargin + (slot * spacing);
                double hazardX = Math.Clamp(centerX - (spriteWidth / 2.0), 0, Math.Max(0, Width - spriteWidth));
                activeEnemyHazards.Add(new SpawnedEnemyHazard
                {
                    Owner = enemy,
                    Body = body,
                    BodySprite = bodySprite,
                    Hitbox = hitbox,
                    RiseFrames = enemy.HazardRiseFrames,
                    SinkFrames = enemy.HazardSinkFrames,
                    X = hazardX,
                    Y = hazardY,
                    Width = spriteWidth,
                    Height = spriteHeight,
                    HitboxWidth = hitboxWidth,
                    HitboxHeight = hitboxHeight,
                    HitboxOffsetX = Math.Max(0, (spriteWidth - hitboxWidth) / 2.0),
                    HitboxOffsetY = Math.Max(0, spriteHeight - hitboxHeight),
                    Damage = FallenKnightSpikeDamage,
                    DelayFrames = FallenKnightSpikeTelegraphFrames,
                    HoldFrames = FallenKnightSpikeHoldFrames,
                    RiseDurationFrames = FallenKnightSpikeRiseFrames,
                    SinkDurationFrames = FallenKnightSpikeSinkFrames,
                    Direction = -1,
                    Phase = EnemyHazardPhase.Delay,
                    IsAlive = true
                });
            }
        }

        private void SpawnEnemySnowballProjectile(SpawnedEnemy enemy, int throwIndex, double targetX)
        {
            var snowballFrames = LoadEnemyFrames("fallen_knight_snowball", "spin");
            BitmapImage? firstFrame = snowballFrames.FirstOrDefault();
            var (drawX, spriteWidth) = GetEnemySpriteDrawMetrics(enemy);
            double sizeScale = 0.88 + (throwIndex * 0.22);
            double bodyWidth = Math.Max(18, (firstFrame?.PixelWidth ?? 20) * sizeScale);
            double bodyHeight = Math.Max(18, (firstFrame?.PixelHeight ?? 20) * sizeScale);
            double startX = enemy.Direction < 0
                ? drawX + (spriteWidth * 0.22)
                : drawX + (spriteWidth * 0.64);
            double startY = enemy.Y + enemy.SpriteGroundOffsetY + (enemy.Definition.Height * 0.22);
            double floorLineY = groundY + playerHeight;
            double landingSpread = FallenKnightSnowballLandingSpreadBase + (throwIndex * FallenKnightSnowballLandingSpreadStep);
            double horizontalMiss = (rng.NextDouble() < 0.5 ? -1.0 : 1.0) *
                GetRandomDouble(landingSpread * 0.35, landingSpread);
            double targetCenterX = Math.Clamp(
                targetX + horizontalMiss,
                bodyWidth / 2.0,
                Math.Max(bodyWidth / 2.0, Width - (bodyWidth / 2.0)));
            double targetCenterY = floorLineY - Math.Max(6, bodyHeight * 0.35);
            double deltaX = targetCenterX - startX;
            double deltaY = targetCenterY - startY;
            double travelFrames = Math.Clamp(
                FallenKnightSnowballBaseTravelFrames +
                (Math.Abs(deltaX) / 20.0) +
                (throwIndex * FallenKnightSnowballTravelFrameStep) +
                GetRandomDouble(-4, 6),
                26,
                76);
            double horizontalVelocity = deltaX / Math.Max(1, travelFrames);
            double gravityPerFrame = FallenKnightSnowballGravity +
                (throwIndex * 0.012) +
                GetRandomDouble(-0.02, 0.035);
            double verticalVelocity =
                (deltaY - (gravityPerFrame * travelFrames * (travelFrames + 1) / 2.0)) /
                Math.Max(1, travelFrames);
            int direction = horizontalVelocity >= 0 ? 1 : -1;
            int damage = Math.Max(8, enemy.Definition.ContactDamage - 2 + (throwIndex * 4));

            FrameworkElement body;
            Image? bodySprite = null;
            if (firstFrame != null)
            {
                bodySprite = new Image
                {
                    Width = bodyWidth,
                    Height = bodyHeight,
                    Stretch = Stretch.Fill,
                    Source = firstFrame
                };
                RenderOptions.SetBitmapScalingMode(bodySprite, BitmapScalingMode.NearestNeighbor);
                body = bodySprite;
            }
            else
            {
                body = new Ellipse
                {
                    Width = bodyWidth,
                    Height = bodyHeight,
                    Fill = Brushes.LightSteelBlue,
                    Stroke = Brushes.WhiteSmoke,
                    StrokeThickness = 1
                };
            }

            GameCanvas.Children.Add(body);
            Panel.SetZIndex(body, 19);

            activeEnemyProjectiles.Add(new SpawnedEnemyProjectile
            {
                Owner = enemy,
                Body = body,
                BodySprite = bodySprite,
                Frames = snowballFrames,
                X = startX,
                Y = startY,
                Direction = direction,
                HorizontalVelocity = horizontalVelocity,
                VerticalVelocity = verticalVelocity,
                GravityPerFrame = gravityPerFrame,
                HitboxWidth = Math.Max(10, bodyWidth * EnemyProjectileDefaultHitboxScale),
                HitboxHeight = Math.Max(10, bodyHeight * EnemyProjectileDefaultHitboxScale),
                Damage = damage,
                AnimationFrameCounter = rng.Next(0, 9999),
                IsAlive = true
            });
        }

        private void SpawnFallenKnightHeadFireGlobProjectile(SpawnedEnemy enemy, int globIndex, double targetX)
        {
            double size = 28 + (globIndex * 6);
            var fireGlobFrames = LoadEnemyFrames("fallen_knight_fire_glob", "spin");
            BitmapImage? firstFrame = fireGlobFrames.FirstOrDefault();
            double spawnX = enemy.X + ((enemy.Definition.Width - size) / 2.0);
            double spawnY = enemy.Y - Math.Max(88, enemy.Definition.Height * 2.7);
            double targetCenterX = Math.Clamp(
                targetX + GetRandomDouble(-34, 34),
                size / 2.0,
                Math.Max(size / 2.0, Width - (size / 2.0)));
            double targetCenterY = playerY + (playerHeight * 0.42);
            int travelFrames = 18 + (globIndex * 3);
            double gravityPerFrame = 0.26 + (globIndex * 0.015);
            double startCenterX = spawnX + (size / 2.0);
            double startCenterY = spawnY + (size / 2.0);
            double deltaX = targetCenterX - startCenterX;
            double deltaY = targetCenterY - startCenterY;
            double horizontalVelocity = deltaX / Math.Max(1, travelFrames);
            double verticalVelocity = (deltaY - (gravityPerFrame * travelFrames * (travelFrames + 1) / 2.0)) / Math.Max(1, travelFrames);
            int direction = horizontalVelocity >= 0 ? 1 : -1;
            int damage = Math.Max(enemy.Definition.ContactDamage + 4 + (globIndex * 2), 26);

            FrameworkElement body;
            Image? bodySprite = null;
            if (firstFrame != null)
            {
                bodySprite = new Image
                {
                    Width = size,
                    Height = size,
                    Stretch = Stretch.Fill,
                    Source = firstFrame
                };
                RenderOptions.SetBitmapScalingMode(bodySprite, BitmapScalingMode.NearestNeighbor);
                body = bodySprite;
            }
            else
            {
                body = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = new SolidColorBrush(Color.FromRgb(255, 132, 46)),
                    Stroke = new SolidColorBrush(Color.FromRgb(255, 222, 128)),
                    StrokeThickness = 2,
                    Opacity = 0.95
                };
            }

            GameCanvas.Children.Add(body);
            Panel.SetZIndex(body, 19);

            activeEnemyProjectiles.Add(new SpawnedEnemyProjectile
            {
                Owner = enemy,
                Body = body,
                BodySprite = bodySprite,
                Frames = fireGlobFrames,
                X = spawnX,
                Y = spawnY,
                Direction = direction,
                HorizontalVelocity = horizontalVelocity,
                VerticalVelocity = verticalVelocity,
                GravityPerFrame = gravityPerFrame,
                HitboxWidth = Math.Max(18, size * 0.74),
                HitboxHeight = Math.Max(18, size * 0.74),
                Damage = damage,
                AnimationFrameCounter = rng.Next(0, 9999),
                IsAlive = true
            });
        }

        private void UpdateEnemyHazards()
        {
            foreach (var hazard in activeEnemyHazards.ToList())
            {
                if (!hazard.IsAlive)
                    continue;

                switch (hazard.Phase)
                {
                    case EnemyHazardPhase.Delay:
                        hazard.DelayFrames--;
                        if (hazard.DelayFrames <= 0)
                        {
                            hazard.Phase = EnemyHazardPhase.Rise;
                            hazard.PhaseTick = 0;
                        }
                        break;

                    case EnemyHazardPhase.Rise:
                        hazard.PhaseTick++;
                        if (hazard.PhaseTick >= Math.Max(1, hazard.RiseDurationFrames))
                        {
                            hazard.Phase = EnemyHazardPhase.Hold;
                            hazard.PhaseTick = 0;
                        }
                        break;

                    case EnemyHazardPhase.Hold:
                        hazard.PhaseTick++;
                        if (hazard.PhaseTick >= hazard.HoldFrames)
                        {
                            hazard.Phase = EnemyHazardPhase.Sink;
                            hazard.PhaseTick = 0;
                        }
                        break;

                    case EnemyHazardPhase.Sink:
                        hazard.PhaseTick++;
                        if (hazard.PhaseTick >= Math.Max(1, hazard.SinkDurationFrames))
                            RemoveEnemyHazard(hazard);
                        break;
                }
            }
        }

        private void HandleDebugCheatSelection()
        {
            if (panelMode == PanelMode.DebugCheatMenu)
            {
                int index = GetPressedNumberIndex();
                if (index < 0)
                    return;

                if (index == 0)
                {
                    debugCheatLevelInput = "";
                    panelMode = PanelMode.DebugCheatSetLevel;
                    RenderCurrentPanel();
                }
                else if (index == 1)
                {
                    panelMode = PanelMode.DebugCheatSetItems;
                    RenderCurrentPanel();
                }
                return;
            }

            if (panelMode == PanelMode.DebugCheatSetLevel)
            {
                int digit = GetPressedDigit();
                if (digit >= 0 && debugCheatLevelInput.Length < 4)
                {
                    if (digit != 0 || debugCheatLevelInput.Length > 0)
                    {
                        debugCheatLevelInput += digit.ToString();
                        RenderCurrentPanel();
                    }
                }

                if (backspaceHeld && !backspaceHeldLastFrame)
                {
                    if (debugCheatLevelInput.Length > 0)
                    {
                        debugCheatLevelInput = debugCheatLevelInput[..^1];
                        RenderCurrentPanel();
                    }
                    else
                    {
                        panelMode = PanelMode.DebugCheatMenu;
                        RenderCurrentPanel();
                    }
                }

                if (enterHeld && !enterHeldLastFrame)
                {
                    if (string.IsNullOrWhiteSpace(debugCheatLevelInput))
                    {
                        ShowStatus("Type a player level first.", 70);
                    }
                    else if (!int.TryParse(debugCheatLevelInput, out int targetLevel) || targetLevel < 1)
                    {
                        ShowStatus("Player level must be at least 1.", 70);
                    }
                    else
                    {
                        ApplyDebugPlayerLevelCheat(targetLevel);
                    }
                }

                return;
            }

            if (panelMode == PanelMode.DebugCheatSetItems)
            {
                int index = GetPressedNumberIndex();
                if (index >= 0 && index < 5)
                    ApplyDebugWeaponLevelCheat(index + 1);
            }
        }

        private void ApplyDebugPlayerLevelCheat(int targetLevel)
        {
            playerData.Level = Math.Max(1, targetLevel);
            playerData.Experience = 0;
            levelUpPulseFramesRemaining = LevelUpPulseDurationFrames;
            SaveGameState();

            debugCheatLevelInput = "";
            panelMode = PanelMode.DebugCheatMenu;
            RenderCurrentPanel();
            ShowStatus($"Player level set to {playerData.Level}.", 90);
        }

        private void ApplyDebugWeaponLevelCheat(int targetLevel)
        {
            int clampedLevel = ItemFactory.ClampWeaponLevel(targetLevel);
            ReplaceEquippedWeapon(WeaponCategory.Sword, CreateDebugWeaponForLevel(WeaponCategory.Sword, clampedLevel));
            ReplaceEquippedWeapon(WeaponCategory.Bow, CreateDebugWeaponForLevel(WeaponCategory.Bow, clampedLevel));
            SaveGameState();

            panelMode = PanelMode.DebugCheatMenu;
            RenderCurrentPanel();
            ShowStatus($"Sword and bow set to level {clampedLevel}.", 90);
        }

        private WeaponItem CreateDebugWeaponForLevel(WeaponCategory category, int targetLevel)
        {
            int clampedLevel = ItemFactory.ClampWeaponLevel(targetLevel);

            if (clampedLevel == 1)
            {
                return category == WeaponCategory.Sword
                    ? ItemFactory.CreateOldSword()
                    : ItemFactory.CreateStarterBow();
            }

            ItemTemplate? template = itemTemplates
                .Where(i => i.Category == category && i.Level == clampedLevel)
                .OrderBy(i => i.CooldownFrames)
                .FirstOrDefault();
            if (template != null)
                return CreateWeaponFromTemplate(template);

            int fallbackCooldown = category == WeaponCategory.Sword ? 12 : 14;
            string fallbackName = category == WeaponCategory.Sword
                ? $"Debug Sword Lv {clampedLevel}"
                : $"Debug Bow Lv {clampedLevel}";
            return ItemFactory.CreateWeapon(fallbackName, category, clampedLevel, fallbackCooldown);
        }

        private void ReplaceEquippedWeapon(WeaponCategory category, WeaponItem replacement)
        {
            WeaponItem? current = category == WeaponCategory.Sword
                ? playerData.EquippedSword
                : playerData.EquippedBow;
            var inventoryEntry = current == null
                ? null
                : playerData.Inventory.FirstOrDefault(i => ReferenceEquals(i.Item, current));

            if (inventoryEntry != null)
                inventoryEntry.Item = replacement;
            else
                AddItemToInventory(replacement, 1);

            if (category == WeaponCategory.Sword)
                playerData.EquippedSword = replacement;
            else
                playerData.EquippedBow = replacement;
        }

        private void HandleEnemyHazardContactWithPlayer()
        {
            if (playerDamageCooldownFrames > 0)
                return;

            Rect playerRect = GetPlayerCollisionRect();

            foreach (var hazard in activeEnemyHazards)
            {
                if (!hazard.IsAlive || hazard.HasAppliedDamage)
                    continue;

                Rect hazardRect = GetEnemyHazardCollisionRect(hazard);
                if (hazardRect.Width <= 0 || hazardRect.Height <= 0)
                    continue;

                if (!playerRect.IntersectsWith(hazardRect))
                    continue;

                playerData.Health = Math.Max(0, playerData.Health - hazard.Damage);
                stageTookDamage = true;
                playerDamageCooldownFrames = PlayerDamageCooldownMax;
                hazard.HasAppliedDamage = true;
                string hazardMessage = IsFallenKnightHeadEnemy(hazard.Owner)
                    ? "Head slam scorches you!"
                    : IsFallenKnightEnemy(hazard.Owner)
                        ? "Knight spikes hit you!"
                        : "Icicle blast hits you!";
                ShowStatus(hazardMessage, 35);
                break;
            }
        }

        private static double GetEnemyHazardExposure(SpawnedEnemyHazard hazard)
        {
            return hazard.Phase switch
            {
                EnemyHazardPhase.Delay => 0,
                EnemyHazardPhase.Rise => Math.Clamp((double)(hazard.PhaseTick + 1) / Math.Max(1, hazard.RiseDurationFrames), 0, 1),
                EnemyHazardPhase.Hold => 1,
                EnemyHazardPhase.Sink => 1.0 - Math.Clamp((double)(hazard.PhaseTick + 1) / Math.Max(1, hazard.SinkDurationFrames), 0, 1),
                _ => 0
            };
        }

        private Rect GetEnemyHazardCollisionRect(SpawnedEnemyHazard hazard)
        {
            if (!hazard.IsAlive)
                return new Rect(hazard.X, hazard.Y, 0, 0);

            double exposure = GetEnemyHazardExposure(hazard);
            if (exposure < 0.35)
                return new Rect(
                    hazard.X + hazard.HitboxOffsetX,
                    hazard.Y + hazard.HitboxOffsetY + hazard.HitboxHeight,
                    0,
                    0);

            double exposedHeight = Math.Max(4, hazard.HitboxHeight * exposure);
            double hitboxX = hazard.X + hazard.HitboxOffsetX;
            double hitboxY = hazard.Y + hazard.HitboxOffsetY + (hazard.HitboxHeight - exposedHeight);
            return new Rect(hitboxX, hitboxY, hazard.HitboxWidth, exposedHeight);
        }

        private void DrawEnemyHazards()
        {
            foreach (var hazard in activeEnemyHazards)
            {
                if (!hazard.IsAlive)
                    continue;

                Canvas.SetLeft(hazard.Body, hazard.X);
                Canvas.SetTop(hazard.Body, hazard.Y);

                if (hazard.BodySprite != null)
                {
                    List<BitmapImage> activeFrames;
                    int frameIndex;
                    switch (hazard.Phase)
                    {
                        case EnemyHazardPhase.Rise:
                            activeFrames = hazard.RiseFrames;
                            frameIndex = activeFrames.Count > 0
                                ? GetHeldAnimationFrameIndex(activeFrames.Count, hazard.PhaseTick, Math.Max(1, hazard.RiseDurationFrames))
                                : 0;
                            break;

                        case EnemyHazardPhase.Sink:
                            activeFrames = hazard.SinkFrames.Count > 0 ? hazard.SinkFrames : hazard.RiseFrames.AsEnumerable().Reverse().ToList();
                            frameIndex = activeFrames.Count > 0
                                ? GetHeldAnimationFrameIndex(activeFrames.Count, hazard.PhaseTick, Math.Max(1, hazard.SinkDurationFrames))
                                : 0;
                            break;

                        case EnemyHazardPhase.Hold:
                            activeFrames = hazard.RiseFrames.Count > 0 ? hazard.RiseFrames : hazard.SinkFrames;
                            frameIndex = Math.Max(0, activeFrames.Count - 1);
                            break;

                        default:
                            activeFrames = hazard.RiseFrames.Count > 0 ? hazard.RiseFrames : hazard.SinkFrames;
                            frameIndex = 0;
                            break;
                    }

                    if (activeFrames.Count > 0)
                        hazard.BodySprite.Source = activeFrames[Math.Clamp(frameIndex, 0, activeFrames.Count - 1)];
                }

                Rect hazardRect = GetEnemyHazardCollisionRect(hazard);
                hazard.Hitbox.Width = Math.Max(0, hazardRect.Width);
                hazard.Hitbox.Height = Math.Max(0, hazardRect.Height);
                Canvas.SetLeft(hazard.Hitbox, hazardRect.X);
                Canvas.SetTop(hazard.Hitbox, hazardRect.Y);
                hazard.Hitbox.Visibility = gameConfig.Debug && hazardRect.Width > 0 && hazardRect.Height > 0
                    ? Visibility.Visible
                    : Visibility.Hidden;
            }
        }

        private void RemoveEnemyHazard(SpawnedEnemyHazard hazard)
        {
            if (!hazard.IsAlive)
                return;

            hazard.IsAlive = false;
            GameCanvas.Children.Remove(hazard.Body);
            GameCanvas.Children.Remove(hazard.Hitbox);
            activeEnemyHazards.Remove(hazard);
        }

        private void RemoveEnemyHazardsForOwner(SpawnedEnemy enemy)
        {
            foreach (var hazard in activeEnemyHazards.Where(h => ReferenceEquals(h.Owner, enemy)).ToList())
                RemoveEnemyHazard(hazard);
        }

        private void ClearEnemyHazards()
        {
            foreach (var hazard in activeEnemyHazards.ToList())
                RemoveEnemyHazard(hazard);
        }

        private void UpdateEnemyProjectiles()
        {
            foreach (var projectile in activeEnemyProjectiles.ToList())
            {
                if (!projectile.IsAlive)
                    continue;

                projectile.X += projectile.HorizontalVelocity;
                projectile.VerticalVelocity += projectile.GravityPerFrame;
                projectile.Y += projectile.VerticalVelocity;

                double floorY = groundY + playerHeight;
                bool outOfBounds = projectile.X < -projectile.Body.Width ||
                    projectile.X > Width + projectile.Body.Width ||
                    projectile.Y < -projectile.Body.Height ||
                    projectile.Y > floorY + projectile.Body.Height;
                if (outOfBounds || projectile.Y + projectile.Body.Height >= floorY)
                {
                    RemoveEnemyProjectile(projectile);
                    continue;
                }
            }
        }

        private void HandleEnemyProjectileContactWithPlayer()
        {
            if (playerDamageCooldownFrames > 0)
                return;

            Rect playerRect = GetPlayerCollisionRect();
            foreach (var projectile in activeEnemyProjectiles.ToList())
            {
                if (!projectile.IsAlive || projectile.HasAppliedDamage)
                    continue;

                if (!playerRect.IntersectsWith(GetEnemyProjectileHitboxRect(projectile)))
                    continue;

                playerData.Health = Math.Max(0, playerData.Health - projectile.Damage);
                stageTookDamage = true;
                playerDamageCooldownFrames = PlayerDamageCooldownMax;
                projectile.HasAppliedDamage = true;
                RemoveEnemyProjectile(projectile);
                ShowStatus(
                    IsFallenKnightHeadEnemy(projectile.Owner)
                        ? "Fire glob burns you!"
                        : "Snowball hits you!",
                    35);
                break;
            }
        }

        private void DrawEnemyProjectiles()
        {
            foreach (var projectile in activeEnemyProjectiles)
            {
                if (!projectile.IsAlive)
                    continue;

                Canvas.SetLeft(projectile.Body, projectile.X);
                Canvas.SetTop(projectile.Body, projectile.Y);

                if (projectile.BodySprite != null && projectile.Frames.Count > 0)
                {
                    projectile.AnimationFrameCounter++;
                    int frameIndex = (projectile.AnimationFrameCounter / 4) % projectile.Frames.Count;
                    projectile.BodySprite.Source = projectile.Frames[frameIndex];
                    projectile.BodySprite.RenderTransformOrigin = new Point(0.5, 0.5);
                    projectile.BodySprite.RenderTransform = new ScaleTransform(projectile.Direction >= 0 ? 1 : -1, 1);
                }
            }
        }

        private Rect GetEnemyProjectileHitboxRect(SpawnedEnemyProjectile projectile)
        {
            double hitboxX = projectile.X + ((projectile.Body.Width - projectile.HitboxWidth) / 2.0);
            double hitboxY = projectile.Y + ((projectile.Body.Height - projectile.HitboxHeight) / 2.0);
            return new Rect(hitboxX, hitboxY, projectile.HitboxWidth, projectile.HitboxHeight);
        }

        private void RemoveEnemyProjectile(SpawnedEnemyProjectile projectile)
        {
            if (!projectile.IsAlive)
                return;

            projectile.IsAlive = false;
            GameCanvas.Children.Remove(projectile.Body);
            activeEnemyProjectiles.Remove(projectile);
        }

        private void RemoveEnemyProjectilesForOwner(SpawnedEnemy enemy)
        {
            foreach (var projectile in activeEnemyProjectiles.Where(p => ReferenceEquals(p.Owner, enemy)).ToList())
                RemoveEnemyProjectile(projectile);
        }

        private void ClearEnemyProjectiles()
        {
            foreach (var projectile in activeEnemyProjectiles.ToList())
                RemoveEnemyProjectile(projectile);
        }

        private void RemoveEnemyTrailVisuals(SpawnedEnemy enemy)
        {
            foreach (var trailBody in enemy.TrailBodies)
                GameCanvas.Children.Remove(trailBody);

            enemy.TrailBodies.Clear();
            enemy.TrailHistory.Clear();
        }

        private void RemoveEnemyWithoutRewards(SpawnedEnemy enemy)
        {
            if (!enemy.IsAlive)
                return;

            enemy.IsAlive = false;
            currentAttackVictims.Remove(enemy);
            RemoveEnemyHazardsForOwner(enemy);
            RemoveEnemyProjectilesForOwner(enemy);
            RemoveEnemyTrailVisuals(enemy);
            GameCanvas.Children.Remove(enemy.Body);
            GameCanvas.Children.Remove(enemy.Label);
            GameCanvas.Children.Remove(enemy.HealthBg);
            GameCanvas.Children.Remove(enemy.HealthFill);
            GameCanvas.Children.Remove(enemy.AttackHitbox);
            activeEnemies.Remove(enemy);
        }

        private void StartFallenKnightBodyCollapse(SpawnedEnemy enemy)
        {
            enemy.IsRecovering = true;
            enemy.RecoveryPauseFrames = int.MaxValue / 4;
            enemy.RecoveryAnimationTick = 0;
            enemy.RecoveryAnimationDuration = 1;
            enemy.HorizontalVelocity = 0;
            enemy.VerticalVelocity = 0;
            enemy.SuppressContactDamage = true;
            enemy.IsInvulnerable = enemy.LinkedEnemy != null && !enemy.LinkedEnemy.IsReturningToOwner;
        }

        private void EndFallenKnightBodyCollapse(SpawnedEnemy enemy)
        {
            enemy.LinkedEnemy = null;
            enemy.IsRecovering = false;
            enemy.RecoveryPauseFrames = 0;
            enemy.RecoveryAnimationTick = 0;
            enemy.RecoveryAnimationDuration = 0;
            enemy.HorizontalVelocity = 0;
            enemy.VerticalVelocity = 0;
            enemy.SuppressContactDamage = false;
            enemy.IsInvulnerable = false;
            enemy.AttackCooldownFrames = FallenKnightAttackCooldownFrames;
            enemy.SpecialActionStep = 0;
            enemy.SpecialActionCounter = 0;
            enemy.FallenKnightSpikeAttacksCompleted = 0;
            enemy.FallenKnightSnowballAttacksCompleted = 0;
            SetEnemyBehavior(enemy, ChooseFallenKnightBehavior(enemy));
        }

        private void ResetFallenKnightHeadFireTowerCycle(SpawnedEnemy head)
        {
            if (!IsFallenKnightHeadEnemy(head))
                return;

            head.SpecialActionStep = rng.Next(
                FallenKnightHeadFireTowerMinJumpCount,
                FallenKnightHeadFireTowerMaxJumpCount + 1);
            head.SpecialActionCounter = 0;
        }

        private void InitializeFallenKnightHeadTrailVisuals(SpawnedEnemy head)
        {
            RemoveEnemyTrailVisuals(head);

            for (int index = 0; index < FallenKnightHeadTrailLength; index++)
            {
                var trail = new Ellipse
                {
                    Width = 18 - (index * 2),
                    Height = 14 - (index * 2),
                    Fill = new SolidColorBrush(index % 2 == 0
                        ? Color.FromArgb(120, 255, 156, 68)
                        : Color.FromArgb(100, 255, 214, 118)),
                    Opacity = 0,
                    Visibility = Visibility.Hidden,
                    IsHitTestVisible = false
                };
                GameCanvas.Children.Add(trail);
                Panel.SetZIndex(trail, 11);
                head.TrailBodies.Add(trail);
            }
        }

        private void SpawnFallenKnightHead(SpawnedEnemy body)
        {
            if (!IsFallenKnightEnemy(body) || body.LinkedEnemy != null)
                return;

            var headDefinition = new EnemyDefinition
            {
                Name = "Fallen Knight Head",
                BehaviorIds = new List<string> { "hop_contact", "fire_tower" },
                BehaviorIntervalFrames = 22,
                PowerLevel = body.Definition.PowerLevel + 1,
                X = body.X + (body.Definition.Width * 0.18),
                PatrolRange = 520,
                AggroRange = 2000,
                Speed = 1.85,
                MaxHealth = Math.Max(34, 24 + (currentStageNumber * 4)),
                ContactDamage = Math.Max(body.Definition.ContactDamage + 6, 24),
                XpReward = 0,
                GoldMin = 0,
                GoldMax = 0,
                Width = 42,
                Height = 42,
                AttackHitboxWidth = 28,
                AttackHitboxHeight = 28,
                CollisionHitboxWidth = 28,
                CollisionHitboxHeight = 28,
                CollisionHitboxOffsetX = 7,
                CollisionHitboxOffsetY = 8,
                Color = Color.FromRgb(255, 132, 66)
            };

            var head = CreateSpawnedEnemy(headDefinition, body.Direction);
            head.X = body.X + (body.Definition.Width * 0.2);
            head.Y = body.Y + Math.Max(4, body.Definition.Height * 0.05);
            head.IsGrounded = false;
            head.SuppressRewards = true;
            head.LinkedEnemy = body;
            head.HazardRiseFrames = LoadEnemyFrames("fallen_knight_head_splash", "rise");
            head.HazardSinkFrames = LoadEnemyFrames("fallen_knight_head_splash", "sink");
            InitializeFallenKnightHeadTrailVisuals(head);
            SetEnemyBehavior(head, "hop_contact");
            ResetFallenKnightHeadFireTowerCycle(head);
            head.BehaviorTimerFrames = 8;
            body.LinkedEnemy = head;
            body.IsInvulnerable = true;
            body.SuppressContactDamage = true;
            activeEnemies.Add(head);
        }

        private void StartFallenKnightHeadReturn(SpawnedEnemy head)
        {
            if (!IsFallenKnightHeadEnemy(head) || head.IsReturningToOwner)
                return;

            head.IsReturningToOwner = true;
            head.IsInvulnerable = true;
            head.SuppressContactDamage = true;
            head.HomeX = head.X;
            head.HomeY = head.Y;
            head.ReturnAnimationTick = 0;
            head.ReturnAnimationDuration = FallenKnightHeadReturnDurationFrames;
            head.HorizontalVelocity = 0;
            head.VerticalVelocity = 0;
            head.CurrentHealth = 0;
            StopEnemyAttack(head);
            StopEnemyTelegraph(head);
            RemoveEnemyTrailVisuals(head);
            head.HasLockedAttackDirection = false;
            SetEnemyBehavior(head, "hop_contact");
            head.Label.Visibility = Visibility.Hidden;
            head.HealthBg.Visibility = Visibility.Hidden;
            head.HealthFill.Visibility = Visibility.Hidden;
            head.AttackHitbox.Visibility = Visibility.Hidden;

            if (head.LinkedEnemy != null)
            {
                head.LinkedEnemy.IsInvulnerable = false;
                head.LinkedEnemy.SuppressContactDamage = true;
                head.LinkedEnemy.IsRecovering = true;
            }

            ShowStatus("The fallen knight's armor is exposed!", 70);
        }

        private void UpdateFallenKnightEnemy(SpawnedEnemy enemy, bool hasAggro, double playerCenterX, double enemyCenterX)
        {
            enemy.IsAggroLocked = true;
            enemy.X = Math.Clamp(FallenKnightFixedX, 0, Math.Max(0, Width - enemy.Definition.Width));
            enemy.Direction = playerCenterX >= enemyCenterX ? 1 : -1;
            enemy.HorizontalVelocity = 0;
            enemy.VerticalVelocity = 0;
            enemy.IsGrounded = true;

            if (enemy.LinkedEnemy != null)
            {
                enemy.IsRecovering = true;
                enemy.RecoveryAnimationTick++;
                enemy.SuppressContactDamage = true;
                enemy.IsInvulnerable = !enemy.LinkedEnemy.IsReturningToOwner;
                return;
            }

            enemy.IsRecovering = false;
            enemy.SuppressContactDamage = false;
            enemy.IsInvulnerable = false;

            if (enemy.IsTelegraphing)
            {
                enemy.TelegraphFramesRemaining--;
                if (enemy.TelegraphFramesRemaining <= 0)
                {
                    StopEnemyTelegraph(enemy);
                    enemy.HasLockedAttackDirection = true;
                    enemy.LockedAttackDirection = enemy.Direction;

                    switch (enemy.CurrentBehaviorId)
                    {
                        case "spike_field":
                            StartEnemyAttack(enemy, Math.Max(22, Math.Max(1, GetAttackFramesForCurrentBehavior(enemy).Count) * 4));
                            enemy.AttackCooldownFrames = FallenKnightAttackCooldownFrames;
                            break;

                        case "snowball_heave":
                            enemy.SpecialActionStep = FallenKnightSnowballVolleyCount;
                            enemy.SpecialActionCounter = 0;
                            StartEnemyAttack(
                                enemy,
                                Math.Max(
                                    FallenKnightSnowballAttackBaseFrames,
                                    20 + (enemy.SpecialActionStep * 12)));
                            enemy.AttackCooldownFrames = FallenKnightAttackCooldownFrames + (enemy.SpecialActionStep * 4);
                            break;

                        case "fire_head":
                            StartEnemyAttack(enemy, Math.Max(FallenKnightFireHeadAttackDurationFrames, Math.Max(1, GetAttackFramesForCurrentBehavior(enemy).Count) * 4));
                            enemy.AttackCooldownFrames = FallenKnightAttackCooldownFrames + 18;
                            enemy.SuppressContactDamage = true;
                            break;
                    }
                }
                return;
            }

            if (enemy.IsAttacking)
            {
                switch (enemy.CurrentBehaviorId)
                {
                    case "spike_field":
                        if (!enemy.AttackEffectTriggered && enemy.AttackAnimationTick >= Math.Max(5, enemy.AttackAnimationDuration / 3))
                        {
                            SpawnFallenKnightSpikeFieldHazards(enemy);
                            enemy.AttackEffectTriggered = true;
                        }
                        break;

                    case "snowball_heave":
                        int firstThrowTick = 8;
                        const int throwInterval = 12;
                        while (enemy.SpecialActionCounter < enemy.SpecialActionStep &&
                               enemy.AttackAnimationTick >= firstThrowTick + (enemy.SpecialActionCounter * throwInterval))
                        {
                            SpawnEnemySnowballProjectile(enemy, enemy.SpecialActionCounter, playerCenterX);
                            enemy.SpecialActionCounter++;
                        }
                        break;

                    case "fire_head":
                        enemy.SuppressContactDamage = true;
                        if (!enemy.AttackEffectTriggered && enemy.AttackAnimationTick >= Math.Max(6, enemy.AttackAnimationDuration / 2))
                        {
                            SpawnFallenKnightHead(enemy);
                            enemy.AttackEffectTriggered = true;
                        }
                        break;
                }
                return;
            }

            if (!hasAggro || enemy.AttackCooldownFrames > 0)
                return;

            SetEnemyBehavior(enemy, ChooseFallenKnightBehavior(enemy));
            int telegraphDuration = enemy.CurrentBehaviorId switch
            {
                "spike_field" => Math.Max(FallenKnightSpikeTelegraphFrames, Math.Max(1, GetTelegraphFramesForCurrentBehavior(enemy).Count) * 4),
                "snowball_heave" => Math.Max(FallenKnightSnowballTelegraphDurationFrames, Math.Max(1, GetTelegraphFramesForCurrentBehavior(enemy).Count) * 4),
                "fire_head" => Math.Max(FallenKnightFireHeadTelegraphDurationFrames, Math.Max(1, GetTelegraphFramesForCurrentBehavior(enemy).Count) * 4),
                _ => 18
            };
            StartEnemyTelegraph(enemy, telegraphDuration);
        }

        private void UpdateFallenKnightHeadEnemy(SpawnedEnemy enemy, double playerCenterX, double enemyCenterX)
        {
            if (enemy.IsReturningToOwner)
            {
                enemy.SuppressContactDamage = true;
                enemy.HorizontalVelocity = 0;
                enemy.VerticalVelocity = 0;

                if (enemy.LinkedEnemy == null || !enemy.LinkedEnemy.IsAlive || enemy.LinkedEnemy.IsDying)
                {
                    RemoveEnemyWithoutRewards(enemy);
                    return;
                }

                double targetX = enemy.LinkedEnemy.X + (enemy.LinkedEnemy.Definition.Width * 0.44);
                double targetY = enemy.LinkedEnemy.Y + (enemy.LinkedEnemy.Definition.Height * 0.08);
                enemy.ReturnAnimationTick = Math.Min(enemy.ReturnAnimationDuration, enemy.ReturnAnimationTick + 1);
                double progress = enemy.ReturnAnimationDuration <= 0
                    ? 1.0
                    : Math.Clamp((double)enemy.ReturnAnimationTick / enemy.ReturnAnimationDuration, 0, 1);
                double easedProgress = 1.0 - Math.Pow(1.0 - progress, 3);
                double floatBob = Math.Sin(animationFrameCounter * 0.18) *
                    Math.Max(0.4, 2.2 * (1.0 - GetFallenKnightHeadReturnProgress(enemy.LinkedEnemy)));
                double startX = double.IsNaN(enemy.HomeX) ? enemy.X : enemy.HomeX;
                double startY = double.IsNaN(enemy.HomeY) ? enemy.Y : enemy.HomeY;
                enemy.X = startX + ((targetX - startX) * easedProgress);
                enemy.Y = startY + (((targetY + floatBob) - startY) * easedProgress);
                enemy.Direction = targetX >= enemy.X ? 1 : -1;

                if (progress >= 1.0)
                {
                    var body = enemy.LinkedEnemy;
                    RemoveEnemyWithoutRewards(enemy);
                    if (body != null && body.IsAlive && !body.IsDying)
                        EndFallenKnightBodyCollapse(body);
                }
                return;
            }

            enemy.IsAggroLocked = true;
            enemy.IsInvulnerable = false;
            if (enemy.IsTelegraphing)
            {
                enemy.SuppressContactDamage = true;
                enemy.Direction = playerCenterX >= enemyCenterX ? 1 : -1;
                enemy.HorizontalVelocity = 0;
                enemy.VerticalVelocity = 0;
                enemy.IsGrounded = true;
                enemy.TelegraphFramesRemaining--;
                if (enemy.TelegraphFramesRemaining <= 0)
                {
                    StopEnemyTelegraph(enemy);
                    int fireGlobCount = rng.Next(3, 5);
                    enemy.SpecialActionStep = fireGlobCount;
                    enemy.SpecialActionCounter = 0;
                    StartEnemyAttack(
                        enemy,
                        Math.Max(
                            FallenKnightHeadFireTowerBaseAttackFrames,
                            14 + (fireGlobCount * 12)));
                    enemy.AttackCooldownFrames = 28;
                }
                return;
            }

            if (enemy.IsAttacking)
            {
                enemy.SuppressContactDamage = true;
                enemy.Direction = playerCenterX >= enemyCenterX ? 1 : -1;
                enemy.HorizontalVelocity = 0;
                enemy.VerticalVelocity = 0;
                enemy.IsGrounded = true;
                int firstGlobTick = 8;
                const int globInterval = 12;
                while (enemy.SpecialActionCounter < enemy.SpecialActionStep &&
                       enemy.AttackAnimationTick >= firstGlobTick + (enemy.SpecialActionCounter * globInterval))
                {
                    SpawnFallenKnightHeadFireGlobProjectile(enemy, enemy.SpecialActionCounter, playerCenterX);
                    enemy.SpecialActionCounter++;
                }
                return;
            }

            enemy.SuppressContactDamage = false;
            UpdateHopContactEnemy(enemy, true, playerCenterX);
        }

        private void SpawnFallenKnightHeadLandingSplash(SpawnedEnemy enemy)
        {
            BitmapImage? firstFrame = enemy.HazardRiseFrames.FirstOrDefault() ?? enemy.HazardSinkFrames.FirstOrDefault();
            double spriteWidth = firstFrame?.PixelWidth > 0 ? firstFrame.PixelWidth : 92;
            double spriteHeight = firstFrame?.PixelHeight > 0 ? firstFrame.PixelHeight : 36;
            double splashX = Math.Clamp(
                enemy.X + ((enemy.Definition.Width - spriteWidth) / 2.0),
                0,
                Math.Max(0, Width - spriteWidth));
            double splashY = (groundY + playerHeight) - spriteHeight;

            FrameworkElement body;
            Image? bodySprite = null;
            if (firstFrame != null)
            {
                bodySprite = new Image
                {
                    Width = spriteWidth,
                    Height = spriteHeight,
                    Stretch = Stretch.Fill,
                    Source = firstFrame
                };
                RenderOptions.SetBitmapScalingMode(bodySprite, BitmapScalingMode.NearestNeighbor);
                body = bodySprite;
            }
            else
            {
                body = new Ellipse
                {
                    Width = spriteWidth,
                    Height = spriteHeight,
                    Fill = new SolidColorBrush(Color.FromArgb(220, 255, 120, 44)),
                    Opacity = 0.9
                };
            }

            var hitbox = new Rectangle
            {
                Width = FallenKnightHeadSplashHitboxWidth,
                Height = FallenKnightHeadSplashHitboxHeight,
                Fill = gameConfig.Debug ? new SolidColorBrush(Color.FromArgb(80, 255, 140, 70)) : Brushes.Transparent,
                Stroke = gameConfig.Debug ? Brushes.OrangeRed : null,
                StrokeThickness = gameConfig.Debug ? 1 : 0,
                Visibility = Visibility.Hidden
            };

            GameCanvas.Children.Add(body);
            GameCanvas.Children.Add(hitbox);
            Panel.SetZIndex(body, 18);
            Panel.SetZIndex(hitbox, 19);

            activeEnemyHazards.Add(new SpawnedEnemyHazard
            {
                Owner = enemy,
                Body = body,
                BodySprite = bodySprite,
                Hitbox = hitbox,
                RiseFrames = enemy.HazardRiseFrames,
                SinkFrames = enemy.HazardSinkFrames,
                X = splashX,
                Y = splashY,
                Width = spriteWidth,
                Height = spriteHeight,
                HitboxWidth = FallenKnightHeadSplashHitboxWidth,
                HitboxHeight = FallenKnightHeadSplashHitboxHeight,
                HitboxOffsetX = Math.Max(0, (spriteWidth - FallenKnightHeadSplashHitboxWidth) / 2.0),
                HitboxOffsetY = Math.Max(0, spriteHeight - FallenKnightHeadSplashHitboxHeight),
                Damage = Math.Max(20, enemy.Definition.ContactDamage - 2),
                DelayFrames = 0,
                HoldFrames = FallenKnightHeadSplashHoldFrames,
                RiseDurationFrames = Math.Max(FallenKnightHeadSplashRiseFrames, enemy.HazardRiseFrames.Count),
                SinkDurationFrames = Math.Max(FallenKnightHeadSplashSinkFrames, enemy.HazardSinkFrames.Count),
                Direction = enemy.Direction,
                Phase = EnemyHazardPhase.Rise,
                PhaseTick = 0,
                IsAlive = true
            });
        }

        private void HandleFallenKnightHeadLanding(SpawnedEnemy enemy, bool wasGrounded, double impactVelocity)
        {
            if (!IsFallenKnightHeadEnemy(enemy) ||
                enemy.IsReturningToOwner ||
                wasGrounded ||
                !enemy.IsGrounded ||
                !enemy.CurrentBehaviorId.Equals("hop_contact", StringComparison.OrdinalIgnoreCase) ||
                enemy.IsTelegraphing ||
                enemy.IsAttacking ||
                impactVelocity < FallenKnightHeadSplashMinImpactSpeed)
                return;

            SpawnFallenKnightHeadLandingSplash(enemy);
        }

        private void UpdateFallenKnightHeadTrailHistory(SpawnedEnemy enemy)
        {
            if (!IsFallenKnightHeadEnemy(enemy) || enemy.TrailBodies.Count == 0)
                return;

            bool shouldRecord = enemy.IsAlive &&
                !enemy.IsDying &&
                !enemy.IsReturningToOwner &&
                !enemy.IsGrounded &&
                !enemy.IsTelegraphing &&
                !enemy.IsAttacking;
            if (!shouldRecord)
            {
                enemy.TrailHistory.Clear();
                return;
            }

            Point trailPoint = new(
                enemy.X + (enemy.Definition.Width / 2.0),
                enemy.Y + (enemy.Definition.Height * 0.58));
            if (enemy.TrailHistory.Count == 0)
            {
                enemy.TrailHistory.Add(trailPoint);
                return;
            }

            Point latestPoint = enemy.TrailHistory[0];
            double distanceFromLatest = Math.Sqrt(
                Math.Pow(trailPoint.X - latestPoint.X, 2) +
                Math.Pow(trailPoint.Y - latestPoint.Y, 2));
            if (distanceFromLatest >= FallenKnightHeadTrailSampleMinDistance)
                enemy.TrailHistory.Insert(0, trailPoint);
            else
                enemy.TrailHistory[0] = trailPoint;

            while (enemy.TrailHistory.Count > enemy.TrailBodies.Count)
                enemy.TrailHistory.RemoveAt(enemy.TrailHistory.Count - 1);
        }

        private void DrawFallenKnightHeadTrail(SpawnedEnemy enemy)
        {
            if (!IsFallenKnightHeadEnemy(enemy) || enemy.TrailBodies.Count == 0)
                return;

            bool shouldShow = !enemy.IsDying &&
                !enemy.IsReturningToOwner &&
                !IsFallenKnightHeadFireTower(enemy) &&
                enemy.TrailHistory.Count > 1;

            for (int index = 0; index < enemy.TrailBodies.Count; index++)
            {
                FrameworkElement trailBody = enemy.TrailBodies[index];
                if (!shouldShow || index >= enemy.TrailHistory.Count)
                {
                    trailBody.Visibility = Visibility.Hidden;
                    trailBody.Opacity = 0;
                    continue;
                }

                Point point = enemy.TrailHistory[index];
                double width = Math.Max(7, 19 - (index * 3));
                double height = Math.Max(5, 15 - (index * 2));
                trailBody.Width = width;
                trailBody.Height = height;
                trailBody.Opacity = Math.Max(0.08, 0.32 - (index * 0.06));
                trailBody.Visibility = Visibility.Visible;
                Canvas.SetLeft(trailBody, point.X - (width / 2.0));
                Canvas.SetTop(trailBody, point.Y - (height / 2.0));
            }
        }

        private string ChooseFallenKnightBehavior(SpawnedEnemy enemy)
        {
            if (!IsFallenKnightEnemy(enemy) || enemy.Definition.BehaviorIds.Count == 0)
                return enemy.CurrentBehaviorId;

            bool readyForHeadDetach =
                enemy.FallenKnightSpikeAttacksCompleted >= 2 &&
                enemy.FallenKnightSnowballAttacksCompleted >= 2 &&
                enemy.Definition.BehaviorIds.Any(id => id.Equals("fire_head", StringComparison.OrdinalIgnoreCase));
            if (readyForHeadDetach)
                return "fire_head";

            var standardBehaviors = enemy.Definition.BehaviorIds
                .Where(id => !id.Equals("fire_head", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (standardBehaviors.Count == 0)
                return SelectBehavior(enemy.Definition.BehaviorIds, enemy.CurrentBehaviorId);

            return SelectBehavior(standardBehaviors, enemy.CurrentBehaviorId);
        }

        private void RecordFallenKnightCompletedAttack(SpawnedEnemy enemy, string behaviorId)
        {
            if (!IsFallenKnightEnemy(enemy))
                return;

            if (behaviorId.Equals("spike_field", StringComparison.OrdinalIgnoreCase))
            {
                enemy.FallenKnightSpikeAttacksCompleted = Math.Min(2, enemy.FallenKnightSpikeAttacksCompleted + 1);
            }
            else if (behaviorId.Equals("snowball_heave", StringComparison.OrdinalIgnoreCase))
            {
                enemy.FallenKnightSnowballAttacksCompleted = Math.Min(2, enemy.FallenKnightSnowballAttacksCompleted + 1);
            }
            else if (behaviorId.Equals("fire_head", StringComparison.OrdinalIgnoreCase))
            {
                enemy.FallenKnightSpikeAttacksCompleted = 0;
                enemy.FallenKnightSnowballAttacksCompleted = 0;
            }
        }

        private bool ApplyFallenKnightPhysics(SpawnedEnemy enemy)
        {
            if (IsFallenKnightEnemy(enemy))
            {
                enemy.Y = groundY + playerHeight - enemy.Body.Height;
                enemy.VerticalVelocity = 0;
                enemy.IsGrounded = true;
                return true;
            }

            if (IsFallenKnightHeadEnemy(enemy) && enemy.IsReturningToOwner)
            {
                enemy.IsGrounded = false;
                return true;
            }

            return false;
        }

        private void UpdateEnemies()
        {
            foreach (var enemy in activeEnemies.ToList())
            {
                if (!enemy.IsAlive) continue;
                if (enemy.IsDying)
                {
                    UpdateEnemyDeathAnimation(enemy);
                    continue;
                }

                double playerCenterX = playerX + playerWidth / 2;
                double enemyCenterX = enemy.X + enemy.Definition.Width / 2;
                double distance = Math.Abs(playerCenterX - enemyCenterX);

                bool inAggroRange = distance <= enemy.Definition.AggroRange;
                if (inAggroRange)
                    enemy.IsAggroLocked = true;
                bool hasAggro = enemy.IsAggroLocked;
                // Enemy damage now comes from direct body contact (no dedicated enemy attack hitbox),
                // so "attack range" should be driven by body size plus a small anticipation buffer.
                double attackReach = Math.Max(16, enemy.Definition.Width * 0.7);
                bool inAttackRange = distance <= attackReach;

                if (IsFallenKnightEnemy(enemy))
                {
                    UpdateFallenKnightEnemy(enemy, hasAggro, playerCenterX, enemyCenterX);
                }
                else if (IsFallenKnightHeadEnemy(enemy))
                {
                    UpdateFallenKnightHeadEnemy(enemy, playerCenterX, enemyCenterX);
                }
                else
                {
                    UpdateEnemyBehaviorSelection(enemy, inAttackRange);

                    if (enemy.CurrentBehaviorId == "hop_contact")
                    {
                        UpdateHopContactEnemy(enemy, hasAggro, playerCenterX);
                    }
                    else if (enemy.CurrentBehaviorId == "swoop_dive")
                    {
                        UpdateSwoopDiveEnemy(enemy, hasAggro, inAggroRange, playerCenterX, enemyCenterX);
                    }
                    else if (enemy.CurrentBehaviorId == "dash_strike")
                    {
                        UpdateDashStrikeEnemy(enemy, hasAggro, inAggroRange, inAttackRange, playerCenterX, enemyCenterX);
                    }
                    else if (enemy.CurrentBehaviorId == "burrow_ambush")
                    {
                        UpdateCrawlerBurrowAmbushEnemy(enemy, hasAggro, inAggroRange, playerCenterX, enemyCenterX);
                    }
                    else if (enemy.CurrentBehaviorId == "ice_slam")
                    {
                        UpdateFrostlingIceSlamEnemy(enemy, hasAggro, inAggroRange, playerCenterX, enemyCenterX);
                    }
                    else
                    {
                        UpdateMeleeChaserEnemy(enemy, hasAggro, inAttackRange, playerCenterX, enemyCenterX);
                    }
                }

                bool wasGrounded = enemy.IsGrounded;
                double impactVelocity = enemy.VerticalVelocity;
                enemy.X += enemy.HorizontalVelocity;
                enemy.X = Math.Max(0, Math.Min(Width - enemy.Definition.Width, enemy.X));

                bool isGooBoss = enemy.Definition.Name.Equals("The Goo", StringComparison.OrdinalIgnoreCase);
                bool isBat = enemy.Definition.Name.Equals("bat", StringComparison.OrdinalIgnoreCase);
                double floorY = groundY + playerHeight - enemy.Body.Height;
                if (isBat)
                {
                    enemy.Y += enemy.VerticalVelocity;
                    double minFlightY = 10;
                    double maxFlightY = floorY - 8;
                    enemy.Y = Math.Max(minFlightY, Math.Min(maxFlightY, enemy.Y));
                    enemy.IsGrounded = false;
                }
                else if (ApplyFallenKnightPhysics(enemy))
                {
                }
                else if (ApplyCrawlerPhysics(enemy))
                {
                }
                else
                {
                    double enemyGravity = isGooBoss ? gameConfig.Gravity * 1.35 : gameConfig.Gravity;
                    enemy.VerticalVelocity += enemyGravity;
                    enemy.Y += enemy.VerticalVelocity;
                    if (enemy.Y >= floorY)
                    {
                        enemy.Y = floorY;
                        enemy.VerticalVelocity = 0;
                        enemy.IsGrounded = true;
                        enemy.HorizontalVelocity *= 0.6;
                        if (Math.Abs(enemy.HorizontalVelocity) < 0.05)
                            enemy.HorizontalVelocity = 0;
                    }
                    else
                    {
                        enemy.IsGrounded = false;
                        enemy.HorizontalVelocity *= 0.985;
                    }
                }

                HandleFallenKnightHeadLanding(enemy, wasGrounded, impactVelocity);
                UpdateFallenKnightHeadTrailHistory(enemy);

                if (enemy.AttackCooldownFrames > 0)
                    enemy.AttackCooldownFrames--;

                if (enemy.IsAttacking)
                {
                    enemy.AttackFramesRemaining--;
                    enemy.AttackAnimationTick++;
                    if (enemy.AttackFramesRemaining <= 0)
                    {
                        string completedBehaviorId = enemy.CurrentBehaviorId;
                        bool endedGooDashAttack =
                            enemy.Definition.Name.Equals("The Goo", StringComparison.OrdinalIgnoreCase) &&
                            enemy.CurrentBehaviorId.Equals("dash_strike", StringComparison.OrdinalIgnoreCase);
                        bool endedWolfDashAttack =
                            enemy.Definition.Name.Equals("wolf", StringComparison.OrdinalIgnoreCase) &&
                            enemy.CurrentBehaviorId.Equals("dash_strike", StringComparison.OrdinalIgnoreCase);
                        bool endedFrostlingIceSlam =
                            enemy.Definition.Name.Equals("frostling", StringComparison.OrdinalIgnoreCase) &&
                            enemy.CurrentBehaviorId.Equals("ice_slam", StringComparison.OrdinalIgnoreCase);
                        bool endedFallenKnightFireHead =
                            IsFallenKnightEnemy(enemy) &&
                            enemy.CurrentBehaviorId.Equals("fire_head", StringComparison.OrdinalIgnoreCase);
                        bool endedFallenKnightHeadFireTower =
                            IsFallenKnightHeadEnemy(enemy) &&
                            !enemy.IsReturningToOwner;
                        StopEnemyAttack(enemy);
                        RecordFallenKnightCompletedAttack(enemy, completedBehaviorId);
                        if (!endedFrostlingIceSlam)
                            enemy.HasLockedAttackDirection = false;
                        if (endedGooDashAttack)
                        {
                            StartEnemyRecoveryPause(enemy, GooDashRecoveryDurationFrames);
                        }
                        if (endedWolfDashAttack)
                        {
                            enemy.RecoveryPauseFrames = 7;
                            enemy.HorizontalVelocity = 0;
                        }
                        if (endedFrostlingIceSlam)
                        {
                            int recoveryDuration = Math.Max(
                                FrostlingRecoveryDurationFrames,
                                Math.Max(1, GetCooldownFramesForCurrentBehavior(enemy).Count) * 4);
                            StartEnemyRecoveryPause(enemy, recoveryDuration);
                        }
                        if (endedFallenKnightFireHead && enemy.LinkedEnemy != null)
                        {
                            StartFallenKnightBodyCollapse(enemy);
                        }
                        if (endedFallenKnightHeadFireTower)
                        {
                            enemy.SuppressContactDamage = false;
                            enemy.HorizontalVelocity = 0;
                            enemy.VerticalVelocity = 0;
                            enemy.IsGrounded = true;
                            enemy.BehaviorTimerFrames = 10;
                            SetEnemyBehavior(enemy, "hop_contact");
                            ResetFallenKnightHeadFireTowerCycle(enemy);
                        }
                    }
                }
                else
                {
                    enemy.AttackAnimationTick = 0;
                }

                if (enemy.IsTelegraphing)
                    enemy.TelegraphAnimationTick++;
                else
                    enemy.TelegraphAnimationTick = 0;

                if (enemy.BodySprite != null)
                {
                    if (IsCrawlerEnemy(enemy))
                    {
                        UpdateCrawlerAnimationSprite(enemy);
                        continue;
                    }

                    var attackFrames = GetAttackFramesForCurrentBehavior(enemy);
                    var telegraphFrames = GetTelegraphFramesForCurrentBehavior(enemy);
                    var cooldownFrames = GetCooldownFramesForCurrentBehavior(enemy);
                    bool useHopAttackFrames = enemy.CurrentBehaviorId.Equals("hop_contact", StringComparison.OrdinalIgnoreCase) &&
                        !enemy.IsGrounded &&
                        attackFrames.Count > 0;
                    bool useCooldownFrames = enemy.IsRecovering && cooldownFrames.Count > 0;
                    List<BitmapImage> frames;
                    if (enemy.IsTelegraphing && telegraphFrames.Count > 0)
                        frames = telegraphFrames;
                    else if (useCooldownFrames)
                        frames = cooldownFrames;
                    else if ((enemy.IsAttacking || useHopAttackFrames) && attackFrames.Count > 0)
                        frames = attackFrames;
                    else
                        frames = enemy.WalkFrames;

                    if (frames.Count > 0)
                    {
                        int frameIndex;
                        if (enemy.IsTelegraphing && telegraphFrames.Count > 0)
                        {
                            frameIndex = GetHeldAnimationFrameIndex(
                                telegraphFrames.Count,
                                enemy.TelegraphAnimationTick,
                                enemy.TelegraphAnimationDuration);
                        }
                        else if (useCooldownFrames)
                        {
                            if (IsFrostlingEnemy(enemy))
                            {
                                frameIndex = GetFrostlingCooldownFrameIndex(enemy, cooldownFrames);
                            }
                            else if (IsFallenKnightEnemy(enemy))
                            {
                                BitmapImage? recoveryFrame = GetFallenKnightRecoveryFrame(enemy, cooldownFrames);
                                if (recoveryFrame != null)
                                {
                                    enemy.BodySprite.Source = recoveryFrame;
                                    continue;
                                }
                                frameIndex = GetFallenKnightCooldownFrameIndex(enemy, cooldownFrames);
                            }
                            else
                            {
                                frameIndex = GetHeldAnimationFrameIndex(
                                    cooldownFrames.Count,
                                    Math.Max(0, enemy.RecoveryAnimationTick - 1),
                                    enemy.RecoveryAnimationDuration);
                            }
                        }
                        else if (enemy.IsAttacking && attackFrames.Count > 0)
                        {
                            frameIndex = GetHeldAnimationFrameIndex(
                                attackFrames.Count,
                                enemy.AttackAnimationTick,
                                enemy.AttackAnimationDuration);
                        }
                        else if (useHopAttackFrames)
                        {
                            frameIndex = (animationFrameCounter / 6) % attackFrames.Count;
                        }
                        else
                        {
                            int walkCadence = enemy.Definition.Name.Equals("frostling", StringComparison.OrdinalIgnoreCase)
                                ? 8
                                : 10;
                            frameIndex = (animationFrameCounter / walkCadence) % frames.Count;
                        }
                        enemy.BodySprite.Source = frames[frameIndex];
                    }
                }
            }
        }

        private string SelectBehavior(IReadOnlyList<string> behaviorIds, string? previousBehavior = null)
        {
            if (behaviorIds.Count == 0)
                return "melee_chaser";
            if (behaviorIds.Count == 1)
                return behaviorIds[0];

            var candidates = behaviorIds
                .Where(id => !string.Equals(id, previousBehavior, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (candidates.Count == 0)
                candidates = behaviorIds.ToList();
            return candidates[rng.Next(candidates.Count)];
        }

        private List<BitmapImage> GetAttackFramesForCurrentBehavior(SpawnedEnemy enemy)
        {
            if (enemy.BehaviorAttackFrames.TryGetValue(enemy.CurrentBehaviorId, out var behaviorFrames) && behaviorFrames.Count > 0)
                return behaviorFrames;
            return enemy.AttackFrames;
        }

        private List<BitmapImage> GetTelegraphFramesForCurrentBehavior(SpawnedEnemy enemy)
        {
            if (enemy.BehaviorTelegraphFrames.TryGetValue(enemy.CurrentBehaviorId, out var behaviorFrames) && behaviorFrames.Count > 0)
                return behaviorFrames;
            return new List<BitmapImage>();
        }

        private List<BitmapImage> GetCooldownFramesForCurrentBehavior(SpawnedEnemy enemy)
        {
            if (enemy.BehaviorCooldownFrames.TryGetValue(enemy.CurrentBehaviorId, out var behaviorFrames) && behaviorFrames.Count > 0)
                return behaviorFrames;

            if (enemy.Definition.Name.Equals("The Goo", StringComparison.OrdinalIgnoreCase) &&
                enemy.BehaviorCooldownFrames.TryGetValue("dash_strike", out var gooCooldownFrames) &&
                gooCooldownFrames.Count > 0)
                return gooCooldownFrames;

            return new List<BitmapImage>();
        }

        private int GetFrostlingCooldownFrameIndex(SpawnedEnemy enemy, List<BitmapImage> cooldownFrames)
        {
            if (cooldownFrames.Count == 0)
                return 0;

            int elapsed = Math.Max(0, enemy.RecoveryAnimationTick - 1);
            int duration = Math.Max(1, enemy.RecoveryAnimationDuration);
            const int transformInFrameCount = 4;
            const int transformOutFrameCount = 4;
            const int transformDurationFrames = 18;

            int introCount = Math.Min(transformInFrameCount, cooldownFrames.Count);
            int outroCount = Math.Min(transformOutFrameCount, Math.Max(0, cooldownFrames.Count - introCount));
            int hoverStart = introCount;
            int hoverCount = Math.Max(0, cooldownFrames.Count - introCount - outroCount);

            if (elapsed < transformDurationFrames || hoverCount == 0)
            {
                return GetHeldAnimationFrameIndex(
                    Math.Max(1, introCount),
                    elapsed,
                    Math.Max(1, Math.Min(duration, transformDurationFrames)));
            }

            if (elapsed >= duration - transformDurationFrames)
            {
                int outroTick = Math.Max(0, elapsed - (duration - transformDurationFrames));
                int outroIndex = GetHeldAnimationFrameIndex(
                    Math.Max(1, outroCount),
                    outroTick,
                    Math.Max(1, Math.Min(duration, transformDurationFrames)));
                return Math.Clamp(hoverStart + hoverCount + outroIndex, 0, cooldownFrames.Count - 1);
            }

            int hoverIndex = (animationFrameCounter / 12) % Math.Max(1, hoverCount);
            return Math.Clamp(hoverStart + hoverIndex, 0, cooldownFrames.Count - 1);
        }

        private int GetFallenKnightCooldownFrameIndex(SpawnedEnemy enemy, List<BitmapImage> cooldownFrames)
        {
            if (cooldownFrames.Count == 0)
                return 0;

            int elapsed = Math.Max(0, enemy.RecoveryAnimationTick);
            int introCount = Math.Min(4, cooldownFrames.Count);
            int rubbleStart = Math.Min(introCount, Math.Max(0, cooldownFrames.Count - 1));
            int rubbleCount = Math.Max(1, cooldownFrames.Count - rubbleStart);

            if (IsFallenKnightBodyCollapsed(enemy) && enemy.LinkedEnemy != null && !enemy.LinkedEnemy.IsReturningToOwner && elapsed < Math.Max(1, introCount * 5))
            {
                return GetHeldAnimationFrameIndex(
                    Math.Max(1, introCount),
                    elapsed,
                    Math.Max(1, introCount * 5));
            }

            int rubbleIndex = (animationFrameCounter / 10) % rubbleCount;
            return Math.Clamp(rubbleStart + rubbleIndex, 0, cooldownFrames.Count - 1);
        }

        private static bool IsFallenKnightHeadFireTower(SpawnedEnemy enemy)
            => IsFallenKnightHeadEnemy(enemy) &&
               !enemy.IsReturningToOwner &&
               (enemy.IsTelegraphing || enemy.IsAttacking);

        private double GetFallenKnightHeadReturnProgress(SpawnedEnemy enemy)
        {
            if (!IsFallenKnightEnemy(enemy) ||
                enemy.LinkedEnemy == null ||
                !enemy.LinkedEnemy.IsReturningToOwner)
                return 0;

            var head = enemy.LinkedEnemy;
            if (head.ReturnAnimationDuration <= 0)
                return 1;
            return Math.Clamp((double)head.ReturnAnimationTick / head.ReturnAnimationDuration, 0, 1);
        }

        private BitmapImage? GetFallenKnightRecoveryFrame(SpawnedEnemy enemy, List<BitmapImage> cooldownFrames)
        {
            if (!IsFallenKnightEnemy(enemy) || cooldownFrames.Count == 0)
                return null;

            if (enemy.LinkedEnemy == null || !enemy.LinkedEnemy.IsReturningToOwner)
                return cooldownFrames[Math.Clamp(GetFallenKnightCooldownFrameIndex(enemy, cooldownFrames), 0, cooldownFrames.Count - 1)];

            double progress = GetFallenKnightHeadReturnProgress(enemy);
            if (progress < 0.35 || enemy.WalkFrames.Count == 0)
                return cooldownFrames[cooldownFrames.Count - 1];

            int walkFrameIndex = Math.Clamp(
                (int)Math.Round(((progress - 0.35) / 0.65) * (enemy.WalkFrames.Count - 1)),
                0,
                enemy.WalkFrames.Count - 1);
            return enemy.WalkFrames[walkFrameIndex];
        }

        private (double ScaleX, double ScaleY, double OffsetY) GetFallenKnightReassemblyVisuals(SpawnedEnemy enemy)
        {
            if (!IsFallenKnightEnemy(enemy) ||
                enemy.LinkedEnemy == null ||
                !enemy.LinkedEnemy.IsReturningToOwner)
                return (1.0, 1.0, 0.0);

            double progress = GetFallenKnightHeadReturnProgress(enemy);
            double scaleX = FallenKnightReassemblyStartScaleX + ((1.0 - FallenKnightReassemblyStartScaleX) * progress);
            double scaleY = FallenKnightReassemblyStartScaleY + ((1.0 - FallenKnightReassemblyStartScaleY) * progress);
            double offsetY = (1.0 - progress) * FallenKnightReassemblyLiftPixels;
            return (scaleX, scaleY, offsetY);
        }

        private (double ScaleX, double ScaleY, double HealthBarLift, double Opacity, bool UseBottomAnchor) GetFallenKnightHeadFireTowerVisuals(SpawnedEnemy enemy)
        {
            if (!IsFallenKnightHeadFireTower(enemy))
                return (1.0, 1.0, 0.0, 1.0, false);

            double progress;
            if (enemy.IsTelegraphing)
            {
                int duration = Math.Max(1, enemy.TelegraphAnimationDuration);
                progress = Math.Clamp((double)enemy.TelegraphAnimationTick / duration, 0, 1);
            }
            else
            {
                progress = 1.0;
            }

            double easedProgress = Math.Sin(progress * (Math.PI / 2.0));
            double scaleX = 1.0 - ((1.0 - FallenKnightHeadFireTowerScaleX) * easedProgress);
            double scaleY = 1.0 + ((FallenKnightHeadFireTowerScaleY - 1.0) * easedProgress);
            double healthBarLift = enemy.Definition.Height * Math.Max(0, scaleY - 1.0);
            double opacity = 1.0;
            return (scaleX, scaleY, healthBarLift, opacity, true);
        }

        private double GetFrostlingSnowballBodyOffsetY(SpawnedEnemy enemy)
        {
            if (!IsFrostlingEnemy(enemy) || !enemy.IsRecovering)
                return 0;

            int duration = Math.Max(1, enemy.RecoveryAnimationDuration);
            double progress = duration <= 1
                ? 0
                : Math.Clamp((double)Math.Max(0, enemy.RecoveryAnimationTick - 1) / (duration - 1), 0, 1);
            double startDrop = Math.Max(18, enemy.Definition.Height * 0.28);
            double peakLift = Math.Max(40, enemy.Definition.Height * 0.42);
            double travelCurve = Math.Sin(progress * Math.PI);
            return Math.Round(startDrop - ((startDrop + peakLift) * travelCurve), 2);
        }

        private List<BitmapImage> GetUndergroundFramesForCurrentBehavior(SpawnedEnemy enemy)
        {
            if (enemy.BehaviorUndergroundFrames.TryGetValue(enemy.CurrentBehaviorId, out var behaviorFrames) && behaviorFrames.Count > 0)
                return behaviorFrames;

            return new List<BitmapImage>();
        }

        private List<BitmapImage> GetFloatFramesForCurrentBehavior(SpawnedEnemy enemy)
        {
            if (enemy.BehaviorFloatFrames.TryGetValue(enemy.CurrentBehaviorId, out var behaviorFrames) && behaviorFrames.Count > 0)
                return behaviorFrames;

            return new List<BitmapImage>();
        }

        private static bool IsCrawlerEnemy(EnemyDefinition definition)
            => definition.Name.Equals("crawler", StringComparison.OrdinalIgnoreCase);

        private static bool IsCrawlerEnemy(SpawnedEnemy enemy)
            => IsCrawlerEnemy(enemy.Definition);

        private static bool IsFrostlingEnemy(SpawnedEnemy enemy)
            => enemy.Definition.Name.Equals("frostling", StringComparison.OrdinalIgnoreCase);

        private static bool IsFallenKnightEnemy(SpawnedEnemy enemy)
            => enemy.Definition.Name.Equals("Fallen Knight", StringComparison.OrdinalIgnoreCase);

        private static bool IsFallenKnightHeadEnemy(SpawnedEnemy enemy)
            => enemy.Definition.Name.Equals("Fallen Knight Head", StringComparison.OrdinalIgnoreCase);

        private static bool IsFallenKnightBodyCollapsed(SpawnedEnemy enemy)
            => IsFallenKnightEnemy(enemy) && enemy.LinkedEnemy != null;

        private static bool CanPlayerDamageEnemy(SpawnedEnemy enemy)
        {
            if (!enemy.IsAlive || enemy.IsDying)
                return false;

            if (enemy.IsInvulnerable)
                return false;

            if (IsFallenKnightHeadEnemy(enemy) && enemy.IsReturningToOwner)
                return false;

            if (IsFallenKnightEnemy(enemy) && enemy.LinkedEnemy != null && !enemy.LinkedEnemy.IsReturningToOwner)
                return false;

            return true;
        }

        private int GetEnemyContactDamage(SpawnedEnemy enemy)
        {
            if (enemy.SuppressContactDamage)
                return 0;

            if (IsFallenKnightHeadEnemy(enemy) && enemy.IsReturningToOwner)
                return 0;

            if (IsFallenKnightBodyCollapsed(enemy))
                return 0;

            return enemy.Definition.ContactDamage;
        }

        private static bool IsEnemyCombatActive(SpawnedEnemy enemy)
            => enemy.IsAlive && !enemy.IsDying;

        private static bool IsCrawlerCollisionDisabled(SpawnedEnemy enemy)
            => IsCrawlerEnemy(enemy) &&
               (enemy.CrawlerPhase == CrawlerPhase.Burrowing ||
                enemy.CrawlerPhase == CrawlerPhase.Underground ||
                enemy.CrawlerPhase == CrawlerPhase.LeapTelegraph);

        private static bool IsEnemyCollisionDisabled(SpawnedEnemy enemy)
            => enemy.IsDying ||
               IsCrawlerCollisionDisabled(enemy) ||
               (IsFallenKnightEnemy(enemy) && enemy.LinkedEnemy != null && !enemy.LinkedEnemy.IsReturningToOwner) ||
               (IsFallenKnightHeadEnemy(enemy) && enemy.IsReturningToOwner);

        private EnemyDeathAnimationType GetEnemyDeathAnimationType(SpawnedEnemy enemy)
        {
            if (enemy.Definition.Name.Equals("slime", StringComparison.OrdinalIgnoreCase))
                return EnemyDeathAnimationType.SlimeBurst;
            if (enemy.Definition.Name.Equals("wolf", StringComparison.OrdinalIgnoreCase))
                return EnemyDeathAnimationType.WolfCollapse;
            if (enemy.Definition.Name.Equals("bat", StringComparison.OrdinalIgnoreCase))
                return EnemyDeathAnimationType.BatPlummet;
            if (enemy.Definition.Name.Equals("crawler", StringComparison.OrdinalIgnoreCase))
                return EnemyDeathAnimationType.CrawlerBackflip;

            return EnemyDeathAnimationType.FadeOut;
        }

        private static int GetDeathAnimationDuration(EnemyDeathAnimationType type)
        {
            return type switch
            {
                EnemyDeathAnimationType.SlimeBurst => SlimeDeathDurationFrames,
                EnemyDeathAnimationType.WolfCollapse => WolfDeathDurationFrames,
                EnemyDeathAnimationType.BatPlummet => BatDeathDurationFrames,
                EnemyDeathAnimationType.CrawlerBackflip => CrawlerDeathDurationFrames,
                _ => EnemyDeathDefaultDurationFrames,
            };
        }

        private void FinalizeEnemyDeath(SpawnedEnemy enemy)
        {
            enemy.IsAlive = false;
            currentAttackVictims.Remove(enemy);
            GameCanvas.Children.Remove(enemy.Body);
            GameCanvas.Children.Remove(enemy.Label);
            GameCanvas.Children.Remove(enemy.HealthBg);
            GameCanvas.Children.Remove(enemy.HealthFill);
            GameCanvas.Children.Remove(enemy.AttackHitbox);
            activeEnemies.Remove(enemy);

            if (currentStageNumber > 0 && activeEnemies.Count == 0)
            {
                int unlockedStage = currentArea.IsBossArea && AreaDefinitions.IsFinalBossStage(currentStageNumber)
                    ? currentStageNumber
                    : currentStageNumber + 1;
                highestUnlockedStage = Math.Max(highestUnlockedStage, unlockedStage);
                string clearMessage = currentArea.IsBossArea
                    ? (AreaDefinitions.IsFinalBossStage(currentStageNumber)
                        ? "Final boss defeated! You win!"
                        : "Boss defeated! Return right to Town.")
                    : $"Area clear! Stage {currentStageNumber + 1} unlocked.";

                if (!stageTookDamage)
                {
                    int bonusXp = Math.Max(0, playerData.NextLevelXp - playerData.Experience);
                    playerData.Gold += PerfectClearGoldBonus;
                    GainExperience(bonusXp, showLevelUpStatus: false);
                    ShowStatus($"{clearMessage} Perfect clear! Bonus: +{bonusXp} XP and +{PerfectClearGoldBonus}g.", 150);
                }
                else
                {
                    ShowStatus(clearMessage, 100);
                }

                if (currentArea.IsBossArea)
                    SaveGameState();
            }
        }

        private void UpdateEnemyDeathAnimation(SpawnedEnemy enemy)
        {
            enemy.DeathAnimationTick++;
            int duration = Math.Max(1, enemy.DeathAnimationDuration);
            double progress = Math.Clamp((double)enemy.DeathAnimationTick / duration, 0, 1);
            double floorY = groundY + playerHeight - enemy.Body.Height;

            enemy.DeathOpacity = 1.0;
            enemy.DeathScaleX = 1.0;
            enemy.DeathScaleY = 1.0;
            enemy.DeathOffsetY = 0;

            switch (enemy.DeathAnimationType)
            {
                case EnemyDeathAnimationType.SlimeBurst:
                    enemy.DeathScaleX = 1.0 + (progress * 0.9);
                    enemy.DeathScaleY = 1.0 + (progress * 0.45);
                    enemy.DeathOpacity = Math.Max(0, 1.0 - (progress * 1.15));
                    enemy.DeathOffsetY = -2.5 * Math.Sin(progress * Math.PI);
                    enemy.DeathRotationDegrees = ((enemy.DeathAnimationTick % 2 == 0) ? -10 : 10) * (1.0 - progress);
                    break;

                case EnemyDeathAnimationType.WolfCollapse:
                    enemy.DeathRotationDegrees = 96 * progress * enemy.Direction;
                    enemy.DeathScaleX = 1.0 + (progress * 0.08);
                    enemy.DeathScaleY = Math.Max(0.5, 1.0 - (progress * 0.42));
                    enemy.DeathOffsetY = progress * 7.5;
                    enemy.DeathOpacity = progress < 0.72
                        ? 1.0
                        : Math.Max(0, 1.0 - ((progress - 0.72) / 0.28));
                    break;

                case EnemyDeathAnimationType.BatPlummet:
                    enemy.VerticalVelocity += gameConfig.Gravity * 1.6;
                    enemy.Y += enemy.VerticalVelocity;
                    enemy.X = Math.Max(0, Math.Min(Width - enemy.Definition.Width, enemy.X + enemy.HorizontalVelocity));
                    enemy.HorizontalVelocity *= 0.985;
                    if (enemy.Y >= floorY)
                    {
                        enemy.Y = floorY;
                        enemy.VerticalVelocity = 0;
                        enemy.HorizontalVelocity = 0;
                        enemy.DeathRotationDegrees = 90 * enemy.Direction;
                        enemy.DeathOffsetY = 4;
                        enemy.DeathScaleY = 0.72;
                    }
                    else
                    {
                        enemy.DeathRotationDegrees += 18 * enemy.Direction;
                    }
                    enemy.DeathOpacity = progress < 0.8
                        ? 1.0
                        : Math.Max(0, 1.0 - ((progress - 0.8) / 0.2));
                    break;

                case EnemyDeathAnimationType.CrawlerBackflip:
                    enemy.VerticalVelocity += gameConfig.Gravity * 0.55;
                    enemy.Y += enemy.VerticalVelocity;
                    if (enemy.Y >= floorY)
                    {
                        enemy.Y = floorY;
                        enemy.VerticalVelocity = 0;
                    }
                    enemy.DeathRotationDegrees = 180 * progress * enemy.Direction;
                    enemy.DeathScaleY = Math.Max(0.76, 1.0 - (progress * 0.22));
                    enemy.DeathOffsetY = progress * 4.5;
                    enemy.DeathOpacity = progress < 0.76
                        ? 1.0
                        : Math.Max(0, 1.0 - ((progress - 0.76) / 0.24));
                    break;

                default:
                    enemy.DeathOpacity = Math.Max(0, 1.0 - progress);
                    enemy.DeathOffsetY = progress * 3;
                    enemy.DeathRotationDegrees = 18 * progress * enemy.Direction;
                    break;
            }

            if (enemy.DeathAnimationTick >= enemy.DeathAnimationDuration)
                FinalizeEnemyDeath(enemy);
        }

        private void StartCrawlerBurrow(SpawnedEnemy enemy)
        {
            enemy.CrawlerPhase = CrawlerPhase.Burrowing;
            enemy.CrawlerPhaseTick = 0;
            enemy.CrawlerPhaseDuration = Math.Max(
                CrawlerBurrowDurationFrames,
                Math.Max(1, GetTelegraphFramesForCurrentBehavior(enemy).Count) * 3);
            enemy.HorizontalVelocity = 0;
            enemy.VerticalVelocity = 0;
            enemy.AttackDamageApplied = false;
            enemy.SpriteRotationDegrees = 0;
            enemy.SpriteRotationVelocityDegrees = 0;
        }

        private void StartCrawlerUnderground(SpawnedEnemy enemy)
        {
            enemy.CrawlerPhase = CrawlerPhase.Underground;
            enemy.CrawlerPhaseTick = 0;
            enemy.CrawlerPhaseDuration = CrawlerUndergroundDurationFrames;
            enemy.HorizontalVelocity = 0;
            enemy.VerticalVelocity = 0;
            enemy.AttackDamageApplied = false;
            enemy.SpriteRotationDegrees = 0;
            enemy.SpriteRotationVelocityDegrees = 0;
        }

        private void StartCrawlerLeap(SpawnedEnemy enemy, double playerCenterX, double enemyCenterX)
        {
            enemy.CrawlerPhase = CrawlerPhase.LeapAttack;
            enemy.CrawlerPhaseTick = 0;
            enemy.CrawlerPhaseDuration = Math.Max(
                CrawlerLeapAnimationDurationFrames,
                Math.Max(1, GetAttackFramesForCurrentBehavior(enemy).Count) * 2);
            enemy.Direction = playerCenterX >= enemyCenterX ? 1 : -1;
            enemy.HorizontalVelocity = enemy.Direction * Math.Max(1.15, enemy.Speed * 1.1);
            enemy.VerticalVelocity = CrawlerLeapVerticalSpeed;
            enemy.IsGrounded = false;
            enemy.AttackDamageApplied = false;
            enemy.SpriteRotationVelocityDegrees = CrawlerSpinDegreesPerFrame * enemy.Direction;
        }

        private void StartCrawlerLeapTelegraph(SpawnedEnemy enemy, double playerCenterX, double enemyCenterX)
        {
            enemy.CrawlerPhase = CrawlerPhase.LeapTelegraph;
            enemy.CrawlerPhaseTick = 0;
            enemy.CrawlerPhaseDuration = CrawlerLeapTelegraphDurationFrames;
            enemy.Direction = playerCenterX >= enemyCenterX ? 1 : -1;
            enemy.HorizontalVelocity = 0;
            enemy.VerticalVelocity = 0;
            enemy.AttackDamageApplied = false;
            enemy.SpriteRotationDegrees = 0;
            enemy.SpriteRotationVelocityDegrees = 0;
        }

        private void StartCrawlerFloat(SpawnedEnemy enemy)
        {
            enemy.CrawlerPhase = CrawlerPhase.FloatDescent;
            enemy.CrawlerPhaseTick = 0;
            enemy.CrawlerPhaseDuration = 0;
            enemy.VerticalVelocity = Math.Max(0.32, enemy.VerticalVelocity);
            enemy.HorizontalVelocity *= 0.82;
        }

        private void FinishCrawlerCycle(SpawnedEnemy enemy)
        {
            enemy.CrawlerPhase = CrawlerPhase.SurfaceWalk;
            enemy.CrawlerPhaseTick = 0;
            enemy.CrawlerPhaseDuration = 0;
            enemy.HorizontalVelocity = 0;
            enemy.VerticalVelocity = 0;
            enemy.IsGrounded = true;
            enemy.AttackCooldownFrames = Math.Max(18, enemy.Definition.BehaviorIntervalFrames / 2);
            enemy.AttackDamageApplied = false;
            enemy.SpriteRotationDegrees = 0;
            enemy.SpriteRotationVelocityDegrees = 0;
        }

        private void UpdateCrawlerBurrowAmbushEnemy(
            SpawnedEnemy enemy,
            bool hasAggro,
            bool inAggroRange,
            double playerCenterX,
            double enemyCenterX)
        {
            if (enemy.CrawlerPhase == CrawlerPhase.Burrowing)
            {
                enemy.CrawlerPhaseTick++;
                enemy.HorizontalVelocity = 0;
                enemy.VerticalVelocity = 0;
                if (enemy.CrawlerPhaseTick >= enemy.CrawlerPhaseDuration)
                    StartCrawlerUnderground(enemy);
                return;
            }

            if (enemy.CrawlerPhase == CrawlerPhase.Underground)
            {
                enemy.CrawlerPhaseTick++;
                double targetX = Math.Clamp(
                    playerCenterX - (enemy.Definition.Width / 2.0),
                    0,
                    Math.Max(0, Width - enemy.Definition.Width));
                double undergroundStep = moveSpeed + CrawlerUndergroundPlayerSpeedLead;
                double step = GetStepTowards(enemy.X, targetX, undergroundStep);
                if (Math.Abs(step) > 0.05)
                    enemy.Direction = step >= 0 ? 1 : -1;

                enemy.X += step;
                enemy.HorizontalVelocity = 0;
                enemy.VerticalVelocity = 0;

                if (enemy.CrawlerPhaseTick >= enemy.CrawlerPhaseDuration)
                {
                    double currentCenterX = enemy.X + (enemy.Definition.Width / 2.0);
                    StartCrawlerLeapTelegraph(enemy, playerCenterX, currentCenterX);
                }
                return;
            }

            if (enemy.CrawlerPhase == CrawlerPhase.LeapTelegraph)
            {
                enemy.CrawlerPhaseTick++;
                enemy.Direction = playerCenterX >= enemyCenterX ? 1 : -1;
                enemy.HorizontalVelocity = 0;
                enemy.VerticalVelocity = 0;

                if (enemy.CrawlerPhaseTick >= enemy.CrawlerPhaseDuration)
                {
                    double currentCenterX = enemy.X + (enemy.Definition.Width / 2.0);
                    StartCrawlerLeap(enemy, playerCenterX, currentCenterX);
                }
                return;
            }

            if (enemy.CrawlerPhase == CrawlerPhase.LeapAttack)
            {
                enemy.CrawlerPhaseTick++;
                enemy.SpriteRotationDegrees += enemy.SpriteRotationVelocityDegrees;
                enemy.HorizontalVelocity = enemy.Direction * Math.Max(0.9, enemy.Speed * 0.95);
                if (enemy.VerticalVelocity >= 0)
                    StartCrawlerFloat(enemy);
                return;
            }

            if (enemy.CrawlerPhase == CrawlerPhase.FloatDescent)
            {
                enemy.CrawlerPhaseTick++;
                enemy.SpriteRotationDegrees += enemy.SpriteRotationVelocityDegrees;
                enemy.SpriteRotationVelocityDegrees *= CrawlerFloatSpinDamping;
                enemy.HorizontalVelocity *= CrawlerFloatHorizontalDamping;
                return;
            }

            enemy.SpriteRotationDegrees = 0;
            enemy.SpriteRotationVelocityDegrees = 0;
            if (hasAggro)
            {
                enemy.Direction = playerCenterX >= enemyCenterX ? 1 : -1;
                bool shouldBurrow =
                    enemy.AttackCooldownFrames <= 0 &&
                    inAggroRange &&
                    Math.Abs(playerCenterX - enemyCenterX) <= CrawlerBurrowTriggerDistance &&
                    enemy.IsGrounded;
                if (shouldBurrow)
                {
                    StartCrawlerBurrow(enemy);
                    return;
                }

                if (Math.Abs(playerCenterX - enemyCenterX) > 10)
                    enemy.X += enemy.Speed * enemy.Direction * 0.92;
                return;
            }

            if (enemy.X <= enemy.LeftBound)
                enemy.Direction = 1;
            else if (enemy.X >= enemy.RightBound)
                enemy.Direction = -1;

            enemy.X += enemy.Speed * enemy.Direction * 0.82;
        }

        private bool ApplyCrawlerPhysics(SpawnedEnemy enemy)
        {
            if (!IsCrawlerEnemy(enemy))
                return false;

            double floorY = groundY + playerHeight - enemy.Body.Height;
            double crawlerMinLeapY = Math.Max(8, floorY - CrawlerLeapMaxRisePixels);
            switch (enemy.CrawlerPhase)
            {
                case CrawlerPhase.Burrowing:
                case CrawlerPhase.Underground:
                case CrawlerPhase.LeapTelegraph:
                    enemy.Y = floorY;
                    enemy.VerticalVelocity = 0;
                    enemy.IsGrounded = true;
                    return true;

                case CrawlerPhase.LeapAttack:
                    enemy.VerticalVelocity += gameConfig.Gravity * CrawlerLeapGravityMultiplier;
                    enemy.Y += enemy.VerticalVelocity;
                    if (enemy.Y < crawlerMinLeapY)
                    {
                        enemy.Y = crawlerMinLeapY;
                        enemy.VerticalVelocity = Math.Max(enemy.VerticalVelocity, -0.35);
                    }
                    enemy.IsGrounded = false;
                    if (enemy.Y >= floorY)
                    {
                        enemy.Y = floorY;
                        StartCrawlerFloat(enemy);
                    }
                    return true;

                case CrawlerPhase.FloatDescent:
                    enemy.VerticalVelocity = Math.Min(
                        enemy.VerticalVelocity + (gameConfig.Gravity * CrawlerFloatGravityMultiplier),
                        CrawlerFloatMaxFallSpeed);
                    enemy.Y += enemy.VerticalVelocity;
                    enemy.IsGrounded = false;
                    if (enemy.Y >= floorY)
                    {
                        enemy.Y = floorY;
                        FinishCrawlerCycle(enemy);
                    }
                    return true;
            }

            return false;
        }

        private void UpdateCrawlerAnimationSprite(SpawnedEnemy enemy)
        {
            if (enemy.BodySprite == null)
                return;

            List<BitmapImage> frames = enemy.WalkFrames;
            int frameIndex = 0;

            switch (enemy.CrawlerPhase)
            {
                case CrawlerPhase.Burrowing:
                    frames = GetTelegraphFramesForCurrentBehavior(enemy);
                    if (frames.Count == 0)
                        frames = enemy.WalkFrames;
                    if (frames.Count > 0)
                    {
                        frameIndex = GetHeldAnimationFrameIndex(
                            frames.Count,
                            enemy.CrawlerPhaseTick,
                            Math.Max(1, enemy.CrawlerPhaseDuration));
                    }
                    break;

                case CrawlerPhase.Underground:
                    frames = GetUndergroundFramesForCurrentBehavior(enemy);
                    if (frames.Count == 0)
                        frames = GetCooldownFramesForCurrentBehavior(enemy);
                    if (frames.Count == 0)
                        frames = enemy.WalkFrames;
                    if (frames.Count > 0)
                        frameIndex = (animationFrameCounter / 4) % frames.Count;
                    break;

                case CrawlerPhase.LeapTelegraph:
                    frames = GetUndergroundFramesForCurrentBehavior(enemy);
                    if (frames.Count == 0)
                        frames = GetCooldownFramesForCurrentBehavior(enemy);
                    if (frames.Count == 0)
                        frames = enemy.WalkFrames;
                    if (frames.Count > 0)
                    {
                        frameIndex = GetHeldAnimationFrameIndex(
                            frames.Count,
                            enemy.CrawlerPhaseTick,
                            Math.Max(1, enemy.CrawlerPhaseDuration));
                    }
                    break;

                case CrawlerPhase.LeapAttack:
                    frames = GetAttackFramesForCurrentBehavior(enemy);
                    if (frames.Count == 0)
                        frames = enemy.WalkFrames;
                    if (frames.Count > 0)
                    {
                        frameIndex = GetHeldAnimationFrameIndex(
                            frames.Count,
                            enemy.CrawlerPhaseTick,
                            Math.Max(1, enemy.CrawlerPhaseDuration));
                    }
                    break;

                case CrawlerPhase.FloatDescent:
                    frames = GetFloatFramesForCurrentBehavior(enemy);
                    if (frames.Count == 0)
                        frames = GetCooldownFramesForCurrentBehavior(enemy);
                    if (frames.Count == 0)
                        frames = enemy.WalkFrames;
                    if (frames.Count > 0)
                        frameIndex = (animationFrameCounter / 10) % frames.Count;
                    break;

                default:
                    if (frames.Count > 0)
                        frameIndex = (animationFrameCounter / 8) % frames.Count;
                    break;
            }

            if (frames.Count > 0)
                enemy.BodySprite.Source = frames[Math.Clamp(frameIndex, 0, frames.Count - 1)];
        }

        private void UpdateFrostlingIceSlamEnemy(SpawnedEnemy enemy, bool hasAggro, bool inAggroRange, double playerCenterX, double enemyCenterX)
        {
            enemy.VerticalVelocity = 0;
            enemy.IsGrounded = true;
            double distanceToPlayer = Math.Abs(playerCenterX - enemyCenterX);

            if (enemy.IsRecovering)
            {
                enemy.HorizontalVelocity = 0;
                enemy.RecoveryAnimationTick++;
                enemy.RecoveryPauseFrames--;
                if (enemy.RecoveryPauseFrames <= 0)
                {
                    StopEnemyRecoveryPause(enemy);
                    enemy.HasLockedAttackDirection = false;
                }
                return;
            }

            if (enemy.IsTelegraphing)
            {
                if (!enemy.HasLockedAttackDirection)
                {
                    enemy.LockedAttackDirection = playerCenterX >= enemyCenterX ? 1 : -1;
                    enemy.HasLockedAttackDirection = true;
                }

                enemy.Direction = enemy.LockedAttackDirection;
                enemy.HorizontalVelocity = 0;
                enemy.TelegraphFramesRemaining--;
                if (enemy.TelegraphFramesRemaining <= 0)
                {
                    StopEnemyTelegraph(enemy);
                    enemy.Direction = enemy.LockedAttackDirection;
                    int attackDuration = Math.Max(
                        FrostlingAttackDurationFrames,
                        Math.Max(1, GetAttackFramesForCurrentBehavior(enemy).Count) * 4);
                    StartEnemyAttack(enemy, attackDuration);
                    enemy.AttackCooldownFrames = Math.Max(enemy.Definition.BehaviorIntervalFrames + 12, 24);
                }
                return;
            }

            if (enemy.IsAttacking)
            {
                enemy.Direction = enemy.HasLockedAttackDirection ? enemy.LockedAttackDirection : enemy.Direction;
                enemy.HorizontalVelocity = 0;
                if (!enemy.AttackEffectTriggered && enemy.AttackAnimationTick >= Math.Max(4, enemy.AttackAnimationDuration / 3))
                {
                    SpawnFrostlingIcicleHazards(enemy);
                    enemy.AttackEffectTriggered = true;
                }
                return;
            }

            if (hasAggro)
            {
                enemy.Direction = playerCenterX >= enemyCenterX ? 1 : -1;
                bool withinAttackWindow = distanceToPlayer >= FrostlingMinimumAttackRange && distanceToPlayer <= FrostlingAttackRange;
                if (inAggroRange && withinAttackWindow && enemy.AttackCooldownFrames <= 0)
                {
                    enemy.LockedAttackDirection = enemy.Direction;
                    enemy.HasLockedAttackDirection = true;
                    int telegraphDuration = Math.Max(
                        FrostlingTelegraphDurationFrames,
                        Math.Max(1, GetTelegraphFramesForCurrentBehavior(enemy).Count) * 4);
                    StartEnemyTelegraph(enemy, telegraphDuration);
                    enemy.HorizontalVelocity = 0;
                    return;
                }
            }
            else
            {
                if (enemy.X <= enemy.LeftBound) enemy.Direction = 1;
                else if (enemy.X >= enemy.RightBound) enemy.Direction = -1;
            }

            double walkSpeed = hasAggro ? enemy.Speed * 0.72 : enemy.Speed * 0.48;
            enemy.HorizontalVelocity = (!hasAggro || distanceToPlayer > FrostlingAttackRange)
                ? walkSpeed * enemy.Direction
                : 0;
        }

        private void UpdateEnemyBehaviorSelection(SpawnedEnemy enemy, bool inAttackRange)
        {
            if (enemy.Definition.BehaviorIds.Count == 0)
            {
                enemy.CurrentBehaviorId = "melee_chaser";
                return;
            }

            if (enemy.Definition.BehaviorIds.Count == 1)
            {
                enemy.CurrentBehaviorId = enemy.Definition.BehaviorIds[0];
                return;
            }

            if (enemy.IsAttacking || enemy.IsTelegraphing || enemy.IsRecovering)
                return;

            bool isGooBoss = enemy.Definition.Name.Equals("The Goo", StringComparison.OrdinalIgnoreCase);
            if (isGooBoss || IsFallenKnightEnemy(enemy) || IsFallenKnightHeadEnemy(enemy))
            {
                return;
            }

            if (enemy.BehaviorCycleFrames > 0)
                enemy.BehaviorCycleFrames--;

            if (enemy.BehaviorCycleFrames <= 0)
            {
                SetEnemyBehavior(enemy, SelectBehavior(enemy.Definition.BehaviorIds, enemy.CurrentBehaviorId));
                enemy.BehaviorCycleFrames = rng.Next(90, 181);
                enemy.BehaviorTimerFrames = rng.Next(0, Math.Max(1, enemy.Definition.BehaviorIntervalFrames));
                StopEnemyTelegraph(enemy);
                StopEnemyAttack(enemy);
                StopEnemyRecoveryPause(enemy);
                enemy.HasLockedAttackDirection = false;
            }
        }

        private void UpdateMeleeChaserEnemy(SpawnedEnemy enemy, bool hasAggro, bool inAttackRange, double playerCenterX, double enemyCenterX)
        {
            if (hasAggro)
            {
                enemy.Direction = playerCenterX >= enemyCenterX ? 1 : -1;

                if (!enemy.IsAttacking && enemy.AttackCooldownFrames <= 0 && inAttackRange)
                {
                    var attackFrames = GetAttackFramesForCurrentBehavior(enemy);
                    StartEnemyAttack(enemy, Math.Max(10, attackFrames.Count * 8));
                    enemy.AttackDamageApplied = false;
                    enemy.AttackCooldownFrames = Math.Max(10, enemy.Definition.BehaviorIntervalFrames);
                }
            }
            else
            {
                if (enemy.X <= enemy.LeftBound) enemy.Direction = 1;
                else if (enemy.X >= enemy.RightBound) enemy.Direction = -1;
            }

            if (!enemy.IsAttacking && (!hasAggro || !inAttackRange))
                enemy.X += enemy.Speed * enemy.Direction;
        }

        private void UpdateHopContactEnemy(SpawnedEnemy enemy, bool hasAggro, double playerCenterX)
        {
            bool isSlime = enemy.Definition.Name.Equals("slime", StringComparison.OrdinalIgnoreCase);
            bool isGooBoss = enemy.Definition.Name.Equals("The Goo", StringComparison.OrdinalIgnoreCase);
            bool isFallenKnightHead = IsFallenKnightHeadEnemy(enemy);

            if (HandleGooRecoveryState(enemy))
                return;

            if (enemy.BehaviorTimerFrames > 0)
                enemy.BehaviorTimerFrames--;

            if (hasAggro)
                enemy.Direction = playerCenterX >= enemy.X ? 1 : -1;

            if (isGooBoss && enemy.GooHopJumpsRemaining <= 0 && enemy.IsGrounded)
            {
                StartEnemyRecoveryPause(enemy, GooDashRecoveryDurationFrames);
                enemy.HorizontalVelocity = 0;
                return;
            }

            if (enemy.IsGrounded && enemy.BehaviorTimerFrames <= 0)
            {
                if (isFallenKnightHead &&
                    hasAggro &&
                    enemy.SpecialActionCounter >= enemy.SpecialActionStep)
                {
                    enemy.Direction = playerCenterX >= enemy.X ? 1 : -1;
                    enemy.HorizontalVelocity = 0;
                    enemy.VerticalVelocity = 0;
                    enemy.IsGrounded = true;
                    enemy.SuppressContactDamage = true;
                    SetEnemyBehavior(enemy, "fire_tower");
                    StartEnemyTelegraph(enemy, FallenKnightHeadFireTowerChargeFrames);
                    return;
                }

                if (!hasAggro)
                {
                    // Wander hops: choose a random direction until the enemy has aggro.
                    bool nearLeftBound = enemy.X <= enemy.LeftBound + 8;
                    bool nearRightBound = enemy.X >= enemy.RightBound - 8;
                    if (nearLeftBound) enemy.Direction = 1;
                    else if (nearRightBound) enemy.Direction = -1;
                    else enemy.Direction = rng.NextDouble() < 0.5 ? -1 : 1;
                }

                double hopJumpMultiplier = isSlime ? 0.68 : isFallenKnightHead ? 0.88 : 1.05;
                if (isGooBoss)
                {
                    // Increase gravity + launch speed together so apex height stays about the same.
                    hopJumpMultiplier *= Math.Sqrt(1.35);
                }
                double hopSpeedBoost = hasAggro
                    ? 3.0
                    : (1.15 + (rng.NextDouble() * 0.95));
                if (isFallenKnightHead)
                {
                    double currentEnemyCenterX = enemy.X + (enemy.Definition.Width / 2.0);
                    double playerSideDirection = playerCenterX >= currentEnemyCenterX ? 1.0 : -1.0;
                    double targetOffsetFromPlayer = rng.NextDouble() < 0.5
                        ? playerSideDirection * GetRandomDouble(36, FallenKnightHeadJumpLandingSpread)
                        : -playerSideDirection * GetRandomDouble(24, FallenKnightHeadJumpLandingSpread * 0.9);
                    double targetX = Math.Clamp(
                        playerCenterX + targetOffsetFromPlayer - (enemy.Definition.Width / 2.0),
                        0,
                        Math.Max(0, Width - enemy.Definition.Width));
                    double deltaX = targetX - enemy.X;
                    if (Math.Abs(deltaX) < FallenKnightHeadJumpMinDistance)
                    {
                        double preferredDirection = Math.Abs(deltaX) < 0.001
                            ? (playerCenterX >= currentEnemyCenterX ? 1 : -1)
                            : Math.Sign(deltaX);
                        targetX = Math.Clamp(
                            enemy.X + (preferredDirection * FallenKnightHeadJumpMinDistance),
                            0,
                            Math.Max(0, Width - enemy.Definition.Width));
                        deltaX = targetX - enemy.X;
                        if (Math.Abs(deltaX) < FallenKnightHeadJumpMinDistance)
                        {
                            preferredDirection *= -1;
                            targetX = Math.Clamp(
                                enemy.X + (preferredDirection * FallenKnightHeadJumpMinDistance),
                                0,
                                Math.Max(0, Width - enemy.Definition.Width));
                            deltaX = targetX - enemy.X;
                        }
                    }

                    const double airDrag = 0.985;
                    int airFrames = Math.Clamp(
                        FallenKnightHeadJumpMinAirFrames +
                        (int)Math.Round(Math.Abs(deltaX) / 13.0) +
                        rng.Next(-2, 4),
                        FallenKnightHeadJumpMinAirFrames,
                        FallenKnightHeadJumpMaxAirFrames);

                    for (int frames = airFrames; frames <= FallenKnightHeadJumpMaxAirFrames; frames++)
                    {
                        double horizontalTravelFactor = (1.0 - Math.Pow(airDrag, frames)) / (1.0 - airDrag);
                        double requiredSpeed = Math.Abs(deltaX) / Math.Max(0.001, horizontalTravelFactor);
                        airFrames = frames;
                        if (requiredSpeed <= FallenKnightHeadTargetedJumpMaxSpeed)
                            break;
                    }

                    double horizontalTravel = (1.0 - Math.Pow(airDrag, airFrames)) / (1.0 - airDrag);
                    enemy.Direction = deltaX >= 0 ? 1 : -1;
                    enemy.HorizontalVelocity = horizontalTravel <= 0.001
                        ? 0
                        : Math.Clamp(
                            deltaX / horizontalTravel,
                            -FallenKnightHeadTargetedJumpMaxSpeed,
                            FallenKnightHeadTargetedJumpMaxSpeed);
                    enemy.VerticalVelocity = -(gameConfig.Gravity * (airFrames + 1) / 2.0);
                    enemy.SpecialActionCounter++;
                }
                else
                {
                    enemy.VerticalVelocity = gameConfig.JumpStrength * hopJumpMultiplier;
                }

                if (isGooBoss)
                    hopSpeedBoost *= hasAggro ? 1.55 : 1.35;
                if (!isFallenKnightHead)
                    enemy.HorizontalVelocity = enemy.Direction * enemy.Speed * hopSpeedBoost;
                int hopIntervalFrames = isSlime
                    ? (int)Math.Round(enemy.Definition.BehaviorIntervalFrames * 0.74)
                    : enemy.Definition.BehaviorIntervalFrames;
                if (isSlime && hasAggro)
                    hopIntervalFrames = (int)Math.Round(hopIntervalFrames / 1.4);
                if (isFallenKnightHead)
                    hopIntervalFrames = hasAggro ? 10 : 14;
                if (isGooBoss)
                {
                    hopIntervalFrames = (int)Math.Round(hopIntervalFrames * 0.66);
                    if (enemy.GooHopJumpsRemaining <= 0)
                        enemy.GooHopJumpsRemaining = rng.Next(4, 11);
                    enemy.GooHopJumpsRemaining = Math.Max(0, enemy.GooHopJumpsRemaining - 1);
                }

                enemy.BehaviorTimerFrames = isGooBoss && enemy.GooHopJumpsRemaining <= 0
                    ? 0
                    : Math.Max(10, hopIntervalFrames - 14 + rng.Next(-10, 11));
                StopEnemyAttack(enemy);
            }
        }

        private void UpdateSwoopDiveEnemy(SpawnedEnemy enemy, bool hasAggro, bool inAggroRange, double playerCenterX, double enemyCenterX)
        {
            double floorLineY = groundY + playerHeight - enemy.Definition.Height;
            if (double.IsNaN(enemy.FlightHomeY))
                enemy.FlightHomeY = Math.Max(10, floorLineY - 44);

            double hoverY = enemy.FlightHomeY;
            double minFlightY = Math.Max(10, hoverY - 14);
            double maxFlightY = Math.Min(floorLineY - 16, hoverY + 14);
            double diveFloorY = floorLineY - 6;
            const double hoverOffsetX = 52;
            const double telegraphOffsetX = 42;
            const double diveOvershootX = 64;
            const double groundSkimDistance = 72;
            double chaseLeftBound = 2;
            double chaseRightBound = Math.Max(chaseLeftBound, Width - enemy.Definition.Width - 2);
            double patrolLeftBound = enemy.LeftBound + 2;
            double patrolRightBound = enemy.RightBound - 2;
            double targetHoverCenterX = playerCenterX + (enemyCenterX <= playerCenterX ? -hoverOffsetX : hoverOffsetX);
            double targetHoverX = Math.Clamp(
                targetHoverCenterX - (enemy.Definition.Width / 2.0),
                chaseLeftBound,
                chaseRightBound);

            if (!inAggroRange && (enemy.IsTelegraphing || enemy.IsAttacking))
            {
                StopEnemyTelegraph(enemy);
                StopEnemyAttack(enemy);
                enemy.AttackDamageApplied = false;
                enemy.HasLockedAttackDirection = false;
                enemy.FlightRetargetFrames = 0;
                enemy.FlightTargetX = targetHoverX;
                enemy.FlightTargetY = hoverY;
                enemy.HorizontalVelocity = GetStepTowards(enemy.X, targetHoverX, enemy.Speed * 1.9);
                enemy.VerticalVelocity = GetStepTowards(enemy.Y, hoverY, enemy.Speed * 1.35);
                if (Math.Abs(playerCenterX - enemyCenterX) > 2)
                    enemy.Direction = playerCenterX >= enemyCenterX ? 1 : -1;
                return;
            }

            if (enemy.IsTelegraphing)
            {
                if (!enemy.HasLockedAttackDirection)
                {
                    enemy.LockedAttackDirection = playerCenterX >= enemyCenterX ? 1 : -1;
                    enemy.HasLockedAttackDirection = true;
                    enemy.FlightAnchorX = playerCenterX;
                    double telegraphCenterX = enemy.FlightAnchorX - (enemy.LockedAttackDirection * telegraphOffsetX);
                    enemy.FlightTargetX = Math.Clamp(
                        telegraphCenterX - (enemy.Definition.Width / 2.0),
                        chaseLeftBound,
                        chaseRightBound);
                    enemy.FlightTargetY = hoverY;
                }

                enemy.FlightRetargetFrames = 0;
                enemy.Direction = enemy.LockedAttackDirection;
                enemy.HorizontalVelocity = GetStepTowards(enemy.X, enemy.FlightTargetX, enemy.Speed * 1.15);
                enemy.VerticalVelocity = GetStepTowards(enemy.Y, hoverY, enemy.Speed * 0.8);

                enemy.TelegraphFramesRemaining--;
                if (enemy.TelegraphFramesRemaining <= 0)
                {
                    StopEnemyTelegraph(enemy);
                    enemy.AttackDamageApplied = false;
                    enemy.Direction = enemy.LockedAttackDirection;
                    double attackOvershootCenterX = enemy.FlightAnchorX + (enemy.LockedAttackDirection * diveOvershootX);
                    enemy.FlightTargetX = Math.Clamp(
                        attackOvershootCenterX - (enemy.Definition.Width / 2.0),
                        chaseLeftBound,
                        chaseRightBound);
                    enemy.FlightTargetY = diveFloorY;
                    StartEnemyAttack(enemy, 34);
                    enemy.AttackCooldownFrames = Math.Max(18, enemy.Definition.BehaviorIntervalFrames);
                }
                return;
            }

            if (enemy.IsAttacking)
            {
                enemy.Direction = enemy.HasLockedAttackDirection ? enemy.LockedAttackDirection : enemy.Direction;
                if (enemy.FlightAttackPhase == 0)
                {
                    enemy.HorizontalVelocity = GetStepTowards(enemy.X, enemy.FlightTargetX, enemy.Speed * 4.35);
                    enemy.VerticalVelocity = Math.Abs(GetStepTowards(enemy.Y, diveFloorY, enemy.Speed * 3.35));

                    if (enemy.Y >= diveFloorY - 1)
                    {
                        enemy.FlightAttackPhase = 1;
                        enemy.FlightAttackPhaseFrames = 12;
                        enemy.FlightTargetX = Math.Clamp(
                            enemy.X + (enemy.LockedAttackDirection * groundSkimDistance),
                            chaseLeftBound,
                            chaseRightBound);
                        enemy.FlightTargetY = diveFloorY - 1;
                    }
                }
                else
                {
                    enemy.FlightAttackPhaseFrames--;
                    enemy.HorizontalVelocity = enemy.LockedAttackDirection * (enemy.Speed * 4.7);
                    enemy.VerticalVelocity = GetStepTowards(enemy.Y, diveFloorY - 1, enemy.Speed * 1.35);

                    bool hitAttackBound =
                        (enemy.LockedAttackDirection < 0 && enemy.X <= chaseLeftBound) ||
                        (enemy.LockedAttackDirection > 0 && enemy.X >= chaseRightBound);
                    bool reachedSkimTarget = Math.Abs(enemy.X - enemy.FlightTargetX) <= 5;
                    if (enemy.FlightAttackPhaseFrames <= 0 || hitAttackBound || reachedSkimTarget)
                    {
                        StopEnemyAttack(enemy);
                        enemy.HasLockedAttackDirection = false;
                        enemy.VerticalVelocity = -enemy.Speed * 1.1;
                        enemy.FlightRetargetFrames = 0;
                    }
                }
                return;
            }

            if (hasAggro)
            {
                if (enemy.FlightRetargetFrames > 0)
                    enemy.FlightRetargetFrames--;

                bool needsRetarget =
                    enemy.FlightRetargetFrames <= 0 ||
                    double.IsNaN(enemy.FlightTargetX) ||
                    double.IsNaN(enemy.FlightTargetY) ||
                    (Math.Abs(enemy.X - enemy.FlightTargetX) <= 5 &&
                    Math.Abs(enemy.Y - enemy.FlightTargetY) <= 4);
                if (needsRetarget)
                {
                    SetBatFlightTarget(
                        enemy,
                        targetHoverX,
                        hoverY,
                        14,
                        10,
                        chaseLeftBound,
                        chaseRightBound,
                        minFlightY,
                        maxFlightY,
                        3,
                        7);
                }

                enemy.HorizontalVelocity = GetStepTowards(enemy.X, enemy.FlightTargetX, enemy.Speed * 2.0);
                enemy.VerticalVelocity = GetStepTowards(enemy.Y, enemy.FlightTargetY, enemy.Speed * 1.55);
                if (Math.Abs(playerCenterX - enemyCenterX) > 2)
                    enemy.Direction = playerCenterX >= enemyCenterX ? 1 : -1;

                bool nearHoverPoint = Math.Abs(enemy.X - targetHoverX) <= 22 &&
                    Math.Abs(enemy.Y - hoverY) <= 12;
                if (nearHoverPoint && enemy.AttackCooldownFrames <= 0 && inAggroRange)
                {
                    enemy.LockedAttackDirection = playerCenterX >= enemyCenterX ? 1 : -1;
                    enemy.HasLockedAttackDirection = true;
                    enemy.FlightAnchorX = playerCenterX;
                    enemy.Direction = enemy.LockedAttackDirection;
                    double telegraphCenterX = enemy.FlightAnchorX - (enemy.LockedAttackDirection * telegraphOffsetX);
                    enemy.FlightTargetX = Math.Clamp(
                        telegraphCenterX - (enemy.Definition.Width / 2.0),
                        chaseLeftBound,
                        chaseRightBound);
                    enemy.FlightTargetY = hoverY;
                    enemy.HorizontalVelocity = 0;
                    enemy.VerticalVelocity = 0;
                    enemy.FlightRetargetFrames = 0;
                    StartEnemyTelegraph(enemy, 18);
                }
                return;
            }

            if (enemy.FlightRetargetFrames > 0)
                enemy.FlightRetargetFrames--;

            if (enemy.X <= enemy.LeftBound + 4)
                enemy.Direction = 1;
            else if (enemy.X >= enemy.RightBound - 4)
                enemy.Direction = -1;

            bool needsPatrolTarget =
                enemy.FlightRetargetFrames <= 0 ||
                double.IsNaN(enemy.FlightTargetX) ||
                double.IsNaN(enemy.FlightTargetY) ||
                (Math.Abs(enemy.X - enemy.FlightTargetX) <= 5 &&
                Math.Abs(enemy.Y - enemy.FlightTargetY) <= 4);
            if (needsPatrolTarget)
            {
                double patrolDistance = GetRandomDouble(20, 58);
                double patrolTargetX = enemy.X + (enemy.Direction * patrolDistance);
                if (patrolTargetX < patrolLeftBound || patrolTargetX > patrolRightBound)
                {
                    enemy.Direction *= -1;
                    patrolTargetX = enemy.X + (enemy.Direction * patrolDistance);
                }

                SetBatFlightTarget(
                    enemy,
                    patrolTargetX,
                    hoverY,
                    0,
                    12,
                    patrolLeftBound,
                    patrolRightBound,
                    minFlightY,
                    maxFlightY,
                    4,
                    8);
            }

            enemy.HorizontalVelocity = GetStepTowards(enemy.X, enemy.FlightTargetX, enemy.Speed * 1.7);
            enemy.VerticalVelocity = GetStepTowards(enemy.Y, enemy.FlightTargetY, enemy.Speed * 1.3);
            if (Math.Abs(enemy.HorizontalVelocity) > 0.05)
                enemy.Direction = enemy.HorizontalVelocity >= 0 ? 1 : -1;
        }

        private void UpdateDashStrikeEnemy(SpawnedEnemy enemy, bool hasAggro, bool inAggroRange, bool inAttackRange, double playerCenterX, double enemyCenterX)
        {
            bool isGooBoss = enemy.Definition.Name.Equals("The Goo", StringComparison.OrdinalIgnoreCase);
            bool isWolf = enemy.Definition.Name.Equals("wolf", StringComparison.OrdinalIgnoreCase);

            if (HandleGooRecoveryState(enemy))
                return;

            if (enemy.RecoveryPauseFrames > 0)
            {
                enemy.RecoveryPauseFrames--;
                enemy.HorizontalVelocity = 0;
                return;
            }

            if (enemy.HasLockedAttackDirection)
            {
                enemy.Direction = enemy.LockedAttackDirection;
            }
            else if (hasAggro)
                enemy.Direction = playerCenterX >= enemyCenterX ? 1 : -1;
            else
            {
                if (enemy.X <= enemy.LeftBound) enemy.Direction = 1;
                else if (enemy.X >= enemy.RightBound) enemy.Direction = -1;
            }

            bool canStartDash = !enemy.IsAttacking &&
                                !enemy.IsTelegraphing &&
                                enemy.AttackCooldownFrames <= 0 &&
                                hasAggro &&
                                (isGooBoss || inAggroRange);
            if (canStartDash)
            {
                enemy.LockedAttackDirection = playerCenterX >= enemyCenterX ? 1 : -1;
                enemy.HasLockedAttackDirection = true;
                enemy.Direction = enemy.LockedAttackDirection;
                StartEnemyTelegraph(enemy, isGooBoss ? 20 : 14);
            }

            if (enemy.IsTelegraphing)
            {
                enemy.TelegraphFramesRemaining--;
                if (enemy.TelegraphFramesRemaining <= 0)
                {
                    StopEnemyTelegraph(enemy);
                    enemy.Direction = enemy.LockedAttackDirection;
                    enemy.AttackDamageApplied = false;
                    if (isGooBoss)
                    {
                        StartEnemyAttack(enemy, GooDashAttackDurationFrames);
                    }
                    else
                    {
                        StartEnemyAttack(enemy, isWolf
                            ? (inAttackRange ? 18 : 22)
                            : (inAttackRange ? 14 : 18));
                    }
                    int wolfCooldownBonusFrames = isWolf ? 24 : 0;
                    enemy.AttackCooldownFrames = Math.Max(14, enemy.Definition.BehaviorIntervalFrames + wolfCooldownBonusFrames);
                    if (isWolf && enemy.IsGrounded)
                    {
                        // Tiny pounce arc for wolves: quick lift + immediate forward drive.
                        enemy.VerticalVelocity = Math.Min(enemy.VerticalVelocity, -6.55);
                        enemy.HorizontalVelocity = enemy.LockedAttackDirection * enemy.Speed * (inAttackRange ? 3.6 : 3.0);
                        enemy.IsGrounded = false;
                    }
                }
                return;
            }

            if (enemy.IsAttacking)
            {
                if (isGooBoss)
                {
                    bool hitRightWall = enemy.LockedAttackDirection > 0 && enemy.X >= Width - enemy.Definition.Width - 1;
                    bool hitLeftWall = enemy.LockedAttackDirection < 0 && enemy.X <= 1;
                    if (hitLeftWall || hitRightWall)
                    {
                        enemy.LockedAttackDirection *= -1;
                        enemy.Direction = enemy.LockedAttackDirection;
                    }

                    enemy.HorizontalVelocity = enemy.LockedAttackDirection * GooDashSpeed;
                    return;
                }

                if (isWolf)
                {
                    // Wolves should lunge forward as they lift off (simultaneous arc + dash).
                    if (enemy.IsGrounded)
                    {
                        enemy.HorizontalVelocity = enemy.LockedAttackDirection * enemy.Speed * 0.45;
                        enemy.AttackFramesRemaining = Math.Min(enemy.AttackFramesRemaining, 3);
                        return;
                    }

                    double dashSpeed = enemy.Speed * (inAttackRange ? 3.8 : 3.2);
                    if (!enemy.IsGrounded)
                        dashSpeed *= 1.1;
                    enemy.HorizontalVelocity = enemy.LockedAttackDirection * dashSpeed;
                    return;
                }

                if (enemy.AttackFramesRemaining > 8)
                    return;

                double groundDashSpeed = enemy.Speed * (inAttackRange ? 4.2 : 3.4);
                enemy.X += enemy.LockedAttackDirection * groundDashSpeed;
                return;
            }

            if (hasAggro)
                enemy.X += enemy.Speed * enemy.Direction * 0.75;
            else
                enemy.X += enemy.Speed * enemy.Direction * 0.45;
        }

        private void DrawEnemies()
        {
            foreach (var enemy in activeEnemies)
            {
                if (!enemy.IsAlive) continue;

                var (drawX, spriteWidth) = GetEnemySpriteDrawMetrics(enemy);

                enemy.Body.Width = spriteWidth;
                enemy.CurrentSpriteWidth = spriteWidth;
                bool hasTelegraphSprites = GetTelegraphFramesForCurrentBehavior(enemy).Count > 0;
                bool isCrawlerLeapTelegraph = IsCrawlerEnemy(enemy) && enemy.CrawlerPhase == CrawlerPhase.LeapTelegraph;
                double baseOpacity = isCrawlerLeapTelegraph
                    ? (((animationFrameCounter / 2) % 2 == 0) ? 0.72 : 1.0)
                    : enemy.IsTelegraphing && !hasTelegraphSprites
                        ? (((animationFrameCounter / 3) % 2 == 0) ? 0.55 : 1.0)
                        : 1.0;
                var fallenKnightReassemblyVisuals = enemy.IsDying
                    ? (1.0, 1.0, 0.0)
                    : GetFallenKnightReassemblyVisuals(enemy);
                var fallenKnightHeadTowerVisuals = enemy.IsDying
                    ? (1.0, 1.0, 0.0, 1.0, false)
                    : GetFallenKnightHeadFireTowerVisuals(enemy);
                bool showEnemyLabel = !enemy.IsDying &&
                    !(IsFallenKnightEnemy(enemy) && enemy.LinkedEnemy != null) &&
                    !(IsFallenKnightHeadEnemy(enemy) && enemy.IsReturningToOwner) &&
                    !IsFallenKnightHeadFireTower(enemy);
                bool showEnemyHealth = !enemy.IsDying &&
                    !((IsFallenKnightEnemy(enemy) && enemy.LinkedEnemy != null && !enemy.LinkedEnemy.IsReturningToOwner) ||
                      (IsFallenKnightHeadEnemy(enemy) && enemy.IsReturningToOwner));
                enemy.Label.Visibility = showEnemyLabel ? Visibility.Visible : Visibility.Hidden;
                enemy.HealthBg.Visibility = showEnemyHealth ? Visibility.Visible : Visibility.Hidden;
                enemy.HealthFill.Visibility = showEnemyHealth ? Visibility.Visible : Visibility.Hidden;
                enemy.Body.Opacity = baseOpacity *
                    (enemy.IsDying ? enemy.DeathOpacity : fallenKnightHeadTowerVisuals.Item4);
                Canvas.SetLeft(enemy.Body, drawX);
                double bodyTop = enemy.Y + enemy.SpriteGroundOffsetY + GetFrostlingSnowballBodyOffsetY(enemy) +
                    (enemy.IsDying ? enemy.DeathOffsetY : fallenKnightReassemblyVisuals.Item3);
                double spriteCenterX = drawX + (spriteWidth / 2.0);
                Canvas.SetTop(enemy.Body, bodyTop);
                DrawFallenKnightHeadTrail(enemy);

                if (enemy.BodySprite != null)
                {
                    enemy.BodySprite.RenderTransformOrigin = fallenKnightHeadTowerVisuals.Item5
                        ? new Point(0.5, 1.0)
                        : new Point(0.5, 0.5);
                    var transformGroup = new TransformGroup();
                    double scaleX = (enemy.IsDying ? enemy.DeathScaleX : fallenKnightReassemblyVisuals.Item1) *
                        fallenKnightHeadTowerVisuals.Item1;
                    double scaleY = (enemy.IsDying ? enemy.DeathScaleY : fallenKnightReassemblyVisuals.Item2) *
                        fallenKnightHeadTowerVisuals.Item2;
                    double rotation = enemy.SpriteRotationDegrees + (enemy.IsDying ? enemy.DeathRotationDegrees : 0);
                    transformGroup.Children.Add(new ScaleTransform((enemy.Direction >= 0 ? 1 : -1) * scaleX, scaleY));
                    transformGroup.Children.Add(new RotateTransform(rotation));
                    enemy.BodySprite.RenderTransform = transformGroup;
                }

                Canvas.SetLeft(enemy.Label, spriteCenterX - (enemy.Label.Width / 2.0));
                Canvas.SetTop(enemy.Label, bodyTop - 14 - fallenKnightHeadTowerVisuals.Item3);

                double hpRatio = enemy.Definition.MaxHealth > 0
                    ? (double)enemy.CurrentHealth / enemy.Definition.MaxHealth : 0;

                enemy.HealthFill.Width = 28 * Math.Max(0, hpRatio);
                double healthBarX = spriteCenterX - (enemy.HealthBg.Width / 2.0);
                Canvas.SetLeft(enemy.HealthBg, healthBarX);
                Canvas.SetTop(enemy.HealthBg, bodyTop - 22 - fallenKnightHeadTowerVisuals.Item3);
                Canvas.SetLeft(enemy.HealthFill, healthBarX);
                Canvas.SetTop(enemy.HealthFill, bodyTop - 22 - fallenKnightHeadTowerVisuals.Item3);

                Rect enemyCollisionRect = GetEnemyCollisionRect(enemy);
                enemy.AttackHitbox.Width = enemyCollisionRect.Width;
                enemy.AttackHitbox.Height = enemyCollisionRect.Height;
                Canvas.SetLeft(enemy.AttackHitbox, enemyCollisionRect.X);
                Canvas.SetTop(enemy.AttackHitbox, enemyCollisionRect.Y);
                enemy.AttackHitbox.Visibility = gameConfig.Debug ? Visibility.Visible : Visibility.Hidden;
            }
        }

        private void HandleEnemyContactWithPlayer()
        {
            if (playerDamageCooldownFrames > 0) return;

            Rect playerRect = GetPlayerCollisionRect();

            foreach (var enemy in activeEnemies)
            {
                if (!IsEnemyCombatActive(enemy)) continue;
                if (IsEnemyCollisionDisabled(enemy)) continue;
                int contactDamage = GetEnemyContactDamage(enemy);
                if (contactDamage <= 0) continue;

                Rect enemyRect = GetEnemyCollisionRect(enemy);
                if (playerRect.IntersectsWith(enemyRect))
                {
                    playerData.Health = Math.Max(0, playerData.Health - contactDamage);
                    stageTookDamage = true;
                    playerDamageCooldownFrames = PlayerDamageCooldownMax;
                    enemy.AttackDamageApplied = true;
                    if (enemy.Definition.Name.Equals("The Goo", StringComparison.OrdinalIgnoreCase) &&
                        enemy.CurrentBehaviorId.Equals("dash_strike", StringComparison.OrdinalIgnoreCase))
                    {
                        if (enemy.IsAttacking)
                        {
                            StopEnemyAttack(enemy);
                            enemy.HasLockedAttackDirection = false;
                            StartEnemyRecoveryPause(enemy, GooDashRecoveryDurationFrames);
                        }
                        else if (enemy.IsTelegraphing)
                        {
                            StopEnemyTelegraph(enemy);
                            enemy.HasLockedAttackDirection = false;
                            enemy.HorizontalVelocity = 0;
                        }
                    }
                    ShowStatus($"{enemy.Definition.Name} hits you!", 35);
                    break;
                }
            }
        }

        private void DamageEnemy(SpawnedEnemy enemy, int damage)
        {
            if (!IsEnemyCombatActive(enemy) || !CanPlayerDamageEnemy(enemy)) return;
            enemy.IsAggroLocked = true;
            enemy.CurrentHealth -= damage;
            if (enemy.CurrentHealth <= 0)
            {
                if (IsFallenKnightHeadEnemy(enemy) && !enemy.IsReturningToOwner)
                    StartFallenKnightHeadReturn(enemy);
                else
                    KillEnemy(enemy);
            }
        }

        private void KillEnemy(SpawnedEnemy enemy)
        {
            if (!enemy.IsAlive || enemy.IsDying)
                return;

            RemoveEnemyHazardsForOwner(enemy);
            RemoveEnemyProjectilesForOwner(enemy);
            RemoveEnemyTrailVisuals(enemy);
            if (IsFallenKnightEnemy(enemy) && enemy.LinkedEnemy != null)
            {
                RemoveEnemyWithoutRewards(enemy.LinkedEnemy);
                enemy.LinkedEnemy = null;
            }
            enemy.IsDying = true;
            enemy.CurrentHealth = 0;
            enemy.AttackDamageApplied = true;
            enemy.IsAttacking = false;
            enemy.AttackFramesRemaining = 0;
            enemy.IsTelegraphing = false;
            enemy.TelegraphFramesRemaining = 0;
            enemy.IsRecovering = false;
            enemy.RecoveryPauseFrames = 0;
            enemy.HasLockedAttackDirection = false;
            enemy.AttackHitbox.Visibility = Visibility.Hidden;
            enemy.Label.Visibility = Visibility.Hidden;
            enemy.HealthBg.Visibility = Visibility.Hidden;
            enemy.HealthFill.Visibility = Visibility.Hidden;
            enemy.DeathAnimationType = GetEnemyDeathAnimationType(enemy);
            enemy.DeathAnimationDuration = GetDeathAnimationDuration(enemy.DeathAnimationType);
            enemy.DeathAnimationTick = 0;
            enemy.DeathOpacity = 1.0;
            enemy.DeathScaleX = 1.0;
            enemy.DeathScaleY = 1.0;
            enemy.DeathOffsetY = 0;
            enemy.DeathRotationDegrees = 0;

            int goldDrop = 0;
            int scaledXp = 0;
            if (!enemy.SuppressRewards)
            {
                goldDrop = rng.Next(enemy.Definition.GoldMin, enemy.Definition.GoldMax + 1);
                scaledXp = ScaleXpForPlayerLevel(enemy.Definition.XpReward, enemy.Definition.PowerLevel);
                playerData.Gold += goldDrop;
                GainExperience(scaledXp);
            }
            currentAttackVictims.Remove(enemy);

            switch (enemy.DeathAnimationType)
            {
                case EnemyDeathAnimationType.BatPlummet:
                    enemy.VerticalVelocity = Math.Max(1.2, enemy.VerticalVelocity);
                    enemy.HorizontalVelocity *= 0.35;
                    break;

                case EnemyDeathAnimationType.CrawlerBackflip:
                    enemy.VerticalVelocity = Math.Min(enemy.VerticalVelocity, -2.1);
                    enemy.HorizontalVelocity = 0;
                    break;

                default:
                    enemy.HorizontalVelocity = 0;
                    enemy.VerticalVelocity = 0;
                    break;
            }

            if (!enemy.SuppressRewards)
                ShowStatus($"+{scaledXp} XP, +{goldDrop}g", 60);
        }

        private int GainExperience(int amount, bool showLevelUpStatus = true)
        {
            if (amount <= 0)
                return 0;

            playerData.Experience += amount;
            int levelsGained = 0;

            while (playerData.Experience >= playerData.NextLevelXp)
            {
                playerData.Experience -= playerData.NextLevelXp;
                playerData.Level++;
                levelsGained++;
                levelUpPulseFramesRemaining = LevelUpPulseDurationFrames;
                if (showLevelUpStatus)
                    ShowStatus($"Level Up! Now Lv {playerData.Level}", 80);
            }

            return levelsGained;
        }

        private int ScaleXpForPlayerLevel(int baseXp, int enemyLevel)
        {
            int diff = enemyLevel - playerData.Level;
            if (diff <= -6) return 1;
            if (diff <= -3) return Math.Max(1, (int)Math.Round(baseXp * 0.4));
            if (diff < 0) return Math.Max(1, (int)Math.Round(baseXp * 0.7));
            if (diff == 0) return baseXp;
            if (diff <= 3) return (int)Math.Round(baseXp * 1.15);
            return (int)Math.Round(baseXp * 1.35);
        }

        private static int GetWeaponDamageMultiplier(WeaponItem? weapon)
        {
            return Math.Max(1, weapon?.Level ?? 1);
        }

        private int GetScaledPlayerDamage(WeaponItem? weapon)
        {
            return Math.Max(1, playerData.BaseDamage * GetWeaponDamageMultiplier(weapon));
        }

        // -----------------------------------------------------------------------
        // Combat — melee
        // -----------------------------------------------------------------------
        private void StartMeleeAttack(bool isDownwardAttack)
        {
            List<BitmapImage> attackFrames = isDownwardAttack && playerDownAttackFrames.Count > 0
                ? playerDownAttackFrames
                : playerAttackFrames;
            int attackFrameCount = Math.Max(1, attackFrames.Count);
            attackDurationFrames = Math.Max(attackFrameCount, attackFrameCount * PlayerAttackFrameTicks);
            isAttacking = true;
            isDownAttack = isDownwardAttack;
            attackFramesRemaining = attackDurationFrames;
            attackHitbox.Visibility = Visibility.Visible;
            currentAttackVictims.Clear();
            meleeCooldownFrames = Math.Max(1, playerData.EquippedSword?.CooldownFrames ?? attackDurationFrames);
            ApplyMeleeDamageNow();
        }

        private void ApplyMeleeDamageNow()
        {
            DrawAttackHitbox();

            Rect attackRect = new Rect(
                Canvas.GetLeft(attackHitbox),
                Canvas.GetTop(attackHitbox),
                attackHitbox.Width,
                attackHitbox.Height);

            int damage = GetScaledPlayerDamage(playerData.EquippedSword);
            bool connectedWithEnemy = false;

            foreach (var enemy in activeEnemies.ToList())
            {
                if (!IsEnemyCombatActive(enemy)) continue;
                if (currentAttackVictims.Contains(enemy)) continue;
                Rect enemyRect = GetEnemyCollisionRect(enemy);
                if (attackRect.IntersectsWith(enemyRect))
                {
                    bool canDamageEnemy = CanPlayerDamageEnemy(enemy);
                    connectedWithEnemy |= canDamageEnemy;
                    currentAttackVictims.Add(enemy);
                    DamageEnemy(enemy, damage);
                    if (canDamageEnemy && IsEnemyCombatActive(enemy))
                        ApplySwordKnockback(enemy);
                }
            }

            if (isDownAttack && connectedWithEnemy)
            {
                velocityY = jumpStrength;
                isOnGround = false;
            }
        }

        private void ApplySwordKnockback(SpawnedEnemy enemy)
        {
            double playerCenterX = playerX + (playerWidth / 2.0);
            double enemyCenterX = enemy.X + (enemy.Definition.Width / 2.0);
            double knockbackDirection = enemyCenterX >= playerCenterX ? 1.0 : -1.0;

            enemy.HorizontalVelocity += knockbackDirection * SwordKnockbackSpeed;
            enemy.Direction = knockbackDirection >= 0 ? 1 : -1;

            if (enemy.IsGrounded)
            {
                enemy.VerticalVelocity = Math.Min(enemy.VerticalVelocity, SwordKnockbackLift);
                enemy.IsGrounded = false;
            }
        }

        private void UpdateAttack()
        {
            if (!isAttacking)
            {
                currentAttackVictims.Clear();
                attackHitbox.Visibility = gameConfig.Debug ? Visibility.Visible : Visibility.Hidden;
                return;
            }

            ApplyMeleeDamageNow();
            attackFramesRemaining--;
            if (attackFramesRemaining <= 0)
            {
                isAttacking = false;
                isDownAttack = false;
                currentAttackVictims.Clear();
                attackHitbox.Visibility = gameConfig.Debug ? Visibility.Visible : Visibility.Hidden;
            }
        }

        // -----------------------------------------------------------------------
        // Combat — bow
        // -----------------------------------------------------------------------
        private void UpdateBowChargeInput(bool firePressedThisFrame, bool fireReleasedThisFrame)
        {
            if (firePressedThisFrame)
                BeginBowCharge();

            if (isBowCharging && fireHeld)
            {
                bowChargeFrames = Math.Min(BowChargeMaxFrames, bowChargeFrames + 1);
                if (bowChargeFrames >= BowChargeMaxFrames && !bowChargeFullNotified)
                {
                    ShowStatus("Bow fully charged!", 20);
                    bowChargeFullNotified = true;
                }
            }

            if (isBowCharging && fireReleasedThisFrame)
            {
                double chargeRatio = BowChargeMaxFrames <= 0 ? 0 : Math.Clamp((double)bowChargeFrames / BowChargeMaxFrames, 0, 1);
                FireBow(chargeRatio);
                CancelBowCharge();
            }
        }

        private void BeginBowCharge()
        {
            if (panelMode != PanelMode.None)
                return;
            if (playerData.EquippedBow == null)
            {
                ShowStatus("No bow equipped", 50);
                return;
            }
            if (bowCooldownFrames > 0)
            {
                ShowStatus($"Bow cooling down ({bowCooldownFrames})", 25);
                return;
            }
            if (playerData.GetArrowCount() <= 0)
            {
                ShowStatus("Out of arrows", 50);
                return;
            }

            isBowCharging = true;
            bowChargeFrames = 0;
            bowChargeFullNotified = false;
        }

        private void CancelBowCharge()
        {
            isBowCharging = false;
            bowChargeFrames = 0;
            bowChargeFullNotified = false;
        }

        private void FireBow(double chargeRatio)
        {
            if (playerData.EquippedBow == null)
            {
                ShowStatus("No bow equipped", 50);
                return;
            }

            if (!playerData.RemoveArrows(1))
            {
                ShowStatus("Out of arrows", 50);
                return;
            }

            double damageScale = 0.65 + (chargeRatio * 2.35) + (Math.Pow(chargeRatio, 2.0) * 1.5);
            int damage = Math.Max(1, (int)Math.Round(GetScaledPlayerDamage(playerData.EquippedBow) * damageScale));
            double speedScale = 0.8 + (chargeRatio * 1.0);
            const double HorizontalRangeMultiplier = 3.0;
            double minRange = Math.Max(120, Width * 0.12) * HorizontalRangeMultiplier;
            double maxRange = Math.Max(minRange + 40, (Width * 0.85) * HorizontalRangeMultiplier);
            bool isMaxCharge = chargeRatio >= 0.98;
            List<BitmapImage> arrowFramesToUse = isMaxCharge ? playerArrowMaxFrames : playerArrowFrames;

            FrameworkElement body;
            if (arrowFramesToUse.Count > 0)
            {
                BitmapImage firstArrowFrame = arrowFramesToUse[0];
                double arrowWidth = firstArrowFrame.PixelWidth > 0 ? firstArrowFrame.PixelWidth : 16;
                double arrowHeight = firstArrowFrame.PixelHeight > 0 ? firstArrowFrame.PixelHeight : 16;
                var arrowImage = new Image
                {
                    Width = arrowWidth,
                    Height = arrowHeight,
                    Stretch = Stretch.Fill,
                    Source = arrowFramesToUse[0]
                };
                RenderOptions.SetBitmapScalingMode(arrowImage, BitmapScalingMode.NearestNeighbor);
                body = arrowImage;
            }
            else
            {
                body = new Rectangle
                {
                    Width = 12,
                    Height = 3,
                    Fill = Brushes.SandyBrown,
                    RadiusX = 1,
                    RadiusY = 1
                };
            }

            GameCanvas.Children.Add(body);
            Panel.SetZIndex(body, 22);

            double startX = facingRight ? playerX + playerWidth + 2 : playerX - body.Width;
            double startY = playerY + (playerHeight / 2) - (body.Height / 2);
            // Keep trajectory low so arrows don't sail over grounded enemies.
            double launchLift = -(0.05 + (chargeRatio * 0.22));
            double projectileGravity = gravity * (0.75 - (chargeRatio * 0.12));
            double initialGravityMultiplier = 0.1;
            int gravityDelayFrames = 6;
            int gravityRampFrames = 18;

            activeProjectiles.Add(new ArrowProjectile
            {
                Body = body,
                Frames = arrowFramesToUse,
                AnimationFrameCounter = rng.Next(0, 1000),
                X = startX,
                Y = startY,
                Direction = facingRight ? 1 : -1,
                Speed = ArrowSpeed * speedScale,
                VerticalVelocity = launchLift,
                GravityPerFrame = projectileGravity,
                InitialGravityMultiplier = initialGravityMultiplier,
                GravityDelayFrames = gravityDelayFrames,
                GravityRampFrames = gravityRampFrames,
                AgeFrames = 0,
                Damage = damage,
                IsAlive = true
            });

            bowCooldownFrames = Math.Max(1, playerData.EquippedBow?.CooldownFrames ?? 12);
        }

        private void UpdateProjectiles()
        {
            foreach (var arrow in activeProjectiles.ToList())
            {
                if (!arrow.IsAlive) continue;

                double move = arrow.Speed * arrow.Direction;
                arrow.X += move;
                arrow.AgeFrames++;
                double gravityScale;
                if (arrow.AgeFrames <= arrow.GravityDelayFrames)
                {
                    gravityScale = arrow.InitialGravityMultiplier;
                }
                else if (arrow.GravityRampFrames <= 0)
                {
                    gravityScale = 1.0;
                }
                else
                {
                    double rampProgress = (double)(arrow.AgeFrames - arrow.GravityDelayFrames) / arrow.GravityRampFrames;
                    gravityScale = arrow.InitialGravityMultiplier + ((1.0 - arrow.InitialGravityMultiplier) * Math.Clamp(rampProgress, 0.0, 1.0));
                }

                arrow.VerticalVelocity += arrow.GravityPerFrame * gravityScale;
                arrow.Y += arrow.VerticalVelocity;

                double floorY = groundY + playerHeight;
                if (arrow.Y + arrow.Body.Height >= floorY)
                {
                    RemoveProjectile(arrow);
                    continue;
                }

                Rect arrowRect = GetArrowHitboxRect(arrow);

                foreach (var enemy in activeEnemies.ToList())
                {
                    if (!IsEnemyCombatActive(enemy)) continue;
                    Rect enemyRect = GetEnemyCollisionRect(enemy);
                    if (arrowRect.IntersectsWith(enemyRect))
                    {
                        DamageEnemy(enemy, arrow.Damage);
                        RemoveProjectile(arrow);
                        break;
                    }
                }
            }
        }

        private void DrawProjectiles()
        {
            foreach (var arrow in activeProjectiles)
            {
                if (!arrow.IsAlive) continue;
                Canvas.SetLeft(arrow.Body, arrow.X);
                Canvas.SetTop(arrow.Body, arrow.Y);

                if (arrow.Body is Image image)
                {
                    if (arrow.Frames.Count > 0)
                    {
                        arrow.AnimationFrameCounter++;
                        int frameIndex = (arrow.AnimationFrameCounter / 4) % arrow.Frames.Count;
                        image.Source = arrow.Frames[frameIndex];
                    }
                    image.RenderTransformOrigin = new Point(0.5, 0.5);
                    image.RenderTransform = new ScaleTransform(arrow.Direction >= 0 ? 1 : -1, 1);
                }
            }
        }

        private Rect GetArrowHitboxRect(ArrowProjectile arrow)
        {
            double hitboxW = Math.Max(2, ArrowHitboxWidth);
            double hitboxH = Math.Max(2, ArrowHitboxHeight);
            double hitboxX = arrow.X + ((arrow.Body.Width - hitboxW) / 2.0);
            double hitboxY = arrow.Y + ((arrow.Body.Height - hitboxH) / 2.0);
            return new Rect(hitboxX, hitboxY, hitboxW, hitboxH);
        }

        private void RemoveProjectile(ArrowProjectile arrow)
        {
            if (!arrow.IsAlive) return;
            arrow.IsAlive = false;
            GameCanvas.Children.Remove(arrow.Body);
            activeProjectiles.Remove(arrow);
        }

        private void ClearProjectiles()
        {
            foreach (var arrow in activeProjectiles)
                GameCanvas.Children.Remove(arrow.Body);
            activeProjectiles.Clear();
        }

        // -----------------------------------------------------------------------
        // Physics
        // -----------------------------------------------------------------------
        private void ApplyPhysics()
        {
            velocityY += gravity;
            playerX += velocityX;
            playerY += velocityY;
        }

        private void ResolveCollisions()
        {
            isOnGround = false;

            // Horizontal soft clamp — allow tiny overshoot so area transitions fire
            if (playerX < -2) playerX = -2;
            if (playerX > Width - playerWidth + 2) playerX = Width - playerWidth + 2;

            // Ceiling
            if (playerY < 0)
            {
                playerY = 0;
                if (velocityY < 0) velocityY = 0;
            }

            // Ground
            if (playerY >= groundY)
            {
                playerY = groundY;
                velocityY = 0;
                isOnGround = true;
            }
        }

        private void UpdatePlayerAnimationState()
        {
            if (isOnGround)
            {
                playerJumpAnimationTick = 0;
                return;
            }

            playerJumpAnimationTick++;
        }

        // -----------------------------------------------------------------------
        // Inventory & shop helpers
        // -----------------------------------------------------------------------
        private void UsePotion()
        {
            if (playerData.Health >= playerData.MaxHealth)
            {
                ShowStatus("Health already full", 50);
                return;
            }

            var potionEntry = playerData.Inventory
                .FirstOrDefault(i => i.Item is ConsumableItem c && c.Name == "Potion");

            if (potionEntry == null)
            {
                ShowStatus("No potion", 50);
                return;
            }

            var potion = (ConsumableItem)potionEntry.Item;
            playerData.Health = Math.Min(playerData.MaxHealth, playerData.Health + potion.HealAmount);
            RemoveItemFromInventory(potionEntry.Item, 1);
            ShowStatus($"Used Potion (+{potion.HealAmount} HP)", 60);
        }

        private void BuyShopListing(ShopListing listing)
        {
            if (playerData.Gold < listing.Price)
            {
                ShowStatus("Not enough gold", 60);
                return;
            }

            playerData.Gold -= listing.Price;

            if (listing.Item is WeaponItem weapon)
            {
                var purchased = CloneWeapon(weapon);
                AddItemToInventory(purchased, 1);

                if (weapon.WeaponCategory == WeaponCategory.Sword)
                {
                    playerData.EquippedSword = purchased;
                    ShowStatus($"Bought & equipped {purchased.Name}", 70);
                }
                else
                {
                    playerData.EquippedBow = purchased;
                    ShowStatus($"Bought & equipped {purchased.Name}", 70);
                }
            }
            else
            {
                AddItemToInventory(CloneItem(listing.Item), listing.Quantity);
                ShowStatus($"Bought {listing.Item.Name} x{listing.Quantity}", 70);
            }
        }

        private void SellInventoryEntry(InventoryEntry entry)
        {
            int sellPrice = Math.Max(1, entry.Item.BasePrice);
            RemoveItemFromInventory(entry.Item, 1);
            playerData.Gold += sellPrice;
            ShowStatus($"Sold {entry.Item.Name} for {sellPrice}g", 60);
        }

        private void AddItemToInventory(ItemBase item, int quantity)
        {
            if (item.Stackable)
            {
                var existing = playerData.Inventory
                    .FirstOrDefault(i => i.Item.GetType() == item.GetType() && i.Item.Name == item.Name);
                if (existing != null) { existing.Quantity += quantity; return; }
            }

            playerData.Inventory.Add(new InventoryEntry { Item = item, Quantity = quantity });
        }

        private void RemoveItemFromInventory(ItemBase item, int quantity)
        {
            var entry = playerData.Inventory.FirstOrDefault(i => i.Item == item);
            if (entry == null) return;

            entry.Quantity -= quantity;
            if (entry.Quantity <= 0)
                playerData.Inventory.Remove(entry);
        }

        private List<InventoryEntry> GetSellableInventory()
        {
            return playerData.Inventory
                .Where(i => i.Item != playerData.EquippedSword &&
                            i.Item != playerData.EquippedBow)
                .ToList();
        }

        private string GetInventorySummary()
        {
            if (playerData.Inventory.Count == 0) return "None";

            return string.Join(", ", playerData.Inventory.Select(e =>
                e.Quantity > 1 ? $"{e.Item.Name} x{e.Quantity}" : e.Item.Name));
        }

        private ItemBase CloneItem(ItemBase item)
        {
            if (item is WeaponItem w) return CloneWeapon(w);
            if (item is ConsumableItem c) return new ConsumableItem { Name = c.Name, HealAmount = c.HealAmount, BasePrice = c.BasePrice };
            if (item is AmmoItem a) return new AmmoItem { Name = a.Name, AmmoType = a.AmmoType, BasePrice = a.BasePrice };
            throw new InvalidOperationException($"Unknown item type: {item.GetType().Name}");
        }

        private WeaponItem CloneWeapon(WeaponItem w) =>
            new WeaponItem
            {
                Name = w.Name,
                WeaponCategory = w.WeaponCategory,
                Level = w.Level,
                CooldownFrames = w.CooldownFrames,
                SpritePath = w.SpritePath,
                BasePrice = w.BasePrice
            };

        private Rect GetPlayerCollisionRect()
        {
            double hitboxW = Math.Max(6, Math.Min(playerWidth, playerHitboxWidth));
            double hitboxH = Math.Max(6, Math.Min(playerHeight, playerHitboxHeight));
            double hitboxX = playerX + ((playerWidth - hitboxW) / 2.0);
            double hitboxY = playerY + (playerHeight - hitboxH);
            return new Rect(hitboxX, hitboxY, hitboxW, hitboxH);
        }

        private Rect GetEnemyCollisionRect(SpawnedEnemy enemy)
        {
            if (IsEnemyCollisionDisabled(enemy))
                return new Rect(enemy.X, enemy.Y, 0, 0);

            var (drawX, spriteWidth) = GetEnemySpriteDrawMetrics(enemy);
            if (IsFallenKnightHeadFireTower(enemy))
            {
                double towerHitboxWidth = Math.Max(34, enemy.Definition.Width * 1.18);
                double towerHitboxHeight = Math.Max(96, enemy.Definition.Height * 2.65);
                double towerHitboxX = drawX + ((spriteWidth - towerHitboxWidth) / 2.0);
                double towerHitboxY = enemy.Y + enemy.Definition.Height - towerHitboxHeight;
                return new Rect(towerHitboxX, towerHitboxY, towerHitboxWidth, towerHitboxHeight);
            }

            if (IsFallenKnightBodyCollapsed(enemy))
            {
                double hitboxWidth = Math.Min(spriteWidth, FallenKnightCollapsedHitboxWidth);
                double hitboxHeight = Math.Min(enemy.Definition.Height, FallenKnightCollapsedHitboxHeight);
                double collapsedHitboxX = drawX + ((spriteWidth - hitboxWidth) / 2.0);
                double collapsedHitboxY = enemy.Y + enemy.Definition.Height - hitboxHeight;
                return new Rect(collapsedHitboxX, collapsedHitboxY, hitboxWidth, hitboxHeight);
            }

            if (IsFrostlingEnemy(enemy) && enemy.IsRecovering)
            {
                double snowballSize = Math.Max(34, Math.Min(spriteWidth * 0.66, enemy.Definition.Height * 0.44));
                double snowballX = drawX + ((spriteWidth - snowballSize) / 2.0);
                double snowballY = enemy.Y + enemy.SpriteGroundOffsetY + GetFrostlingSnowballBodyOffsetY(enemy) + Math.Max(10, enemy.Definition.Height * 0.12);
                return new Rect(snowballX, snowballY, snowballSize, snowballSize);
            }

            double maxHitboxW = Math.Max(6, spriteWidth);
            double maxHitboxH = Math.Max(6, enemy.Definition.Height);
            double hitboxW = enemy.Definition.CollisionHitboxWidth ?? (enemy.Definition.Width * 0.74);
            double hitboxH = enemy.Definition.CollisionHitboxHeight ?? (enemy.Definition.Height * 0.76);
            hitboxW = Math.Min(maxHitboxW, Math.Max(6, hitboxW));
            hitboxH = Math.Min(maxHitboxH, Math.Max(6, hitboxH));

            double availableWidth = Math.Max(0, spriteWidth - hitboxW);
            double defaultOffsetX = availableWidth / 2.0;
            double defaultOffsetY = enemy.Definition.Height - hitboxH;
            double localOffsetX = enemy.Definition.CollisionHitboxOffsetX ?? defaultOffsetX;
            if (enemy.Definition.CollisionHitboxOffsetX.HasValue && enemy.Direction < 0)
            {
                localOffsetX = availableWidth - enemy.Definition.CollisionHitboxOffsetX.Value;
            }
            double localOffsetY = enemy.Definition.CollisionHitboxOffsetY ?? defaultOffsetY;
            localOffsetX = Math.Max(0, Math.Min(availableWidth, localOffsetX));
            localOffsetY = Math.Max(0, Math.Min(enemy.Definition.Height - hitboxH, localOffsetY));

            double hitboxX = drawX + localOffsetX;
            double hitboxY = enemy.Y + localOffsetY;
            return new Rect(hitboxX, hitboxY, hitboxW, hitboxH);
        }

        private (double DrawX, double SpriteWidth) GetEnemySpriteDrawMetrics(SpawnedEnemy enemy)
        {
            double spriteWidth = enemy.Definition.Width;
            if (enemy.BodySprite?.Source is BitmapSource currentFrame)
            {
                spriteWidth = GetSpriteWidthForHeight(currentFrame, enemy.Definition.Height);
            }
            else if (enemy.IsAttacking && GetAttackFramesForCurrentBehavior(enemy).Count > 0)
            {
                spriteWidth = enemy.Definition.Width * 2;
            }

            double drawX = enemy.X;
            if (enemy.Direction < 0 && spriteWidth > enemy.Definition.Width)
            {
                drawX = enemy.X - (spriteWidth - enemy.Definition.Width);
            }

            return (drawX, spriteWidth);
        }

        // -----------------------------------------------------------------------
        // Drawing helpers
        // -----------------------------------------------------------------------
        private ImageBrush GetPlayerLevelUpPulseMask(BitmapSource frame)
        {
            if (playerLevelUpPulseMasks.TryGetValue(frame, out var cachedBrush))
                return cachedBrush;

            var pulseMask = new ImageBrush(frame)
            {
                Stretch = Stretch.Fill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };
            if (pulseMask.CanFreeze)
                pulseMask.Freeze();

            playerLevelUpPulseMasks[frame] = pulseMask;
            return pulseMask;
        }

        private void DrawPlayer()
        {
            animationFrameCounter++;
            double spriteDrawWidth = playerWidth;
            BitmapImage activeFrame = playerIdleFrames[0];
            bool isMovingHorizontally = Math.Abs(velocityX) > 0.1;

            if (isAttacking)
            {
                List<BitmapImage> attackFrames = isDownAttack && playerDownAttackFrames.Count > 0
                    ? playerDownAttackFrames
                    : playerAttackFrames;
                if (attackFrames.Count > 0)
                {
                    int attackElapsedFrames = Math.Max(0, attackDurationFrames - attackFramesRemaining);
                    int attackIndex = GetHeldAnimationFrameIndex(attackFrames.Count, attackElapsedFrames, attackDurationFrames);
                    activeFrame = attackFrames[attackIndex];
                }
            }
            else if (isBowCharging)
            {
                double chargeRatio = BowChargeMaxFrames <= 0 ? 0 : Math.Clamp((double)bowChargeFrames / BowChargeMaxFrames, 0, 1);
                bool isFullCharge = chargeRatio >= 1.0;
                List<BitmapImage> bowFrames = isFullCharge
                    ? (isMovingHorizontally && playerBowFullWalkFrames.Count > 0 ? playerBowFullWalkFrames : playerBowFullFrames)
                    : chargeRatio >= 0.66
                        ? (isMovingHorizontally && playerBowCharge3WalkFrames.Count > 0 ? playerBowCharge3WalkFrames : playerBowCharge3Frames)
                        : chargeRatio >= 0.33
                            ? (isMovingHorizontally && playerBowCharge2WalkFrames.Count > 0 ? playerBowCharge2WalkFrames : playerBowCharge2Frames)
                            : (isMovingHorizontally && playerBowCharge1WalkFrames.Count > 0 ? playerBowCharge1WalkFrames : playerBowCharge1Frames);

                if (bowFrames.Count == 0)
                    bowFrames = playerAttackFrames.Count > 0 ? playerAttackFrames : playerIdleFrames;

                if (!isFullCharge && !isMovingHorizontally)
                {
                    activeFrame = bowFrames[0];
                }
                else
                {
                    int bowAnimationCadence = isFullCharge ? 6 : 8;
                    int bowFrameIndex = (animationFrameCounter / bowAnimationCadence) % Math.Max(1, bowFrames.Count);
                    activeFrame = bowFrames[bowFrameIndex];
                }
            }
            else if (playerDamageCooldownFrames > 0 && playerDamagedFrames.Count > 0)
            {
                int damageIndex = (animationFrameCounter / 6) % playerDamagedFrames.Count;
                activeFrame = playerDamagedFrames[damageIndex];
            }
            else if (!isOnGround && playerJumpFrames.Count > 0)
            {
                int jumpIndex = Math.Min(playerJumpFrames.Count - 1, playerJumpAnimationTick / 7);
                activeFrame = playerJumpFrames[jumpIndex];
            }
            else if (isMovingHorizontally)
            {
                int walkIndex = (animationFrameCounter / 8) % playerWalkFrames.Count;
                activeFrame = playerWalkFrames[walkIndex];
            }
            else
            {
                int idleIndex = (animationFrameCounter / 12) % playerIdleFrames.Count;
                activeFrame = playerIdleFrames[idleIndex];
            }

            player.Source = activeFrame;
            if (activeFrame.PixelHeight > 0)
                spriteDrawWidth = playerHeight * ((double)activeFrame.PixelWidth / activeFrame.PixelHeight);

            double pulseProgress = levelUpPulseFramesRemaining > 0
                ? Math.Clamp((double)levelUpPulseFramesRemaining / LevelUpPulseDurationFrames, 0, 1)
                : 0;
            double pulseStrength = pulseProgress > 0
                ? (0.35 + (0.65 * Math.Sin((1.0 - pulseProgress) * Math.PI * 4.0) * 0.5) + 0.325) * pulseProgress
                : 0;
            double pulseScale = 1.0 + (pulseStrength * 0.06);

            player.RenderTransformOrigin = new Point(0.5, 0.5);
            player.RenderTransform = new ScaleTransform((facingRight ? 1 : -1) * pulseScale, pulseScale);
            player.Width = spriteDrawWidth;

            double playerDrawX = playerX;
            if ((isAttacking || isBowCharging) && !facingRight)
                playerDrawX = playerX - (spriteDrawWidth - playerWidth);

            Canvas.SetLeft(player, playerDrawX);
            Canvas.SetTop(player, playerY);

            playerLevelUpPulseOverlay.Width = spriteDrawWidth;
            playerLevelUpPulseOverlay.Height = playerHeight;
            playerLevelUpPulseOverlay.RenderTransformOrigin = new Point(0.5, 0.5);
            playerLevelUpPulseOverlay.RenderTransform = new ScaleTransform((facingRight ? 1 : -1) * pulseScale, pulseScale);
            playerLevelUpPulseOverlay.OpacityMask = GetPlayerLevelUpPulseMask(activeFrame);
            playerLevelUpPulseOverlay.Opacity = 0.15 + (pulseStrength * 0.55);
            playerLevelUpPulseOverlay.Visibility = pulseStrength > 0.01 ? Visibility.Visible : Visibility.Hidden;
            Canvas.SetLeft(playerLevelUpPulseOverlay, playerDrawX);
            Canvas.SetTop(playerLevelUpPulseOverlay, playerY);

            Rect playerHitbox = GetPlayerCollisionRect();
            playerHitboxDebug.Width = playerHitbox.Width;
            playerHitboxDebug.Height = playerHitbox.Height;
            Canvas.SetLeft(playerHitboxDebug, playerHitbox.X);
            Canvas.SetTop(playerHitboxDebug, playerHitbox.Y);
        }

        private void DrawAttackHitbox()
        {
            if (isDownAttack)
            {
                const double downAttackHitboxWidth = 24;
                const double downAttackHitboxHeight = 34;
                const double downAttackForwardBias = 4;
                attackHitbox.Width = downAttackHitboxWidth;
                attackHitbox.Height = downAttackHitboxHeight;
                double hitboxX = playerX + ((playerWidth - attackHitbox.Width) / 2.0) +
                    (facingRight ? downAttackForwardBias : -downAttackForwardBias);
                Canvas.SetLeft(attackHitbox, hitboxX);
                Canvas.SetTop(attackHitbox, playerY + playerHeight - 4);
                return;
            }

            const double sideHitboxBaseWidth = 20;
            const double rearBridgeWidth = 8;
            attackHitbox.Height = 12;
            double overlapIntoPlayer = 16;
            double forwardReach = gameConfig.AttackPosition;
            attackHitbox.Width = sideHitboxBaseWidth + rearBridgeWidth;

            double sideHitboxX = facingRight
                ? playerX + playerWidth - overlapIntoPlayer + forwardReach - rearBridgeWidth
                : playerX - sideHitboxBaseWidth + overlapIntoPlayer - forwardReach;

            Canvas.SetLeft(attackHitbox, sideHitboxX);
            Canvas.SetTop(attackHitbox, playerY + 10);
        }

        private void DrawPlayerHud()
        {
            double hudX = playerX - 10;
            double hudY = playerY - 24;

            Canvas.SetLeft(playerHealthBg, hudX);
            Canvas.SetTop(playerHealthBg, hudY);

            double hpRatio = playerData.MaxHealth > 0
                ? (double)playerData.Health / playerData.MaxHealth : 0;

            playerHealthFill.Width = 44 * Math.Max(0, hpRatio);
            Canvas.SetLeft(playerHealthFill, hudX);
            Canvas.SetTop(playerHealthFill, hudY);

            playerHealthText.Text = $"Lv {playerData.Level}";
            Canvas.SetLeft(playerHealthText, Math.Max(20, (Width - playerHealthText.Width) / 2.0));
            Canvas.SetTop(playerHealthText, 8);

            playerArrowText.Text = $"Arrows:{playerData.GetArrowCount()}";
            Canvas.SetLeft(playerArrowText, playerX - 18);
            Canvas.SetTop(playerArrowText, playerY - 47);
        }

        private void ShowStatus(string text, int frames)
        {
            statusText.Text = text;
            statusFramesRemaining = frames;
        }
    }
}
