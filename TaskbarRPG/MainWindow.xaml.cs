using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Media.Imaging;

namespace TaskbarRPG
{
    public enum AreaType
    {
        Town,
        Plains,
        Cave,
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

        public WeaponItem()
        {
            Kind = ItemKind.Weapon;
            Stackable = false;
        }

        public override string GetDisplayText() => $"{Name} (DMG {Damage})";
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

        public int NextLevelXp => Level * 12;
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

    public class Area
    {
        public AreaType Type { get; set; }
        public string Name { get; set; } = "";
        public Color GroundColor { get; set; }
        public int LevelRequirement { get; set; }
        public AreaType? LeftExit { get; set; }
        public AreaType? RightExit { get; set; }
        public VariableZone[] Zones { get; set; } = new VariableZone[6];
        public List<EnemyDefinition> EnemySpawns { get; set; } = new();
    }

    public static class AreaDefinitions
    {
        public static readonly List<Area> Ordered = new()
        {
            new Area
            {
                Type = AreaType.Town,
                Name = "Town",
                GroundColor = Color.FromRgb(194, 154, 108),
                LevelRequirement = 0,
                LeftExit = null,
                RightExit = AreaType.Plains,
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
            },

            new Area
            {
                Type = AreaType.Plains,
                Name = "Plains",
                GroundColor = Color.FromRgb(90, 170, 80),
                LevelRequirement = 0,
                LeftExit = AreaType.Town,
                RightExit = AreaType.Cave,
                Zones = CreateEmptyZones(),
                EnemySpawns = new List<EnemyDefinition>
                {
                    new EnemyDefinition
                    {
                        Name = "Slime",
                        X = 450,
                        PatrolRange = 90,
                        AggroRange = 170,
                        Speed = 1.0,
                        MaxHealth = 5,
                        ContactDamage = 5,
                        XpReward = 4,
                        GoldMin = 1,
                        GoldMax = 2,
                        Color = Color.FromRgb(80, 220, 130),
                    },
                    new EnemyDefinition
                    {
                        Name = "Slime",
                        X = 980,
                        PatrolRange = 120,
                        AggroRange = 170,
                        Speed = 1.1,
                        MaxHealth = 5,
                        ContactDamage = 5,
                        XpReward = 4,
                        GoldMin = 1,
                        GoldMax = 2,
                        Color = Color.FromRgb(50, 190, 100),
                    }
                }
            },

            new Area
            {
                Type = AreaType.Cave,
                Name = "Cave",
                GroundColor = Color.FromRgb(95, 95, 105),
                LevelRequirement = 5,
                LeftExit = AreaType.Plains,
                RightExit = null,
                Zones = CreateEmptyZones(),
                EnemySpawns = new List<EnemyDefinition>
                {
                    new EnemyDefinition
                    {
                        Name = "Bat",
                        X = 520,
                        PatrolRange = 140,
                        AggroRange = 210,
                        Speed = 1.4,
                        MaxHealth = 9,
                        ContactDamage = 8,
                        XpReward = 8,
                        GoldMin = 3,
                        GoldMax = 5,
                        Color = Color.FromRgb(150, 80, 180),
                    },
                    new EnemyDefinition
                    {
                        Name = "Crawler",
                        X = 1180,
                        PatrolRange = 80,
                        AggroRange = 180,
                        Speed = 1.2,
                        MaxHealth = 12,
                        ContactDamage = 10,
                        XpReward = 10,
                        GoldMin = 4,
                        GoldMax = 6,
                        Color = Color.FromRgb(170, 70, 70),
                    }
                }
            },
        };

        public static readonly Dictionary<AreaType, Area> All =
            Ordered.ToDictionary(a => a.Type, a => a);

        public static Area Get(AreaType type) => All[type];

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
        private readonly Action<AreaType, TransitionDirection> onMidpoint;

        private bool fadingOut = true;
        private AreaType pendingArea;
        private TransitionDirection pendingDir;
        private double opacity = 0;

        public bool IsActive { get; private set; }

        public AreaTransition(Canvas canvas, Action<AreaType, TransitionDirection> onMidpoint)
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

