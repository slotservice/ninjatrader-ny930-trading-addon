// ============================================================
//  NY930AddOn — root NTAddOn entry point (v1.4 POC layout)
// ------------------------------------------------------------
//  v1.4 changes Option C from the client conversation:
//
//    * No more column injection into the chart's main grid.
//      That approach was squeezing Chart Trader and the client
//      reported the layout looked broken.
//    * Instead, every chart window gets a small NY930 toggle
//      button overlaid in the top-right corner of its canvas.
//    * Clicking the toggle opens a floating NTWindow snapped
//      to the chart's right edge, hosting the existing
//      NY930ShellView. Clicking again closes it. The chart
//      and Chart Trader keep their full original layout.
//    * The Control Center → New → NY930 menu still opens a
//      standalone window for users who never use the chart
//      toggle.
//
//  This POC keeps the UI shell and bridge intact — only the
//  hosting/anchoring strategy changed.
// ============================================================

#region Using declarations
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public class NY930AddOn : NinjaTrader.NinjaScript.AddOnBase
    {
        // Standalone window opened from the Control Center menu.
        private static NTWindow      _shellWindow;
        private static NY930ShellView _shellView;

        // Per-chart toggle button (a tiny borderless window that
        // floats in the chart's top-right corner) + per-chart
        // floating shell panel.
        //
        // Why a window instead of a Popup or grid injection:
        //   - Popup needs the chart to have a valid layout at
        //     IsOpen=true time. In NT 8.1.6 OnWindowCreated fires
        //     before the chart is rendered, so the popup positions
        //     itself off-screen.
        //   - Grid injection placed the button under the chrome
        //     because the largest grid is the outer one.
        //   - A tiny owned Window is rock-solid: explicit screen
        //     coordinates, follows the chart on move/resize,
        //     always visible.
        private static readonly Dictionary<Window, Window>   _chartToggles
            = new Dictionary<Window, Window>();
        private static readonly Dictionary<Window, NTWindow> _chartPanels
            = new Dictionary<Window, NTWindow>();

        // Control Center menu item refs.
        private NTMenuItem _addOnMenuItem;
        private NTMenuItem _newMenuRoot;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "NY930 — unified Open Range + Hedge control plane (v1.4 POC).";
                Name        = "NY930";
            }
            else if (State == State.Active)
            {
                NY930Settings.EnsureLoaded();
                NY930Log.PrintSink = msg => System.Diagnostics.Debug.WriteLine(msg);
                NY930Log.Info("AddOn", "NY930 AddOn active.");
            }
            else if (State == State.Terminated)
            {
                NY930Log.Info("AddOn", "NY930 AddOn terminated.");
            }
        }

        // ── Window lifecycle ─────────────────────────────────────
        // ChartWindow is not directly resolvable in NT 8.1.6.x
        // NinjaScript references, so we identify chart windows by
        // their concrete type name string instead of a typed cast.
        // We also accept any window whose chrome contains the word
        // "Chart" in its type name as a fallback in case NT renamed
        // the type in some build.
        private static bool IsChartWindow(Window w)
        {
            if (w == null) return false;
            string tn = w.GetType().Name;
            return tn == "ChartWindow"
                || tn.IndexOf("Chart", StringComparison.OrdinalIgnoreCase) >= 0
                   && !tn.Equals("ControlCenter", StringComparison.Ordinal);
        }

        protected override void OnWindowCreated(Window window)
        {
            // Diagnostic: log every window that comes through so we
            // can see exactly what NT is creating.
            try { NY930Log.Info("AddOn", "OnWindowCreated: " + (window == null ? "null" : window.GetType().FullName)); }
            catch { }

            ControlCenter cc = window as ControlCenter;
            if (cc != null) { AttachControlCenterMenu(cc); return; }

            if (IsChartWindow(window))
            {
                if (window.IsLoaded) AttachChartToggle(window);
                else                  window.Loaded += OnChartLoaded;
            }
        }

        protected override void OnWindowDestroyed(Window window)
        {
            try
            {
                ControlCenter cc = window as ControlCenter;
                if (cc != null && _addOnMenuItem != null && _newMenuRoot != null)
                {
                    if (_newMenuRoot.Items.Contains(_addOnMenuItem))
                        _newMenuRoot.Items.Remove(_addOnMenuItem);
                    _addOnMenuItem = null;
                    _newMenuRoot   = null;
                }

                if (IsChartWindow(window))
                {
                    NTWindow panel;
                    if (_chartPanels.TryGetValue(window, out panel) && panel != null)
                    {
                        try { panel.Close(); } catch { }
                    }
                    Window toggleWin;
                    if (_chartToggles.TryGetValue(window, out toggleWin) && toggleWin != null)
                    {
                        try { toggleWin.Close(); } catch { }
                    }
                    _chartPanels.Remove(window);
                    _chartToggles.Remove(window);
                }
            }
            catch { /* don't crash on shutdown */ }
        }

        // ── Control Center menu ──────────────────────────────────
        private void AttachControlCenterMenu(ControlCenter cc)
        {
            try
            {
                _newMenuRoot = cc.FindFirst("ControlCenterMenuItemNew") as NTMenuItem;
                if (_newMenuRoot == null) { NY930Log.Warn("AddOn", "Control Center 'New' menu not found."); return; }

                _addOnMenuItem = new NTMenuItem
                {
                    Header = "NY930",
                    Style  = Application.Current.TryFindResource("MainMenuItem") as Style
                };
                _addOnMenuItem.Click += (s, e) => OpenStandaloneShell();
                _newMenuRoot.Items.Add(_addOnMenuItem);
                NY930Log.Info("AddOn", "NY930 menu item added to Control Center.");
            }
            catch (Exception ex) { NY930Log.Warn("AddOn", "AttachControlCenterMenu failed: " + ex.Message); }
        }

        // ── Standalone (menu-launched) shell window ─────────────
        public static void OpenStandaloneShell()
        {
            try
            {
                if (_shellWindow != null) { _shellWindow.Activate(); return; }

                _shellWindow = new NTWindow
                {
                    Title      = "NY930",
                    Width      = 360,
                    Height     = 720,
                    Background = NY930Theme.BgBrush
                };
                _shellView = new NY930ShellView();
                _shellWindow.Content = _shellView;
                _shellWindow.Closed += (s, e) =>
                {
                    if (_shellView != null) _shellView.Dispose();
                    _shellView = null; _shellWindow = null;
                };
                _shellWindow.Show();
                _shellWindow.Activate();
            }
            catch (Exception ex) { NY930Log.Error("AddOn", "OpenStandaloneShell error: " + ex.Message); }
        }

        // ── Chart toggle button + floating panel ────────────────

        private void OnChartLoaded(object sender, RoutedEventArgs e)
        {
            Window ch = sender as Window;
            if (ch == null) return;
            ch.Loaded -= OnChartLoaded;
            AttachChartToggle(ch);
        }

        private void AttachChartToggle(Window ch)
        {
            // Defer the whole creation to after layout finishes.
            // OnWindowCreated may fire while the chart is still
            // measuring; PointToScreen / ActualWidth aren't reliable
            // until then. DispatcherPriority.Background runs after
            // the current layout pass.
            Action work = () =>
            {
                try
                {
                    if (_chartToggles.ContainsKey(ch)) return;

                    // Chip lives in a 140×46 transparent window. The
                    // visible badge inside is ~120×34, leaving glow
                    // room for the hover drop-shadow without clipping.
                    // Position: nudged ~210px in from the right edge
                    // so we clear the price-axis gutter (~60-80px) and
                    // the chart's window-chrome buttons. Y=46 puts the
                    // chip just under the chart title bar.
                    const double TogWinW = 140;
                    const double TogWinH = 46;
                    const double TogOffX = 210; // px from chart right edge
                    const double TogOffY = 46;  // px from chart top edge

                    Point screenPt = new Point(100, 100);
                    try
                    {
                        if (ch.IsLoaded && ch.ActualWidth > 0)
                            screenPt = ch.PointToScreen(new Point(ch.ActualWidth - TogOffX, TogOffY));
                        else if (!double.IsNaN(ch.Left))
                            screenPt = new Point(ch.Left + Math.Max(220, ch.Width) - TogOffX, ch.Top + TogOffY);
                    }
                    catch { /* keep default */ }

                    var btn = BuildToggleButton();
                    btn.Click += (s, e) => ToggleChartPanel(ch);

                    var btnWin = new Window
                    {
                        Width                 = TogWinW,
                        Height                = TogWinH,
                        WindowStyle           = WindowStyle.None,
                        ResizeMode            = ResizeMode.NoResize,
                        AllowsTransparency    = true,
                        Background            = Brushes.Transparent,
                        ShowInTaskbar         = false,
                        Topmost               = true,    // stay above chart chrome
                        ShowActivated         = false,
                        Owner                 = ch,
                        Content               = btn,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        Left                  = screenPt.X,
                        Top                   = screenPt.Y
                    };
                    btnWin.Show();
                    NY930Log.Info("AddOn", "NY930 toggle window shown at "
                        + screenPt.X.ToString("F0") + "," + screenPt.Y.ToString("F0"));

                    // Reposition on chart move/resize.
                    Action reposition = () =>
                    {
                        try
                        {
                            if (!ch.IsLoaded || ch.ActualWidth <= 0) return;
                            Point pt = ch.PointToScreen(new Point(ch.ActualWidth - TogOffX, TogOffY));
                            btnWin.Left = pt.X;
                            btnWin.Top  = pt.Y;
                        }
                        catch { }
                    };
                    ch.LocationChanged += (s, e) => reposition();
                    ch.SizeChanged     += (s, e) => reposition();
                    ch.IsVisibleChanged += (s, e) =>
                    {
                        btnWin.Visibility = ch.IsVisible ? Visibility.Visible : Visibility.Collapsed;
                        if (ch.IsVisible) reposition();
                    };

                    _chartToggles[ch] = btnWin;
                    _chartPanels[ch]  = null;
                    NY930Log.Info("AddOn", "NY930 toggle attached to chart.");
                }
                catch (Exception ex)
                {
                    NY930Log.Warn("AddOn", "AttachChartToggle failed: " + ex.Message);
                }
            };

            // If the chart is already loaded, defer to Background
            // priority so layout finishes first; otherwise wait for
            // Loaded then defer the same way.
            if (ch.IsLoaded)
            {
                ch.Dispatcher.BeginInvoke(work,
                    System.Windows.Threading.DispatcherPriority.Background);
            }
            else
            {
                RoutedEventHandler handler = null;
                handler = (s, e) =>
                {
                    ch.Loaded -= handler;
                    ch.Dispatcher.BeginInvoke(work,
                        System.Windows.Threading.DispatcherPriority.Background);
                };
                ch.Loaded += handler;
            }
        }

        // The branded badge that floats over the chart's top-right
        // corner and shows / hides the NY930 panel. Designed to read
        // as a product chip, not a debug button:
        //   • NY (silver gradient) + 930 (gold gradient) wordmark
        //     same stops as the home logo
        //   • Subtle dark vertical sheen background
        //   • Diagonal gold gradient border, gently rounded corners
        //   • Soft ambient gold glow at rest, brighter on hover
        //   • Tiny "MENU" caption + chevron so the user can tell at
        //     a glance that it's a clickable toggle
        private static Button BuildToggleButton()
        {
            // ── Brushes (frozen) ────────────────────────────────
            var silver = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Colors.White,                 0.10),
                    new GradientStop(Color.FromRgb(216, 226, 239), 0.45),
                    new GradientStop(Color.FromRgb(176, 187, 204), 1.00),
                },
                new Point(0, 0), new Point(0, 1));
            silver.Freeze();

            var gold = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(245, 210, 114), 0.0),
                    new GradientStop(NY930Theme.GoldLight,         0.35),
                    new GradientStop(NY930Theme.Gold,              0.65),
                    new GradientStop(NY930Theme.GoldDark,          1.0),
                },
                new Point(0, 0), new Point(0, 1));
            gold.Freeze();

            var bgGrad = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromRgb(0x2c, 0x2c, 0x2c), 0.0),
                    new GradientStop(Color.FromRgb(0x16, 0x16, 0x16), 0.55),
                    new GradientStop(Color.FromRgb(0x08, 0x08, 0x08), 1.0),
                },
                new Point(0, 0), new Point(0, 1));
            bgGrad.Freeze();

            var borderRest = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(NY930Theme.GoldLight, 0.0),
                    new GradientStop(NY930Theme.Gold,      0.5),
                    new GradientStop(NY930Theme.GoldDark,  1.0),
                },
                new Point(0, 0), new Point(1, 1));
            borderRest.Freeze();

            var borderHover = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Colors.White,         0.0),
                    new GradientStop(NY930Theme.GoldLight, 0.5),
                    new GradientStop(NY930Theme.Gold,      1.0),
                },
                new Point(0, 0), new Point(1, 1));
            borderHover.Freeze();

            // ── Wordmark (NY silver + 930 gold) ─────────────────
            var wordmark = new TextBlock
            {
                FontFamily          = NY930Theme.MonoFont,
                FontSize            = 17,
                FontWeight          = FontWeights.Black,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                SnapsToDevicePixels = true,
                LineHeight          = 17,
                Margin              = new Thickness(0, 1, 0, 0)
            };
            wordmark.Inlines.Add(new Run("NY")  { Foreground = silver });
            wordmark.Inlines.Add(new Run("930") { Foreground = gold });

            // Tiny chevron to hint at "click to expand panel".
            var chevron = new TextBlock
            {
                Text                = "›",
                FontFamily          = NY930Theme.SansFont,
                FontSize            = 14,
                FontWeight          = FontWeights.Bold,
                Foreground          = NY930Theme.GoldBrush,
                VerticalAlignment   = VerticalAlignment.Center,
                Margin              = new Thickness(2, 0, 6, 1)
            };

            // Inner row: wordmark on the left, chevron on the right.
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(wordmark, 0);
            Grid.SetColumn(chevron,  1);
            row.Children.Add(wordmark);
            row.Children.Add(chevron);

            // Hairline gold accent under the wordmark so the chip
            // reads as a brand badge rather than a plain button.
            var accent = new Border
            {
                Height          = 1,
                Background      = gold,
                Margin          = new Thickness(10, 0, 10, 4),
                Opacity         = 0.55,
                VerticalAlignment = VerticalAlignment.Bottom
            };

            var content = new Grid();
            content.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(row,    0);
            Grid.SetRow(accent, 1);
            content.Children.Add(row);
            content.Children.Add(accent);

            // Resting glow — visible at all times so the chip pops
            // off the chart canvas.
            var restGlow = new DropShadowEffect
            {
                Color       = NY930Theme.Gold,
                BlurRadius  = 7,
                ShadowDepth = 0,
                Opacity     = 0.35
            };
            restGlow.Freeze();

            var hoverGlow = new DropShadowEffect
            {
                Color       = NY930Theme.GoldLight,
                BlurRadius  = 12,
                ShadowDepth = 0,
                Opacity     = 0.75
            };
            hoverGlow.Freeze();

            var chip = new Border
            {
                CornerRadius        = new CornerRadius(5),
                Background          = bgGrad,
                BorderBrush         = borderRest,
                BorderThickness     = new Thickness(1),
                SnapsToDevicePixels = true,
                Child               = content,
                Effect              = restGlow
            };

            // Strip the default WPF Button chrome so only the chip
            // visual is rendered (no blue hover overlay etc.).
            var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
            presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Stretch);
            presenter.SetValue(ContentPresenter.VerticalAlignmentProperty,   VerticalAlignment.Stretch);
            var template = new ControlTemplate(typeof(Button)) { VisualTree = presenter };

            var btn = new Button
            {
                Width               = 120,
                Height              = 34,
                Background          = Brushes.Transparent,
                BorderBrush         = Brushes.Transparent,
                BorderThickness     = new Thickness(0),
                Padding             = new Thickness(0),
                Margin              = new Thickness(0, 6, 10, 6),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment   = VerticalAlignment.Center,
                Cursor              = System.Windows.Input.Cursors.Hand,
                FocusVisualStyle    = null,
                ToolTip             = "Mostrar / ocultar NY930",
                Content             = chip,
                Template            = template
            };

            // Hover: brighter border + bigger glow.
            btn.MouseEnter += (s, e) =>
            {
                chip.BorderBrush = borderHover;
                chip.Effect      = hoverGlow;
                accent.Opacity   = 0.95;
            };
            btn.MouseLeave += (s, e) =>
            {
                chip.BorderBrush = borderRest;
                chip.Effect      = restGlow;
                accent.Opacity   = 0.55;
            };

            return btn;
        }

        private void ToggleChartPanel(Window ch)
        {
            try
            {
                NTWindow existing;
                if (_chartPanels.TryGetValue(ch, out existing) && existing != null)
                {
                    existing.Close();
                    _chartPanels[ch] = null;
                    return;
                }

                // Create floating panel snapped to the chart's right edge.
                var win = new NTWindow
                {
                    Title         = "NY930",
                    Width         = 290,
                    Height        = Math.Max(520, ch.ActualHeight - 80),
                    Background    = NY930Theme.BgBrush,
                    Owner         = ch,
                    ShowInTaskbar = false
                };
                win.Content = new NY930ShellView();
                win.Closed += (s, e) =>
                {
                    var content = win.Content as IDisposable;
                    if (content != null) content.Dispose();
                    if (_chartPanels.ContainsKey(ch)) _chartPanels[ch] = null;
                };

                // Snap to the right edge of the chart, top-aligned.
                try
                {
                    Point screenTopRight = ch.PointToScreen(new Point(ch.ActualWidth, 0));
                    win.WindowStartupLocation = WindowStartupLocation.Manual;
                    win.Left = screenTopRight.X + 6;
                    win.Top  = screenTopRight.Y + 40;
                }
                catch
                {
                    win.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                }

                win.Show();
                _chartPanels[ch] = win;
            }
            catch (Exception ex)
            {
                NY930Log.Error("AddOn", "ToggleChartPanel error: " + ex.Message);
            }
        }

        // Walk the chart's visual tree, return the largest Grid
        // we find. Used as the host for the floating toggle button.
        private static Grid FindMainChartGrid(DependencyObject root)
        {
            if (root == null) return null;
            Grid best = null;
            int bestArea = 0;

            var queue = new Queue<DependencyObject>();
            queue.Enqueue(root);
            int budget = 400;

            while (queue.Count > 0 && budget-- > 0)
            {
                var node = queue.Dequeue();
                Grid g = node as Grid;
                if (g != null)
                {
                    int area = g.ColumnDefinitions.Count * Math.Max(1, g.RowDefinitions.Count);
                    if (area > bestArea) { bestArea = area; best = g; }
                }
                int n = VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < n; i++)
                    queue.Enqueue(VisualTreeHelper.GetChild(node, i));
            }
            return best;
        }
    }
}
