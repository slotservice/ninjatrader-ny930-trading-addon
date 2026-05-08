// ============================================================
//  NY930ParametersView — in-AddOn parameter editor
// ------------------------------------------------------------
//  Replaces the need to open NinjaScript's Strategies dialog
//  for the most common parameters. Reads current values from
//  the latest snapshot, lets the user edit them, and pushes
//  a single `*ApplyParameters` action through the bridge.
//
//  Fields that would invalidate a working/filled order
//  (Quantity, SL/TP/range ticks, Direction) are disabled
//  while the strategy is in a trade. The schedule fields and
//  toggles can be edited at any time.
// ============================================================

#region Using declarations
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public sealed class NY930ParametersView : Grid, INY930Localizable, IDisposable
    {
        private readonly NY930ShellView _shell;
        private readonly bool _isOpenRange;

        // Schedule
        private TextBox _hourBox;
        private TextBox _minuteBox;
        private TextBox _secondBox;

        // Range/qty
        private TextBox _qtyBox;
        private TextBox _ticksLongBox;
        private TextBox _ticksShortBox;
        private TextBox _slLongBox;
        private TextBox _tpLongBox;
        private TextBox _slShortBox;
        private TextBox _tpShortBox;
        private TextBox _slBox;
        private TextBox _tpBox;

        // Hedge direction
        private ComboBox _directionBox;

        // Toggles
        private CheckBox _enableLong;
        private CheckBox _enableShort;
        private CheckBox _enableBE;
        private CheckBox _enableTrail;
        private CheckBox _enableTrailTP;
        private CheckBox _enablePartials;
        private CheckBox _enableTimeExit;
        private CheckBox _enableTpGuard;
        private CheckBox _enableSlGuard;
        private CheckBox _enableSingleRev;

        // Guards
        private TextBox _tpGuardTicks;
        private TextBox _slGuardTicks;
        private TextBox _singleRevTicks;

        // Apply
        private Button _btnApply;
        private TextBlock _hdrSchedule;
        private TextBlock _hdrRange;
        private TextBlock _hdrManagement;
        private TextBlock _hdrGuards;
        private TextBlock _applyNote;

        public NY930ParametersView(NY930ShellView shell, bool isOpenRange)
        {
            _shell = shell;
            _isOpenRange = isOpenRange;
            Background = NY930Theme.BgNavyBrush;

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Children.Add(scroll);

            var root = new StackPanel();
            scroll.Content = root;

            // Header — same trade header used by the rest of the
            // navy views, with an "EDITAR" tag on the right.
            var header = new NY930Theme.TradeHeader(isOpenRange ? "APERTURA BREAKOUT" : "APERTURA");
            header.StatusTag.Update("EDITAR", NY930Theme.BlueAccent);
            root.Children.Add(header);

            var inner = new StackPanel { Margin = new Thickness(12, 0, 12, 12) };
            root.Children.Add(inner);

            BuildSchedule(inner);
            BuildRange(inner);
            BuildManagement(inner);
            BuildGuards(inner);

            _applyNote = new TextBlock
            {
                Text       = NY930Localization.T("params.apply.note"),
                FontSize   = 9,
                Foreground = NY930Theme.TextNavyLowBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin     = new Thickness(0, 4, 0, 8)
            };
            inner.Children.Add(_applyNote);

            _btnApply = NY930Theme.NavyPrimaryButton(NY930Localization.T("params.apply"));
            _btnApply.Click += (s, e) => ApplyChanges();
            inner.Children.Add(_btnApply);

            // Initial fill from latest snapshot
            LoadFromSnapshot();

            if (_isOpenRange) NY930Bridge.OpenRangeChanged += OnOR;
            else              NY930Bridge.HedgeChanged     += OnHedge;
        }

        // ── Build sections ─────────────────────────────────────

        private void BuildSchedule(StackPanel root)
        {
            var stack = new StackPanel();
            _hdrSchedule = NY930Theme.NavySectionHeader(NY930Localization.T("params.section.schedule"));
            stack.Children.Add(_hdrSchedule);

            var timeRow = new Grid();
            timeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            timeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            timeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            timeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });
            timeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            timeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(50) });

            var lbl = new TextBlock
            {
                Text       = NY930Localization.T("params.entry_time"),
                FontSize   = 11,
                Foreground = NY930Theme.TextMidBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbl, 0);
            timeRow.Children.Add(lbl);

            _hourBox   = NY930Theme.NavyInputBox(50);
            _minuteBox = NY930Theme.NavyInputBox(50);
            _secondBox = NY930Theme.NavyInputBox(50);
            _hourBox.TextAlignment   = TextAlignment.Center;
            _minuteBox.TextAlignment = TextAlignment.Center;
            _secondBox.TextAlignment = TextAlignment.Center;
            Grid.SetColumn(_hourBox,   1);
            Grid.SetColumn(_minuteBox, 3);
            Grid.SetColumn(_secondBox, 5);

            var sep1 = new TextBlock { Text = ":", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = NY930Theme.TextMidBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var sep2 = new TextBlock { Text = ":", FontSize = 14, FontWeight = FontWeights.Bold, Foreground = NY930Theme.TextMidBrush, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(sep1, 2);
            Grid.SetColumn(sep2, 4);

            timeRow.Children.Add(_hourBox);
            timeRow.Children.Add(sep1);
            timeRow.Children.Add(_minuteBox);
            timeRow.Children.Add(sep2);
            timeRow.Children.Add(_secondBox);
            stack.Children.Add(timeRow);

            _qtyBox = NY930Theme.NavyInputBox();
            stack.Children.Add(NY930Theme.FormField(NY930Localization.T("params.quantity"), _qtyBox));

            if (!_isOpenRange)
            {
                _directionBox = new ComboBox
                {
                    Background      = NY930Theme.BgNavyInputBrush,
                    Foreground      = NY930Theme.TextNavyHiBrush,
                    BorderBrush     = NY930Theme.BorderNavyBrush,
                    BorderThickness = new Thickness(1),
                    FontSize        = 11
                };
                _directionBox.Items.Add(NY930Localization.T("hedge.none"));
                _directionBox.Items.Add(NY930Localization.T("hedge.long"));
                _directionBox.Items.Add(NY930Localization.T("hedge.short"));
                _directionBox.SelectedIndex = 0;
                stack.Children.Add(NY930Theme.FormField(NY930Localization.T("hedge.direction"), _directionBox));
            }

            root.Children.Add(NY930Theme.NavyPanel(stack, new Thickness(0, 0, 0, 10)));
        }

        private void BuildRange(StackPanel root)
        {
            var stack = new StackPanel();
            _hdrRange = NY930Theme.NavySectionHeader(NY930Localization.T("params.section.range"));
            stack.Children.Add(_hdrRange);

            if (_isOpenRange)
            {
                _enableLong  = NY930Theme.NavyToggle(NY930Localization.T("params.enable_long"));
                _enableShort = NY930Theme.NavyToggle(NY930Localization.T("params.enable_short"));
                stack.Children.Add(_enableLong);
                stack.Children.Add(_enableShort);

                _ticksLongBox  = NY930Theme.NavyInputBox();
                _ticksShortBox = NY930Theme.NavyInputBox();
                _slLongBox     = NY930Theme.NavyInputBox();
                _tpLongBox     = NY930Theme.NavyInputBox();
                _slShortBox    = NY930Theme.NavyInputBox();
                _tpShortBox    = NY930Theme.NavyInputBox();

                stack.Children.Add(NY930Theme.FormField(NY930Localization.T("params.long_offset"),  _ticksLongBox));
                stack.Children.Add(NY930Theme.FormField(NY930Localization.T("params.long_sl"),      _slLongBox));
                stack.Children.Add(NY930Theme.FormField(NY930Localization.T("params.long_tp"),      _tpLongBox));
                stack.Children.Add(NY930Theme.FormField(NY930Localization.T("params.short_offset"), _ticksShortBox));
                stack.Children.Add(NY930Theme.FormField(NY930Localization.T("params.short_sl"),     _slShortBox));
                stack.Children.Add(NY930Theme.FormField(NY930Localization.T("params.short_tp"),     _tpShortBox));
            }
            else
            {
                _slBox = NY930Theme.NavyInputBox();
                _tpBox = NY930Theme.NavyInputBox();
                stack.Children.Add(NY930Theme.FormField(NY930Localization.T("params.sl_ticks"), _slBox));
                stack.Children.Add(NY930Theme.FormField(NY930Localization.T("params.tp_ticks"), _tpBox));
            }

            root.Children.Add(NY930Theme.NavyPanel(stack, new Thickness(0, 0, 0, 10)));
        }

        private void BuildManagement(StackPanel root)
        {
            var stack = new StackPanel();
            _hdrManagement = NY930Theme.NavySectionHeader(NY930Localization.T("params.section.management"));
            stack.Children.Add(_hdrManagement);

            _enableBE       = NY930Theme.NavyToggle(NY930Localization.T("params.enable_be"));
            _enableTrail    = NY930Theme.NavyToggle(NY930Localization.T("params.enable_trail"));
            _enableTrailTP  = NY930Theme.NavyToggle(NY930Localization.T("params.enable_traiTP"));
            _enablePartials = NY930Theme.NavyToggle(NY930Localization.T("params.enable_partials"));
            _enableTimeExit = NY930Theme.NavyToggle(NY930Localization.T("params.enable_time_exit"));

            stack.Children.Add(_enableBE);
            stack.Children.Add(_enableTrail);
            stack.Children.Add(_enableTrailTP);
            stack.Children.Add(_enablePartials);
            stack.Children.Add(_enableTimeExit);

            root.Children.Add(NY930Theme.NavyPanel(stack, new Thickness(0, 0, 0, 10)));
        }

        private void BuildGuards(StackPanel root)
        {
            var stack = new StackPanel();
            _hdrGuards = NY930Theme.NavySectionHeader(NY930Localization.T("params.section.guards"));
            stack.Children.Add(_hdrGuards);

            _enableTpGuard = NY930Theme.NavyToggle(NY930Localization.T("params.enable_tp_guard"));
            _tpGuardTicks  = NY930Theme.NavyInputBox();
            stack.Children.Add(_enableTpGuard);
            stack.Children.Add(NY930Theme.FormField(NY930Localization.T("params.tp_guard_ticks"), _tpGuardTicks));

            _enableSlGuard = NY930Theme.NavyToggle(NY930Localization.T("params.enable_sl_guard"));
            _slGuardTicks  = NY930Theme.NavyInputBox();
            stack.Children.Add(_enableSlGuard);
            stack.Children.Add(NY930Theme.FormField(NY930Localization.T("params.sl_guard_ticks"), _slGuardTicks));

            if (_isOpenRange)
            {
                _enableSingleRev = NY930Theme.NavyToggle(NY930Localization.T("params.enable_single_rev"));
                _singleRevTicks  = NY930Theme.NavyInputBox();
                stack.Children.Add(_enableSingleRev);
                stack.Children.Add(NY930Theme.FormField(NY930Localization.T("params.single_rev_ticks"), _singleRevTicks));
            }

            root.Children.Add(NY930Theme.NavyPanel(stack, new Thickness(0, 0, 0, 10)));
        }

        // ── Snapshot binding ───────────────────────────────────

        private void LoadFromSnapshot()
        {
            if (_isOpenRange)
            {
                var s = NY930Bridge.GetOpenRange();
                if (s == null) return;
                if (s.EntryTime != default(DateTime))
                {
                    _hourBox.Text   = s.EntryTime.Hour.ToString();
                    _minuteBox.Text = s.EntryTime.Minute.ToString();
                    _secondBox.Text = s.EntryTime.Second.ToString();
                }
                _qtyBox.Text         = s.Quantity.ToString();
                _enableLong.IsChecked  = s.EnableLong;
                _enableShort.IsChecked = s.EnableShort;
                _ticksLongBox.Text   = s.TicksLong.ToString();
                _ticksShortBox.Text  = s.TicksShort.ToString();
                _slLongBox.Text      = s.StopLossLongTicks.ToString();
                _tpLongBox.Text      = s.TakeProfitLongTicks.ToString();
                _slShortBox.Text     = s.StopLossShortTicks.ToString();
                _tpShortBox.Text     = s.TakeProfitShortTicks.ToString();
                _enableBE.IsChecked       = s.EnableBreakeven;
                _enableTrail.IsChecked    = s.EnableTrailing;
                _enableTrailTP.IsChecked  = s.EnableTrailingTP;
                _enablePartials.IsChecked = s.EnablePartials;
                _enableTimeExit.IsChecked = s.EnableTimeExit;
                _enableTpGuard.IsChecked  = s.EnableTpGapGuard;
                _enableSlGuard.IsChecked  = s.EnableSlGapGuard;
                _tpGuardTicks.Text        = s.TpGapGuardTicks.ToString();
                _slGuardTicks.Text        = s.SlGapGuardTicks.ToString();
                _enableSingleRev.IsChecked = s.EnableSingleStopReverseProtection;
                _singleRevTicks.Text       = s.SingleStopReverseTicks.ToString();

                bool inTrade = s.InLong || s.InShort;
                ApplyTradeLock(inTrade);
            }
            else
            {
                var s = NY930Bridge.GetHedge();
                if (s == null) return;
                if (s.EntryTime != default(DateTime))
                {
                    _hourBox.Text   = s.EntryTime.Hour.ToString();
                    _minuteBox.Text = s.EntryTime.Minute.ToString();
                    _secondBox.Text = s.EntryTime.Second.ToString();
                }
                _qtyBox.Text  = s.Quantity.ToString();
                _slBox.Text   = s.StopLossTicks.ToString();
                _tpBox.Text   = s.TakeProfitTicks.ToString();

                if (_directionBox != null)
                {
                    if      (s.Direction == "Long")  _directionBox.SelectedIndex = 1;
                    else if (s.Direction == "Short") _directionBox.SelectedIndex = 2;
                    else                              _directionBox.SelectedIndex = 0;
                }

                _enableBE.IsChecked       = s.EnableBreakeven;
                _enableTrail.IsChecked    = s.EnableTrailing;
                _enableTrailTP.IsChecked  = s.EnableTrailingTP;
                _enablePartials.IsChecked = s.EnablePartials;
                _enableTimeExit.IsChecked = s.EnableTimeExit;
                _enableTpGuard.IsChecked  = s.EnableTpGapGuard;
                _enableSlGuard.IsChecked  = s.EnableSlGapGuard;
                _tpGuardTicks.Text        = s.TpGapGuardTicks.ToString();
                _slGuardTicks.Text        = s.SlGapGuardTicks.ToString();

                ApplyTradeLock(s.InPosition);
            }
        }

        private void ApplyTradeLock(bool inTrade)
        {
            // While in a trade, the parameters that would invalidate
            // the working orders are disabled. The schedule fields and
            // toggles can still be edited.
            _qtyBox.IsEnabled = !inTrade;
            if (_isOpenRange)
            {
                _enableLong.IsEnabled = _enableShort.IsEnabled = !inTrade;
                _ticksLongBox.IsEnabled = _ticksShortBox.IsEnabled = !inTrade;
                _slLongBox.IsEnabled = _tpLongBox.IsEnabled = !inTrade;
                _slShortBox.IsEnabled = _tpShortBox.IsEnabled = !inTrade;
            }
            else
            {
                if (_directionBox != null) _directionBox.IsEnabled = !inTrade;
                _slBox.IsEnabled = _tpBox.IsEnabled = !inTrade;
            }
        }

        private void OnOR(NY930OpenRangeSnapshot s)   => Dispatcher.InvokeAsync(LoadFromSnapshot);
        private void OnHedge(NY930HedgeSnapshot s)    => Dispatcher.InvokeAsync(LoadFromSnapshot);

        // ── Apply ──────────────────────────────────────────────

        private void ApplyChanges()
        {
            var p = new NY930Parameters();
            p.EntryHour   = ParseInt(_hourBox.Text);
            p.EntryMinute = ParseInt(_minuteBox.Text);
            p.EntrySecond = ParseInt(_secondBox.Text);
            p.Quantity    = ParseInt(_qtyBox.Text);

            p.EnableBreakeven  = _enableBE.IsChecked == true;
            p.EnableTrailing   = _enableTrail.IsChecked == true;
            p.EnableTrailingTP = _enableTrailTP.IsChecked == true;
            p.EnablePartials   = _enablePartials.IsChecked == true;
            p.EnableTimeExit   = _enableTimeExit.IsChecked == true;
            p.EnableTpGapGuard = _enableTpGuard.IsChecked == true;
            p.EnableSlGapGuard = _enableSlGuard.IsChecked == true;
            p.TpGapGuardTicks  = ParseInt(_tpGuardTicks.Text);
            p.SlGapGuardTicks  = ParseInt(_slGuardTicks.Text);

            if (_isOpenRange)
            {
                p.EnableLong            = _enableLong.IsChecked  == true;
                p.EnableShort           = _enableShort.IsChecked == true;
                p.TicksLong             = ParseInt(_ticksLongBox.Text);
                p.TicksShort            = ParseInt(_ticksShortBox.Text);
                p.StopLossLongTicks     = ParseInt(_slLongBox.Text);
                p.TakeProfitLongTicks   = ParseInt(_tpLongBox.Text);
                p.StopLossShortTicks    = ParseInt(_slShortBox.Text);
                p.TakeProfitShortTicks  = ParseInt(_tpShortBox.Text);
                p.EnableSingleStopReverseProtection = _enableSingleRev.IsChecked == true;
                p.SingleStopReverseTicks            = ParseInt(_singleRevTicks.Text);

                NY930Bridge.RequestOpenRangeAction(new NY930Action
                {
                    Type       = NY930ActionType.OpenRangeApplyParameters,
                    Parameters = p
                });
            }
            else
            {
                p.StopLossTicks   = ParseInt(_slBox.Text);
                p.TakeProfitTicks = ParseInt(_tpBox.Text);

                if (_directionBox != null)
                {
                    switch (_directionBox.SelectedIndex)
                    {
                        case 1: p.Direction = "Long";  break;
                        case 2: p.Direction = "Short"; break;
                        default: p.Direction = "None"; break;
                    }
                }

                NY930Bridge.RequestHedgeAction(new NY930Action
                {
                    Type       = NY930ActionType.HedgeApplyParameters,
                    Parameters = p
                });
            }

            // Visual feedback: briefly flash the apply button.
            _btnApply.Content = "✓";
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            t.Tick += (s, ev) =>
            {
                _btnApply.Content = NY930Localization.T("params.apply");
                t.Stop();
            };
            t.Start();
        }

        private static int? ParseInt(string s)
        {
            int n;
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out n)) return n;
            return null;
        }

        public void RefreshLocalization()
        {
            _hdrSchedule.Text   = NY930Localization.T("params.section.schedule");
            _hdrRange.Text      = NY930Localization.T("params.section.range");
            _hdrManagement.Text = NY930Localization.T("params.section.management");
            _hdrGuards.Text     = NY930Localization.T("params.section.guards");
            _btnApply.Content   = NY930Localization.T("params.apply");
            _applyNote.Text     = NY930Localization.T("params.apply.note");
        }

        public void Dispose()
        {
            if (_isOpenRange) NY930Bridge.OpenRangeChanged -= OnOR;
            else              NY930Bridge.HedgeChanged     -= OnHedge;
        }
    }
}
