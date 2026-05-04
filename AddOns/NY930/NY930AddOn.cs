// ============================================================
//  NY930AddOn — root NTAddOn entry point
// ------------------------------------------------------------
//  Adds a "NY930" item to the Control Center's New menu.
//  Clicking the item opens the NY930HomeView inside an NTWindow
//  that the user can drag, dock, or float on a second monitor.
//
//  Uses the canonical NT8 pattern: ControlCenter.FindFirst is
//  called with the well-known automation name of the New menu
//  ("ControlCenterMenuItemNew"). That avoids guessing at the
//  WPF visual tree.
// ============================================================

#region Using declarations
using System;
using System.Windows;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public class NY930AddOn : NinjaTrader.NinjaScript.AddOnBase
    {
        private static NTWindow      _shellWindow;
        private static NY930ShellView _shellView;

        // One menu item per Control Center instance. NT can spawn
        // more than one (rare, but possible with multiple workspaces),
        // so we only track the most recent for cleanup.
        private NTMenuItem _addOnMenuItem;
        private NTMenuItem _newMenuRoot;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "NY930 — unified Open Range + Hedge control plane (Phase 1).";
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

        protected override void OnWindowCreated(Window window)
        {
            // Only hook the Control Center — chart windows, etc. are
            // not the right place for the NY930 launcher.
            ControlCenter cc = window as ControlCenter;
            if (cc == null) return;

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
                _addOnMenuItem.Click += OnNY930MenuClicked;
                _newMenuRoot.Items.Add(_addOnMenuItem);

                NY930Log.Info("AddOn", "NY930 menu item added to Control Center.");
            }
            catch (Exception ex)
            {
                NY930Log.Warn("AddOn", "AttachMenu failed: " + ex.Message);
            }
        }

        protected override void OnWindowDestroyed(Window window)
        {
            if (!(window is ControlCenter)) return;

            try
            {
                if (_addOnMenuItem != null)
                {
                    _addOnMenuItem.Click -= OnNY930MenuClicked;
                    if (_newMenuRoot != null && _newMenuRoot.Items.Contains(_addOnMenuItem))
                        _newMenuRoot.Items.Remove(_addOnMenuItem);
                }
            }
            catch { /* don't crash on shutdown */ }

            _addOnMenuItem = null;
            _newMenuRoot   = null;
        }

        private void OnNY930MenuClicked(object sender, RoutedEventArgs e)
        {
            OpenShell();
        }

        // ── Shell window ─────────────────────────────────────────
        public static void OpenShell()
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
                NY930Log.Error("AddOn", "OpenShell error: " + ex.Message);
            }
        }
    }
}
