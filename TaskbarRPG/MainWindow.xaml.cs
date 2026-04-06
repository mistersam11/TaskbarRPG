using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace TaskbarRPG
{
    // ════════════════════════════════════════════════════════════════════════════
    // AREA SYSTEM
    // To add a new area:
    //   1. Add a value to AreaType
    //   2. Add a new Area entry in AreaDefinitions.All
    //   3. Set LeftExit / RightExit on neighboring areas to connect them
    // That's it — the rest is automatic.
    //
    // Fast travel menu is dynamic:
    //   - F7 opens/closes the menu
    //   - Number keys 1..9 travel to the corresponding area in the list
    // ════════════════════════════════════════════════════════════════════════════

    public enum AreaType
    {
        Town,
        Plains,
        Cave,
        // Add new area types here ↓
    }

    public class Area
    {
        public AreaType Type { get; set; }
        public string Name { get; set; } = "";
        public Color GroundColor { get; set; }
        public AreaType? LeftExit { get; set; }
        public AreaType? RightExit { get; set; }
    }

    public static class AreaDefinitions
    {
        // Keep this list in the order you want it to appear in the fast-travel menu.
        public static readonly List<Area> Ordered = new()
        {
            new Area
            {
                Type = AreaType.Town,
                Name = "Town",
                GroundColor = Color.FromRgb(194, 154, 108),   // light brown dirt road
                LeftExit = null,
                RightExit = AreaType.Plains,
            },

            new Area
            {
                Type = AreaType.Plains,
                Name = "Plains",
                GroundColor = Color.FromRgb(90, 170, 80),     // grass green
                LeftExit = AreaType.Town,
                RightExit = AreaType.Cave,
            },

            new Area
            {
                Type = AreaType.Cave,
                Name = "Cave",
                GroundColor = Color.FromRgb(95, 95, 105),     // cave stone
                LeftExit = AreaType.Plains,
                RightExit = null,
            },
        };

        public static readonly Dictionary<AreaType, Area> All =
            Ordered.ToDictionary(a => a.Type, a => a);

        public static Area Get(AreaType type) => All[type];
    }

    // ════════════════════════════════════════════════════════════════════════════
    // TRANSITION SYSTEM
    // Handles the black fade between areas.
    // ════════════════════════════════════════════════════════════════════════════

    public enum TransitionDirection { Left, Right }

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

    // ════════════════════════════════════════════════════════════════════════════
    // MAIN WINDOW
    // ════════════════════════════════════════════════════════════════════════════

    public partial class MainWindow : Window
    {
        // ── Win32 ────────────────────────────────────────────────────────────────
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x80;

        private const int VK_LEFT = 0x25;
        private const int VK_UP = 0x26;
        private const int VK_RIGHT = 0x27;
        private const int VK_SPACE = 0x20;
        private const int VK_A = 0x41;
        private const int VK_D = 0x44;
        private const int VK_W = 0x57;
        private const int VK_Z = 0x5A;
        private const int VK_F7 = 0x76;
        private const int VK_F8 = 0x77;
        private const int VK_1 = 0x31;
        private const int VK_2 = 0x32;
        private const int VK_3 = 0x33;
        private const int VK_4 = 0x34;
        private const int VK_5 = 0x35;
        private const int VK_6 = 0x36;
        private const int VK_7 = 0x37;
        private const int VK_8 = 0x38;
        private const int VK_9 = 0x39;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("shell32.dll")]
        private static extern int SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

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

        // ── Game objects ─────────────────────────────────────────────────────────
        private DispatcherTimer timer = null!;
        private Rectangle player = null!;
        private Rectangle attackHitbox = null!;
        private Rectangle groundRect = null!;

        // ── Fast travel menu visuals ─────────────────────────────────────────────
        private Border fastTravelPanel = null!;
        private TextBlock fastTravelText = null!;

        // ── Area state ───────────────────────────────────────────────────────────
        private Area currentArea = null!;
        private AreaTransition transition = null!;

        // ── Physics ──────────────────────────────────────────────────────────────
        private double playAreaHeight = 140;
        private double playerX = 100;
        private double playerY = 0;
        private double playerWidth = 22;
        private double playerHeight = 30;
        private double velocityX = 0;
        private double velocityY = 0;
        private double moveSpeed = 3.0;
        private double gravity = 0.45;
        private double jumpStrength = -8.5;
        private double groundY = 0;
        private bool isOnGround = false;
        private bool facingRight = true;
        private double groundStripHeight = 14;

        // ── Input state ──────────────────────────────────────────────────────────
        private bool jumpHeldLastFrame = false;
        private bool attackHeldLastFrame = false;
        private bool toggleHeldLastFrame = false;
        private bool fastTravelHeldLastFrame = false;
        private bool controlsEnabled = false;
        private bool fastTravelOpen = false;
        private readonly bool[] numberHeldLastFrame = new bool[9];

        // ── Attack state ─────────────────────────────────────────────────────────
        private bool isAttacking = false;
        private int attackFramesRemaining = 0;
        private int attackDurationFrames = 10;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // For true transparency, your XAML window should also use:
            // AllowsTransparency="True"
            // Background="Transparent"

            PositionAboveTaskbar();
            MakeClickThrough();
            CreateBackground();
            CreateFastTravelMenu();
            CreatePlayer();
            SetupTransition();

            // Start in town
            LoadArea(AreaType.Town, TransitionDirection.Right, animate: false);

            StartGameLoop();
        }

        // ── Area loading ─────────────────────────────────────────────────────────

        private void SetupTransition()
        {
            transition = new AreaTransition(GameCanvas, OnTransitionMidpoint);
        }

        private void LoadArea(AreaType type, TransitionDirection entryDir, bool animate = true)
        {
            if (animate)
            {
                transition.Start(type, entryDir);
            }
            else
            {
                ApplyArea(type, entryDir);

                if (entryDir == TransitionDirection.Right)
                    playerX = 10;
                else
                    playerX = Width - playerWidth - 10;

                velocityX = 0;
                DrawPlayer();
                DrawAttackHitbox();
            }
        }

        private void OnTransitionMidpoint(AreaType type, TransitionDirection dir)
        {
            ApplyArea(type, dir);

            if (dir == TransitionDirection.Right)
                playerX = 10;
            else
                playerX = Width - playerWidth - 10;

            velocityX = 0;
        }

        private void ApplyArea(AreaType type, TransitionDirection dir)
        {
            currentArea = AreaDefinitions.Get(type);
            groundRect.Fill = new SolidColorBrush(currentArea.GroundColor);
            UpdateFastTravelMenuText();
        }

        // ── Window setup ─────────────────────────────────────────────────────────

        private void PositionAboveTaskbar()
        {
            APPBARDATA data = new APPBARDATA();
            data.cbSize = Marshal.SizeOf(typeof(APPBARDATA));
            int result = SHAppBarMessage(ABM_GETTASKBARPOS, ref data);

            if (result != 0)
            {
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;

                int left = data.rc.left, top = data.rc.top,
                    right = data.rc.right, bottom = data.rc.bottom;

                if (top > 0 && bottom == screenHeight)          // bottom taskbar
                {
                    Left = 0;
                    Top = top - playAreaHeight;
                    Width = screenWidth;
                    Height = playAreaHeight;
                }
                else if (top == 0 && bottom < screenHeight)     // top taskbar
                {
                    Left = 0;
                    Top = bottom;
                    Width = screenWidth;
                    Height = playAreaHeight;
                }
                else if (left == 0 && right < screenWidth)      // left taskbar
                {
                    Left = right;
                    Top = 0;
                    Width = playAreaHeight;
                    Height = screenHeight;
                }
                else                                             // right taskbar
                {
                    Left = left - playAreaHeight;
                    Top = 0;
                    Width = playAreaHeight;
                    Height = screenHeight;
                }
            }
            else
            {
                Left = 0;
                Top = SystemParameters.PrimaryScreenHeight - 80 - playAreaHeight;
                Width = SystemParameters.PrimaryScreenWidth;
                Height = playAreaHeight;
            }
        }

        private void MakeClickThrough()
        {
            IntPtr hwnd = new WindowInteropHelper(this).Handle;
            int extStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
        }

        // ── Visual setup ─────────────────────────────────────────────────────────

        private void CreateBackground()
        {
            // No background sky rectangle at all:
            // everything stays transparent except the ground strip.
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

        private void CreateFastTravelMenu()
        {
            fastTravelText = new TextBlock
            {
                Foreground = Brushes.White,
                FontSize = 14,
                FontFamily = new FontFamily("Consolas"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(10),
            };

            fastTravelPanel = new Border
            {
                Width = 280,
                Background = new SolidColorBrush(Color.FromArgb(220, 20, 20, 20)),
                BorderBrush = Brushes.White,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = fastTravelText,
                Visibility = Visibility.Hidden,
            };

            GameCanvas.Children.Add(fastTravelPanel);
            Panel.SetZIndex(fastTravelPanel, 50);

            Canvas.SetLeft(fastTravelPanel, 20);
            Canvas.SetTop(fastTravelPanel, 20);

            UpdateFastTravelMenuText();
        }

        private void UpdateFastTravelMenuText()
        {
            if (fastTravelText == null)
                return;

            var lines = new List<string>
            {
                "FAST TRAVEL",
                "",
                "F7 = open/close",
                "F8 = toggle control",
                "",
            };

            for (int i = 0; i < AreaDefinitions.Ordered.Count && i < 9; i++)
            {
                var area = AreaDefinitions.Ordered[i];
                string currentMarker = area.Type == currentArea?.Type ? "  <==" : "";
                lines.Add($"{i + 1}. {area.Name}{currentMarker}");
            }

            fastTravelText.Text = string.Join(Environment.NewLine, lines);
        }

        private void CreatePlayer()
        {
            player = new Rectangle
            {
                Width = playerWidth,
                Height = playerHeight,
                Fill = Brushes.Red,
                RadiusX = 3,
                RadiusY = 3,
            };

            attackHitbox = new Rectangle
            {
                Width = 18,
                Height = 12,
                Fill = Brushes.OrangeRed,
                Visibility = Visibility.Hidden,
            };

            GameCanvas.Children.Add(player);
            GameCanvas.Children.Add(attackHitbox);

            Panel.SetZIndex(player, 1);
            Panel.SetZIndex(attackHitbox, 1);

            groundY = Height - groundStripHeight - playerHeight;
            playerY = groundY;
            isOnGround = true;

            DrawPlayer();
            DrawAttackHitbox();
        }

        // ── Game loop ────────────────────────────────────────────────────────────

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

            if (!fastTravelOpen)
            {
                ApplyPhysics();
                ResolveCollisions();
                CheckAreaTransition();
                UpdateAttack();
            }

            DrawPlayer();
            DrawAttackHitbox();
        }

        private void RefreshLayoutSizedElements()
        {
            groundRect.Width = Width;
            Canvas.SetTop(groundRect, Height - groundStripHeight);

            groundY = Height - groundStripHeight - playerHeight;

            if (fastTravelPanel != null)
            {
                Canvas.SetLeft(fastTravelPanel, 20);
                Canvas.SetTop(fastTravelPanel, 20);
            }
        }

        // ── Input ────────────────────────────────────────────────────────────────

        private void HandleInput()
        {
            bool togglePressed = IsKeyDown(VK_F8);
            if (togglePressed && !toggleHeldLastFrame)
                controlsEnabled = !controlsEnabled;
            toggleHeldLastFrame = togglePressed;

            bool fastTravelPressed = IsKeyDown(VK_F7);
            if (fastTravelPressed && !fastTravelHeldLastFrame)
            {
                fastTravelOpen = !fastTravelOpen;
                fastTravelPanel.Visibility = fastTravelOpen ? Visibility.Visible : Visibility.Hidden;
                UpdateFastTravelMenuText();
            }
            fastTravelHeldLastFrame = fastTravelPressed;

            if (fastTravelOpen)
            {
                velocityX = 0;
                jumpHeldLastFrame = false;
                attackHeldLastFrame = false;

                HandleFastTravelSelection();
                return;
            }

            if (!controlsEnabled)
            {
                velocityX = 0;
                jumpHeldLastFrame = false;
                attackHeldLastFrame = false;
                return;
            }

            bool left = IsKeyDown(VK_LEFT) || IsKeyDown(VK_A);
            bool right = IsKeyDown(VK_RIGHT) || IsKeyDown(VK_D);
            bool jump = IsKeyDown(VK_SPACE) || IsKeyDown(VK_UP) || IsKeyDown(VK_W);
            bool attack = IsKeyDown(VK_Z);

            velocityX = 0;
            if (left && !right)
            {
                velocityX = -moveSpeed;
                facingRight = false;
            }
            if (right && !left)
            {
                velocityX = moveSpeed;
                facingRight = true;
            }

            if (jump && !jumpHeldLastFrame && isOnGround)
            {
                velocityY = jumpStrength;
                isOnGround = false;
            }

            if (attack && !attackHeldLastFrame && !isAttacking)
                StartAttack();

            jumpHeldLastFrame = jump;
            attackHeldLastFrame = attack;
        }

        private void HandleFastTravelSelection()
        {
            int[] keys = { VK_1, VK_2, VK_3, VK_4, VK_5, VK_6, VK_7, VK_8, VK_9 };

            for (int i = 0; i < keys.Length; i++)
            {
                bool pressed = IsKeyDown(keys[i]);

                if (pressed && !numberHeldLastFrame[i])
                {
                    if (i < AreaDefinitions.Ordered.Count)
                    {
                        var target = AreaDefinitions.Ordered[i].Type;

                        if (target != currentArea.Type)
                        {
                            fastTravelOpen = false;
                            fastTravelPanel.Visibility = Visibility.Hidden;

                            // Pick a sensible direction based on menu order
                            int currentIndex = AreaDefinitions.Ordered.FindIndex(a => a.Type == currentArea.Type);
                            TransitionDirection dir = i >= currentIndex
                                ? TransitionDirection.Right
                                : TransitionDirection.Left;

                            LoadArea(target, dir, animate: true);
                        }
                        else
                        {
                            fastTravelOpen = false;
                            fastTravelPanel.Visibility = Visibility.Hidden;
                        }
                    }
                }

                numberHeldLastFrame[i] = pressed;
            }
        }

        // ── Area transitions ─────────────────────────────────────────────────────

        private void CheckAreaTransition()
        {
            if (playerX > Width - playerWidth && currentArea.RightExit.HasValue)
            {
                LoadArea(currentArea.RightExit.Value, TransitionDirection.Right);
            }
            else if (playerX < 0 && currentArea.LeftExit.HasValue)
            {
                LoadArea(currentArea.LeftExit.Value, TransitionDirection.Left);
            }
        }

        // ── Attack ───────────────────────────────────────────────────────────────

        private void StartAttack()
        {
            isAttacking = true;
            attackFramesRemaining = attackDurationFrames;
            player.Fill = Brushes.Yellow;
            attackHitbox.Visibility = Visibility.Visible;
        }

        private void UpdateAttack()
        {
            if (!isAttacking)
            {
                player.Fill = controlsEnabled ? Brushes.Red : Brushes.DarkGray;
                attackHitbox.Visibility = Visibility.Hidden;
                return;
            }

            attackFramesRemaining--;

            if (attackFramesRemaining <= 0)
            {
                isAttacking = false;
                player.Fill = controlsEnabled ? Brushes.Red : Brushes.DarkGray;
                attackHitbox.Visibility = Visibility.Hidden;
            }
        }

        // ── Physics & collision ──────────────────────────────────────────────────

        private void ApplyPhysics()
        {
            velocityY += gravity;
            playerX += velocityX;
            playerY += velocityY;
        }

        private void ResolveCollisions()
        {
            isOnGround = false;

            // Let the player cross the edge a tiny bit so transitions can trigger naturally
            if (playerX < -2) playerX = -2;
            if (playerX > Width - playerWidth + 2) playerX = Width - playerWidth + 2;

            if (playerY < 0)
            {
                playerY = 0;
                if (velocityY < 0) velocityY = 0;
            }

            if (playerY >= groundY)
            {
                playerY = groundY;
                velocityY = 0;
                isOnGround = true;
            }
        }

        // ── Drawing ──────────────────────────────────────────────────────────────

        private void DrawPlayer()
        {
            Canvas.SetLeft(player, playerX);
            Canvas.SetTop(player, playerY);
        }

        private void DrawAttackHitbox()
        {
            double hitboxX = facingRight
                ? playerX + playerWidth
                : playerX - attackHitbox.Width;

            Canvas.SetLeft(attackHitbox, hitboxX);
            Canvas.SetTop(attackHitbox, playerY + 8);
        }

        private bool IsKeyDown(int keyCode) => (GetAsyncKeyState(keyCode) & 0x8000) != 0;
    }
}