        public void Start(AreaType target, TransitionDirection dir)
        {
            if (IsActive) return;

            pendingArea = target;
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
                    onMidpoint(pendingArea, pendingDir);
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
        public Rectangle Building = null!;
        public TextBlock BuildingLabel = null!;
        public Rectangle Npc = null!;
        public TextBlock NpcLabel = null!;
    }

    public class SpawnedEnemy
    {
        public EnemyDefinition Definition = null!;
        public Rectangle Body = null!;
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
        public Rectangle Body = null!;
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

        private BitmapImage playerIdleSprite = null!;
        private BitmapImage playerWalk1Sprite = null!;
        private BitmapImage playerWalk2Sprite = null!;
        private BitmapImage playerAttackSprite = null!;

        private int animationFrameCounter = 0;

        private BitmapImage LoadSprite(string relativePath)
        {
            return new BitmapImage(new Uri($"pack://application:,,,/{relativePath}", UriKind.Absolute));
        }

        // Game state
        private Area currentArea = null!;
        private Area? previousArea = null;
        private AreaTransition transition = null!;

        private readonly List<SpawnedZoneVisual> activeZoneVisuals = new();
        private readonly List<SpawnedEnemy> activeEnemies = new();
        private readonly List<ArrowProjectile> activeProjectiles = new();

        private readonly PlayerData playerData = new();

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
        private double groundY = 0;
        private bool isOnGround = false;
        private bool facingRight = true;
        private double groundStripHeight = 14;

        // Game state flags
        private bool controlsEnabled = false;
        private PanelMode panelMode = PanelMode.None;

        private bool isAttacking = false;
        private int attackFramesRemaining = 0;
        private int attackDurationFrames = 8;

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
            InitializePlayerData();
            PositionAboveTaskbar();
            MakeClickThrough();
            CreateBackground();
            CreateHud();
            CreateMainPanel();
            CreateEdgeTexts();

            playerIdleSprite = LoadSprite("Assets/Player/player_idle.png");
            playerWalk1Sprite = LoadSprite("Assets/Player/player_walk1.png");
            playerWalk2Sprite = LoadSprite("Assets/Player/player_walk2.png");
            playerAttackSprite = LoadSprite("Assets/Player/player_attack.png");

            CreatePlayer();
            SetupTransition();
            InstallKeyboardHook();

            LoadArea(AreaType.Town, TransitionDirection.Right, animate: false);
            StartGameLoop();

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
                case VK_C: closeHeld = isDown; break;
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

            playerArrowText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 9,
                FontFamily = new FontFamily("Consolas"),
                Width = 60,
                TextAlignment = TextAlignment.Center
            };

            statusText = new TextBlock
            {
                Foreground = Brushes.Gold,
                FontSize = 12,
                FontFamily = new FontFamily("Consolas"),
                Width = 420,
                TextAlignment = TextAlignment.Center,
            };

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

            for (double size = 10.0; size >= 5.5; size -= 0.5)
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

            rightExitText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 10,
                FontFamily = new FontFamily("Consolas"),
                Width = 180,
                TextAlignment = TextAlignment.Right
            };

            GameCanvas.Children.Add(leftExitText);
            GameCanvas.Children.Add(rightExitText);

            Panel.SetZIndex(leftExitText, 30);
            Panel.SetZIndex(rightExitText, 30);
        }

        private void UpdateExitTexts()
        {
            if (currentArea.LeftExit.HasValue)
            {
                var leftArea = AreaDefinitions.Get(currentArea.LeftExit.Value);
                leftExitText.Text = $"< {leftArea.Name} (Lv {leftArea.LevelRequirement})";
                leftExitText.Foreground = playerData.Level >= leftArea.LevelRequirement
                    ? Brushes.White : Brushes.OrangeRed;
            }
            else
            {
                leftExitText.Text = "";
            }

            if (currentArea.RightExit.HasValue)
            {
                var rightArea = AreaDefinitions.Get(currentArea.RightExit.Value);
                rightExitText.Text = $"{rightArea.Name} (Lv {rightArea.LevelRequirement}) >";
                rightExitText.Foreground = playerData.Level >= rightArea.LevelRequirement
                    ? Brushes.White : Brushes.OrangeRed;
            }
            else
            {
                rightExitText.Text = "";
            }
        }

