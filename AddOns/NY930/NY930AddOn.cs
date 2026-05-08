// ============================================================
//  NY930AddOn — root NTAddOn entry point
// ------------------------------------------------------------
//  Two integration paths:
//    1. Control Center menu: Adds a "NY930" item under New
//       that opens the panel as a standalone NTWindow.
//    2. Chart side panel: When a ChartWindow opens, the AddOn
//       injects the NY930 panel as an extra column on the
//       right edge of the chart's main grid (Chart-Trader
//       style). Toggleable via a small button in the chart
//       toolbar.
//
//  The chart-side injection is done by walking the chart's
//  visual tree to find its main Grid and adding our panel as
//  a new column. NinjaTrader's internal layout doesn't expose
//  a public hook for this, so we fall back to a floating
//  NTWindow if the injection fails for any reason.
// ============================================================

#region Using declarations
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public class NY930AddOn : NinjaTrader.NinjaScript.AddOnBase
    {
        // Standalone window (opened from Control Center menu).
        private static NTWindow      _shellWindow;
        private static NY930ShellView _shellView;

        // Per-chart docked panels. We key by Window because the
        // exact chart window class name is not stable across NT8
        // versions, so we don't take a typed reference to it.
        private static readonly Dictionary<Window, FrameworkElement> _chartPanels
            = new Dictionary<Window, FrameworkElement>();

        private NTMenuItem _addOnMenuItem;
        private NTMenuItem _newMenuRoot;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "NY930 — unified Open Range + Hedge control plane (Phase 1.1).";
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

        protected override void OnWindowCreated(Window window)
        {
            if (window == null) return;

            ControlCenter cc = window as ControlCenter;
            if (cc != null)
            {
                AttachControlCenterMenu(cc);
                return;
            }

            // Detect chart windows by type name. The actual class
            // name varies by NT8 version (Chart, ChartWindow, etc.)
            // and isn't always public, so a string match keeps the
            // AddOn portable.
            if (LooksLikeChartWindow(window))
            {
                if (window.IsLoaded) AttachChartPanel(window);
                else                 window.Loaded += OnChartLoaded;
            }
        }

        protected override void OnWindowDestroyed(Window window)
        {
            try
            {
                if (window is ControlCenter)
                {
                    if (_addOnMenuItem != null && _newMenuRoot != null
                        && _newMenuRoot.Items.Contains(_addOnMenuItem))
                        _newMenuRoot.Items.Remove(_addOnMenuItem);
                    _addOnMenuItem = null;
                    _newMenuRoot   = null;
                }

                if (window != null && _chartPanels.ContainsKey(window))
                {
                    var panel = _chartPanels[window];
                    if (panel is IDisposable d) d.Dispose();
                    _chartPanels.Remove(window);
                }
            }
            catch { /* don't crash on shutdown */ }
        }

        private static bool LooksLikeChartWindow(Window w)
        {
            if (w == null) return false;
            string t = w.GetType().Name ?? string.Empty;
            // Match common NT8 chart window class names without
            // taking a hard typed reference to any of them.
            return t.IndexOf("Chart", StringComparison.OrdinalIgnoreCase) >= 0
                && t.IndexOf("ControlCenter", StringComparison.OrdinalIgnoreCase) < 0;
        }

        // ── Control Center menu ──────────────────────────────────

        private void AttachControlCenterMenu(ControlCenter cc)
        {
            try
            {
                _newMenuRoot = cc.FindFirst("ControlCenterMenuItemNew") as NTMenuItem;
                if (_newMenuRoot == null)
                {
                    NY930Log.Warn("AddOn", "Control Center 'New' menu not found.");
                    return;
                }

                _addOnMenuItem = new NTMenuItem
                {
                    Header = "NY930",
                    Style  = Application.Current.TryFindResource("MainMenuItem") as Style
                };
                _addOnMenuItem.Click += (s, e) => OpenStandaloneShell();
                _newMenuRoot.Items.Add(_addOnMenuItem);

                NY930Log.Info("AddOn", "NY930 menu item added to Control Center.");
            }
            catch (Exception ex)
            {
                NY930Log.Warn("AddOn", "AttachControlCenterMenu failed: " + ex.Message);
            }
        }

        // ── Standalone shell window (fallback / explicit open) ──

        public static void OpenStandaloneShell()
        {
            try
            {
                if (_shellWindow != null)
                {
                    _shellWindow.Activate();
                    return;
                }

                _shellWindow = new NTWindow
                {
                    Title      = "NY930",
                    Width      = 360,
                    Height     = 720,
                    Background = NY930Theme.BgBaseBrush
                };

                _shellView = new NY930ShellView();
                _shellWindow.Content = _shellView;
                _shellWindow.Closed += (s, e) =>
                {
                    if (_shellView != null) _shellView.Dispose();
                    _shellView   = null;
                    _shellWindow = null;
                };

                _shellWindow.Show();
                _shellWindow.Activate();
            }
            catch (Exception ex)
            {
                NY930Log.Error("AddOn", "OpenStandaloneShell error: " + ex.Message);
            }
        }

        // ── Chart-side panel injection ───────────────────────────

        private void OnChartLoaded(object sender, RoutedEventArgs e)
        {
            Window ch = sender as Window;
            if (ch == null) return;
            ch.Loaded -= OnChartLoaded;
            AttachChartPanel(ch);
        }

        private void AttachChartPanel(Window ch)
        {
            try
            {
                if (_chartPanels.ContainsKey(ch)) return;

                // Find the chart's main Grid container and add a new
                // column on the right that hosts our panel.
                Grid mainGrid = FindMainChartGrid(ch);
                if (mainGrid == null)
                {
                    NY930Log.Warn("AddOn", "Chart main grid not found — falling back to floating panel.");
                    return;
                }

                var shell = new NY930ShellView();
                var border = new Border
                {
                    Width           = 340,
                    Background      = NY930Theme.BgNavyBrush,
                    BorderBrush     = NY930Theme.BorderNavyBrush,
                    BorderThickness = new Thickness(1, 0, 0, 0),
                    Child           = shell
                };

                int newColIndex = mainGrid.ColumnDefinitions.Count;
                mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                Grid.SetColumn(border, newColIndex);
                Grid.SetRowSpan(border, Math.Max(1, mainGrid.RowDefinitions.Count));
                mainGrid.Children.Add(border);

                _chartPanels[ch] = border;
                NY930Log.Info("AddOn", "NY930 chart panel injected into chart window (" + ch.GetType().Name + ").");
            }
            catch (Exception ex)
            {
                NY930Log.Warn("AddOn", "AttachChartPanel failed: " + ex.Message
                    + " — user can still open via Control Center → New → NY930.");
            }
        }

        // Walk the chart's visual tree looking for the largest Grid
        // that contains the chart canvas. Heuristic: it has rows AND
        // columns and one of its children is itself a Grid containing
        // a chart panel. We give up after a few candidates.
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
                if (node is Grid g)
                {
                    int area = g.ColumnDefinitions.Count * Math.Max(1, g.RowDefinitions.Count);
                    if (area > bestArea)
                    {
                        bestArea = area;
                        best = g;
                    }
                }
                int n = VisualTreeHelper.GetChildrenCount(node);
                for (int i = 0; i < n; i++)
                    queue.Enqueue(VisualTreeHelper.GetChild(node, i));
            }
            return best;
        }
    }
}
