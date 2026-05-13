// ============================================================
//  NY930AddOn — root NTAddOn entry point (v1.5.0)
// ------------------------------------------------------------
//  v1.5.0 — replace floating chip + floating window with a
//  proper chart-toolbar button + column-injected panel.
//
//  Why the change:
//    * The v1.4 floating chip overlapped Chart Trader and could
//      disappear when the NT window was maximized.
//    * The client provided NY930AddOn-toolbar.cs showing the
//      pattern they wanted: a Button added to chart.MainMenu
//      that toggles a side panel inserted as a real Grid column
//      next to MainTabControl, with a GridSplitter for resize.
//    * Result: panel docks inside the chart layout (like Chart
//      Trader does), so maximizing or moving the NT window
//      keeps the panel in place and the panel never overlays
//      anything.
//
//  We still expose NY930 from Control Center → New → NY930 for
//  users who want a standalone floating window.
// ============================================================

#region Using declarations
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public class NY930AddOn : NinjaTrader.NinjaScript.AddOnBase
    {
        // ── Standalone shell (Control Center → New → NY930) ─────
        private static NTWindow       _shellWindow;
        private static NY930ShellView _shellView;

        // ── Per-chart hook state ────────────────────────────────
        // We attach a Button to the chart's MainMenu and, when the
        // user clicks it, inject a Border (hosting NY930ShellView)
        // and a GridSplitter into the chart's main Grid right next
        // to MainTabControl. Clicking again removes them. We hold
        // refs to everything we added so we can clean up correctly
        // on close and on chart-destroyed events.
        private class ChartHook
        {
            public Window       Chart;
            public Button       MenuButton;
            public object       MenuHost;          // whatever container holds MenuButton (Menu, ToolBar, or custom collection)
            public Grid         HostGrid;          // chart's main Grid (parent of MainTabControl)
            public int          MainTabColumn;
            public int          MainTabRow;
            public Border       PanelBorder;       // hosts NY930ShellView when open
            public GridSplitter Splitter;
            public bool         IsOpen;
        }

        private static readonly Dictionary<Window, ChartHook> _hooks
            = new Dictionary<Window, ChartHook>();

        // ── Control Center menu refs ────────────────────────────
        private NTMenuItem _addOnMenuItem;
        private NTMenuItem _newMenuRoot;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "NY930 — unified Open Range + Hedge control plane (v1.5).";
                Name        = "NY930";
            }
            else if (State == State.Active)
            {
                NY930Settings.EnsureLoaded();
                // v1.5.0a: log to NinjaScript Output (Print) so the
                // user can see attach progress, AND mirror to a file
                // so we can diagnose problems that happen before the
                // Output window is open.
                NY930Log.PrintSink = msg =>
                {
                    try { Print(msg); } catch { }
                    AppendToFileLog(msg);
                };
                NY930Log.LogSink = (msg, lvl) =>
                {
                    try { Log(msg, lvl); } catch { }
                };
                NY930Log.Info("AddOn", "NY930 AddOn active (v1.5).");
            }
            else if (State == State.Terminated)
            {
                NY930Log.Info("AddOn", "NY930 AddOn terminated.");
            }
        }

        // ── Window detection ────────────────────────────────────
        // ChartWindow is not directly resolvable in NT 8.1.6.x
        // NinjaScript references, so we identify chart windows by
        // their type-name string and use reflection to read the
        // properties we care about (MainMenu, MainTabControl).
        private static bool IsChartWindow(Window w)
        {
            if (w == null) return false;
            string tn = w.GetType().Name;
            return tn == "ChartWindow"
                || (tn.IndexOf("Chart", StringComparison.OrdinalIgnoreCase) >= 0
                    && !tn.Equals("ControlCenter", StringComparison.Ordinal));
        }

        protected override void OnWindowCreated(Window window)
        {
            try { NY930Log.Info("AddOn", "OnWindowCreated: " + (window == null ? "null" : window.GetType().FullName)); }
            catch { }

            ControlCenter cc = window as ControlCenter;
            if (cc != null) { AttachControlCenterMenu(cc); return; }

            if (IsChartWindow(window))
            {
                if (window.IsLoaded) AttachChartMenu(window);
                else                 window.Loaded += OnChartLoaded;
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
                    ChartHook hook;
                    if (_hooks.TryGetValue(window, out hook) && hook != null)
                    {
                        try { ClosePanel(hook); } catch { }
                        try { RemoveMenuButton(hook); } catch { }
                        _hooks.Remove(window);
                    }
                }
            }
            catch { /* don't crash on shutdown */ }
        }

        // ── Control Center menu (unchanged from v1.4) ───────────
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

        // ── Chart toolbar button ────────────────────────────────
        private void OnChartLoaded(object sender, RoutedEventArgs e)
        {
            Window ch = sender as Window;
            if (ch == null) return;
            ch.Loaded -= OnChartLoaded;
            AttachChartMenu(ch);
        }

        private void AttachChartMenu(Window chart)
        {
            // Defer to Background priority so the chart's chrome
            // (including MainMenu and MainTabControl) is fully
            // realized before we try to touch it.
            Action work = () =>
            {
                try
                {
                    if (_hooks.ContainsKey(chart)) return;

                    // Resolve a host for the button. NT 8 chart windows
                    // expose MainMenu, but the property type / layout
                    // differs between builds — sometimes Menu, sometimes
                    // a custom collection. We try several strategies:
                    //   1. MainMenu reachable & has Items collection
                    //      (Menu / MenuBase / ItemsControl)
                    //   2. MainMenu has a public Add(object) method
                    //      (custom collection, matches client's
                    //      reference NY930AddOn-toolbar.cs)
                    //   3. Fallback: walk the chart's visual tree for
                    //      the first ToolBar or Menu and add to its
                    //      Items.
                    var btn = BuildMenuButton();
                    object hostObj = null;
                    string hostDesc = null;

                    var menu = GetReflectedProperty(chart, "MainMenu");
                    NY930Log.Info("AddOn", "MainMenu reflected = "
                        + (menu == null ? "null" : menu.GetType().FullName));

                    if (menu is ItemsControl ic)
                    {
                        ic.Items.Add(btn);
                        hostObj  = ic;
                        hostDesc = "MainMenu (ItemsControl)";
                    }
                    else if (menu != null && TryInvokeAdd(menu, btn))
                    {
                        hostObj  = menu;
                        hostDesc = "MainMenu (.Add via reflection)";
                    }
                    else
                    {
                        // Fallback: visual tree walk.
                        var hostFound = FindMenuOrToolBar(chart);
                        if (hostFound != null)
                        {
                            hostFound.Items.Add(btn);
                            hostObj  = hostFound;
                            hostDesc = "visual-tree " + hostFound.GetType().Name;
                        }
                    }

                    if (hostObj == null)
                    {
                        NY930Log.Warn("AddOn", "No menu/toolbar host found — toolbar button not attached.");
                        return;
                    }

                    var mainTabControl = GetReflectedProperty(chart, "MainTabControl") as FrameworkElement;
                    Grid hostGrid = mainTabControl != null ? mainTabControl.Parent as Grid : null;
                    if (hostGrid == null)
                    {
                        NY930Log.Warn("AddOn", "Chart MainTabControl parent grid not found — panel insertion will not work.");
                        // We still keep the button — clicking it will log
                        // an error rather than crash.
                    }

                    var hook = new ChartHook
                    {
                        Chart         = chart,
                        MenuButton    = btn,
                        MenuHost      = hostObj,
                        HostGrid      = hostGrid,
                        MainTabColumn = mainTabControl != null ? Grid.GetColumn(mainTabControl) : 0,
                        MainTabRow    = mainTabControl != null ? Grid.GetRow(mainTabControl)    : 0,
                        IsOpen        = false
                    };
                    btn.Click += (s, e) => TogglePanel(hook);

                    _hooks[chart] = hook;
                    NY930Log.Info("AddOn", "NY930 toolbar button attached to " + hostDesc + ".");
                }
                catch (Exception ex)
                {
                    NY930Log.Warn("AddOn", "AttachChartMenu failed: " + ex.Message);
                }
            };

            if (chart.IsLoaded)
                chart.Dispatcher.BeginInvoke(work, System.Windows.Threading.DispatcherPriority.Background);
            else
            {
                RoutedEventHandler handler = null;
                handler = (s, e) =>
                {
                    chart.Loaded -= handler;
                    chart.Dispatcher.BeginInvoke(work, System.Windows.Threading.DispatcherPriority.Background);
                };
                chart.Loaded += handler;
            }
        }

        // ── Reflection helpers ──────────────────────────────────
        private static object GetReflectedProperty(object obj, string name)
        {
            if (obj == null) return null;
            var pi = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return pi != null ? pi.GetValue(obj) : null;
        }

        // Tries to call collection.Add(item) via reflection. Used as
        // a fallback when MainMenu isn't a standard ItemsControl
        // (the client's reference code called chart.MainMenu.Add()
        // directly on what may be a custom collection type).
        private static bool TryInvokeAdd(object collection, object item)
        {
            if (collection == null || item == null) return false;
            try
            {
                var add = collection.GetType().GetMethod("Add",
                    BindingFlags.Public | BindingFlags.Instance,
                    null, new[] { typeof(object) }, null);
                if (add == null)
                    add = collection.GetType().GetMethod("Add",
                        BindingFlags.Public | BindingFlags.Instance,
                        null, new[] { item.GetType() }, null);
                if (add == null) return false;
                add.Invoke(collection, new object[] { item });
                return true;
            }
            catch (Exception ex)
            {
                NY930Log.Warn("AddOn", "TryInvokeAdd failed: " + ex.Message);
                return false;
            }
        }

        // Visual-tree walk: return the first Menu / ToolBar /
        // ItemsControl that looks like the chart's top toolbar.
        // We prefer Menu first, then ToolBar, then any ItemsControl
        // that has at least one Button or MenuItem already in it
        // (so we don't accidentally inject into an unrelated list).
        private static ItemsControl FindMenuOrToolBar(DependencyObject root)
        {
            if (root == null) return null;
            ItemsControl menuMatch = null;
            ItemsControl toolBarMatch = null;
            ItemsControl genericMatch = null;
            var queue = new Queue<DependencyObject>();
            queue.Enqueue(root);
            int budget = 600;
            while (queue.Count > 0 && budget-- > 0)
            {
                var node = queue.Dequeue();
                if (node is Menu m && menuMatch == null) menuMatch = m;
                else if (node is ToolBar tb && toolBarMatch == null) toolBarMatch = tb;
                else if (node is ItemsControl ic && genericMatch == null
                         && ic.Items.Count > 0
                         && ic.GetType().Name.IndexOf("Combo", StringComparison.OrdinalIgnoreCase) < 0
                         && ic.GetType().Name.IndexOf("Tab",   StringComparison.OrdinalIgnoreCase) < 0
                         && ic.GetType().Name.IndexOf("List",  StringComparison.OrdinalIgnoreCase) < 0)
                {
                    genericMatch = ic;
                }
                int n = VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < n; i++)
                    queue.Enqueue(VisualTreeHelper.GetChild(node, i));
            }
            return menuMatch ?? toolBarMatch ?? genericMatch;
        }

        // Append to ${USERPROFILE}\Documents\NinjaTrader 8\log\NY930-AddOn.log
        // so we can read attach progress even if the Output window
        // wasn't open at the time. Best-effort only — never throws.
        private static readonly object _fileLogLock = new object();
        private static void AppendToFileLog(string msg)
        {
            try
            {
                string dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "NinjaTrader 8", "log");
                if (!System.IO.Directory.Exists(dir))
                    System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, "NY930-AddOn.log");
                lock (_fileLogLock)
                {
                    System.IO.File.AppendAllText(path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff ")
                        + msg + Environment.NewLine);
                }
            }
            catch { /* ignore */ }
        }

        // The chart-toolbar button. Visually we let NinjaTrader's
        // own ButtonBackgroundBrush etc resources style it so it
        // blends with the other built-in toolbar buttons (Save
        // Workspace, etc.). The label and tooltip are localized.
        private static Button BuildMenuButton()
        {
            var btn = new Button
            {
                Content    = "NY930",
                ToolTip    = "Mostrar / Ocultar NY930",
                Padding    = new Thickness(8, 2, 8, 2),
                Margin     = new Thickness(2, 0, 2, 0),
                FontSize   = 12,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.GoldBrush,
                Cursor     = System.Windows.Input.Cursors.Hand
            };

            // Pick up NT's standard button chrome if available so
            // the button doesn't look out of place next to the
            // built-in toolbar buttons.
            var bg = Application.Current.TryFindResource("ButtonBackgroundBrush") as Brush;
            var bd = Application.Current.TryFindResource("ButtonBorderBrush")     as Brush;
            if (bg != null) btn.Background      = bg;
            if (bd != null) btn.BorderBrush     = bd;
            btn.BorderThickness = new Thickness(1);

            return btn;
        }

        // ── Toggle panel: open / close ──────────────────────────
        private void TogglePanel(ChartHook hook)
        {
            try
            {
                if (hook.IsOpen) ClosePanel(hook);
                else             OpenPanel(hook);
            }
            catch (Exception ex)
            {
                NY930Log.Error("AddOn", "TogglePanel error: " + ex.Message);
            }
        }

        // Inject a 290px-wide column to the right of MainTabControl
        // and place an NY930ShellView (wrapped in a Border) plus a
        // GridSplitter into it. Existing siblings whose column index
        // is greater than MainTabControl's get shifted by +1 so they
        // stay in their original visual slots.
        private void OpenPanel(ChartHook hook)
        {
            if (hook == null || hook.HostGrid == null || hook.IsOpen) return;

            Grid grid = hook.HostGrid;
            int  col  = hook.MainTabColumn;
            int  row  = hook.MainTabRow;

            grid.ColumnDefinitions.Insert(col + 1, new ColumnDefinition
            {
                Width    = new GridLength(290, GridUnitType.Pixel),
                MinWidth = 240,
                MaxWidth = 520
            });

            // Shift siblings to keep their visual position.
            foreach (UIElement child in grid.Children)
            {
                int c = Grid.GetColumn(child);
                if (c > col) Grid.SetColumn(child, c + 1);
            }

            var border = new Border
            {
                Background      = NY930Theme.BgBrush,
                BorderBrush     = NY930Theme.BorderBrush,
                BorderThickness = new Thickness(1, 0, 0, 0),
                Child           = new NY930ShellView()
            };
            Grid.SetColumn(border, col + 1);
            Grid.SetRow(border, row);
            grid.Children.Add(border);

            var splitter = new GridSplitter
            {
                Width               = 5,
                Background          = NY930Theme.BorderBrush,
                ResizeBehavior      = GridResizeBehavior.PreviousAndCurrent,
                ResizeDirection     = GridResizeDirection.Columns,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment   = VerticalAlignment.Stretch
            };
            Grid.SetColumn(splitter, col + 1);
            Grid.SetRow(splitter, row);
            grid.Children.Add(splitter);

            hook.PanelBorder = border;
            hook.Splitter    = splitter;
            hook.IsOpen      = true;
        }

        private void ClosePanel(ChartHook hook)
        {
            if (hook == null || !hook.IsOpen) return;

            Grid grid     = hook.HostGrid;
            int  panelCol = hook.PanelBorder != null ? Grid.GetColumn(hook.PanelBorder) : hook.MainTabColumn + 1;

            if (hook.PanelBorder != null)
            {
                var disposable = hook.PanelBorder.Child as IDisposable;
                if (disposable != null) { try { disposable.Dispose(); } catch { } }
                grid.Children.Remove(hook.PanelBorder);
            }
            if (hook.Splitter != null) grid.Children.Remove(hook.Splitter);

            if (panelCol >= 0 && panelCol < grid.ColumnDefinitions.Count)
                grid.ColumnDefinitions.RemoveAt(panelCol);

            foreach (UIElement child in grid.Children)
            {
                int c = Grid.GetColumn(child);
                if (c > panelCol) Grid.SetColumn(child, c - 1);
            }

            hook.PanelBorder = null;
            hook.Splitter    = null;
            hook.IsOpen      = false;
        }

        private static void RemoveMenuButton(ChartHook hook)
        {
            if (hook == null || hook.MenuButton == null) return;
            try
            {
                if (hook.MenuHost is ItemsControl ic && ic.Items.Contains(hook.MenuButton))
                {
                    ic.Items.Remove(hook.MenuButton);
                    return;
                }
                if (hook.MenuHost != null)
                {
                    // Try Remove(item) via reflection (mirror of TryInvokeAdd).
                    var rem = hook.MenuHost.GetType().GetMethod("Remove",
                        BindingFlags.Public | BindingFlags.Instance,
                        null, new[] { typeof(object) }, null);
                    if (rem == null)
                        rem = hook.MenuHost.GetType().GetMethod("Remove",
                            BindingFlags.Public | BindingFlags.Instance,
                            null, new[] { hook.MenuButton.GetType() }, null);
                    if (rem != null) rem.Invoke(hook.MenuHost, new object[] { hook.MenuButton });
                }
            }
            catch { /* shutdown — ignore */ }
        }
    }
}
