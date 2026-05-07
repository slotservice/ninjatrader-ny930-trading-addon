// ============================================================
//  NY930HedgeView — redesigned (v1.1)
// ------------------------------------------------------------
//  Same visual structure as the Open Range view but tailored
//  for the Hedge strategy: single-side direct entry with full
//  position management. No range / spread cards — instead the
//  status banner shows the current direction badge.
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

        // Status block
        private TextBlock _statusBanner;
        private TextBlock _countdownLabel;
        private TextBlock _instrumentText;
        private Border    _directionBadge;
        private TextBlock _directionBadgeText;

        // Hero PnL
        private NY930Theme.BigPnLDisplay _bigPnL;
        private Border _pillCurrency;
        private Border _pillDuration;
        private Border _pillContracts;
        private TextBlock _pillCurrencyText;
        private TextBlock _pillDurationText;
        private TextBlock _pillContractsText;

        // Progress cards
        private NY930Theme.TpProgressCard _tp1Card;
        private NY930Theme.TpProgressCard _tp2Card;
        private NY930Theme.TpProgressCard _tpCard;
        private NY930Theme.TpProgressCard _slCard;

        // Action buttons
        private Button   _btnBreakeven;
        private Button   _btnCloseNow;
        private Button   _btnPartial;
        private Button   _btnTrailing;
        private TextBox  _partialQty;
        private Button   _btnBuyNow;
        private Button   _btnSellNow;
        private Button   _btnCancelEntry;
        private Button   _btnEditParams;

        // Section headers
        private TextBlock _hdrTargets;
        private TextBlock _hdrStop;
        private TextBlock _hdrManagement;

        private System.Windows.Threading.DispatcherTimer _ticker;
        private DateTime _entryTime;

        public NY930HedgeView(NY930ShellView shell)
        {
            _shell = shell;
            Background = NY930Theme.BgBaseBrush;

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Children.Add(scroll);

            var root = new StackPanel { Margin = new Thickness(14, 12, 14, 14) };
            scroll.Content = root;

            BuildStatus(root);
            BuildHeroPnL(root);
            BuildTargets(root);
            BuildManagement(root);
            BuildEntryActions(root);
            BuildParamsLink(root);

            NY930Bridge.HedgeChanged += OnSnapshot;
            var current = NY930Bridge.GetHedge();
            if (current != null) OnSnapshot(current);
            else                  RefreshNoStrategy();

            _ticker = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _ticker.Tick += (s, e) => RefreshCountdown();
            _ticker.Start();
        }

        // ── Status (with direction badge) ───────────────────────
        private void BuildStatus(StackPanel root)
        {
            var box = new StackPanel();
            _statusBanner = new TextBlock
            {
                Text       = NY930Localization.T("status.no_chart"),
                FontSize   = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = NY930Theme.WarnAmberBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _countdownLabel = new TextBlock
            {
                Text       = "",
                FontSize   = 18,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = NY930Theme.GoldBrightBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 2, 0, 4)
            };
            _instrumentText = new TextBlock
            {
                Text       = "—",
                FontSize   = 10,
                Foreground = NY930Theme.TextLowBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 0, 0, 6)
            };

            _directionBadgeText = new TextBlock
            {
                Text       = NY930Localization.T("hedge.none").ToUpperInvariant(),
                FontSize   = 14,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.TextLowBrush
            };
            _directionBadge = new Border
            {
                Background      = NY930Theme.BrushAlpha(NY930Theme.GoldDim, 0x22),
                BorderBrush     = NY930Theme.BrushAlpha(NY930Theme.GoldDim, 0x55),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(12, 4, 12, 4),
                HorizontalAlignment = HorizontalAlignment.Center,
                Child           = _directionBadgeText
            };

            box.Children.Add(_statusBanner);
            box.Children.Add(_countdownLabel);
            box.Children.Add(_instrumentText);
            box.Children.Add(_directionBadge);
            root.Children.Add(NY930Theme.Panel(box, new Thickness(0, 0, 0, 10)));
        }

        // ── Hero PnL + pills ────────────────────────────────────
        private void BuildHeroPnL(StackPanel root)
        {
            var stack = new StackPanel();
            _bigPnL = new NY930Theme.BigPnLDisplay();
            stack.Children.Add(_bigPnL);

            var pills = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 6, 0, 0)
            };
            _pillCurrency  = NY930Theme.Pill("$0.00",      NY930Theme.CyanAccent);
            _pillDuration  = NY930Theme.Pill("00:00",      NY930Theme.GoldDim);
            _pillContracts = NY930Theme.Pill("0 contracts",NY930Theme.GoldDim);

            _pillCurrencyText  = (TextBlock)_pillCurrency.Child;
            _pillDurationText  = (TextBlock)_pillDuration.Child;
            _pillContractsText = (TextBlock)_pillContracts.Child;

            pills.Children.Add(_pillCurrency);
            pills.Children.Add(_pillDuration);
            pills.Children.Add(_pillContracts);
            stack.Children.Add(pills);

            root.Children.Add(NY930Theme.Panel(stack, new Thickness(0, 0, 0, 10)));
        }

        // ── Targets + stop ──────────────────────────────────────
        private void BuildTargets(StackPanel root)
        {
            var stack = new StackPanel();
            _hdrTargets = NY930Theme.SectionHeader(NY930Localization.T("trade.section.targets"));
            stack.Children.Add(_hdrTargets);

            _tp1Card = new NY930Theme.TpProgressCard(NY930Localization.T("trade.tp1.label"));
            _tp2Card = new NY930Theme.TpProgressCard(NY930Localization.T("trade.tp2.label"));
            _tpCard  = new NY930Theme.TpProgressCard(NY930Localization.T("trade.tp.label"));
            stack.Children.Add(_tp1Card);
            stack.Children.Add(_tp2Card);
            stack.Children.Add(_tpCard);

            _hdrStop = NY930Theme.SectionHeader(NY930Localization.T("trade.section.stops"));
            stack.Children.Add(_hdrStop);

            _slCard = new NY930Theme.TpProgressCard(NY930Localization.T("trade.sl.label"), isSlSide: true);
            stack.Children.Add(_slCard);

            root.Children.Add(NY930Theme.Panel(stack, new Thickness(0, 0, 0, 10)));
        }

        // ── Position management ─────────────────────────────────
        private void BuildManagement(StackPanel root)
        {
            var stack = new StackPanel();
            _hdrManagement = NY930Theme.SectionHeader(NY930Localization.T("trade.section.management"));
            stack.Children.Add(_hdrManagement);

            var row1 = new Grid();
            row1.ColumnDefinitions.Add(new ColumnDefinition());
            row1.ColumnDefinitions.Add(new ColumnDefinition());
            _btnBreakeven = NY930Theme.BigActionButton(NY930Localization.T("trade.action.breakeven"), NY930Theme.BlueAccent, false);
            _btnBreakeven.Margin = new Thickness(0, 0, 4, 0);
            _btnBreakeven.Click += (s, e) => Send(NY930ActionType.HedgeBreakeven, 0);
            Grid.SetColumn(_btnBreakeven, 0);
            _btnCloseNow = NY930Theme.BigActionButton(NY930Localization.T("trade.action.close_now"), NY930Theme.ShortRed, false);
            _btnCloseNow.Margin = new Thickness(4, 0, 0, 0);
            _btnCloseNow.Click += (s, e) => Send(NY930ActionType.HedgeFlatten, 0);
            Grid.SetColumn(_btnCloseNow, 1);
            row1.Children.Add(_btnBreakeven);
            row1.Children.Add(_btnCloseNow);
            stack.Children.Add(row1);

            var row2 = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _btnPartial = NY930Theme.BigActionButton(NY930Localization.T("trade.action.partial"), NY930Theme.GoldBright, false);
            _btnPartial.Margin = new Thickness(0, 0, 4, 0);
            _btnPartial.Click += (s, e) =>
            {
                int n;
                if (int.TryParse(_partialQty.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n > 0)
                    Send(NY930ActionType.HedgePartialClose, n);
            };
            Grid.SetColumn(_btnPartial, 0);

            _partialQty = NY930Theme.InputBox(50);
            _partialQty.Text = "1";
            _partialQty.Margin = new Thickness(2, 0, 6, 0);
            _partialQty.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_partialQty, 1);

            _btnTrailing = NY930Theme.BigActionButton(NY930Localization.T("trade.action.trailing"), NY930Theme.LongGreen, false);
            _btnTrailing.Click += (s, e) => Send(NY930ActionType.HedgeTrailingTrigger, 0);
            Grid.SetColumn(_btnTrailing, 2);

            row2.Children.Add(_btnPartial);
            row2.Children.Add(_partialQty);
            row2.Children.Add(_btnTrailing);
            stack.Children.Add(row2);

            root.Children.Add(NY930Theme.Panel(stack, new Thickness(0, 0, 0, 10)));
        }

        // ── Manual entry / cancel pending ───────────────────────
        private void BuildEntryActions(StackPanel root)
        {
            var stack = new StackPanel();
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition());
            _btnBuyNow  = NY930Theme.BigActionButton(NY930Localization.T("or.buy_now"),  NY930Theme.LongGreen, true);
            _btnSellNow = NY930Theme.BigActionButton(NY930Localization.T("or.sell_now"), NY930Theme.ShortRed,  true);
            _btnBuyNow.Margin  = new Thickness(0, 0, 4, 0);
            _btnSellNow.Margin = new Thickness(4, 0, 0, 0);
            _btnBuyNow.Click  += (s, e) => Send(NY930ActionType.HedgeBuyNow,  0);
            _btnSellNow.Click += (s, e) => Send(NY930ActionType.HedgeSellNow, 0);
            Grid.SetColumn(_btnBuyNow,  0);
            Grid.SetColumn(_btnSellNow, 1);
            row.Children.Add(_btnBuyNow);
            row.Children.Add(_btnSellNow);
            stack.Children.Add(row);

            _btnCancelEntry = NY930Theme.BigActionButton(NY930Localization.T("or.cancel"), NY930Theme.WarnAmber, false);
            _btnCancelEntry.Margin = new Thickness(0, 6, 0, 0);
            _btnCancelEntry.Click += (s, e) => Send(NY930ActionType.HedgeCancelEntry, 0);
            stack.Children.Add(_btnCancelEntry);

            root.Children.Add(NY930Theme.Panel(stack, new Thickness(0, 0, 0, 10)));
        }

        private void BuildParamsLink(StackPanel root)
        {
            _btnEditParams = NY930Theme.OutlineButton(NY930Localization.T("params.title"));
            _btnEditParams.Margin = new Thickness(0, 0, 0, 6);
            _btnEditParams.Click += (s, e) => _shell.Show(new NY930ParametersView(_shell, isOpenRange: false));
            root.Children.Add(_btnEditParams);
        }

        // ── Bridge plumbing ─────────────────────────────────────
        private void Send(NY930ActionType type, int arg)
        {
            NY930Bridge.RequestHedgeAction(new NY930Action { Type = type, IntArg = arg });
        }

        private void OnSnapshot(NY930HedgeSnapshot snap)
        {
            Dispatcher.InvokeAsync(() => Render(snap));
        }

        private void Render(NY930HedgeSnapshot snap)
        {
            if (snap == null) { RefreshNoStrategy(); return; }

            _entryTime = snap.EntryTime;
            _instrumentText.Text = snap.Instrument ?? "—";

            // Direction badge
            if (snap.Direction == "Long")
            {
                _directionBadgeText.Text = "▲ " + NY930Localization.T("hedge.long").ToUpperInvariant();
                _directionBadgeText.Foreground = NY930Theme.LongGreenBrush;
                _directionBadge.Background  = NY930Theme.BrushAlpha(NY930Theme.LongGreen, 0x22);
                _directionBadge.BorderBrush = NY930Theme.BrushAlpha(NY930Theme.LongGreen, 0x66);
            }
            else if (snap.Direction == "Short")
            {
                _directionBadgeText.Text = "▼ " + NY930Localization.T("hedge.short").ToUpperInvariant();
                _directionBadgeText.Foreground = NY930Theme.ShortRedBrush;
                _directionBadge.Background  = NY930Theme.BrushAlpha(NY930Theme.ShortRed, 0x22);
                _directionBadge.BorderBrush = NY930Theme.BrushAlpha(NY930Theme.ShortRed, 0x66);
            }
            else
            {
                _directionBadgeText.Text = NY930Localization.T("hedge.none").ToUpperInvariant();
                _directionBadgeText.Foreground = NY930Theme.TextLowBrush;
                _directionBadge.Background  = NY930Theme.BrushAlpha(NY930Theme.GoldDim, 0x22);
                _directionBadge.BorderBrush = NY930Theme.BrushAlpha(NY930Theme.GoldDim, 0x55);
            }

            // Status text
            if (snap.InPosition)
            {
                _statusBanner.Text       = snap.Direction == "Long"
                    ? NY930Localization.T("status.in_long")
                    : NY930Localization.T("status.in_short");
                _statusBanner.Foreground = snap.Direction == "Long"
                    ? NY930Theme.LongGreenBrush
                    : NY930Theme.ShortRedBrush;
            }
            else if (snap.SessionDone)
            {
                _statusBanner.Text       = NY930Localization.T("status.session_done");
                _statusBanner.Foreground = NY930Theme.TextLowBrush;
            }
            else
            {
                _statusBanner.Text       = "";
                _statusBanner.Foreground = NY930Theme.TextLowBrush;
            }

            // Hero PnL — currency from the strategy
            string side     = snap.Direction;
            double currency = snap.UnrealizedCurrency;
            _bigPnL.Update(currency, snap.UnrealizedTicks, side);

            // Pills
            _pillCurrencyText.Text = (snap.UnrealizedTicks >= 0 ? "+" : "") + currency.ToString("C", CultureInfo.CurrentCulture);
            _pillCurrency.Visibility  = snap.InPosition ? Visibility.Visible : Visibility.Collapsed;
            if (snap.TradeStartTime != DateTime.MinValue && snap.InPosition)
            {
                var d = DateTime.Now - snap.TradeStartTime;
                _pillDurationText.Text = ((int)d.TotalMinutes).ToString("D2") + ":" + d.Seconds.ToString("D2");
                _pillDuration.Visibility = Visibility.Visible;
            }
            else _pillDuration.Visibility = Visibility.Collapsed;
            _pillContractsText.Text = snap.ContractsRemaining + " / " + Math.Max(1, snap.Quantity);
            _pillContracts.Visibility = snap.InPosition ? Visibility.Visible : Visibility.Collapsed;

            // Targets
            UpdateTpCard(_tp1Card, NY930Localization.T("trade.tp1.label"), snap.P1Price, snap.Partial1Done, snap.InPosition);
            UpdateTpCard(_tp2Card, NY930Localization.T("trade.tp2.label"), snap.P2Price, snap.Partial2Done, snap.InPosition);
            UpdateTpCard(_tpCard,  NY930Localization.T("trade.tp.label"),  snap.TpPrice, false,             snap.InPosition);
            UpdateSlCard(snap);

            // Buttons
            bool canEnter = !snap.InPosition && !snap.SessionDone;
            _btnBreakeven.IsEnabled  = snap.InPosition;
            _btnCloseNow.IsEnabled   = snap.InPosition;
            _btnPartial.IsEnabled    = snap.InPosition;
            _btnTrailing.IsEnabled   = snap.InPosition;
            _btnBuyNow.IsEnabled     = canEnter;
            _btnSellNow.IsEnabled    = canEnter;
            _btnCancelEntry.IsEnabled = !canEnter && !snap.InPosition;

            // Result screen takes over on close
            if (snap.LastResult != null && !snap.InPosition && snap.SessionDone
                && _shell != null && _shell.CurrentViewIs<NY930HedgeView>())
            {
                _shell.Show(new NY930ResultView(_shell, snap.LastResult));
            }

            RefreshCountdown();
        }

        private void UpdateTpCard(NY930Theme.TpProgressCard card, string baseLabel,
                                  double price, bool done, bool inPosition)
        {
            if (price <= 0)
            {
                card.SetState(NY930Theme.TpState.Pending,
                    string.Format(NY930Localization.T("trade.tp.pending"), baseLabel),
                    "");
                return;
            }
            if (done)
            {
                card.SetState(NY930Theme.TpState.Done,
                    string.Format(NY930Localization.T("trade.tp.reached"), baseLabel),
                    price.ToString("F5", CultureInfo.InvariantCulture));
            }
            else if (inPosition)
            {
                card.SetState(NY930Theme.TpState.Active,
                    string.Format(NY930Localization.T("trade.tp.in_progress"), baseLabel),
                    price.ToString("F5", CultureInfo.InvariantCulture));
            }
            else
            {
                card.SetState(NY930Theme.TpState.Pending,
                    string.Format(NY930Localization.T("trade.tp.pending"), baseLabel),
                    price.ToString("F5", CultureInfo.InvariantCulture));
            }
        }

        private void UpdateSlCard(NY930HedgeSnapshot snap)
        {
            if (snap.SlPrice <= 0)
            {
                _slCard.SetState(NY930Theme.TpState.Pending, NY930Localization.T("trade.sl.label"), "");
                return;
            }

            if (snap.LastResult != null && snap.LastResult.SlHit && !snap.InPosition)
            {
                _slCard.SetState(NY930Theme.TpState.Failed, NY930Localization.T("trade.sl.hit"),
                    snap.SlPrice.ToString("F5", CultureInfo.InvariantCulture));
                return;
            }

            if (snap.InPosition && snap.LastPrice > 0 && snap.TickSize > 0)
            {
                double distTicks = snap.Direction == "Long"
                    ? (snap.LastPrice - snap.SlPrice) / snap.TickSize
                    : (snap.SlPrice - snap.LastPrice) / snap.TickSize;
                if (distTicks <= 5)
                {
                    _slCard.SetState(NY930Theme.TpState.Active,
                        NY930Localization.T("trade.sl.danger"),
                        snap.SlPrice.ToString("F5", CultureInfo.InvariantCulture));
                    return;
                }
            }

            _slCard.SetState(NY930Theme.TpState.Pending, NY930Localization.T("trade.sl.label"),
                snap.SlPrice.ToString("F5", CultureInfo.InvariantCulture));
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
            if (now >= _entryTime)
            {
                _countdownLabel.Text = "";
                return;
            }
            TimeSpan remaining = _entryTime - now;
            string txt = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)remaining.TotalHours, remaining.Minutes, remaining.Seconds);
            _countdownLabel.Text = string.Format(
                NY930Localization.T("status.countdown"), txt);
        }

        private void RefreshNoStrategy()
        {
            _statusBanner.Text       = NY930Localization.T("status.no_chart");
            _statusBanner.Foreground = NY930Theme.WarnAmberBrush;
            _countdownLabel.Text     = "";
            _instrumentText.Text     = "—";
            _directionBadgeText.Text = NY930Localization.T("hedge.none").ToUpperInvariant();
            _bigPnL.Update(0, 0, "None");
            _pillCurrency.Visibility = _pillDuration.Visibility = _pillContracts.Visibility = Visibility.Collapsed;
            _tp1Card.SetState(NY930Theme.TpState.Pending, NY930Localization.T("trade.tp1.label"), "");
            _tp2Card.SetState(NY930Theme.TpState.Pending, NY930Localization.T("trade.tp2.label"), "");
            _tpCard.SetState(NY930Theme.TpState.Pending,  NY930Localization.T("trade.tp.label"),  "");
            _slCard.SetState(NY930Theme.TpState.Pending,  NY930Localization.T("trade.sl.label"),  "");
            _btnBreakeven.IsEnabled = _btnCloseNow.IsEnabled = false;
            _btnPartial.IsEnabled   = _btnTrailing.IsEnabled = false;
            _btnBuyNow.IsEnabled    = _btnSellNow.IsEnabled  = false;
            _btnCancelEntry.IsEnabled = false;
        }

        public void RefreshLocalization()
        {
            _hdrTargets.Text    = NY930Localization.T("trade.section.targets");
            _hdrStop.Text       = NY930Localization.T("trade.section.stops");
            _hdrManagement.Text = NY930Localization.T("trade.section.management");

            _btnBreakeven.Content = NY930Localization.T("trade.action.breakeven");
            _btnCloseNow.Content  = NY930Localization.T("trade.action.close_now");
            _btnPartial.Content   = NY930Localization.T("trade.action.partial");
            _btnTrailing.Content  = NY930Localization.T("trade.action.trailing");
            _btnBuyNow.Content    = NY930Localization.T("or.buy_now");
            _btnSellNow.Content   = NY930Localization.T("or.sell_now");
            _btnCancelEntry.Content = NY930Localization.T("or.cancel");
            _btnEditParams.Content = NY930Localization.T("params.title");

            var current = NY930Bridge.GetHedge();
            if (current != null) Render(current);
            else                  RefreshNoStrategy();
        }

        public void Dispose()
        {
            NY930Bridge.HedgeChanged -= OnSnapshot;
            if (_ticker != null) { _ticker.Stop(); _ticker = null; }
        }
    }
}
