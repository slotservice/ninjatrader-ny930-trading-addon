// ============================================================
//  NY930HedgeView — v1.2 pixel-match against client mockup
// ------------------------------------------------------------
//  Same visual language as the Open Range view but tailored
//  for the Hedge strategy (single-direction direct entry).
//  No range/spread cards. Direction badge in the header tag.
// ============================================================

#region Using declarations
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public sealed class NY930HedgeView : Grid, INY930Localizable, IDisposable
    {
        private readonly NY930ShellView _shell;

        private NY930Theme.TradeHeader _header;

        private TextBlock _instrumentText;
        private TextBlock _countdownLabel;

        private NY930Theme.BigPnLDisplay _bigPnL;
        private Border _pillCurrency;
        private Border _pillDuration;
        private Border _pillContracts;
        private TextBlock _pillCurrencyText;
        private TextBlock _pillDurationText;
        private TextBlock _pillContractsText;

        private NY930Theme.NavyTpRow _tp1Row;
        private NY930Theme.NavyTpRow _tp2Row;
        private NY930Theme.NavyTpRow _tpRow;
        private NY930Theme.NavyTpRow _slRow;

        private Button _btnBreakeven;
        private Button _btnCloseNow;
        private Button _btnPartial;
        private Button _btnTrailing;
        private NY930Theme.PartialPercentSelector _partialPct;

        private Button _btnBuyNow;
        private Button _btnSellNow;
        private Button _btnCancelEntry;
        private Button _btnEditParams;

        private TextBlock _hdrActions;
        private TextBlock _hdrManagement;

        private System.Windows.Threading.DispatcherTimer _ticker;
        private DateTime _entryTime;

        public NY930HedgeView(NY930ShellView shell)
        {
            _shell = shell;
            Background = NY930Theme.BgNavyBrush;

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Children.Add(scroll);

            var root = new StackPanel();
            scroll.Content = root;

            _header = new NY930Theme.TradeHeader("APERTURA");
            root.Children.Add(_header);

            BuildHero(root);
            BuildTargets(root);
            BuildActions(root);
            BuildManagement(root);
            BuildEntry(root);

            NY930Bridge.HedgeChanged += OnSnapshot;
            var current = NY930Bridge.GetHedge();
            if (current != null) OnSnapshot(current);
            else                 RenderEmpty();

            _ticker = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _ticker.Tick += (s, e) => RefreshCountdown();
            _ticker.Start();
        }

        private void BuildHero(StackPanel root)
        {
            var stack = new StackPanel { Margin = new Thickness(12, 0, 12, 0) };

            var subRow = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            subRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            subRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _instrumentText = new TextBlock
            {
                Text       = "—",
                FontSize   = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = NY930Theme.TextNavyMidBrush
            };
            Grid.SetColumn(_instrumentText, 0);
            subRow.Children.Add(_instrumentText);

            _countdownLabel = new TextBlock
            {
                Text       = "",
                FontSize   = 11,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = NY930Theme.CyanAccentHiBrush
            };
            Grid.SetColumn(_countdownLabel, 1);
            subRow.Children.Add(_countdownLabel);
            stack.Children.Add(subRow);

            var inner = new StackPanel();
            _bigPnL = new NY930Theme.BigPnLDisplay();
            inner.Children.Add(_bigPnL);

            var pills = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            _pillCurrency  = NY930Theme.Pill("$0.00",  NY930Theme.CyanAccent);
            _pillDuration  = NY930Theme.Pill("00:00",  NY930Theme.TextNavyMid);
            _pillContracts = NY930Theme.Pill("0 ctos", NY930Theme.TextNavyMid);
            _pillCurrencyText  = (TextBlock)_pillCurrency.Child;
            _pillDurationText  = (TextBlock)_pillDuration.Child;
            _pillContractsText = (TextBlock)_pillContracts.Child;
            pills.Children.Add(_pillCurrency);
            pills.Children.Add(_pillDuration);
            pills.Children.Add(_pillContracts);
            inner.Children.Add(pills);

            stack.Children.Add(NY930Theme.NavyPanel(inner, new Thickness(0, 0, 0, 8)));
            root.Children.Add(stack);
        }

        private void BuildTargets(StackPanel root)
        {
            var stack = new StackPanel();
            _tp1Row = new NY930Theme.NavyTpRow();
            _tp2Row = new NY930Theme.NavyTpRow();
            _tpRow  = new NY930Theme.NavyTpRow();
            _slRow  = new NY930Theme.NavyTpRow(isStop: true);
            stack.Children.Add(_tp1Row);
            stack.Children.Add(_tp2Row);
            stack.Children.Add(_tpRow);
            stack.Children.Add(_slRow);
            root.Children.Add(NY930Theme.NavyPanel(stack, new Thickness(12, 0, 12, 8)));
        }

        private void BuildActions(StackPanel root)
        {
            var stack = new StackPanel();
            _hdrActions = NY930Theme.NavySectionHeader(NY930Localization.T("trade.section.management"));
            stack.Children.Add(_hdrActions);

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition());
            _btnBreakeven = NY930Theme.NavyButton(NY930Localization.T("trade.action.breakeven"), NY930Theme.BlueAccent, false);
            _btnBreakeven.Margin = new Thickness(0, 0, 4, 0);
            _btnBreakeven.Click += (s, e) => Send(NY930ActionType.HedgeBreakeven, 0);
            Grid.SetColumn(_btnBreakeven, 0);
            row.Children.Add(_btnBreakeven);

            _btnCloseNow = NY930Theme.NavyButton(NY930Localization.T("trade.action.close_now"), NY930Theme.DangerRed, false);
            _btnCloseNow.Margin = new Thickness(4, 0, 0, 0);
            _btnCloseNow.Click += (s, e) => Send(NY930ActionType.HedgeFlatten, 0);
            Grid.SetColumn(_btnCloseNow, 1);
            row.Children.Add(_btnCloseNow);

            stack.Children.Add(row);
            root.Children.Add(NY930Theme.NavyPanel(stack, new Thickness(12, 0, 12, 8)));
        }

        private void BuildManagement(StackPanel root)
        {
            var stack = new StackPanel();
            _hdrManagement = NY930Theme.NavySectionHeader(NY930Localization.T("trade.section.management"));
            stack.Children.Add(_hdrManagement);

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _btnPartial = NY930Theme.NavyButton(NY930Localization.T("trade.action.partial"), NY930Theme.BlueAccent, false);
            _btnPartial.Margin = new Thickness(0, 0, 6, 0);
            _btnPartial.Click += (s, e) => SendPartial();
            Grid.SetColumn(_btnPartial, 0);

            _partialPct = new NY930Theme.PartialPercentSelector
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            };
            Grid.SetColumn(_partialPct, 1);

            _btnTrailing = NY930Theme.NavyButton(NY930Localization.T("trade.action.trailing"), NY930Theme.SuccessGreen, false);
            _btnTrailing.Click += (s, e) => Send(NY930ActionType.HedgeTrailingTrigger, 0);
            Grid.SetColumn(_btnTrailing, 2);

            row.Children.Add(_btnPartial);
            row.Children.Add(_partialPct);
            row.Children.Add(_btnTrailing);

            stack.Children.Add(row);
            root.Children.Add(NY930Theme.NavyPanel(stack, new Thickness(12, 0, 12, 8)));
        }

        private void BuildEntry(StackPanel root)
        {
            var stack = new StackPanel();

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition());
            _btnBuyNow  = NY930Theme.NavyButton(NY930Localization.T("or.buy_now"),  NY930Theme.SuccessGreen, true);
            _btnSellNow = NY930Theme.NavyButton(NY930Localization.T("or.sell_now"), NY930Theme.DangerRed,    true);
            _btnBuyNow.Margin  = new Thickness(0, 0, 4, 0);
            _btnSellNow.Margin = new Thickness(4, 0, 0, 0);
            _btnBuyNow.Click  += (s, e) => Send(NY930ActionType.HedgeBuyNow,  0);
            _btnSellNow.Click += (s, e) => Send(NY930ActionType.HedgeSellNow, 0);
            Grid.SetColumn(_btnBuyNow,  0);
            Grid.SetColumn(_btnSellNow, 1);
            row.Children.Add(_btnBuyNow);
            row.Children.Add(_btnSellNow);
            stack.Children.Add(row);

            _btnCancelEntry = NY930Theme.NavyButton(NY930Localization.T("or.cancel"), NY930Theme.WarnAmberHi, false);
            _btnCancelEntry.Margin = new Thickness(0, 6, 0, 0);
            _btnCancelEntry.Click += (s, e) => Send(NY930ActionType.HedgeCancelEntry, 0);
            stack.Children.Add(_btnCancelEntry);

            _btnEditParams = new Button
            {
                Content    = NY930Localization.T("params.title"),
                Background = Brushes.Transparent,
                Foreground = NY930Theme.TextNavyMidBrush,
                BorderBrush = NY930Theme.BorderNavyBrush,
                BorderThickness = new Thickness(1),
                Padding    = new Thickness(0, 8, 0, 8),
                FontSize   = 11,
                Margin     = new Thickness(0, 6, 0, 0),
                Cursor     = System.Windows.Input.Cursors.Hand
            };
            _btnEditParams.Click += (s, e) => _shell.Show(new NY930ParametersView(_shell, isOpenRange: false));
            stack.Children.Add(_btnEditParams);

            root.Children.Add(NY930Theme.NavyPanel(stack, new Thickness(12, 0, 12, 12)));
        }

        // ── Bridge ──────────────────────────────────────────────

        private void Send(NY930ActionType type, int arg)
        {
            NY930Bridge.RequestHedgeAction(new NY930Action { Type = type, IntArg = arg });
        }

        private void SendPartial()
        {
            var snap = NY930Bridge.GetHedge();
            int total = snap != null ? Math.Max(1, snap.ContractsRemaining) : 1;
            int qty = (int)Math.Round(total * (_partialPct.Percent / 100.0));
            if (qty < 1)     qty = 1;
            if (qty > total) qty = total;
            Send(NY930ActionType.HedgePartialClose, qty);
        }

        private void OnSnapshot(NY930HedgeSnapshot snap)
            => Dispatcher.InvokeAsync(() => Render(snap));

        private void Render(NY930HedgeSnapshot snap)
        {
            if (snap == null) { RenderEmpty(); return; }

            _entryTime = snap.EntryTime;
            _instrumentText.Text = snap.Instrument ?? "—";

            // Header status tag + direction
            UpdateHeaderTag(snap);

            // Hero PnL
            _bigPnL.Update(snap.UnrealizedCurrency, snap.UnrealizedTicks, snap.Direction);

            // Pills
            _pillCurrencyText.Text = (snap.UnrealizedCurrency >= 0 ? "+" : "")
                + snap.UnrealizedCurrency.ToString("C", CultureInfo.CurrentCulture);
            _pillCurrency.Visibility = snap.InPosition ? Visibility.Visible : Visibility.Collapsed;
            if (snap.TradeStartTime != DateTime.MinValue && snap.InPosition)
            {
                var d = DateTime.Now - snap.TradeStartTime;
                _pillDurationText.Text = ((int)d.TotalMinutes).ToString("D2") + ":" + d.Seconds.ToString("D2");
                _pillDuration.Visibility = Visibility.Visible;
            }
            else _pillDuration.Visibility = Visibility.Collapsed;
            _pillContractsText.Text = snap.ContractsRemaining + " ctos";
            _pillContracts.Visibility = snap.InPosition ? Visibility.Visible : Visibility.Collapsed;

            // TP / SL rows
            UpdateTpRow(_tp1Row, "TP1", snap.P1Price, snap.Partial1Done, snap, snap.Partial1Ticks);
            UpdateTpRow(_tp2Row, "TP2", snap.P2Price, snap.Partial2Done, snap, snap.Partial2Ticks);
            UpdateTpRow(_tpRow,  "TP",  snap.TpPrice, false,             snap, snap.TakeProfitTicks);
            UpdateSlRow(_slRow, snap);

            // Buttons
            bool canEnter = !snap.InPosition && !snap.SessionDone;
            _btnBreakeven.IsEnabled  = snap.InPosition;
            _btnCloseNow.IsEnabled   = snap.InPosition;
            _btnPartial.IsEnabled    = snap.InPosition;
            _btnTrailing.IsEnabled   = snap.InPosition;
            _btnBuyNow.IsEnabled     = canEnter;
            _btnSellNow.IsEnabled    = canEnter;
            _btnCancelEntry.IsEnabled = !canEnter && !snap.InPosition;

            // Auto-route to result on close
            if (snap.LastResult != null && !snap.InPosition && snap.SessionDone
                && _shell != null && _shell.CurrentViewIs<NY930HedgeView>())
            {
                _shell.Show(new NY930ResultView(_shell, snap.LastResult));
            }

            RefreshCountdown();
        }

        private void UpdateHeaderTag(NY930HedgeSnapshot s)
        {
            if (s.InPosition)
            {
                Color tint = s.Direction == "Long" ? NY930Theme.SuccessGreen : NY930Theme.DangerRed;
                string txt = s.Direction == "Long" ? NY930Localization.T("hedge.long").ToUpperInvariant()
                                                     : NY930Localization.T("hedge.short").ToUpperInvariant();
                _header.StatusTag.Update(txt, tint);
            }
            else if (s.SessionDone)
                _header.StatusTag.Update(NY930Localization.T("status.session_done"), NY930Theme.TextNavyMid);
            else
                _header.StatusTag.Update("WAITING", NY930Theme.WarnAmberHi);
        }

        private void UpdateTpRow(NY930Theme.NavyTpRow row, string label, double price, bool done,
                                 NY930HedgeSnapshot s, int targetTicks)
        {
            if (price <= 0) { row.SetIdle(string.Format(NY930Localization.T("trade.tp.pending"), label)); return; }

            string baseLabelDone     = string.Format(NY930Localization.T("trade.tp.reached"),     label);
            string baseLabelActive   = string.Format(NY930Localization.T("trade.tp.in_progress"), label);
            string baseLabelPending  = string.Format(NY930Localization.T("trade.tp.pending"),     label);

            string ticksTxt = (targetTicks > 0 ? "+" : "") + targetTicks + " " + NY930Localization.T("common.ticks");

            if (done)
            {
                row.SetDone(baseLabelDone, ticksTxt, "");
            }
            else if (s.InPosition)
            {
                double progress = 0;
                if (targetTicks > 0 && s.UnrealizedTicks > 0)
                    progress = Math.Max(0, Math.Min(1, s.UnrealizedTicks / (double)targetTicks));
                int currentTicks = (int)Math.Max(0, Math.Round(s.UnrealizedTicks));
                string activeTicksTxt = "+" + currentTicks + " / " + targetTicks + " " + NY930Localization.T("common.ticks");
                row.SetActive(baseLabelActive, activeTicksTxt, "", progress);
            }
            else
            {
                row.SetPending(baseLabelPending, ticksTxt, "");
            }
        }

        private void UpdateSlRow(NY930Theme.NavyTpRow row, NY930HedgeSnapshot s)
        {
            if (s.SlPrice <= 0) { row.SetIdle(NY930Localization.T("trade.sl.label")); return; }

            if (s.LastResult != null && s.LastResult.SlHit && !s.InPosition)
            {
                row.SetDone(NY930Localization.T("trade.sl.hit"),
                    s.SlPrice.ToString("F5", CultureInfo.InvariantCulture), "");
                return;
            }

            if (s.InPosition && s.LastPrice > 0 && s.TickSize > 0)
            {
                double distTicks = s.Direction == "Long"
                    ? (s.LastPrice - s.SlPrice) / s.TickSize
                    : (s.SlPrice - s.LastPrice) / s.TickSize;
                if (distTicks <= 5)
                {
                    row.SetDanger(NY930Localization.T("trade.sl.danger"),
                        string.Format("SL a {0:F0} ticks", Math.Max(0, distTicks)));
                    return;
                }
            }

            string ticksTxt = "-" + s.StopLossTicks + " " + NY930Localization.T("common.ticks");
            row.SetPending(NY930Localization.T("trade.sl.label"), ticksTxt, "");
        }

        private void RefreshCountdown()
        {
            if (_countdownLabel == null) return;
            if (_entryTime == DateTime.MinValue || _entryTime == default(DateTime))
            {
                _countdownLabel.Text = "";
                return;
            }
            DateTime now = DateTime.Now;
            if (now >= _entryTime) { _countdownLabel.Text = ""; return; }
            TimeSpan rem = _entryTime - now;
            string txt = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)rem.TotalHours, rem.Minutes, rem.Seconds);
            _countdownLabel.Text = string.Format(NY930Localization.T("status.countdown"), txt);
        }

        private void RenderEmpty()
        {
            _instrumentText.Text = "—";
            _countdownLabel.Text = "";
            _header.StatusTag.Update("NO STRATEGY", NY930Theme.TextNavyLow);

            _bigPnL.Update(0, 0, "None");
            _pillCurrency.Visibility = _pillDuration.Visibility = _pillContracts.Visibility = Visibility.Collapsed;

            _tp1Row.SetIdle("TP1");
            _tp2Row.SetIdle("TP2");
            _tpRow.SetIdle("TP");
            _slRow.SetIdle("SL");

            _btnBreakeven.IsEnabled = _btnCloseNow.IsEnabled = false;
            _btnPartial.IsEnabled   = _btnTrailing.IsEnabled = false;
            _btnBuyNow.IsEnabled    = _btnSellNow.IsEnabled  = false;
            _btnCancelEntry.IsEnabled = false;
        }

        public void RefreshLocalization()
        {
            _hdrActions.Text     = NY930Localization.T("trade.section.management");
            _hdrManagement.Text  = NY930Localization.T("trade.section.management");
            _btnBreakeven.Content = NY930Localization.T("trade.action.breakeven");
            _btnCloseNow.Content  = NY930Localization.T("trade.action.close_now");
            _btnPartial.Content   = NY930Localization.T("trade.action.partial");
            _btnTrailing.Content  = NY930Localization.T("trade.action.trailing");
            _btnBuyNow.Content    = NY930Localization.T("or.buy_now");
            _btnSellNow.Content   = NY930Localization.T("or.sell_now");
            _btnCancelEntry.Content = NY930Localization.T("or.cancel");
            _btnEditParams.Content  = NY930Localization.T("params.title");

            var current = NY930Bridge.GetHedge();
            if (current != null) Render(current);
            else                 RenderEmpty();
        }

        public void Dispose()
        {
            NY930Bridge.HedgeChanged -= OnSnapshot;
            if (_ticker != null) { _ticker.Stop(); _ticker = null; }
        }
    }
}
