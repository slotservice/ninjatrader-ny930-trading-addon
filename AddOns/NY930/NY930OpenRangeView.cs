// ============================================================
//  NY930OpenRangeView — redesigned (v1.1)
// ------------------------------------------------------------
//  Layout matches the client's reference flow image:
//
//    ┌────────────────────────────────┐
//    │ Status banner / countdown      │
//    ├────────────────────────────────┤
//    │ +$312.50  ▲ LONG               │  ← BigPnLDisplay
//    │ +25 ticks                      │
//    │ [+$75] [06:44] [10 ctos]       │  ← stat pills
//    ├────────────────────────────────┤
//    │ TARGETS                        │
//    │ ✓ TP1 reached      +25t  +$75  │  ← TpProgressCard
//    │ ● TP2 in progress  +15/25 +$45 │
//    │ ○ TP  pending      +60t        │
//    ├────────────────────────────────┤
//    │ STOP                           │
//    │ ○ SL  -90 ticks                │
//    ├────────────────────────────────┤
//    │ BUY STOP   7263.25  Spread 80t │  ← live read-out
//    │ SELL STOP  7243.75             │
//    ├────────────────────────────────┤
//    │ MOVE BOTH ▲▼   |   SPREAD + −  │  ← step cards (kept)
//    ├────────────────────────────────┤
//    │ POSITION MANAGEMENT            │
//    │ [BREAKEVEN] [CLOSE NOW]        │
//    │ [PARTIAL ▾] [TRAILING STOP]    │
//    ├────────────────────────────────┤
//    │ [BUY NOW] [SELL NOW]           │
//    │ [CANCEL ORDERS]                │
//    └────────────────────────────────┘
//
//  When the trade closes, the result view (NY930ResultView)
//  takes over via the shell.
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
    public sealed class NY930OpenRangeView : Grid, INY930Localizable, IDisposable
    {
        private readonly NY930ShellView _shell;

        // Status block
        private TextBlock _statusBanner;
        private TextBlock _countdownLabel;

        // Hero PnL
        private NY930Theme.BigPnLDisplay _bigPnL;
        private StackPanel _pillRow;
        private TextBlock  _pillCurrencyText;
        private TextBlock  _pillDurationText;
        private TextBlock  _pillContractsText;
        private Border     _pillCurrency;
        private Border     _pillDuration;
        private Border     _pillContracts;

        // Progress cards
        private NY930Theme.TpProgressCard _tp1Card;
        private NY930Theme.TpProgressCard _tp2Card;
        private NY930Theme.TpProgressCard _tpCard;
        private NY930Theme.TpProgressCard _slCard;

        // Live read-out
        private TextBlock _buyPriceText;
        private TextBlock _sellPriceText;
        private TextBlock _spreadText;
        private Border    _liveBox;

        // Step cards
        private TextBlock _moveValue;
        private TextBlock _spreadValue;
        private int       _moveStep   = 5;
        private int       _spreadStep = 10;

        // Action buttons
        private Button   _btnBreakeven;
        private Button   _btnCloseNow;
        private Button   _btnPartial;
        private Button   _btnTrailing;
        private TextBox  _partialQty;
        private Button   _btnBuyNow;
        private Button   _btnSellNow;
        private Button   _btnCancel;

        // Parameters quick link
        private Button _btnEditParams;

        // Section headers cached for live language refresh
        private TextBlock _hdrTargets;
        private TextBlock _hdrStop;
        private TextBlock _hdrManagement;
        private TextBlock _hdrLive;

        // Countdown timer
        private System.Windows.Threading.DispatcherTimer _ticker;
        private DateTime _entryTime;

        public NY930OpenRangeView(NY930ShellView shell)
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
            BuildLiveReadout(root);
            BuildStepCards(root);
            BuildManagement(root);
            BuildEntryActions(root);
            BuildParamsLink(root);

            // Wire up to bridge
            NY930Bridge.OpenRangeChanged += OnSnapshot;
            var current = NY930Bridge.GetOpenRange();
            if (current != null) OnSnapshot(current);
            else                  RefreshNoStrategy();

            // Countdown ticker (1 Hz, only renders, never touches order state)
            _ticker = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _ticker.Tick += (s, e) => RefreshCountdown();
            _ticker.Start();
        }

        // ── Status ──────────────────────────────────────────────
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
                Margin     = new Thickness(0, 2, 0, 0)
            };
            box.Children.Add(_statusBanner);
            box.Children.Add(_countdownLabel);
            root.Children.Add(NY930Theme.Panel(box, new Thickness(0, 0, 0, 10)));
        }

        // ── Hero PnL + stat pills ───────────────────────────────
        private void BuildHeroPnL(StackPanel root)
        {
            var stack = new StackPanel();

            _bigPnL = new NY930Theme.BigPnLDisplay();
            stack.Children.Add(_bigPnL);

            _pillRow = new StackPanel
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

            _pillRow.Children.Add(_pillCurrency);
            _pillRow.Children.Add(_pillDuration);
            _pillRow.Children.Add(_pillContracts);
            stack.Children.Add(_pillRow);

            root.Children.Add(NY930Theme.Panel(stack, new Thickness(0, 0, 0, 10)));
        }

        // ── Targets section ─────────────────────────────────────
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

        // ── Live BUY STOP / SELL STOP read-out ──────────────────
        private void BuildLiveReadout(StackPanel root)
        {
            var stack = new StackPanel();
            _hdrLive = NY930Theme.SectionHeader(NY930Localization.T("nav.openrange").ToUpperInvariant());
            stack.Children.Add(_hdrLive);

            _buyPriceText  = MakePriceRow(stack, NY930Localization.T("or.buystop"),  NY930Theme.LongGreen);
            _sellPriceText = MakePriceRow(stack, NY930Localization.T("or.sellstop"), NY930Theme.ShortRed);

            _spreadText = new TextBlock
            {
                Text       = NY930Localization.T("or.spread") + ": —",
                FontSize   = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = NY930Theme.TextLowBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 4, 0, 0)
            };
            stack.Children.Add(_spreadText);

            _liveBox = NY930Theme.Panel(stack, new Thickness(0, 0, 0, 10));
            root.Children.Add(_liveBox);
        }

        private TextBlock MakePriceRow(StackPanel parent, string label, Color tint)
        {
            var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var badge = new Border
            {
                Background      = NY930Theme.BrushAlpha(tint, 0x22),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(8, 3, 8, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text       = label,
                    FontSize   = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(tint)
                }
            };
            Grid.SetColumn(badge, 0);

            var price = new TextBlock
            {
                Text       = "—",
                FontSize   = 14,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = new SolidColorBrush(tint),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(price, 1);

            grid.Children.Add(badge);
            grid.Children.Add(price);
            parent.Children.Add(grid);
            return price;
        }

        // ── Move / Spread step cards ────────────────────────────
        private void BuildStepCards(StackPanel root)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            var moveCard = BuildStepCard(
                NY930Localization.T("or.move.title"),
                NY930Localization.T("or.move.sub"),
                NY930Theme.GoldBright,
                "▲", "▼",
                () => Send(NY930ActionType.OpenRangeMoveBoth, +_moveStep),
                () => Send(NY930ActionType.OpenRangeMoveBoth, -_moveStep),
                v => _moveStep = v,
                _moveStep,
                out _moveValue);
            moveCard.Margin = new Thickness(0, 0, 4, 0);
            Grid.SetColumn(moveCard, 0);

            var spreadCard = BuildStepCard(
                NY930Localization.T("or.spread.title"),
                NY930Localization.T("or.spread.sub"),
                NY930Theme.LongGreen,
                "+", "−",
                () => Send(NY930ActionType.OpenRangeAdjustSpread, +_spreadStep),
                () => Send(NY930ActionType.OpenRangeAdjustSpread, -_spreadStep),
                v => _spreadStep = v,
                _spreadStep,
                out _spreadValue);
            spreadCard.Margin = new Thickness(4, 0, 0, 0);
            Grid.SetColumn(spreadCard, 1);

            grid.Children.Add(moveCard);
            grid.Children.Add(spreadCard);
            root.Children.Add(grid);
        }

        private Border BuildStepCard(
            string title, string sub, Color accent,
            string upGlyph, string downGlyph,
            Action onUp, Action onDown,
            Action<int> onStep, int defStep,
            out TextBlock valueOut)
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(accent),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text = sub,
                FontSize = 9,
                Foreground = NY930Theme.TextLowBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var btnUp = NY930Theme.BigActionButton(upGlyph, accent, false);
            btnUp.FontSize = 18;
            btnUp.Click += (s, e) => onUp();
            stack.Children.Add(btnUp);

            valueOut = new TextBlock
            {
                Text       = defStep.ToString(),
                FontSize   = 22,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.TextHiBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 6, 0, 0)
            };
            stack.Children.Add(valueOut);
            stack.Children.Add(new TextBlock
            {
                Text = NY930Localization.T("common.ticks").ToUpperInvariant(),
                FontSize = 8,
                Foreground = NY930Theme.TextLowBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            var btnDown = NY930Theme.BigActionButton(downGlyph, accent, false);
            btnDown.FontSize = 18;
            btnDown.Margin   = new Thickness(0, 6, 0, 0);
            btnDown.Click += (s, e) => onDown();
            stack.Children.Add(btnDown);

            // Step chips
            int[] steps = { 1, 5, 10, 25 };
            var chips = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 8, 0, 0)
            };
            Button[] chipBtns = new Button[steps.Length];
            // Local copy for the lambda below (out-param can't be captured).
            TextBlock valueLocal = valueOut;
            for (int i = 0; i < steps.Length; i++)
            {
                int sv = steps[i]; int idx = i;
                var chip = new Button
                {
                    Content    = sv.ToString(),
                    FontSize   = 9,
                    FontWeight = FontWeights.Bold,
                    Padding    = new Thickness(8, 2, 8, 2),
                    Margin     = new Thickness(2),
                    Background = sv == defStep ? new SolidColorBrush(accent) : NY930Theme.BgInputBrush,
                    Foreground = sv == defStep ? NY930Theme.BgBaseBrush : NY930Theme.TextMidBrush,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                chipBtns[i] = chip;
                int captured = sv;
                chip.Click += (s, e) =>
                {
                    onStep(captured);
                    valueLocal.Text = captured.ToString();
                    for (int j = 0; j < chipBtns.Length; j++)
                    {
                        bool sel = j == idx;
                        chipBtns[j].Background = sel ? new SolidColorBrush(accent) : NY930Theme.BgInputBrush;
                        chipBtns[j].Foreground = sel ? NY930Theme.BgBaseBrush : NY930Theme.TextMidBrush;
                    }
                };
                chips.Children.Add(chip);
            }
            stack.Children.Add(chips);

            return NY930Theme.Card(stack, new SolidColorBrush(accent));
        }

        // ── Position management section ─────────────────────────
        private void BuildManagement(StackPanel root)
        {
            var stack = new StackPanel();
            _hdrManagement = NY930Theme.SectionHeader(NY930Localization.T("trade.section.management"));
            stack.Children.Add(_hdrManagement);

            // Row 1: BREAKEVEN | CLOSE NOW
            var row1 = new Grid();
            row1.ColumnDefinitions.Add(new ColumnDefinition());
            row1.ColumnDefinitions.Add(new ColumnDefinition());
            _btnBreakeven = NY930Theme.BigActionButton(NY930Localization.T("trade.action.breakeven"), NY930Theme.BlueAccent, false);
            _btnBreakeven.Margin = new Thickness(0, 0, 4, 0);
            _btnBreakeven.Click += (s, e) => Send(NY930ActionType.OpenRangeBreakeven, 0);
            Grid.SetColumn(_btnBreakeven, 0);
            _btnCloseNow = NY930Theme.BigActionButton(NY930Localization.T("trade.action.close_now"), NY930Theme.ShortRed, false);
            _btnCloseNow.Margin = new Thickness(4, 0, 0, 0);
            _btnCloseNow.Click += (s, e) => Send(NY930ActionType.OpenRangeFlatten, 0);
            Grid.SetColumn(_btnCloseNow, 1);
            row1.Children.Add(_btnBreakeven);
            row1.Children.Add(_btnCloseNow);
            stack.Children.Add(row1);

            // Row 2: PARTIAL CLOSE [qty] | TRAILING STOP
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
                    Send(NY930ActionType.OpenRangePartialClose, n);
            };
            Grid.SetColumn(_btnPartial, 0);

            _partialQty = NY930Theme.InputBox(50);
            _partialQty.Text = "1";
            _partialQty.Margin = new Thickness(2, 0, 6, 0);
            _partialQty.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_partialQty, 1);

            _btnTrailing = NY930Theme.BigActionButton(NY930Localization.T("trade.action.trailing"), NY930Theme.LongGreen, false);
            _btnTrailing.Click += (s, e) => Send(NY930ActionType.OpenRangeTrailingTrigger, 0);
            Grid.SetColumn(_btnTrailing, 2);

            row2.Children.Add(_btnPartial);
            row2.Children.Add(_partialQty);
            row2.Children.Add(_btnTrailing);
            stack.Children.Add(row2);

            root.Children.Add(NY930Theme.Panel(stack, new Thickness(0, 0, 0, 10)));
        }

        // ── Manual entry / cancel ───────────────────────────────
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
            _btnBuyNow.Click  += (s, e) => Send(NY930ActionType.OpenRangeBuyNow,  0);
            _btnSellNow.Click += (s, e) => Send(NY930ActionType.OpenRangeSellNow, 0);
            Grid.SetColumn(_btnBuyNow,  0);
            Grid.SetColumn(_btnSellNow, 1);
            row.Children.Add(_btnBuyNow);
            row.Children.Add(_btnSellNow);
            stack.Children.Add(row);

            _btnCancel = NY930Theme.BigActionButton(NY930Localization.T("or.cancel"), NY930Theme.WarnAmber, false);
            _btnCancel.Margin = new Thickness(0, 6, 0, 0);
            _btnCancel.Click += (s, e) => Send(NY930ActionType.OpenRangeCancelAll, 0);
            stack.Children.Add(_btnCancel);

            root.Children.Add(NY930Theme.Panel(stack, new Thickness(0, 0, 0, 10)));
        }

        private void BuildParamsLink(StackPanel root)
        {
            _btnEditParams = NY930Theme.OutlineButton(NY930Localization.T("params.title"));
            _btnEditParams.Margin = new Thickness(0, 0, 0, 6);
            _btnEditParams.Click += (s, e) => _shell.Show(new NY930ParametersView(_shell, isOpenRange: true));
            root.Children.Add(_btnEditParams);
        }

        // ────────────────────────────────────────────────────────
        //  Bridge plumbing
        // ────────────────────────────────────────────────────────
        private void Send(NY930ActionType type, int arg)
        {
            NY930Bridge.RequestOpenRangeAction(new NY930Action { Type = type, IntArg = arg });
        }

        private void OnSnapshot(NY930OpenRangeSnapshot snap)
        {
            Dispatcher.InvokeAsync(() => Render(snap));
        }

        private void Render(NY930OpenRangeSnapshot snap)
        {
            if (snap == null) { RefreshNoStrategy(); return; }

            _entryTime = snap.EntryTime;

            // Status banner
            if (snap.InLong)
            {
                _statusBanner.Text       = NY930Localization.T("status.in_long");
                _statusBanner.Foreground = NY930Theme.LongGreenBrush;
            }
            else if (snap.InShort)
            {
                _statusBanner.Text       = NY930Localization.T("status.in_short");
                _statusBanner.Foreground = NY930Theme.ShortRedBrush;
            }
            else if (snap.LongEntryWorking || snap.ShortEntryWorking)
            {
                _statusBanner.Text       = NY930Localization.T("status.active");
                _statusBanner.Foreground = NY930Theme.GoldBrightBrush;
            }
            else if (snap.SessionDone)
            {
                _statusBanner.Text       = NY930Localization.T("status.session_done");
                _statusBanner.Foreground = NY930Theme.TextLowBrush;
            }
            else
            {
                _statusBanner.Text       = ""; // countdown takes over
                _statusBanner.Foreground = NY930Theme.TextLowBrush;
            }

            // PnL hero — currency comes accurate from the strategy
            string side     = snap.InLong ? "Long" : (snap.InShort ? "Short" : "None");
            double currency = snap.UnrealizedCurrency;
            _bigPnL.Update(currency, snap.UnrealizedTicks, side);

            // Pills
            _pillCurrencyText.Text = (snap.UnrealizedTicks >= 0 ? "+" : "") + currency.ToString("C", CultureInfo.CurrentCulture);
            _pillCurrency.Visibility = snap.InLong || snap.InShort ? Visibility.Visible : Visibility.Collapsed;

            if (snap.TradeStartTime != DateTime.MinValue && (snap.InLong || snap.InShort))
            {
                var d = DateTime.Now - snap.TradeStartTime;
                _pillDurationText.Text = ((int)d.TotalMinutes).ToString("D2") + ":" + d.Seconds.ToString("D2");
                _pillDuration.Visibility = Visibility.Visible;
            }
            else _pillDuration.Visibility = Visibility.Collapsed;

            _pillContractsText.Text = snap.ContractsRemaining + " / " + Math.Max(1, snap.Quantity);
            _pillContracts.Visibility = (snap.InLong || snap.InShort) ? Visibility.Visible : Visibility.Collapsed;

            // Targets
            UpdateTpCard(_tp1Card, NY930Localization.T("trade.tp1.label"), snap.P1Price, snap.Partial1Done, snap, 1);
            UpdateTpCard(_tp2Card, NY930Localization.T("trade.tp2.label"), snap.P2Price, snap.Partial2Done, snap, 2);
            UpdateTpCard(_tpCard,  NY930Localization.T("trade.tp.label"),  snap.TpPrice, false,             snap, 0);
            UpdateSlCard(_slCard, snap);

            // Live read-out
            _buyPriceText.Text  = snap.LongEntryStopPrice  > 0 ? snap.LongEntryStopPrice.ToString("F5",  CultureInfo.InvariantCulture) : "—";
            _sellPriceText.Text = snap.ShortEntryStopPrice > 0 ? snap.ShortEntryStopPrice.ToString("F5", CultureInfo.InvariantCulture) : "—";
            if (snap.LongEntryStopPrice > 0 && snap.ShortEntryStopPrice > 0 && snap.TickSize > 0)
            {
                int sp = (int)Math.Round((snap.LongEntryStopPrice - snap.ShortEntryStopPrice) / snap.TickSize);
                _spreadText.Text = NY930Localization.T("or.spread") + ": " + sp + " " + NY930Localization.T("common.ticks");
            }
            else _spreadText.Text = NY930Localization.T("or.spread") + ": —";

            // Button states
            bool inPosition = snap.InLong || snap.InShort;
            bool canEnter = !inPosition && !snap.SessionDone && !snap.LongEntryWorking && !snap.ShortEntryWorking;
            _btnBreakeven.IsEnabled = inPosition;
            _btnCloseNow.IsEnabled  = inPosition;
            _btnPartial.IsEnabled   = inPosition;
            _btnTrailing.IsEnabled  = inPosition;
            _btnBuyNow.IsEnabled    = canEnter;
            _btnSellNow.IsEnabled   = canEnter;
            _btnCancel.IsEnabled    = snap.LongEntryWorking || snap.ShortEntryWorking;

            // Result screen takes over on close
            if (snap.LastResult != null
                && !inPosition
                && snap.SessionDone
                && _shell != null
                && _shell.CurrentViewIs<NY930OpenRangeView>())
            {
                _shell.Show(new NY930ResultView(_shell, snap.LastResult));
            }

            RefreshCountdown();
        }

        private void UpdateTpCard(NY930Theme.TpProgressCard card, string baseLabel,
                                  double price, bool done, NY930OpenRangeSnapshot snap, int kind)
        {
            if (price <= 0)
            {
                card.SetState(NY930Theme.TpState.Pending,
                    string.Format(NY930Localization.T("trade.tp.pending"), baseLabel),
                    "");
                return;
            }

            string detail;
            if (done)
            {
                detail = string.Format(NY930Localization.T("trade.tp.reached"), baseLabel);
                card.SetState(NY930Theme.TpState.Done, detail, price.ToString("F5", CultureInfo.InvariantCulture));
            }
            else if (snap.InLong || snap.InShort)
            {
                detail = string.Format(NY930Localization.T("trade.tp.in_progress"), baseLabel);
                card.SetState(NY930Theme.TpState.Active, detail, price.ToString("F5", CultureInfo.InvariantCulture));
            }
            else
            {
                detail = string.Format(NY930Localization.T("trade.tp.pending"), baseLabel);
                card.SetState(NY930Theme.TpState.Pending, detail, price.ToString("F5", CultureInfo.InvariantCulture));
            }
        }

        private void UpdateSlCard(NY930Theme.TpProgressCard card, NY930OpenRangeSnapshot snap)
        {
            if (snap.SlPrice <= 0)
            {
                card.SetState(NY930Theme.TpState.Pending, NY930Localization.T("trade.sl.label"), "");
                return;
            }

            // Hit detected via session done + last result with SlHit=true.
            if (snap.LastResult != null && snap.LastResult.SlHit && !snap.InLong && !snap.InShort)
            {
                card.SetState(NY930Theme.TpState.Failed,
                    NY930Localization.T("trade.sl.hit"),
                    snap.SlPrice.ToString("F5", CultureInfo.InvariantCulture));
                return;
            }

            // Active position — check if we're in danger (price within 5 ticks of SL).
            if ((snap.InLong || snap.InShort) && snap.LastPrice > 0 && snap.TickSize > 0)
            {
                double distTicks = snap.InLong
                    ? (snap.LastPrice - snap.SlPrice) / snap.TickSize
                    : (snap.SlPrice - snap.LastPrice) / snap.TickSize;
                if (distTicks <= 5)
                {
                    card.SetState(NY930Theme.TpState.Active,
                        NY930Localization.T("trade.sl.danger"),
                        snap.SlPrice.ToString("F5", CultureInfo.InvariantCulture));
                    return;
                }
            }

            card.SetState(NY930Theme.TpState.Pending,
                NY930Localization.T("trade.sl.label"),
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
            _bigPnL.Update(0, 0, "None");
            _pillCurrency.Visibility = _pillDuration.Visibility = _pillContracts.Visibility = Visibility.Collapsed;
            _tp1Card.SetState(NY930Theme.TpState.Pending, NY930Localization.T("trade.tp1.label"), "");
            _tp2Card.SetState(NY930Theme.TpState.Pending, NY930Localization.T("trade.tp2.label"), "");
            _tpCard.SetState(NY930Theme.TpState.Pending,  NY930Localization.T("trade.tp.label"),  "");
            _slCard.SetState(NY930Theme.TpState.Pending,  NY930Localization.T("trade.sl.label"),  "");
            _buyPriceText.Text  = "—";
            _sellPriceText.Text = "—";
            _spreadText.Text    = NY930Localization.T("or.spread") + ": —";

            _btnBreakeven.IsEnabled = _btnCloseNow.IsEnabled = _btnPartial.IsEnabled = _btnTrailing.IsEnabled = false;
            _btnBuyNow.IsEnabled = _btnSellNow.IsEnabled = false;
            _btnCancel.IsEnabled = false;
        }

        public void RefreshLocalization()
        {
            _hdrTargets.Text    = NY930Localization.T("trade.section.targets");
            _hdrStop.Text       = NY930Localization.T("trade.section.stops");
            _hdrManagement.Text = NY930Localization.T("trade.section.management");
            _hdrLive.Text       = NY930Localization.T("nav.openrange").ToUpperInvariant();

            _btnBreakeven.Content = NY930Localization.T("trade.action.breakeven");
            _btnCloseNow.Content  = NY930Localization.T("trade.action.close_now");
            _btnPartial.Content   = NY930Localization.T("trade.action.partial");
            _btnTrailing.Content  = NY930Localization.T("trade.action.trailing");
            _btnBuyNow.Content    = NY930Localization.T("or.buy_now");
            _btnSellNow.Content   = NY930Localization.T("or.sell_now");
            _btnCancel.Content    = NY930Localization.T("or.cancel");
            _btnEditParams.Content = NY930Localization.T("params.title");

            // Force a re-render with the latest snapshot
            var current = NY930Bridge.GetOpenRange();
            if (current != null) Render(current);
            else                  RefreshNoStrategy();
        }

        public void Dispose()
        {
            NY930Bridge.OpenRangeChanged -= OnSnapshot;
            if (_ticker != null) { _ticker.Stop(); _ticker = null; }
        }
    }
}
