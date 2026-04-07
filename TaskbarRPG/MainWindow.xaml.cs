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
        Tundra
    }

    public enum TransitionDirection
    {
        Left,
        Right
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
        public int Damage { get; set; }
        public int CooldownFrames { get; set; } = 10;
        public string? SpritePath { get; set; } = null;

        public WeaponItem()
        {
            Kind = ItemKind.Weapon;
            Stackable = false;
        }

        public override string GetDisplayText() => $"{Name} (DMG {Damage}, CD {CooldownFrames})";
    }

    public class ItemTemplate
    {
        public string Name { get; set; } = "";
        public int Damage { get; set; } = 1;
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
        public double ArrowHitboxWidth { get; set; } = 10;
        public double ArrowHitboxHeight { get; set; } = 6;
        public double ArrowSpeed { get; set; } = 8.5;
        public int ArrowDurationFrames { get; set; } = 35;
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
    }

    public class EnemyTemplate
    {
        public string Name { get; set; } = "Enemy";
        public int Health { get; set; } = 10;
        public int AttackDamage { get; set; } = 4;
        public double MoveSpeed { get; set; } = 1.0;
        public int Level { get; set; } = 1;
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
            new BossTemplate { Name = "The Goo", Health = 80, AttackDamage = 15, MoveSpeed = 1.05, Width = 64, Height = 64 },
            new BossTemplate { Name = "Fallen Knight", Health = 105, AttackDamage = 19, MoveSpeed = 1.2, Width = 64, Height = 64 },
            new BossTemplate { Name = "DB-5000", Health = 130, AttackDamage = 24, MoveSpeed = 1.1, Width = 72, Height = 64 },
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
            if (stage % 5 != 0) return true;
            int bossIndex = (stage / 5) - 1;
            return bossIndex >= 0 && bossIndex < bossTemplates.Count;
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
                int enemyLevel = Math.Max(1, chosen.Level + (stage / 5));
                spawns.Add(new EnemyDefinition
                {
                    Name = chosen.Name,
                    PowerLevel = enemyLevel,
                    X = 160 + (i * spacing) + rng.Next(-25, 26),
                    PatrolRange = 90 + rng.Next(0, 70),
                    AggroRange = 160 + rng.Next(0, 70),
                    Speed = Math.Max(0.4, chosen.MoveSpeed),
                    MaxHealth = Math.Max(2, (int)Math.Round(chosen.Health * hpScale)),
                    ContactDamage = Math.Max(1, (int)Math.Round(chosen.AttackDamage * dmgScale)),
                    XpReward = Math.Max(4, 6 + (enemyLevel * 2)),
                    GoldMin = Math.Max(1, enemyLevel / 2),
                    GoldMax = Math.Max(2, enemyLevel + 2),
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
            var boss = new EnemyDefinition
            {
                Name = template.Name,
                PowerLevel = Math.Max(1, stage + 2),
                X = 820,
                PatrolRange = 170,
                AggroRange = 260,
                Speed = Math.Max(0.7, template.MoveSpeed + Math.Min(0.35, power * 0.01)),
                MaxHealth = Math.Max(20, template.Health + (power * 6)),
                ContactDamage = Math.Max(3, template.AttackDamage + (power / 2)),
                XpReward = 20 + power * 2,
                GoldMin = 12 + power,
                GoldMax = 20 + power * 2,
                Width = Math.Max(24, template.Width),
                Height = Math.Max(24, template.Height),
                Color = Color.FromRgb(180, 60, 70),
            };

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
        public static WeaponItem CreateOldSword()
        {
            return new WeaponItem
            {
                Name = "Old Sword",
                WeaponCategory = WeaponCategory.Sword,
                Damage = 2,
                CooldownFrames = 12,
                BasePrice = 8,
            };
        }

        public static WeaponItem CreateStarterBow()
        {
            return new WeaponItem
            {
                Name = "Simple Bow",
                WeaponCategory = WeaponCategory.Bow,
                Damage = 1,
                CooldownFrames = 14,
                BasePrice = 10,
            };
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

        public static WeaponItem CreateRandomSword(Random rng)
        {
            string[] prefixes = { "Bronze", "Iron", "Steel", "Hunter's", "Knight's" };
            string[] suffixes = { "Sword", "Blade", "Sabre" };
            int damage = rng.Next(2, 7);
            int price = 6 + (damage * 6);

            return new WeaponItem
            {
                Name = $"{prefixes[rng.Next(prefixes.Length)]} {suffixes[rng.Next(suffixes.Length)]}",
                WeaponCategory = WeaponCategory.Sword,
                Damage = damage,
                CooldownFrames = rng.Next(8, 15),
                BasePrice = price,
            };
        }

        public static WeaponItem CreateRandomBow(Random rng)
        {
            string[] prefixes = { "Oak", "Recurve", "Hunter's", "Elm", "Long" };
            string[] suffixes = { "Bow", "Shortbow", "Longbow" };
            int damage = rng.Next(1, 6);
            int price = 8 + (damage * 7);

            return new WeaponItem
            {
                Name = $"{prefixes[rng.Next(prefixes.Length)]} {suffixes[rng.Next(suffixes.Length)]}",
                WeaponCategory = WeaponCategory.Bow,
                Damage = damage,
                CooldownFrames = rng.Next(10, 17),
                BasePrice = price,
            };
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

    public class SpawnedEnemy
    {
        public EnemyDefinition Definition = null!;
        public FrameworkElement Body = null!;
        public Image? BodySprite = null;
        public List<BitmapImage> WalkFrames { get; set; } = new();
        public List<BitmapImage> AttackFrames { get; set; } = new();
        public Rectangle AttackHitbox = null!;
        public bool IsAttacking = false;
        public int AttackFramesRemaining = 0;
        public int AttackCooldownFrames = 0;
        public bool AttackDamageApplied = false;
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
    }

    public class ArrowProjectile
    {
        public FrameworkElement Body = null!;
        public double X;
        public double Y;
        public int Direction;
        public double Speed;
        public double DistanceTraveled;
        public double MaxDistance;
        public int Damage;
        public bool IsAlive = true;
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

        private const int VK_LEFT = 0x25;
        private const int VK_UP = 0x26;
        private const int VK_RIGHT = 0x27;
        private const int VK_SPACE = 0x20;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_A = 0x41;
        private const int VK_C = 0x43;
        private const int VK_D = 0x44;
        private const int VK_E = 0x45;
        private const int VK_F = 0x46;
        private const int VK_M = 0x4D;
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
        private bool meleeHeld = false;
        private bool fireHeld = false;
        private bool closeHeld = false;
        private bool fastTravelHeld = false;
        private bool statsHeld = false;
        private bool mapHeld = false;
        private bool potionHeld = false;

        private readonly bool[] numberHeld = new bool[9];
        private readonly bool[] numberHeldLastFrame = new bool[9];

        private bool jumpHeldLastFrame = false;
        private bool meleeHeldLastFrame = false;
        private bool fireHeldLastFrame = false;
        private bool closeHeldLastFrame = false;
        private bool fastTravelHeldLastFrame = false;
        private bool statsHeldLastFrame = false;
        private bool mapHeldLastFrame = false;
        private bool potionHeldLastFrame = false;

        // UI elements
        private DispatcherTimer timer = null!;
        private Image player = null!;
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
        private List<BitmapImage> playerJumpFrames = new();
        private List<BitmapImage> playerDamagedFrames = new();
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

        private List<BitmapImage> LoadFramesFromDirectory(string dir, string normalizedName, string action)
        {
            var frames = new List<BitmapImage>();
            if (!System.IO.Directory.Exists(dir))
                return frames;

            string pattern = $"{normalizedName}_{action}*.png";
            foreach (string file in System.IO.Directory
                .GetFiles(dir, pattern)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
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

        private double GetSpriteWidthForHeight(BitmapSource frame, double targetHeight)
        {
            if (frame.PixelHeight <= 0 || targetHeight <= 0)
                return targetHeight;

            return targetHeight * ((double)frame.PixelWidth / frame.PixelHeight);
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
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(file, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    frames.Add(bitmap);
                }
            }

            if (frames.Count == 0 && fallbackPackPath != null)
                frames.Add(LoadSprite(fallbackPackPath));

            return frames;
        }

        // Game state
        private Area currentArea = null!;
        private Area? previousArea = null;
        private AreaTransition transition = null!;
        private readonly Dictionary<int, Area> stageAreas = new();
        private int currentStageNumber = 0;
        private int highestUnlockedStage = 1;

        private readonly List<SpawnedZoneVisual> activeZoneVisuals = new();
        private readonly List<SpawnedEnemy> activeEnemies = new();
        private readonly List<ArrowProjectile> activeProjectiles = new();
        private readonly List<AreaTemplate> areaTemplates = new();
        private readonly List<EnemyTemplate> enemyTemplates = new();
        private readonly List<BossTemplate> bossTemplates = new();
        private readonly List<ItemTemplate> itemTemplates = new();

        private readonly PlayerData playerData = new();
        private GameConfig gameConfig = new();

        // Physics / layout
        private double playAreaHeight = 140;
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
        private double arrowHitboxWidth = 10;
        private double arrowHitboxHeight = 6;
        private double arrowSpeed = 8.5;
        private int arrowDurationFrames = 35;
        private double groundY = 0;
        private bool isOnGround = false;
        private bool facingRight = true;
        private double groundStripHeight = 14;

        // Game state flags
        private bool controlsEnabled = true;
        private PanelMode panelMode = PanelMode.None;

        private bool isAttacking = false;
        private int attackFramesRemaining = 0;
        private int attackDurationFrames = 8;
        private int meleeCooldownFrames = 0;
        private int bowCooldownFrames = 0;

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
            playerJumpFrames = LoadPlayerAnimationFrames("jump");
            playerDamagedFrames = LoadPlayerAnimationFrames("damaged");
            playerArrowSprite = LoadOptionalPlayerSpriteFromDisk("arrow.png");

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
            arrowHitboxWidth = Math.Max(2, gameConfig.ArrowHitboxWidth);
            arrowHitboxHeight = Math.Max(2, gameConfig.ArrowHitboxHeight);
            arrowSpeed = Math.Max(1, gameConfig.ArrowSpeed);
            arrowDurationFrames = Math.Max(1, gameConfig.ArrowDurationFrames);
        }

        private void LoadAreaTemplates()
        {
            areaTemplates.Clear();
            string path = IOPath.Combine(AppContext.BaseDirectory, "area_definitions.txt");

            if (!System.IO.File.Exists(path))
            {
                string seed =
                    "# name;stages;enemies;colorHex(optional)\n" +
                    "Plains;1-4,16-19;slime,wolf;5AAA50\n" +
                    "Cave;6-9,21-24;bat,crawler;5F5F69\n" +
                    "Forest;11-14,26-29;wolf,slime;3C8246\n" +
                    "Tundra;31-34;frostling,bat;8CAABE";
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
            string defsPath = IOPath.Combine(AppContext.BaseDirectory, "enemy_definitions.txt");

            if (!System.IO.File.Exists(defsPath))
            {
                string seed =
                    "slime;10;4;0.9;1;plains;\n" +
                    "bat;9;5;1.4;2;cave;\n" +
                    "wolf;14;6;1.2;4;forest;\n" +
                    "crawler;18;8;1.1;6;cave;6-9\n" +
                    "frostling;22;10;1.0;8;tundra;6-9";
                System.IO.File.WriteAllText(defsPath, seed);
            }

            foreach (var raw in System.IO.File.ReadAllLines(defsPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                string[] parts = line.Split(';');
                if (parts.Length < 5) continue;

                if (!int.TryParse(parts[1], out int hp)) continue;
                if (!int.TryParse(parts[2], out int atk)) continue;
                if (!double.TryParse(parts[3], out double speed)) continue;
                if (!int.TryParse(parts[4], out int level)) continue;

                var template = new EnemyTemplate
                {
                    Name = parts[0].Trim(),
                    Health = hp,
                    AttackDamage = atk,
                    MoveSpeed = speed,
                    Level = level
                };

                if (parts.Length >= 6 && !string.IsNullOrWhiteSpace(parts[5]))
                {
                    var biomes = new HashSet<BiomeType>();
                    foreach (string token in parts[5].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (token == "*") { biomes.Clear(); break; }
                        if (Enum.TryParse<BiomeType>(token, true, out var biome))
                            biomes.Add(biome);
                    }
                    if (biomes.Count > 0)
                        template.AllowedBiomes = biomes;
                }

                if (parts.Length >= 7 && !string.IsNullOrWhiteSpace(parts[6]))
                {
                    foreach (string token in parts[6].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (token == "*")
                        {
                            template.StageRanges.Clear();
                            break;
                        }

                        string[] range = token.Split('-', StringSplitOptions.TrimEntries);
                        if (range.Length == 1 && int.TryParse(range[0], out int exact))
                            template.StageRanges.Add((exact, exact));
                        else if (range.Length == 2 &&
                                 int.TryParse(range[0], out int min) &&
                                 int.TryParse(range[1], out int max))
                            template.StageRanges.Add((Math.Min(min, max), Math.Max(min, max)));
                    }
                }

                enemyTemplates.Add(template);
            }
        }

        private void LoadBossTemplates()
        {
            bossTemplates.Clear();
            string path = IOPath.Combine(AppContext.BaseDirectory, "boss_definitions.txt");

            if (!System.IO.File.Exists(path))
            {
                string seed =
                    "# name;health;attackdamage;movespeed;width(optional);height(optional)\n" +
                    "The Goo;80;15;1.05;64;64\n" +
                    "Fallen Knight;105;19;1.20;64;64\n" +
                    "DB-5000;130;24;1.10;72;64";
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

                bossTemplates.Add(new BossTemplate
                {
                    Name = parts[0].Trim(),
                    Health = Math.Max(20, health),
                    AttackDamage = Math.Max(2, attackDamage),
                    MoveSpeed = Math.Max(0.4, moveSpeed),
                    Width = Math.Max(24, width),
                    Height = Math.Max(24, height)
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
                    "Rusty Sword;2;12\n" +
                    "Copper Sword;3;11\n" +
                    "Iron Sword;4;10\n" +
                    "Simple Bow;2;14\n" +
                    "Hunter Bow;3;13\n" +
                    "War Bow;4;12";
                System.IO.File.WriteAllText(itemPath, seed);
            }

            foreach (var raw in System.IO.File.ReadAllLines(itemPath))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                string[] parts = line.Split(';');
                if (parts.Length < 3) continue;
                if (!int.TryParse(parts[1], out int dmg)) continue;
                if (!int.TryParse(parts[2], out int cooldown)) continue;

                string name = parts[0].Trim();
                itemTemplates.Add(new ItemTemplate
                {
                    Name = name,
                    Damage = Math.Max(1, dmg),
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

        private static string SerializeInventoryEntry(InventoryEntry entry)
        {
            string basePart = $"{entry.Item.Kind}|{entry.Item.Name.Replace("|", "")}|{entry.Quantity}|{entry.Item.BasePrice}";
            return entry.Item switch
            {
                WeaponItem w => $"{basePart}|{w.WeaponCategory}|{w.Damage}|{w.CooldownFrames}",
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

            ItemBase item = kind switch
            {
                ItemKind.Weapon when parts.Length >= 6 &&
                    Enum.TryParse<WeaponCategory>(parts[4], out var category) &&
                    int.TryParse(parts[5], out int dmg) =>
                    new WeaponItem
                    {
                        Name = name,
                        BasePrice = basePrice,
                        WeaponCategory = category,
                        Damage = dmg,
                        CooldownFrames = parts.Length >= 7 && int.TryParse(parts[6], out int cd) ? cd : 10
                    },
                ItemKind.Consumable when parts.Length >= 5 &&
                    int.TryParse(parts[4], out int heal) =>
                    new ConsumableItem { Name = name, BasePrice = basePrice, HealAmount = heal },
                ItemKind.Ammo when parts.Length >= 5 =>
                    new AmmoItem { Name = name, BasePrice = basePrice, AmmoType = parts[4] },
                _ => new AmmoItem { Name = name, BasePrice = basePrice, AmmoType = "Arrow" }
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
                case VK_Z: meleeHeld = isDown; break;
                case VK_X: fireHeld = isDown; break;
                case VK_C:
                case VK_ESCAPE:
                    closeHeld = isDown;
                    break;
                case VK_F: fastTravelHeld = isDown; break;
                case VK_E: statsHeld = isDown; break;
                case VK_M: mapHeld = isDown; break;
                case VK_OEM_2: potionHeld = isDown; break;
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
                case VK_LEFT:
                case VK_UP:
                case VK_RIGHT:
                case VK_SPACE:
                case VK_A:
                case VK_C:
                case VK_ESCAPE:
                case VK_D:
                case VK_E:
                case VK_F:
                case VK_M:
                case VK_OEM_2:
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
            leftHeld = rightHeld = jumpHeld = meleeHeld = fireHeld =
            closeHeld = fastTravelHeld = statsHeld = mapHeld = potionHeld = false;

            jumpHeldLastFrame = meleeHeldLastFrame = fireHeldLastFrame =
            closeHeldLastFrame = fastTravelHeldLastFrame = statsHeldLastFrame =
            mapHeldLastFrame = potionHeldLastFrame = false;

            for (int i = 0; i < numberHeld.Length; i++)
            {
                numberHeld[i] = false;
                numberHeldLastFrame[i] = false;
            }
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
            GameCanvas.Children.Add(playerHitboxDebug);
            GameCanvas.Children.Add(attackHitbox);

            Panel.SetZIndex(player, 20);
            Panel.SetZIndex(playerHitboxDebug, 21);
            Panel.SetZIndex(attackHitbox, 22);

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
                UpdateEnemies();
                UpdateProjectiles();
                HandleEnemyContactWithPlayer();
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

            if (statusFramesRemaining > 0)
                statusFramesRemaining--;
            else
                statusText.Text = "";

            if (panelMode != PanelMode.None)
                RenderCurrentPanel();

            UpdateExitTexts();
            DrawPlayer();
            DrawAttackHitbox();
            DrawPlayerHud();
            UpdateNpcAnimations();
            DrawInteractionIndicators();
            DrawEnemies();
            DrawProjectiles();
        }

        private void RefreshLayoutSizedElements()
        {
            groundRect.Width = Width;
            Canvas.SetTop(groundRect, Height - groundStripHeight);
            groundY = Height - groundStripHeight - playerHeight;

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
            bool closePressedThisFrame = closeHeld && !closeHeldLastFrame;
            bool fastPressedThisFrame = fastTravelHeld && !fastTravelHeldLastFrame;
            bool statsPressedThisFrame = statsHeld && !statsHeldLastFrame;
            bool mapPressedThisFrame = mapHeld && !mapHeldLastFrame;
            bool potionPressedThisFrame = potionHeld && !potionHeldLastFrame;

            if (!controlsEnabled)
            {
                velocityX = 0;
                CacheLastFrameInput();
                return;
            }

            if (closePressedThisFrame)
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
            velocityX = 0;
            if (leftHeld && !rightHeld) { velocityX = -moveSpeed; facingRight = false; }
            if (rightHeld && !leftHeld) { velocityX = moveSpeed; facingRight = true; }

            if (jumpPressedThisFrame && isOnGround)
            {
                velocityY = jumpStrength;
                isOnGround = false;
            }

            if (meleePressedThisFrame)
            {
                var interactable = FindInteractableZoneInRange();
                if (interactable != null)
                    InteractWithZone(interactable);
                else if (!isAttacking && meleeCooldownFrames <= 0)
                    StartMeleeAttack();
            }

            if (firePressedThisFrame && panelMode == PanelMode.None)
            {
                if (bowCooldownFrames <= 0)
                    FireBow();
                else
                    ShowStatus($"Bow cooling down ({bowCooldownFrames})", 25);
            }

            CacheLastFrameInput();
        }

        private void CacheLastFrameInput()
        {
            jumpHeldLastFrame = jumpHeld;
            meleeHeldLastFrame = meleeHeld;
            fireHeldLastFrame = fireHeld;
            closeHeldLastFrame = closeHeld;
            fastTravelHeldLastFrame = fastTravelHeld;
            statsHeldLastFrame = statsHeld;
            mapHeldLastFrame = mapHeld;
            potionHeldLastFrame = potionHeld;

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
                RenderCurrentPanel();
            }
        }

        private void CloseAllPanels()
        {
            panelMode = PanelMode.None;
            panelBorder.Visibility = Visibility.Hidden;
            panelText.Text = "";
            activeShop = null;
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
            panelText.Text =
                "FAST TRAVEL\n" +
                "\n" +
                "1. Town\n" +
                $"2. Stage {highestUnlockedStage} frontier\n" +
                "\n" +
                $"Current: {(currentStageNumber == 0 ? "Town" : $"Stage {currentStageNumber}")}\n" +
                "C = close";
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
                $"Gold:{playerData.Gold}  DMG:{playerData.BaseDamage}  Loc:{currentArea.Name}\n" +
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
                    int sellPrice = Math.Max(1, entry.Item.BasePrice / 2);
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
            else if (number == 2)
            {
                if (currentStageNumber == highestUnlockedStage) return;
                CloseAllPanels();
                LoadArea(highestUnlockedStage, TransitionDirection.Right, animate: true);
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
                ShowStatus("You defeated all configured bosses. You win for now!", 150);
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

            bool enteringTown = currentArea.Type == AreaType.Town &&
                                (previousArea == null || previousArea.Type != AreaType.Town);
            if (enteringTown)
                RefreshTownShops();

            groundRect.Fill = new SolidColorBrush(currentArea.GroundColor);
            ClearAreaZoneVisuals();
            ClearEnemies();
            ClearProjectiles();
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
                        shop.Stock.Add(new ShopListing { Item = arrow, Quantity = 5, Price = 5 });
                        break;

                    case ShopType.Healing:
                        var potion = ItemFactory.CreatePotion();
                        shop.Stock.Add(new ShopListing { Item = potion, Quantity = 1, Price = potion.BasePrice });
                        shop.Stock.Add(new ShopListing { Item = potion, Quantity = 2, Price = potion.BasePrice * 2 });
                        break;
                }
            }
        }

        private List<WeaponItem> GenerateShopWeapons(WeaponCategory category, int count)
        {
            int tier = Math.Max(0, (highestUnlockedStage - 1) / 5);
            var pool = itemTemplates
                .Where(i => i.Category == category)
                .OrderBy(i => i.Damage)
                .ToList();

            if (pool.Count == 0)
            {
                return Enumerable.Range(0, count)
                    .Select(_ => category == WeaponCategory.Sword
                        ? ItemFactory.CreateRandomSword(rng)
                        : ItemFactory.CreateRandomBow(rng))
                    .ToList();
            }

            int maxIndex = Math.Min(pool.Count - 1, tier + 1);
            var unlocked = pool.Take(maxIndex + 1).ToList();
            var results = new List<WeaponItem>();

            for (int i = 0; i < count; i++)
            {
                var template = unlocked[rng.Next(unlocked.Count)];
                int tierDamageBonus = (tier * 2) + (tier >= 3 ? 1 : 0);
                int dmg = template.Damage + tierDamageBonus + rng.Next(0, 2);
                int cooldown = Math.Max(1, template.CooldownFrames - (tier / 2));
                int price = (dmg * 14) + (tier * 18) + Math.Max(1, 24 - cooldown);

                results.Add(new WeaponItem
                {
                    Name = template.Name,
                    WeaponCategory = category,
                    Damage = dmg,
                    CooldownFrames = cooldown,
                    BasePrice = price,
                    SpritePath = template.SpritePath
                });
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
                        ShowStatus("No configured boss for the next milestone. You win for now!", 120);
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
                            ShowStatus("No more bosses configured. You win for now!", 140);
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
        private void SpawnEnemies(Area area)
        {
            if (area.Type == AreaType.Town) return;

            foreach (var def in area.EnemySpawns)
            {
                var walkFrames = LoadEnemyFrames(def.Name, "walk");
                var attackFrames = LoadEnemyFrames(def.Name, "attack");

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
                    Width = def.Width * 2,
                    Height = Math.Max(8, def.Height * 0.65),
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

                activeEnemies.Add(new SpawnedEnemy
                {
                    Definition = def,
                    Body = body,
                    BodySprite = bodySprite,
                    WalkFrames = walkFrames,
                    AttackFrames = attackFrames,
                    AttackHitbox = attackHitbox,
                    Label = label,
                    HealthBg = healthBg,
                    HealthFill = healthFill,
                    X = def.X,
                    Y = floorLine - def.Height,
                    LeftBound = Math.Max(10, def.X - def.PatrolRange),
                    RightBound = Math.Min(Width - def.Width - 10, def.X + def.PatrolRange),
                    Speed = def.Speed,
                    Direction = 1,
                    IsAlive = true,
                    CurrentHealth = def.MaxHealth
                });
            }
        }

        private void ClearEnemies()
        {
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

        private void UpdateEnemies()
        {
            foreach (var enemy in activeEnemies)
            {
                if (!enemy.IsAlive) continue;

                double playerCenterX = playerX + playerWidth / 2;
                double enemyCenterX = enemy.X + enemy.Body.Width / 2;
                double distance = Math.Abs(playerCenterX - enemyCenterX);

                bool inAggroRange = distance <= enemy.Definition.AggroRange;
                bool inAttackRange = distance <= Math.Max(36, enemy.Definition.Width + 20);

                if (inAggroRange)
                {
                    // Chase the player
                    enemy.Direction = playerCenterX >= enemyCenterX ? 1 : -1;

                    if (!enemy.IsAttacking && enemy.AttackCooldownFrames <= 0 && inAttackRange)
                    {
                        enemy.IsAttacking = true;
                        enemy.AttackFramesRemaining = Math.Max(10, enemy.AttackFrames.Count * 8);
                        enemy.AttackDamageApplied = false;
                        enemy.AttackCooldownFrames = 38;
                    }
                }
                else
                {
                    // Patrol — reverse at bounds
                    if (enemy.X <= enemy.LeftBound) enemy.Direction = 1;
                    else if (enemy.X >= enemy.RightBound) enemy.Direction = -1;
                }

                if (!enemy.IsAttacking && (!inAggroRange || !inAttackRange))
                    enemy.X += enemy.Speed * enemy.Direction;

                enemy.X = Math.Max(0, Math.Min(Width - enemy.Definition.Width, enemy.X));
                enemy.Y = groundY + playerHeight - enemy.Body.Height;

                if (enemy.AttackCooldownFrames > 0)
                    enemy.AttackCooldownFrames--;

                if (enemy.IsAttacking)
                {
                    enemy.AttackFramesRemaining--;
                    if (enemy.AttackFramesRemaining <= 0)
                        enemy.IsAttacking = false;
                }

                if (enemy.BodySprite != null)
                {
                    var frames = (enemy.IsAttacking && enemy.AttackFrames.Count > 0)
                        ? enemy.AttackFrames
                        : enemy.WalkFrames;

                    if (frames.Count > 0)
                    {
                        int frameIndex = (animationFrameCounter / 10) % frames.Count;
                        enemy.BodySprite.Source = frames[frameIndex];
                    }
                }
            }
        }

        private void DrawEnemies()
        {
            foreach (var enemy in activeEnemies)
            {
                if (!enemy.IsAlive) continue;

                double drawX = enemy.X;
                double spriteWidth = enemy.Definition.Width;
                if (enemy.BodySprite?.Source is BitmapSource currentFrame)
                {
                    spriteWidth = GetSpriteWidthForHeight(currentFrame, enemy.Definition.Height);
                }
                else if (enemy.IsAttacking && enemy.AttackFrames.Count > 0)
                {
                    spriteWidth = enemy.Definition.Width * 2;
                }

                if (enemy.Direction < 0 && spriteWidth > enemy.Definition.Width)
                {
                    drawX = enemy.X - (spriteWidth - enemy.Definition.Width);
                }

                enemy.Body.Width = spriteWidth;
                Canvas.SetLeft(enemy.Body, drawX);
                Canvas.SetTop(enemy.Body, enemy.Y);

                if (enemy.BodySprite != null)
                {
                    enemy.BodySprite.RenderTransformOrigin = new Point(0.5, 0.5);
                    enemy.BodySprite.RenderTransform = new ScaleTransform(enemy.Direction >= 0 ? 1 : -1, 1);
                }

                Canvas.SetLeft(enemy.Label, enemy.X - 26);
                Canvas.SetTop(enemy.Label, enemy.Y - 14);

                double hpRatio = enemy.Definition.MaxHealth > 0
                    ? (double)enemy.CurrentHealth / enemy.Definition.MaxHealth : 0;

                enemy.HealthFill.Width = 28 * Math.Max(0, hpRatio);
                Canvas.SetLeft(enemy.HealthBg, enemy.X - 5);
                Canvas.SetTop(enemy.HealthBg, enemy.Y - 22);
                Canvas.SetLeft(enemy.HealthFill, enemy.X - 5);
                Canvas.SetTop(enemy.HealthFill, enemy.Y - 22);

                double hitboxX = enemy.Direction >= 0
                    ? enemy.X + enemy.Definition.Width - 2
                    : enemy.X - enemy.AttackHitbox.Width + 2;
                double hitboxY = enemy.Y + 6;
                Canvas.SetLeft(enemy.AttackHitbox, hitboxX);
                Canvas.SetTop(enemy.AttackHitbox, hitboxY);
            }
        }

        private void HandleEnemyContactWithPlayer()
        {
            if (playerDamageCooldownFrames > 0) return;

            Rect playerRect = GetPlayerCollisionRect();

            foreach (var enemy in activeEnemies)
            {
                if (!enemy.IsAlive) continue;

                if (enemy.IsAttacking && !enemy.AttackDamageApplied)
                {
                    Rect attackRect = new Rect(
                        Canvas.GetLeft(enemy.AttackHitbox),
                        Canvas.GetTop(enemy.AttackHitbox),
                        enemy.AttackHitbox.Width,
                        enemy.AttackHitbox.Height);

                    if (playerRect.IntersectsWith(attackRect))
                    {
                        playerData.Health = Math.Max(0, playerData.Health - enemy.Definition.ContactDamage);
                        playerDamageCooldownFrames = PlayerDamageCooldownMax;
                        enemy.AttackDamageApplied = true;
                        ShowStatus($"{enemy.Definition.Name} attacks!", 35);
                        break;
                    }
                }
            }
        }

        private void DamageEnemy(SpawnedEnemy enemy, int damage)
        {
            if (!enemy.IsAlive) return;
            enemy.CurrentHealth -= damage;
            if (enemy.CurrentHealth <= 0)
                KillEnemy(enemy);
        }

        private void KillEnemy(SpawnedEnemy enemy)
        {
            enemy.IsAlive = false;

            int goldDrop = rng.Next(enemy.Definition.GoldMin, enemy.Definition.GoldMax + 1);
            int scaledXp = ScaleXpForPlayerLevel(enemy.Definition.XpReward, enemy.Definition.PowerLevel);
            playerData.Gold += goldDrop;
            GainExperience(scaledXp);

            GameCanvas.Children.Remove(enemy.Body);
            GameCanvas.Children.Remove(enemy.Label);
            GameCanvas.Children.Remove(enemy.HealthBg);
            GameCanvas.Children.Remove(enemy.HealthFill);
            GameCanvas.Children.Remove(enemy.AttackHitbox);
            activeEnemies.Remove(enemy);

            ShowStatus($"+{scaledXp} XP, +{goldDrop}g", 60);

            if (currentStageNumber > 0 && activeEnemies.Count == 0)
            {
                highestUnlockedStage = Math.Max(highestUnlockedStage, currentStageNumber + 1);
                string clearMessage = currentArea.IsBossArea
                    ? "Boss defeated! Return right to Town."
                    : $"Area clear! Stage {currentStageNumber + 1} unlocked.";
                ShowStatus(clearMessage, 100);

                if (currentArea.IsBossArea)
                    SaveGameState();
            }
        }

        private void GainExperience(int amount)
        {
            playerData.Experience += amount;

            while (playerData.Experience >= playerData.NextLevelXp)
            {
                playerData.Experience -= playerData.NextLevelXp;
                playerData.Level++;
                ShowStatus($"Level Up! Now Lv {playerData.Level}", 80);
            }
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

        // -----------------------------------------------------------------------
        // Combat — melee
        // -----------------------------------------------------------------------
        private void StartMeleeAttack()
        {
            isAttacking = true;
            attackFramesRemaining = attackDurationFrames;
            attackHitbox.Visibility = Visibility.Visible;
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

            // Guard: use BaseDamage alone if no sword equipped
            int damage = playerData.BaseDamage +
                         (playerData.EquippedSword?.Damage ?? 0);

            foreach (var enemy in activeEnemies.ToList())
            {
                if (!enemy.IsAlive) continue;
                Rect enemyRect = new Rect(enemy.X, enemy.Y, enemy.Body.Width, enemy.Body.Height);
                if (attackRect.IntersectsWith(enemyRect))
                    DamageEnemy(enemy, damage);
            }
        }

        private void UpdateAttack()
        {
            if (!isAttacking)
            {
                attackHitbox.Visibility = gameConfig.Debug ? Visibility.Visible : Visibility.Hidden;
                return;
            }

            attackFramesRemaining--;
            if (attackFramesRemaining <= 0)
            {
                isAttacking = false;
                attackHitbox.Visibility = gameConfig.Debug ? Visibility.Visible : Visibility.Hidden;
            }
        }

        // -----------------------------------------------------------------------
        // Combat — bow
        // -----------------------------------------------------------------------
        private void FireBow()
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

            int damage = playerData.BaseDamage + playerData.EquippedBow.Damage;

            FrameworkElement body;
            if (playerArrowSprite != null)
            {
                var arrowImage = new Image
                {
                    Width = 16,
                    Height = 16,
                    Stretch = Stretch.Fill,
                    Source = playerArrowSprite
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
            double maxDistance = arrowSpeed * arrowDurationFrames;

            activeProjectiles.Add(new ArrowProjectile
            {
                Body = body,
                X = startX,
                Y = startY,
                Direction = facingRight ? 1 : -1,
                Speed = arrowSpeed,
                MaxDistance = maxDistance,
                DistanceTraveled = 0,
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
                arrow.DistanceTraveled += Math.Abs(move);

                if (arrow.DistanceTraveled >= arrow.MaxDistance)
                {
                    RemoveProjectile(arrow);
                    continue;
                }

                Rect arrowRect = GetArrowHitboxRect(arrow);

                foreach (var enemy in activeEnemies.ToList())
                {
                    if (!enemy.IsAlive) continue;
                    Rect enemyRect = new Rect(enemy.X, enemy.Y, enemy.Body.Width, enemy.Body.Height);
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
                    image.RenderTransformOrigin = new Point(0.5, 0.5);
                    image.RenderTransform = new ScaleTransform(arrow.Direction >= 0 ? 1 : -1, 1);
                }
            }
        }

        private Rect GetArrowHitboxRect(ArrowProjectile arrow)
        {
            double hitboxW = Math.Max(2, arrowHitboxWidth);
            double hitboxH = Math.Max(2, arrowHitboxHeight);
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
            int sellPrice = Math.Max(1, entry.Item.BasePrice / 2);
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
                Damage = w.Damage,
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

        // -----------------------------------------------------------------------
        // Drawing helpers
        // -----------------------------------------------------------------------
        private void DrawPlayer()
        {
            animationFrameCounter++;
            double spriteDrawWidth = playerWidth;
            BitmapImage activeFrame = playerIdleFrames[0];

            if (isAttacking && playerAttackFrames.Count > 0)
            {
                int attackIndex = (animationFrameCounter / 5) % playerAttackFrames.Count;
                activeFrame = playerAttackFrames[attackIndex];
            }
            else if (playerDamageCooldownFrames > 0 && playerDamagedFrames.Count > 0)
            {
                int damageIndex = (animationFrameCounter / 6) % playerDamagedFrames.Count;
                activeFrame = playerDamagedFrames[damageIndex];
            }
            else if (!isOnGround && playerJumpFrames.Count > 0)
            {
                int jumpIndex = (animationFrameCounter / 7) % playerJumpFrames.Count;
                activeFrame = playerJumpFrames[jumpIndex];
            }
            else if (Math.Abs(velocityX) > 0.1)
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

            player.RenderTransformOrigin = new Point(0.5, 0.5);
            player.RenderTransform = new ScaleTransform(facingRight ? 1 : -1, 1);
            player.Width = spriteDrawWidth;

            double playerDrawX = playerX;
            if (isAttacking && !facingRight)
                playerDrawX = playerX - (spriteDrawWidth - playerWidth);

            Canvas.SetLeft(player, playerDrawX);
            Canvas.SetTop(player, playerY);

            Rect playerHitbox = GetPlayerCollisionRect();
            playerHitboxDebug.Width = playerHitbox.Width;
            playerHitboxDebug.Height = playerHitbox.Height;
            Canvas.SetLeft(playerHitboxDebug, playerHitbox.X);
            Canvas.SetTop(playerHitboxDebug, playerHitbox.Y);
        }

        private void DrawAttackHitbox()
        {
            double overlapIntoPlayer = 16;
            double forwardReach = gameConfig.AttackPosition;

            double hitboxX = facingRight
                ? playerX + playerWidth - overlapIntoPlayer + forwardReach
                : playerX - attackHitbox.Width + overlapIntoPlayer - forwardReach;

            Canvas.SetLeft(attackHitbox, hitboxX);
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

            playerHealthText.Text = $"{playerData.Health}/{playerData.MaxHealth}";
            Canvas.SetLeft(playerHealthText, playerX - 18);
            Canvas.SetTop(playerHealthText, playerY - 36);

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