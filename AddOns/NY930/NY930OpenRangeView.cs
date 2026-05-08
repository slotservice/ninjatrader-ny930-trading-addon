// ============================================================
//  NY930OpenRangeView — v1.2 pixel-match against client mockup
// ------------------------------------------------------------
//  Layout faithfully reproduces the "Trade progress" screen
//  from flujo de la app (1).png:
//
//   ┌────────────────────────────────────┐
//   │ NY930  | APERTURA BREAKOUT |  TAG  │   TradeHeader
//   ├────────────────────────────────────┤
//   │ NQ MAR25                +25.00     │   sub-row
//   │ ┌────────────────────────────────┐ │
//   │ │  +$312.50          ▲ LONG      │ │
//   │ │  +25 ticks                     │ │
//   │ │  [+$75] [06:44] [10 ctos]      │ │   pills
//   │ └────────────────────────────────┘ │
//   ├────────────────────────────────────┤
//   │ ✓ TP1 alcanzado    +25t      +$75 │
//   │ ● TP2 progreso ▓▓░ +15/25t   +$45 │
//   │ ○ TP3 pendiente    +60t           │
//   │ ○ SL              -90t            │
//   ├────────────────────────────────────┤
//   │ ACCIONES                           │
//   │ [BREAKEVEN]            [CERRAR YA] │
//   ├────────────────────────────────────┤
//   │ GESTIÓN DE POSICIÓN                │
//   │ [PARTIAL CLOSE] 50%  [TRAILING]    │
//   ├────────────────────────────────────┤
//   │ Live read-out (BUY/SELL/spread)    │
//   │ MOVE BOTH ▲▼  |  SPREAD + −        │
//   │ [BUY NOW] [SELL NOW]               │
//   │ [CANCEL ORDERS]                    │
//   │ [Parameters]                        │
//   └────────────────────────────────────┘
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

        // Header
        private NY930Theme.TradeHeader _header;

        // Sub-row
        private TextBlock _instrumentText;
        private TextBlock _countdownLabel;

        // Hero PnL (using BigPnLDisplay introduced in v1.1, themed for navy)
        private NY930Theme.BigPnLDisplay _bigPnL;
        private StackPanel _pillRow;
        private TextBlock  _pillCurrencyText;
        private TextBlock  _pillDurationText;
        private TextBlock  _pillContractsText;
        private Border     _pillCurrency;
        private Border     _pillDuration;
        private Border     _pillContracts;

        // Targets / stop rows (new navy style with progress)
        private NY930Theme.NavyTpRow _tp1Row;
        private NY930Theme.NavyTpRow _tp2Row;
        private NY930Theme.NavyTpRow _tpRow;
        private NY930Theme.NavyTpRow _slRow;

        // Action sections
        private Button _btnBreakeven;
        private Button _btnCloseNow;
        private Button _btnPartial;
        private Button _btnTrailing;
        private NY930Theme.PartialPercentSelector _partialPct;

        // Live read-out
        private TextBlock _buyPriceText;
        private TextBlock _sellPriceText;
        private TextBlock _spreadText;

        // Move / Spread step cards
        private TextBlock _moveValue;
        private TextBlock _spreadValue;
        private int       _moveStep   = 5;
        private int       _spreadStep = 10;

        // Manual entry / cancel / params
        private Button   _btnBuyNow;
        private Button   _btnSellNow;
        private Button   _btnCancel;
        private Button   _btnEditParams;

        // Section headers cached for live language refresh
        private TextBlock _hdrActions;
        private TextBlock _hdrManagement;
        private TextBlock _hdrLive;

        // Countdown
        private System.Windows.Threading.DispatcherTimer _ticker;
        private DateTime _entryTime;

        public NY930OpenRangeView(NY930ShellView shell)
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

            BuildHeader(root);
            BuildHeroPanel(root);
            BuildTargetsPanel(root);
            BuildActionsPanel(root);
            BuildManagementPanel(root);
            BuildRangePanel(root);
            BuildEntryPanel(root);

            // Bridge wiring
            NY930Bridge.OpenRangeChanged += OnSnapshot;
            var current = NY930Bridge.GetOpenRange();
            if (current != null) OnSnapshot(current);
            else                 RenderEmpty();

            _ticker = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _ticker.Tick += (s, e) => RefreshCountdown();
            _ticker.Start();
        }

        // ── Sections ────────────────────────────────────────────

        private void BuildHeader(StackPanel root)
        {
            _header = new NY930Theme.TradeHeader("APERTURA BREAKOUT");
            root.Children.Add(_header);
        }

        private void BuildHeroPanel(StackPanel root)
        {
            var stack = new StackPanel { Margin = new Thickness(12, 0, 12, 0) };

            // Sub-row: instrument + countdown
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

            // Hero PnL inside a navy panel
            var heroInner = new StackPanel();
            _bigPnL = new NY930Theme.BigPnLDisplay();
            heroInner.Children.Add(_bigPnL);

            _pillRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            _pillCurrency  = NY930Theme.Pill("$0.00",      NY930Theme.CyanAccent);
            _pillDuration  = NY930Theme.Pill("00:00",      NY930Theme.TextNavyMid);
            _pillContracts = NY930Theme.Pill("0 ctos",     NY930Theme.TextNavyMid);
            _pillCurrencyText  = (TextBlock)_pillCurrency.Child;
            _pillDurationText  = (TextBlock)_pillDuration.Child;
            _pillContractsText = (TextBlock)_pillContracts.Child;
            _pillRow.Children.Add(_pillCurrency);
            _pillRow.Children.Add(_pillDuration);
            _pillRow.Children.Add(_pillContracts);
            heroInner.Children.Add(_pillRow);

            stack.Children.Add(NY930Theme.NavyPanel(heroInner, new Thickness(0, 0, 0, 8)));
            root.Children.Add(stack);
        }

        private void BuildTargetsPanel(StackPanel root)
        {
            var stack = new StackPanel { Margin = new Thickness(12, 0, 12, 0) };

            _tp1Row = new NY930Theme.NavyTpRow();
            _tp2Row = new NY930Theme.NavyTpRow();
            _tpRow  = new NY930Theme.NavyTpRow();
            _slRow  = new NY930Theme.NavyTpRow(isStop: true);

            stack.Children.Add(_tp1Row);
            stack.Children.Add(_tp2Row);
            stack.Children.Add(_tpRow);
            stack.Children.Add(_slRow);

            // Wrapper panel
            root.Children.Add(NY930Theme.NavyPanel(stack, new Thickness(12, 0, 12, 8)));
        }

        private void BuildActionsPanel(StackPanel root)
        {
            var stack = new StackPanel();
            _hdrActions = NY930Theme.NavySectionHeader(NY930Localization.T("trade.section.management"));
            stack.Children.Add(_hdrActions);

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition());

            _btnBreakeven = NY930Theme.NavyButton(NY930Localization.T("trade.action.breakeven"), NY930Theme.BlueAccent, false);
            _btnBreakeven.Margin = new Thickness(0, 0, 4, 0);
            _btnBreakeven.Click += (s, e) => Send(NY930ActionType.OpenRangeBreakeven, 0);
            Grid.SetColumn(_btnBreakeven, 0);
            row.Children.Add(_btnBreakeven);

            _btnCloseNow = NY930Theme.NavyButton(NY930Localization.T("trade.action.close_now"), NY930Theme.DangerRed, false);
            _btnCloseNow.Margin = new Thickness(4, 0, 0, 0);
            _btnCloseNow.Click += (s, e) => Send(NY930ActionType.OpenRangeFlatten, 0);
            Grid.SetColumn(_btnCloseNow, 1);
            row.Children.Add(_btnCloseNow);

            stack.Children.Add(row);
            root.Children.Add(NY930Theme.NavyPanel(stack, new Thickness(12, 0, 12, 8)));
        }

        private void BuildManagementPanel(StackPanel root)
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
            _btnTrailing.Click += (s, e) => Send(NY930ActionType.OpenRangeTrailingTrigger, 0);
            Grid.SetColumn(_btnTrailing, 2);

            row.Children.Add(_btnPartial);
            row.Children.Add(_partialPct);
            row.Children.Add(_btnTrailing);

            stack.Children.Add(row);
            root.Children.Add(NY930Theme.NavyPanel(stack, new Thickness(12, 0, 12, 8)));
        }

        private void BuildRangePanel(StackPanel root)
        {
            var stack = new StackPanel();
            _hdrLive = NY930Theme.NavySectionHeader(NY930Localization.T("nav.openrange").ToUpperInvariant());
            stack.Children.Add(_hdrLive);

            // BUY / SELL / spread read-out
            _buyPriceText  = MakePriceLine(stack, NY930Localization.T("or.buystop"),  NY930Theme.SuccessGreen);
            _sellPriceText = MakePriceLine(stack, NY930Localization.T("or.sellstop"), NY930Theme.DangerRed);
            _spreadText = new TextBlock
            {
                Text = NY930Localization.T("or.spread") + ": —",
                FontSize = 10,
                Foreground = NY930Theme.TextNavyMidBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 6, 0, 0)
            };
            stack.Children.Add(_spreadText);

            // Move + Spread step cards
            var grid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            var moveCard = BuildStepCard(NY930Localization.T("or.move.title"),
                NY930Localization.T("or.move.sub"),
                NY930Theme.BlueAccent, "▲", "▼",
                () => Send(NY930ActionType.OpenRangeMoveBoth, +_moveStep),
                () => Send(NY930ActionType.OpenRangeMoveBoth, -_moveStep),
                v => _moveStep = v, _moveStep, out _moveValue);
            moveCard.Margin = new Thickness(0, 0, 4, 0);
            Grid.SetColumn(moveCard, 0);

            var spreadCard = BuildStepCard(NY930Localization.T("or.spread.title"),
                NY930Localization.T("or.spread.sub"),
                NY930Theme.SuccessGreen, "+", "−",
                () => Send(NY930ActionType.OpenRangeAdjustSpread, +_spreadStep),
                () => Send(NY930ActionType.OpenRangeAdjustSpread, -_spreadStep),
                v => _spreadStep = v, _spreadStep, out _spreadValue);
            spreadCard.Margin = new Thickness(4, 0, 0, 0);
            Grid.SetColumn(spreadCard, 1);

            grid.Children.Add(moveCard);
            grid.Children.Add(spreadCard);
            stack.Children.Add(grid);

            root.Children.Add(NY930Theme.NavyPanel(stack, new Thickness(12, 0, 12, 8)));
        }

        private void BuildEntryPanel(StackPanel root)
        {
            var stack = new StackPanel();

            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition());
            _btnBuyNow  = NY930Theme.NavyButton(NY930Localization.T("or.buy_now"),  NY930Theme.SuccessGreen, true);
            _btnSellNow = NY930Theme.NavyButton(NY930Localization.T("or.sell_now"), NY930Theme.DangerRed,    true);
            _btnBuyNow.Margin  = new Thickness(0, 0, 4, 0);
            _btnSellNow.Margin = new Thickness(4, 0, 0, 0);
            _btnBuyNow.Click  += (s, e) => Send(NY930ActionType.OpenRangeBuyNow,  0);
            _btnSellNow.Click += (s, e) => Send(NY930ActionType.OpenRangeSellNow, 0);
            Grid.SetColumn(_btnBuyNow,  0);
            Grid.SetColumn(_btnSellNow, 1);
            row.Children.Add(_btnBuyNow);
            row.Children.Add(_btnSellNow);
            stack.Children.Add(row);

            _btnCancel = NY930Theme.NavyButton(NY930Localization.T("or.cancel"), NY930Theme.WarnAmberHi, false);
            _btnCancel.Margin = new Thickness(0, 6, 0, 0);
            _btnCancel.Click += (s, e) => Send(NY930ActionType.OpenRangeCancelAll, 0);
            stack.Children.Add(_btnCancel);

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
            _btnEditParams.Click += (s, e) => _shell.Show(new NY930ParametersView(_shell, isOpenRange: true));
            stack.Children.Add(_btnEditParams);

            root.Children.Add(NY930Theme.NavyPanel(stack, new Thickness(12, 0, 12, 12)));
        }

        // ── Helpers ─────────────────────────────────────────────

        private TextBlock MakePriceLine(StackPanel parent, string label, Color tint)
        {
            var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var badge = new Border
            {
                Background      = NY930Theme.BrushAlpha(tint, 0x33),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(8, 3, 8, 3),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text       = label,
                    FontSize   = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = NY930Theme.SolidBrush(tint)
                }
            };
            Grid.SetColumn(badge, 0);

            var price = new TextBlock
            {
                Text       = "—",
                FontSize   = 14,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = NY930Theme.SolidBrush(tint),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(price, 1);

            grid.Children.Add(badge);
            grid.Children.Add(price);
            parent.Children.Add(grid);
            return price;
        }

        private Border BuildStepCard(string title, string sub, Color accent,
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
                Foreground = NY930Theme.SolidBrush(accent),
                HorizontalAlignment = HorizontalAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text = sub,
                FontSize = 9,
                Foreground = NY930Theme.TextNavyLowBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var btnUp = NY930Theme.NavyButton(upGlyph, accent, false);
            btnUp.FontSize = 18;
            btnUp.Click += (s, e) => onUp();
            stack.Children.Add(btnUp);

            valueOut = new TextBlock
            {
                Text       = defStep.ToString(),
                FontSize   = 22,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.TextNavyHiBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 6, 0, 0)
            };
            stack.Children.Add(valueOut);
            stack.Children.Add(new TextBlock
            {
                Text = NY930Localization.T("common.ticks").ToUpperInvariant(),
                FontSize = 8,
                Foreground = NY930Theme.TextNavyLowBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            var btnDown = NY930Theme.NavyButton(downGlyph, accent, false);
            btnDown.FontSize = 18;
            btnDown.Margin = new Thickness(0, 6, 0, 0);
            btnDown.Click += (s, e) => onDown();
            stack.Children.Add(btnDown);

            // Step chips
            int[] steps = { 1, 5, 10, 25 };
            var chips = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
            Button[] chipBtns = new Button[steps.Length];
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
                    Background = sv == defStep ? NY930Theme.SolidBrush(accent) : NY930Theme.BgNavyInputBrush,
                    Foreground = sv == defStep ? Brushes.White : NY930Theme.TextNavyMidBrush,
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
                        chipBtns[j].Background = sel ? NY930Theme.SolidBrush(accent) : NY930Theme.BgNavyInputBrush;
                        chipBtns[j].Foreground = sel ? Brushes.White : NY930Theme.TextNavyMidBrush;
                    }
                };
                chips.Children.Add(chip);
            }
            stack.Children.Add(chips);

            var card = new Border
            {
                Background      = NY930Theme.BgNavyInputBrush,
                BorderBrush     = NY930Theme.BrushAlpha(accent, 0x88),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(10, 12, 10, 12),
                Child           = stack
            };
            return card;
        }

        // ── Bridge ──────────────────────────────────────────────

        private void Send(NY930ActionType type, int arg)
        {
            NY930Bridge.RequestOpenRangeAction(new NY930Action { Type = type, IntArg = arg });
        }

        private void SendPartial()
        {
            var snap = NY930Bridge.GetOpenRange();
            int total = snap != null ? Math.Max(1, snap.ContractsRemaining) : 1;
            int qty = (int)Math.Round(total * (_partialPct.Percent / 100.0));
            if (qty < 1) qty = 1;
            if (qty > total) qty = total;
            Send(NY930ActionType.OpenRangePartialClose, qty);
        }

        private void OnSnapshot(NY930OpenRangeSnapshot snap)
            => Dispatcher.InvokeAsync(() => Render(snap));

        private void Render(NY930OpenRangeSnapshot snap)
        {
            if (snap == null) { RenderEmpty(); return; }

            _entryTime = snap.EntryTime;
            _instrumentText.Text = snap.Instrument ?? "—";

            // Status tag
            string side = snap.InLong ? "Long" : (snap.InShort ? "Short" : "None");
            UpdateHeaderTag(snap, side);

            // Hero PnL
            _bigPnL.Update(snap.UnrealizedCurrency, snap.UnrealizedTicks, side);

            // Pills
            _pillCurrencyText.Text = (snap.UnrealizedCurrency >= 0 ? "+" : "")
                + snap.UnrealizedCurrency.ToString("C", CultureInfo.CurrentCulture);
            _pillCurrency.Visibility = (snap.InLong || snap.InShort) ? Visibility.Visible : Visibility.Collapsed;
            if (snap.TradeStartTime != DateTime.MinValue && (snap.InLong || snap.InShort))
            {
                var d = DateTime.Now - snap.TradeStartTime;
                _pillDurationText.Text = ((int)d.TotalMinutes).ToString("D2") + ":" + d.Seconds.ToString("D2");
                _pillDuration.Visibility = Visibility.Visible;
            }
            else _pillDuration.Visibility = Visibility.Collapsed;
            _pillContractsText.Text = snap.ContractsRemaining + " ctos";
            _pillContracts.Visibility = (snap.InLong || snap.InShort) ? Visibility.Visible : Visibility.Collapsed;

            // Targets / stop
            UpdateTpRow(_tp1Row, "TP1", snap.P1Price, snap.Partial1Done, snap, snap.Partial1Ticks);
            UpdateTpRow(_tp2Row, "TP2", snap.P2Price, snap.Partial2Done, snap, snap.Partial2Ticks);
            UpdateTpRow(_tpRow,  "TP",  snap.TpPrice, false,             snap,
                snap.InLong ? snap.TakeProfitLongTicks : snap.TakeProfitShortTicks);
            UpdateSlRow(_slRow, snap);

            // Live read-out
            _buyPriceText.Text  = snap.LongEntryStopPrice  > 0 ? snap.LongEntryStopPrice.ToString("F5",  CultureInfo.InvariantCulture) : "—";
            _sellPriceText.Text = snap.ShortEntryStopPrice > 0 ? snap.ShortEntryStopPrice.ToString("F5", CultureInfo.InvariantCulture) : "—";
            if (snap.LongEntryStopPrice > 0 && snap.ShortEntryStopPrice > 0 && snap.TickSize > 0)
            {
                int sp = (int)Math.Round((snap.LongEntryStopPrice - snap.ShortEntryStopPrice) / snap.TickSize);
                _spreadText.Text = NY930Localization.T("or.spread") + ": " + sp + " " + NY930Localization.T("common.ticks");
            }
            else _spreadText.Text = NY930Localization.T("or.spread") + ": —";

            // Buttons
            bool inPosition = snap.InLong || snap.InShort;
            bool canEnter   = !inPosition && !snap.SessionDone && !snap.LongEntryWorking && !snap.ShortEntryWorking;
            _btnBreakeven.IsEnabled = inPosition;
            _btnCloseNow.IsEnabled  = inPosition;
            _btnPartial.IsEnabled   = inPosition;
            _btnTrailing.IsEnabled  = inPosition;
            _btnBuyNow.IsEnabled    = canEnter;
            _btnSellNow.IsEnabled   = canEnter;
            _btnCancel.IsEnabled    = snap.LongEntryWorking || snap.ShortEntryWorking;

            // Result auto-route
            if (snap.LastResult != null && !inPosition && snap.SessionDone
                && _shell != null && _shell.CurrentViewIs<NY930OpenRangeView>())
            {
                _shell.Show(new NY930ResultView(_shell, snap.LastResult));
            }

            RefreshCountdown();
        }

        private void UpdateHeaderTag(NY930OpenRangeSnapshot s, string side)
        {
            if (s.InLong || s.InShort)
            {
                Color tint = s.InLong ? NY930Theme.SuccessGreen : NY930Theme.DangerRed;
                _header.StatusTag.Update(NY930Localization.T(s.InLong ? "status.in_long" : "status.in_short"), tint);
            }
            else if (s.LongEntryWorking || s.ShortEntryWorking)
                _header.StatusTag.Update(NY930Localization.T("status.active"), NY930Theme.CyanAccent);
            else if (s.SessionDone)
                _header.StatusTag.Update(NY930Localization.T("status.session_done"), NY930Theme.TextNavyMid);
            else
                _header.StatusTag.Update("WAITING", NY930Theme.WarnAmberHi);
        }

        private void UpdateTpRow(NY930Theme.NavyTpRow row, string label, double price, bool done,
                                 NY930OpenRangeSnapshot s, int targetTicks)
        {
            if (price <= 0) { row.SetIdle(string.Format(NY930Localization.T("trade.tp.pending"), label)); return; }

            string baseLabelDone     = string.Format(NY930Localization.T("trade.tp.reached"),     label);
            string baseLabelActive   = string.Format(NY930Localization.T("trade.tp.in_progress"), label);
            string baseLabelPending  = string.Format(NY930Localization.T("trade.tp.pending"),     label);

            string ticksTxt    = (targetTicks > 0 ? "+" : "") + targetTicks + " " + NY930Localization.T("common.ticks");
            string currencyTxt = ApproxRowCurrency(s, targetTicks);

            if (done)
            {
                row.SetDone(baseLabelDone, ticksTxt, currencyTxt);
            }
            else if (s.InLong || s.InShort)
            {
                // Compute progress = current ticks toward target / target ticks
                double progress = 0;
                if (targetTicks > 0 && s.UnrealizedTicks > 0)
                    progress = Math.Max(0, Math.Min(1, s.UnrealizedTicks / (double)targetTicks));

                int currentTicks = (int)Math.Max(0, Math.Round(s.UnrealizedTicks));
                string activeTicksTxt = "+" + currentTicks + " / " + targetTicks + " " + NY930Localization.T("common.ticks");
                row.SetActive(baseLabelActive, activeTicksTxt, currencyTxt, progress);
            }
            else
            {
                row.SetPending(baseLabelPending, ticksTxt, "");
            }
        }

        private void UpdateSlRow(NY930Theme.NavyTpRow row, NY930OpenRangeSnapshot s)
        {
            if (s.SlPrice <= 0) { row.SetIdle(NY930Localization.T("trade.sl.label")); return; }

            // SL hit detected via last result
            if (s.LastResult != null && s.LastResult.SlHit && !s.InLong && !s.InShort)
            {
                row.SetDone(NY930Localization.T("trade.sl.hit"),
                    s.SlPrice.ToString("F5", CultureInfo.InvariantCulture), "");
                return;
            }

            // Active position — danger zone if within 5 ticks
            if ((s.InLong || s.InShort) && s.LastPrice > 0 && s.TickSize > 0)
            {
                double distTicks = s.InLong
                    ? (s.LastPrice - s.SlPrice) / s.TickSize
                    : (s.SlPrice - s.LastPrice) / s.TickSize;
                if (distTicks <= 5)
                {
                    string warn = string.Format("SL a {0:F0} ticks", Math.Max(0, distTicks));
                    row.SetDanger(NY930Localization.T("trade.sl.danger"), warn);
                    return;
                }
            }

            int slTicks = s.InLong ? s.StopLossLongTicks : s.StopLossShortTicks;
            string ticksTxt = "-" + slTicks + " " + NY930Localization.T("common.ticks");
            row.SetPending(NY930Localization.T("trade.sl.label"), ticksTxt, "");
        }

        private static string ApproxRowCurrency(NY930OpenRangeSnapshot s, int ticks)
        {
            if (s == null || s.TickSize <= 0 || ticks <= 0 || s.Quantity <= 0) return "";
            double tickValue = (s.UnrealizedCurrency != 0 && s.UnrealizedTicks != 0)
                ? Math.Abs(s.UnrealizedCurrency / s.UnrealizedTicks / Math.Max(1, s.ContractsRemaining))
                : 12.5;
            // Per-row currency = ticks * tickValue * (whatever contracts are usually closed at this row).
            // Without knowing the partial layout, default to 1 contract for TP rows.
            int contracts = 1;
            return "+" + (ticks * tickValue * contracts).ToString("C0", CultureInfo.CurrentCulture);
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

            _buyPriceText.Text  = "—";
            _sellPriceText.Text = "—";
            _spreadText.Text    = NY930Localization.T("or.spread") + ": —";

            _btnBreakeven.IsEnabled = _btnCloseNow.IsEnabled = false;
            _btnPartial.IsEnabled   = _btnTrailing.IsEnabled = false;
            _btnBuyNow.IsEnabled    = _btnSellNow.IsEnabled  = false;
            _btnCancel.IsEnabled    = false;
        }

        public void RefreshLocalization()
        {
            _hdrActions.Text    = NY930Localization.T("trade.section.management");
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

            var current = NY930Bridge.GetOpenRange();
            if (current != null) Render(current);
            else                 RenderEmpty();
        }

        public void Dispose()
        {
            NY930Bridge.OpenRangeChanged -= OnSnapshot;
            if (_ticker != null) { _ticker.Stop(); _ticker = null; }
        }
    }
}