        private void CreatePlayer()
        {
            player = new Image
            {
                Width = playerWidth,
                Height = playerHeight,
                Stretch = Stretch.Fill,
                Source = playerIdleSprite,
            };

            RenderOptions.SetBitmapScalingMode(player, BitmapScalingMode.NearestNeighbor);

            attackHitbox = new Rectangle
            {
                Width = 20,
                Height = 12,
                Fill = Brushes.Transparent,
                Visibility = Visibility.Hidden,
                Stroke = null,
            };

            GameCanvas.Children.Add(player);
            GameCanvas.Children.Add(attackHitbox);

            Panel.SetZIndex(player, 20);
            Panel.SetZIndex(attackHitbox, 21);

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
                    attackHitbox.Visibility = Visibility.Hidden;
            }

            currentInteractableZone = FindInteractableZoneInRange();
            mapPulseTime += 0.08;

            if (playerDamageCooldownFrames > 0)
                playerDamageCooldownFrames--;

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
                CloseAllPanels();
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
                else if (!isAttacking)
                    StartMeleeAttack();
            }

            if (firePressedThisFrame && panelMode == PanelMode.None)
                FireBow();

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
        private void RenderFastTravelPanel()
        {
            var lines = new List<string>
            {
                "FAST TRAVEL",
                "",
                "Choose destination with number keys:",
                ""
            };

            for (int i = 0; i < AreaDefinitions.Ordered.Count && i < 9; i++)
            {
                var area = AreaDefinitions.Ordered[i];
                string marker = area.Type == currentArea.Type ? "  <== CURRENT" : "";
                string lockText = playerData.Level >= area.LevelRequirement
                    ? "" : $" [LOCKED Lv {area.LevelRequirement}]";
                lines.Add($"{i + 1}. {area.Name}{lockText}{marker}");
            }

            lines.Add("");
            lines.Add("C = close");
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
            var ordered = AreaDefinitions.Ordered;
            int currentIndex = ordered.FindIndex(a => a.Type == currentArea.Type);
            string[] names = ordered.Select(a => $"{a.Name}(Lv{a.LevelRequirement})").ToArray();
            string mapLine = string.Join(" -- ", names);

            // Build character-position lookup for marker arrows
            var centers = new List<int>();
            int cursor = 0;
            for (int i = 0; i < names.Length; i++)
            {
                centers.Add(cursor + names[i].Length / 2);
                cursor += names[i].Length;
                if (i < names.Length - 1) cursor += 4; // " -- "
            }

            char[] markerLine = new string(' ', mapLine.Length).ToCharArray();
            char[] pulseLine = new string(' ', mapLine.Length).ToCharArray();

            if (currentIndex >= 0 && currentIndex < centers.Count)
            {
                int pos = centers[currentIndex];
                bool pulseOn = Math.Sin(mapPulseTime) > 0;

                if (pos >= 0 && pos < markerLine.Length) markerLine[pos] = '^';
                if (pos >= 0 && pos < pulseLine.Length) pulseLine[pos] = pulseOn ? '*' : 'o';
            }

            panelText.Text =
                "WORLD MAP\n" +
                new string(markerLine) + "\n" +
                mapLine + "\n" +
                new string(pulseLine) + "\n" +
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

        private void HandleFastTravelSelection()
        {
            int index = GetPressedNumberIndex();
            if (index < 0 || index >= AreaDefinitions.Ordered.Count)
                return;

            var target = AreaDefinitions.Ordered[index];

            if (playerData.Level < target.LevelRequirement)
            {
                ShowStatus($"Need Lv {target.LevelRequirement} for {target.Name}", 80);
                return;
            }

            if (target.Type == currentArea.Type)
                return;

            int currentIndex = AreaDefinitions.Ordered.FindIndex(a => a.Type == currentArea.Type);
            TransitionDirection dir = index >= currentIndex
                ? TransitionDirection.Right
                : TransitionDirection.Left;

            CloseAllPanels();
            LoadArea(target.Type, dir, animate: true);
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
        private void LoadArea(AreaType type, TransitionDirection entryDir, bool animate = true)
        {
            if (animate)
            {
                transition.Start(type, entryDir);
            }
            else
            {
                ApplyArea(type);
                playerX = entryDir == TransitionDirection.Right ? 10 : Width - playerWidth - 10;
                velocityX = 0;
                DrawPlayer();
                DrawAttackHitbox();
            }
        }

        private void OnTransitionMidpoint(AreaType type, TransitionDirection dir)
        {
            ApplyArea(type);
            playerX = dir == TransitionDirection.Right ? 10 : Width - playerWidth - 10;
            velocityX = 0;
            CloseAllPanels();
        }

        private void ApplyArea(AreaType type)
        {
            previousArea = currentArea;
            currentArea = AreaDefinitions.Get(type);

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
        }

        private void RefreshTownShops()
        {
            foreach (var zone in AreaDefinitions.Get(AreaType.Town).Zones)
            {
                if (zone.Content is not ShopZoneContent shop)
                    continue;

                shop.Stock.Clear();

                switch (shop.ShopType)
                {
                    case ShopType.Sword:
                        for (int i = 0; i < 3; i++)
                        {
                            var sword = ItemFactory.CreateRandomSword(rng);
                            shop.Stock.Add(new ShopListing { Item = sword, Quantity = 1, Price = sword.BasePrice });
                        }
                        break;

                    case ShopType.Bow:
                        for (int i = 0; i < 2; i++)
                        {
                            var bow = ItemFactory.CreateRandomBow(rng);
                            shop.Stock.Add(new ShopListing { Item = bow, Quantity = 1, Price = bow.BasePrice });
                        }
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

        private void CheckAreaTransition()
        {
            if (playerX > Width - playerWidth && currentArea.RightExit.HasValue)
            {
                var target = AreaDefinitions.Get(currentArea.RightExit.Value);
                if (playerData.Level < target.LevelRequirement)
                {
                    playerX = Width - playerWidth;
                    ShowStatus($"Need Lv {target.LevelRequirement} for {target.Name}", 80);
                }
                else
                {
                    LoadArea(currentArea.RightExit.Value, TransitionDirection.Right);
                }
            }
            else if (playerX < 0 && currentArea.LeftExit.HasValue)
            {
                var target = AreaDefinitions.Get(currentArea.LeftExit.Value);
                if (playerData.Level < target.LevelRequirement)
                {
                    playerX = 0;
                    ShowStatus($"Need Lv {target.LevelRequirement} for {target.Name}", 80);
                }
                else
                {
                    LoadArea(currentArea.LeftExit.Value, TransitionDirection.Left);
                }
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

                var building = new Rectangle
                {
                    Width = 96,
                    Height = 52,
                    Fill = new SolidColorBrush(zone.Content.BuildingColor),
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    RadiusX = 4,
                    RadiusY = 4,
                };

                var buildingLabel = new TextBlock
                {
                    Text = zone.Content.DisplayName,
                    Foreground = Brushes.White,
                    FontSize = 11,
                    FontFamily = new FontFamily("Consolas"),
                    Width = 110,
                    TextAlignment = TextAlignment.Center,
                };

                var npc = new Rectangle
                {
                    Width = 16,
                    Height = 24,
                    Fill = new SolidColorBrush(zone.Content.NpcColor),
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    RadiusX = 3,
                    RadiusY = 3,
                };

                var npcLabel = new TextBlock
                {
                    Text = zone.Content.NpcName,
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                    Width = 90,
                    TextAlignment = TextAlignment.Center,
                };

                GameCanvas.Children.Add(building);
                GameCanvas.Children.Add(buildingLabel);
                GameCanvas.Children.Add(npc);
                GameCanvas.Children.Add(npcLabel);

                Panel.SetZIndex(building, 3);
                Panel.SetZIndex(buildingLabel, 4);
                Panel.SetZIndex(npc, 6);
                Panel.SetZIndex(npcLabel, 7);

                double floorLine = groundY + playerHeight;
                double buildingX = zone.X;
                double buildingY = floorLine - building.Height;
                double npcX = zone.X + 36;
                double npcY = floorLine - npc.Height;

                Canvas.SetLeft(building, buildingX);
                Canvas.SetTop(building, buildingY);
                Canvas.SetLeft(buildingLabel, buildingX - 7);
                Canvas.SetTop(buildingLabel, buildingY - 18);
                Canvas.SetLeft(npc, npcX);
                Canvas.SetTop(npc, npcY);
                Canvas.SetLeft(npcLabel, npcX - 37);
                Canvas.SetTop(npcLabel, npcY - 16);

                activeZoneVisuals.Add(new SpawnedZoneVisual
                {
                    Zone = zone,
                    Building = building,
                    BuildingLabel = buildingLabel,
                    Npc = npc,
                    NpcLabel = npcLabel
                });
            }
        }

        private void ClearAreaZoneVisuals()
        {
            foreach (var visual in activeZoneVisuals)
            {
                GameCanvas.Children.Remove(visual.Building);
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
                    visual.Npc.Stroke = Brushes.Gold;
                    visual.Npc.StrokeThickness = 2;
                    visual.NpcLabel.Text = $"{visual.Zone.Content!.NpcName} [Z]";
                }
                else
                {
                    visual.Npc.Stroke = Brushes.Black;
                    visual.Npc.StrokeThickness = 1;
                    visual.NpcLabel.Text = visual.Zone.Content!.NpcName;
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
                var body = new Rectangle
                {
                    Width = def.Width,
                    Height = def.Height,
                    Fill = new SolidColorBrush(def.Color),
                    Stroke = Brushes.Black,
                    StrokeThickness = 1,
                    RadiusX = 4,
                    RadiusY = 4,
                };

                var label = new TextBlock
                {
                    Text = def.Name,
                    Foreground = Brushes.White,
                    FontSize = 10,
                    FontFamily = new FontFamily("Consolas"),
                    Width = 70,
                    TextAlignment = TextAlignment.Center
                };

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

                GameCanvas.Children.Add(body);
                GameCanvas.Children.Add(label);
                GameCanvas.Children.Add(healthBg);
                GameCanvas.Children.Add(healthFill);

                Panel.SetZIndex(body, 12);
                Panel.SetZIndex(label, 13);
                Panel.SetZIndex(healthBg, 14);
                Panel.SetZIndex(healthFill, 15);

                double floorLine = groundY + playerHeight;

                activeEnemies.Add(new SpawnedEnemy
                {
                    Definition = def,
                    Body = body,
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

                if (distance <= enemy.Definition.AggroRange)
                {
                    // Chase the player
                    enemy.Direction = playerCenterX >= enemyCenterX ? 1 : -1;
                }
                else
                {
                    // Patrol — reverse at bounds
                    if (enemy.X <= enemy.LeftBound) enemy.Direction = 1;
                    else if (enemy.X >= enemy.RightBound) enemy.Direction = -1;
                }

                enemy.X += enemy.Speed * enemy.Direction;
                enemy.X = Math.Max(0, Math.Min(Width - enemy.Body.Width, enemy.X));
                enemy.Y = groundY + playerHeight - enemy.Body.Height;
            }
        }

        private void DrawEnemies()
        {
            foreach (var enemy in activeEnemies)
            {
                if (!enemy.IsAlive) continue;

                Canvas.SetLeft(enemy.Body, enemy.X);
                Canvas.SetTop(enemy.Body, enemy.Y);
                Canvas.SetLeft(enemy.Label, enemy.X - 26);
                Canvas.SetTop(enemy.Label, enemy.Y - 14);

                double hpRatio = enemy.Definition.MaxHealth > 0
                    ? (double)enemy.CurrentHealth / enemy.Definition.MaxHealth : 0;

                enemy.HealthFill.Width = 28 * Math.Max(0, hpRatio);
                Canvas.SetLeft(enemy.HealthBg, enemy.X - 5);
                Canvas.SetTop(enemy.HealthBg, enemy.Y - 22);
                Canvas.SetLeft(enemy.HealthFill, enemy.X - 5);
                Canvas.SetTop(enemy.HealthFill, enemy.Y - 22);
            }
        }

        private void HandleEnemyContactWithPlayer()
        {
            if (playerDamageCooldownFrames > 0) return;

            Rect playerRect = new Rect(playerX, playerY, playerWidth, playerHeight);

            foreach (var enemy in activeEnemies)
            {
                if (!enemy.IsAlive) continue;

                Rect enemyRect = new Rect(enemy.X, enemy.Y, enemy.Body.Width, enemy.Body.Height);
                if (!playerRect.IntersectsWith(enemyRect)) continue;

                playerData.Health = Math.Max(0, playerData.Health - enemy.Definition.ContactDamage);
                playerDamageCooldownFrames = PlayerDamageCooldownMax;
                ShowStatus($"Hit by {enemy.Definition.Name}!", 35);
                break;
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
            playerData.Gold += goldDrop;
            GainExperience(enemy.Definition.XpReward);

            GameCanvas.Children.Remove(enemy.Body);
            GameCanvas.Children.Remove(enemy.Label);
            GameCanvas.Children.Remove(enemy.HealthBg);
            GameCanvas.Children.Remove(enemy.HealthFill);
            activeEnemies.Remove(enemy);

            ShowStatus($"+{enemy.Definition.XpReward} XP, +{goldDrop}g", 60);
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

        // -----------------------------------------------------------------------
        // Combat — melee
        // -----------------------------------------------------------------------
        private void StartMeleeAttack()
        {
            isAttacking = true;
            attackFramesRemaining = attackDurationFrames;
            attackHitbox.Visibility = Visibility.Visible;
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
                attackHitbox.Visibility = Visibility.Hidden;
                return;
            }

            attackFramesRemaining--;
            if (attackFramesRemaining <= 0)
            {
                isAttacking = false;
                attackHitbox.Visibility = Visibility.Hidden;
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

            var body = new Rectangle
            {
                Width = 12,
                Height = 3,
                Fill = Brushes.SandyBrown,
                RadiusX = 1,
                RadiusY = 1
            };

            GameCanvas.Children.Add(body);
            Panel.SetZIndex(body, 22);

            double startX = facingRight ? playerX + playerWidth + 2 : playerX - 12;
            double startY = playerY + 12;

            activeProjectiles.Add(new ArrowProjectile
            {
                Body = body,
                X = startX,
                Y = startY,
                Direction = facingRight ? 1 : -1,
                Speed = 8.5,
                MaxDistance = 280,
                DistanceTraveled = 0,
                Damage = damage,
                IsAlive = true
            });
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

                Rect arrowRect = new Rect(arrow.X, arrow.Y, arrow.Body.Width, arrow.Body.Height);

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
            }
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
            new WeaponItem { Name = w.Name, WeaponCategory = w.WeaponCategory, Damage = w.Damage, BasePrice = w.BasePrice };

        // -----------------------------------------------------------------------
        // Drawing helpers
        // -----------------------------------------------------------------------
        private void DrawPlayer()
        {
            animationFrameCounter++;

            if (isAttacking)
            {
                player.Source = playerAttackSprite;
            }
            else if (!isOnGround)
            {
                player.Source = playerIdleSprite;
            }
            else if (Math.Abs(velocityX) > 0.1)
            {
                player.Source = ((animationFrameCounter / 8) % 2 == 0)
                    ? playerWalk1Sprite
                    : playerWalk2Sprite;
            }
            else
            {
                player.Source = playerIdleSprite;
            }

            player.RenderTransformOrigin = new Point(0.5, 0.5);
            player.RenderTransform = new ScaleTransform(facingRight ? 1 : -1, 1);

            Canvas.SetLeft(player, playerX);
            Canvas.SetTop(player, playerY);
        }

        private void DrawAttackHitbox()
        {
            double overlapIntoPlayer = 16;
            double forwardReach = 8;

            double hitboxX = facingRight
                ? playerX + playerWidth - overlapIntoPlayer
                : playerX - attackHitbox.Width + overlapIntoPlayer;

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