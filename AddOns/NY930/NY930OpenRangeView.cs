// ============================================================
//  NY930OpenRangeView — control panel for AperturaBreakout
// ------------------------------------------------------------
//  Layout (top → bottom):
//    1. Status banner (waiting / orders working / in long / etc.)
//    2. Live read-out: BUY STOP / spread / SELL STOP
//    3. MOVE BOTH  ▲▼ + step chips (1 / 5 / 10 / 25 ticks)
//    4. SPREAD     ←→ + step chips (1 / 5 / 10 / 25 ticks)
//    5. CANCEL ORDERS button
//    6. Manual entry: BUY NOW / SELL NOW (only valid before
//       orders placed)
//    7. CLOSE POSITION (after fill) + manual partial close
//    8. Progress strip: TP1 / TP2 / TP / SL / PnL / contracts /
//       duration. Dimmed before fill, lit after.
// ============================================================

#region Using declarations
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public sealed class NY930OpenRangeView : Grid, INY930Localizable, IDisposable
    {
        private readonly NY930ShellView _shell;

        private TextBlock _status;
        private TextBlock _buyPrice;
        private TextBlock _sellPrice;
        private TextBlock _spread;
        private TextBlock _moveTitle;
        private TextBlock _spreadTitle;
        private TextBlock _moveSub;
        private TextBlock _spreadSub;
        private TextBlock _moveValue;
        private TextBlock _spreadValue;
        private TextBlock _ticksLbl1;
        private TextBlock _ticksLbl2;
        private Button   _btnCancel;
        private Button   _btnBuyNow;
        private Button   _btnSellNow;
        private Button   _btnFlatten;
        private TextBox  _partialQty;
        private Button   _btnPartial;
        private TextBlock _progTitle;
        private TextBlock _progEntry;
        private TextBlock _progLast;
        private TextBlock _progSL;
        private TextBlock _progTP1;
        private TextBlock _progTP2;
        private TextBlock _progTP;
        private TextBlock _progPnL;
        private TextBlock _progDuration;
        private TextBlock _progContracts;

        // Result row (after close)
        private Border    _resultBox;
        private TextBlock _resultTitle;
        private TextBlock _resultLine1;
        private TextBlock _resultLine2;

        private int _moveStep   = 5;
        private int _spreadStep = 10;

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

            var root = new StackPanel { Margin = new Thickness(14, 14, 14, 14) };
            scroll.Content = root;

            // ── Status ───────────────────────────────────────────
            _status = new TextBlock
            {
                Text       = NY930Localization.T("status.waiting").Replace("{0}", "09:29:58"),
                FontSize   = 12,
                Foreground = NY930Theme.WarnAmberBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 0, 0, 12)
            };
            root.Children.Add(_status);

            // ── Live prices ─────────────────────────────────────
            var priceStack = new StackPanel();
            _buyPrice  = BuildPriceRow(NY930Localization.T("or.buystop"),  NY930Theme.LongGreen,
                                        out var buyLabel);
            _sellPrice = BuildPriceRow(NY930Localization.T("or.sellstop"), NY930Theme.ShortRed,
                                        out var sellLabel);
            _spread    = new TextBlock
            {
                Text     = NY930Localization.T("or.spread") + ": —",
                FontSize = 11,
                FontFamily = new FontFamily("Consolas"),
                Foreground = NY930Theme.TextMidBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin   = new Thickness(0, 6, 0, 6)
            };

            priceStack.Children.Add(WrapPriceRow(buyLabel, _buyPrice));
            priceStack.Children.Add(_spread);
            priceStack.Children.Add(WrapPriceRow(sellLabel, _sellPrice));
            root.Children.Add(NY930Theme.Panel(priceStack));

            // ── Move both / Spread cards (side by side) ─────────
            var grid = new Grid();
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
                out _moveTitle, out _moveSub, out _moveValue, out _ticksLbl1);
            Grid.SetColumn(moveCard, 0);
            moveCard.Margin = new Thickness(0, 0, 4, 0);

            var spreadCard = BuildStepCard(
                NY930Localization.T("or.spread.title"),
                NY930Localization.T("or.spread.sub"),
                NY930Theme.LongGreen,
                "+", "−",
                () => Send(NY930ActionType.OpenRangeAdjustSpread, +_spreadStep),
                () => Send(NY930ActionType.OpenRangeAdjustSpread, -_spreadStep),
                v => _spreadStep = v,
                _spreadStep,
                out _spreadTitle, out _spreadSub, out _spreadValue, out _ticksLbl2);
            Grid.SetColumn(spreadCard, 1);
            spreadCard.Margin = new Thickness(4, 0, 0, 0);

            grid.Children.Add(moveCard);
            grid.Children.Add(spreadCard);
            root.Children.Add(grid);

            // ── Cancel ───────────────────────────────────────────
            _btnCancel = NY930Theme.ActionButton(
                NY930Localization.T("or.cancel"), NY930Theme.ShortRed);
            _btnCancel.Margin = new Thickness(0, 10, 0, 6);
            _btnCancel.Click += (s, e) => Send(NY930ActionType.OpenRangeCancelAll, 0);
            root.Children.Add(_btnCancel);

            // ── Manual entries ──────────────────────────────────
            var manGrid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            manGrid.ColumnDefinitions.Add(new ColumnDefinition());
            manGrid.ColumnDefinitions.Add(new ColumnDefinition());
            _btnBuyNow  = NY930Theme.ActionButton(NY930Localization.T("or.buy_now"),  NY930Theme.LongGreen);
            _btnSellNow = NY930Theme.ActionButton(NY930Localization.T("or.sell_now"), NY930Theme.ShortRed);
            _btnBuyNow.Margin  = new Thickness(0, 0, 4, 0);
            _btnSellNow.Margin = new Thickness(4, 0, 0, 0);
            _btnBuyNow.Click  += (s, e) => Send(NY930ActionType.OpenRangeBuyNow,  0);
            _btnSellNow.Click += (s, e) => Send(NY930ActionType.OpenRangeSellNow, 0);
            Grid.SetColumn(_btnBuyNow,  0);
            Grid.SetColumn(_btnSellNow, 1);
            manGrid.Children.Add(_btnBuyNow);
            manGrid.Children.Add(_btnSellNow);
            root.Children.Add(manGrid);

            // ── Flatten + partial ───────────────────────────────
            var flattenStack = new StackPanel { Margin = new Thickness(0, 6, 0, 0) };
            _btnFlatten = NY930Theme.ActionButton(NY930Localization.T("or.flatten"), NY930Theme.WarnAmber);
            _btnFlatten.Click += (s, e) => Send(NY930ActionType.OpenRangeFlatten, 0);
            flattenStack.Children.Add(_btnFlatten);

            var partialRow = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            partialRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            partialRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _partialQty = NY930Theme.InputBox();
            _partialQty.Text = "1";
            _partialQty.HorizontalAlignment = HorizontalAlignment.Stretch;
            _partialQty.Margin = new Thickness(0, 0, 6, 0);
            Grid.SetColumn(_partialQty, 0);
            _btnPartial = NY930Theme.OutlineButton("Partial close");
            _btnPartial.Click += (s, e) =>
            {
                int n;
                if (int.TryParse(_partialQty.Text, out n) && n > 0)
                    Send(NY930ActionType.OpenRangePartialClose, n);
            };
            Grid.SetColumn(_btnPartial, 1);
            partialRow.Children.Add(_partialQty);
            partialRow.Children.Add(_btnPartial);
            flattenStack.Children.Add(partialRow);

            root.Children.Add(flattenStack);

            // ── Progress ────────────────────────────────────────
            var progStack = new StackPanel();
            _progTitle = new TextBlock
            {
                Text       = NY930Localization.T("progress.title"),
                FontSize   = 11,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.GoldBrush,
                Margin     = new Thickness(0, 0, 0, 8)
            };
            progStack.Children.Add(_progTitle);

            _progEntry     = ProgRow(NY930Localization.T("progress.entry"), "—");
            _progLast      = ProgRow(NY930Localization.T("progress.last"),  "—");
            _progSL        = ProgRow(NY930Localization.T("progress.sl"),    "—");
            _progTP1       = ProgRow(NY930Localization.T("progress.tp1"),   "—");
            _progTP2       = ProgRow(NY930Localization.T("progress.tp2"),   "—");
            _progTP        = ProgRow(NY930Localization.T("progress.tp3"),   "—");
            _progPnL       = ProgRow(NY930Localization.T("progress.pnl"),   "—");
            _progContracts = ProgRow(NY930Localization.T("progress.contracts"), "—");
            _progDuration  = ProgRow(NY930Localization.T("progress.duration"),  "—");

            progStack.Children.Add(_progEntry);
            progStack.Children.Add(_progLast);
            progStack.Children.Add(_progSL);
            progStack.Children.Add(_progTP1);
            progStack.Children.Add(_progTP2);
            progStack.Children.Add(_progTP);
            progStack.Children.Add(_progPnL);
            progStack.Children.Add(_progContracts);
            progStack.Children.Add(_progDuration);

            root.Children.Add(NY930Theme.Panel(progStack, new Thickness(0, 12, 0, 0)));

            // ── Result box (hidden until first close) ───────────
            var resultStack = new StackPanel();
            _resultTitle = new TextBlock
            {
                Text       = NY930Localization.T("result.title"),
                FontSize   = 11,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.GoldBrush,
                Margin     = new Thickness(0, 0, 0, 6)
            };
            _resultLine1 = new TextBlock { FontSize = 11, Foreground = NY930Theme.TextHiBrush };
            _resultLine2 = new TextBlock { FontSize = 10, Foreground = NY930Theme.TextMidBrush };
            resultStack.Children.Add(_resultTitle);
            resultStack.Children.Add(_resultLine1);
            resultStack.Children.Add(_resultLine2);
            _resultBox = NY930Theme.Panel(resultStack, new Thickness(0, 12, 0, 0));
            _resultBox.Visibility = Visibility.Collapsed;
            root.Children.Add(_resultBox);

            // ── Subscribe ───────────────────────────────────────
            NY930Bridge.OpenRangeChanged += OnSnapshot;
            // Render whatever the bridge has cached at construction time.
            var current = NY930Bridge.GetOpenRange();
            if (current != null) OnSnapshot(current);
            UpdateButtonStates(current);
        }

        // ── Helpers ──────────────────────────────────────────────

        private static FrameworkElement WrapPriceRow(FrameworkElement label, FrameworkElement price)
        {
            var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetColumn(label, 0);
            Grid.SetColumn(price, 1);
            g.Children.Add(label);
            g.Children.Add(price);
            return g;
        }

        private static TextBlock BuildPriceRow(string label, Color tint, out FrameworkElement labelOut)
        {
            var border = new Border
            {
                Background      = NY930Theme.BrushAlpha(tint, 0x33),
                CornerRadius    = new CornerRadius(10),
                Padding         = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            border.Child = new TextBlock
            {
                Text     = label,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(tint)
            };
            labelOut = border;

            var price = new TextBlock
            {
                Text     = "—",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(tint),
                FontFamily = new FontFamily("Consolas"),
                VerticalAlignment = VerticalAlignment.Center
            };
            return price;
        }

        private Border BuildStepCard(string title, string sub, Color accent,
            string upGlyph, string downGlyph,
            Action onUp, Action onDown,
            Action<int> onStep, int defStep,
            out TextBlock titleTb, out TextBlock subTb,
            out TextBlock valueTb, out TextBlock ticksTb)
        {
            var stack = new StackPanel();

            titleTb = new TextBlock
            {
                Text     = title,
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(accent),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            subTb = new TextBlock
            {
                Text     = sub,
                FontSize = 9,
                Foreground = NY930Theme.TextLowBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin   = new Thickness(0, 0, 0, 8)
            };
            stack.Children.Add(titleTb);
            stack.Children.Add(subTb);

            var btnUp = NY930Theme.ActionButton(upGlyph, accent);
            btnUp.Margin = new Thickness(0, 0, 0, 4);
            btnUp.FontSize = 16;
            btnUp.Click += (s, e) => onUp();
            stack.Children.Add(btnUp);

            // Local copies — needed because lambdas below cannot
            // capture `out` parameters directly (CS1628).
            var valueLocal = new TextBlock
            {
                Text     = defStep.ToString(),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.TextHiBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin   = new Thickness(0, 4, 0, 0)
            };
            var ticksLocal = new TextBlock
            {
                Text     = NY930Localization.T("common.ticks").ToUpperInvariant(),
                FontSize = 8,
                Foreground = NY930Theme.TextLowBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            valueTb = valueLocal;
            ticksTb = ticksLocal;
            stack.Children.Add(valueLocal);
            stack.Children.Add(ticksLocal);

            var btnDown = NY930Theme.ActionButton(downGlyph, accent);
            btnDown.Margin = new Thickness(0, 4, 0, 6);
            btnDown.FontSize = 16;
            btnDown.Click += (s, e) => onDown();
            stack.Children.Add(btnDown);

            // Step chips
            int[] steps = { 1, 5, 10, 25 };
            var chips = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center };
            Button[] chipBtns = new Button[steps.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                int sv = steps[i]; int idx = i;
                var chip = new Button
                {
                    Content = sv.ToString(),
                    FontSize = 9,
                    FontWeight = FontWeights.Bold,
                    Padding  = new Thickness(8, 2, 8, 2),
                    Margin   = new Thickness(2),
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

        private static TextBlock ProgRow(string label, string value)
        {
            var tb = new TextBlock
            {
                FontSize = 11,
                Foreground = NY930Theme.TextHiBrush,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 2, 0, 2),
                Tag    = label
            };
            tb.Text = (label + ":").PadRight(14) + value;
            return tb;
        }

        private static void SetProg(TextBlock row, string value)
        {
            string label = row.Tag as string ?? string.Empty;
            row.Text = (label + ":").PadRight(14) + value;
        }

        // ── Send actions to the strategy ─────────────────────────
        private void Send(NY930ActionType type, int arg)
        {
            NY930Bridge.RequestOpenRangeAction(new NY930Action { Type = type, IntArg = arg });
        }

        // ── React to snapshots ──────────────────────────────────
        private void OnSnapshot(NY930OpenRangeSnapshot snap)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (snap == null) return;

                _buyPrice.Text  = snap.LongEntryStopPrice  > 0 ? snap.LongEntryStopPrice.ToString("F5")  : "—";
                _sellPrice.Text = snap.ShortEntryStopPrice > 0 ? snap.ShortEntryStopPrice.ToString("F5") : "—";

                if (snap.LongEntryStopPrice > 0 && snap.ShortEntryStopPrice > 0 && snap.TickSize > 0)
                {
                    int sp = (int)Math.Round((snap.LongEntryStopPrice - snap.ShortEntryStopPrice) / snap.TickSize);
                    _spread.Text = NY930Localization.T("or.spread") + ": " + sp + " " + NY930Localization.T("common.ticks");
                }
                else
                {
                    _spread.Text = NY930Localization.T("or.spread") + ": —";
                }

                // Status banner
                if (snap.InLong)         { _status.Text = NY930Localization.T("status.in_long");   _status.Foreground = NY930Theme.LongGreenBrush; }
                else if (snap.InShort)   { _status.Text = NY930Localization.T("status.in_short");  _status.Foreground = NY930Theme.ShortRedBrush; }
                else if (snap.SessionDone) { _status.Text = NY930Localization.T("status.session_done"); _status.Foreground = NY930Theme.TextLowBrush; }
                else if (snap.LongEntryWorking || snap.ShortEntryWorking)
                {                          _status.Text = NY930Localization.T("status.active");    _status.Foreground = NY930Theme.GoldBrush; }
                else
                {                          _status.Text = NY930Localization.T("status.waiting").Replace("{0}", "09:29:58");
                                           _status.Foreground = NY930Theme.WarnAmberBrush; }

                // Progress
                SetProg(_progEntry,     snap.EntryFill > 0 ? snap.EntryFill.ToString("F5") : "—");
                SetProg(_progLast,      snap.LastPrice > 0 ? snap.LastPrice.ToString("F5") : "—");
                SetProg(_progSL,        snap.SlPrice  > 0 ? snap.SlPrice.ToString("F5") : "—");
                SetProg(_progTP1,       snap.P1Price  > 0 ? snap.P1Price.ToString("F5") + (snap.Partial1Done ? " ✓" : "") : "—");
                SetProg(_progTP2,       snap.P2Price  > 0 ? snap.P2Price.ToString("F5") + (snap.Partial2Done ? " ✓" : "") : "—");
                SetProg(_progTP,        snap.TpPrice  > 0 ? snap.TpPrice.ToString("F5") : "—");
                SetProg(_progPnL,       snap.UnrealizedTicks.ToString("F1") + " " + NY930Localization.T("progress.ticks"));
                SetProg(_progContracts, snap.ContractsRemaining + " / " + snap.Quantity);

                if (snap.TradeStartTime != DateTime.MinValue && snap.InPositionEffective())
                {
                    var dur = DateTime.Now - snap.TradeStartTime;
                    SetProg(_progDuration, ((int)dur.TotalMinutes).ToString("D2") + ":" + dur.Seconds.ToString("D2"));
                }
                else
                {
                    SetProg(_progDuration, "—");
                }

                // PnL color
                if (snap.UnrealizedTicks > 0) _progPnL.Foreground = NY930Theme.LongGreenBrush;
                else if (snap.UnrealizedTicks < 0) _progPnL.Foreground = NY930Theme.ShortRedBrush;
                else _progPnL.Foreground = NY930Theme.TextHiBrush;

                // Result box
                var r = snap.LastResult;
                if (r != null)
                {
                    _resultBox.Visibility = Visibility.Visible;
                    bool win = r.PnLTicks >= 0;
                    _resultLine1.Foreground = win ? NY930Theme.LongGreenBrush : NY930Theme.ShortRedBrush;
                    _resultLine1.Text = (win ? NY930Localization.T("result.profit") : NY930Localization.T("result.loss"))
                        + ": " + r.PnLTicks.ToString("F1") + " " + NY930Localization.T("progress.ticks")
                        + "  (" + r.PnLCurrency.ToString("C") + ")";
                    _resultLine2.Text = NY930Localization.T("result.entry") + " " + r.EntryPrice.ToString("F5")
                        + "  →  " + NY930Localization.T("result.exit") + " " + r.ExitPrice.ToString("F5")
                        + "   [" + NY930Localization.T("result.reason") + ": " + r.ExitReason + "]";
                }

                UpdateButtonStates(snap);
            });
        }

        private void UpdateButtonStates(NY930OpenRangeSnapshot snap)
        {
            bool canEnter = snap == null
                          || (!snap.LongEntryWorking && !snap.ShortEntryWorking
                              && !snap.InLong && !snap.InShort && !snap.SessionDone);

            bool inPosition = snap != null && (snap.InLong || snap.InShort);

            _btnBuyNow.IsEnabled  = canEnter;
            _btnSellNow.IsEnabled = canEnter;
            _btnCancel.IsEnabled  = snap != null && (snap.LongEntryWorking || snap.ShortEntryWorking);
            _btnFlatten.IsEnabled = inPosition;
            _btnPartial.IsEnabled = inPosition;
        }

        public void RefreshLocalization()
        {
            _moveTitle.Text   = NY930Localization.T("or.move.title");
            _moveSub.Text     = NY930Localization.T("or.move.sub");
            _spreadTitle.Text = NY930Localization.T("or.spread.title");
            _spreadSub.Text   = NY930Localization.T("or.spread.sub");
            _ticksLbl1.Text   = NY930Localization.T("common.ticks").ToUpperInvariant();
            _ticksLbl2.Text   = NY930Localization.T("common.ticks").ToUpperInvariant();
            _btnCancel.Content  = NY930Localization.T("or.cancel");
            _btnBuyNow.Content  = NY930Localization.T("or.buy_now");
            _btnSellNow.Content = NY930Localization.T("or.sell_now");
            _btnFlatten.Content = NY930Localization.T("or.flatten");
            _progTitle.Text     = NY930Localization.T("progress.title");
            _resultTitle.Text   = NY930Localization.T("result.title");
        }

        public void Dispose()
        {
            NY930Bridge.OpenRangeChanged -= OnSnapshot;
        }
    }

    internal static class OpenRangeSnapshotExtensions
    {
        public static bool InPositionEffective(this NY930OpenRangeSnapshot snap)
            => snap.InLong || snap.InShort;
    }
}
