// ============================================================
//  NY930HedgeView — control panel for Apertura
// ------------------------------------------------------------
//  Layout:
//    1. Status banner
//    2. Direction badge (Long / Short / None) + entry time
//    3. Manual entry: BUY NOW / SELL NOW (before fill)
//    4. CLOSE POSITION + manual partial close (after fill)
//    5. Progress strip identical to Open Range
//    6. Result row after close
//
//  All persistent strategy parameters (qty, SL, TP, BE, Trail,
//  TrailTP, Partials, TimeExit, Gap Guards) are managed via the
//  standard NinjaScript parameter dialog of the Apertura
//  strategy. The AddOn keeps the live actions for things the
//  parameter dialog cannot give: instant manual entry, instant
//  flatten, instant partial close, live progress.
// ============================================================

#region Using declarations
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public sealed class NY930HedgeView : Grid, INY930Localizable, IDisposable
    {
        private readonly NY930ShellView _shell;

        private TextBlock _status;
        private TextBlock _directionBadge;
        private TextBlock _instrument;

        private Button   _btnBuyNow;
        private Button   _btnSellNow;
        private Button   _btnFlatten;
        private Button   _btnBE;
        private TextBox  _partialQty;
        private Button   _btnPartial;
        private Button   _btnCancelEntry;

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

        private Border    _resultBox;
        private TextBlock _resultTitle;
        private TextBlock _resultLine1;
        private TextBlock _resultLine2;

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

            var root = new StackPanel { Margin = new Thickness(14) };
            scroll.Content = root;

            // Status
            _status = new TextBlock
            {
                Text       = NY930Localization.T("status.no_strategy"),
                FontSize   = 12,
                Foreground = NY930Theme.WarnAmberBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 0, 0, 12)
            };
            root.Children.Add(_status);

            // Direction badge + instrument
            var headerStack = new StackPanel();
            _directionBadge = new TextBlock
            {
                Text     = NY930Localization.T("hedge.none"),
                FontSize = 22,
                FontWeight = FontWeights.Black,
                Foreground = NY930Theme.GoldBrightBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _instrument = new TextBlock
            {
                Text     = "—",
                FontSize = 10,
                Foreground = NY930Theme.TextLowBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin   = new Thickness(0, 2, 0, 0)
            };
            headerStack.Children.Add(_directionBadge);
            headerStack.Children.Add(_instrument);
            root.Children.Add(NY930Theme.Panel(headerStack));

            // Manual entries
            var manGrid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            manGrid.ColumnDefinitions.Add(new ColumnDefinition());
            manGrid.ColumnDefinitions.Add(new ColumnDefinition());
            _btnBuyNow  = NY930Theme.ActionButton(NY930Localization.T("or.buy_now"),  NY930Theme.LongGreen);
            _btnSellNow = NY930Theme.ActionButton(NY930Localization.T("or.sell_now"), NY930Theme.ShortRed);
            _btnBuyNow.Margin  = new Thickness(0, 0, 4, 0);
            _btnSellNow.Margin = new Thickness(4, 0, 0, 0);
            _btnBuyNow.Click  += (s, e) => Send(NY930ActionType.HedgeBuyNow,  0);
            _btnSellNow.Click += (s, e) => Send(NY930ActionType.HedgeSellNow, 0);
            Grid.SetColumn(_btnBuyNow,  0);
            Grid.SetColumn(_btnSellNow, 1);
            manGrid.Children.Add(_btnBuyNow);
            manGrid.Children.Add(_btnSellNow);
            root.Children.Add(manGrid);

            // Cancel entry (before fill if user used schedule)
            _btnCancelEntry = NY930Theme.OutlineButton("Cancel pending entry");
            _btnCancelEntry.Margin = new Thickness(0, 4, 0, 4);
            _btnCancelEntry.Click += (s, e) => Send(NY930ActionType.HedgeCancelEntry, 0);
            root.Children.Add(_btnCancelEntry);

            // Position management
            _btnBE = NY930Theme.OutlineButton("Move SL → Breakeven");
            _btnBE.Margin = new Thickness(0, 4, 0, 4);
            _btnBE.Click += (s, e) => Send(NY930ActionType.HedgeBreakeven, 0);
            root.Children.Add(_btnBE);

            _btnFlatten = NY930Theme.ActionButton(NY930Localization.T("or.flatten"), NY930Theme.WarnAmber);
            _btnFlatten.Margin = new Thickness(0, 4, 0, 4);
            _btnFlatten.Click += (s, e) => Send(NY930ActionType.HedgeFlatten, 0);
            root.Children.Add(_btnFlatten);

            // Partial close row
            var partialRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
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
                    Send(NY930ActionType.HedgePartialClose, n);
            };
            Grid.SetColumn(_btnPartial, 1);
            partialRow.Children.Add(_partialQty);
            partialRow.Children.Add(_btnPartial);
            root.Children.Add(partialRow);

            // Progress
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

            root.Children.Add(NY930Theme.Panel(progStack, new Thickness(0, 8, 0, 0)));

            // Result
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
            _resultBox = NY930Theme.Panel(resultStack, new Thickness(0, 8, 0, 0));
            _resultBox.Visibility = Visibility.Collapsed;
            root.Children.Add(_resultBox);

            NY930Bridge.HedgeChanged += OnSnapshot;
            var current = NY930Bridge.GetHedge();
            if (current != null) OnSnapshot(current);
            UpdateButtonStates(current);
        }

        private static TextBlock ProgRow(string label, string value)
        {
            var tb = new TextBlock
            {
                FontSize = 11,
                Foreground = NY930Theme.TextHiBrush,
                FontFamily = new FontFamily("Consolas"),
                Margin = new Thickness(0, 2, 0, 2),
                Tag = label
            };
            tb.Text = (label + ":").PadRight(14) + value;
            return tb;
        }

        private static void SetProg(TextBlock row, string value)
        {
            string label = row.Tag as string ?? string.Empty;
            row.Text = (label + ":").PadRight(14) + value;
        }

        private void Send(NY930ActionType type, int arg)
        {
            NY930Bridge.RequestHedgeAction(new NY930Action { Type = type, IntArg = arg });
        }

        private void OnSnapshot(NY930HedgeSnapshot snap)
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (snap == null) return;

                _instrument.Text = snap.Instrument ?? "—";

                if (snap.Direction == "Long")
                {
                    _directionBadge.Text = NY930Localization.T("hedge.long").ToUpperInvariant();
                    _directionBadge.Foreground = NY930Theme.LongGreenBrush;
                }
                else if (snap.Direction == "Short")
                {
                    _directionBadge.Text = NY930Localization.T("hedge.short").ToUpperInvariant();
                    _directionBadge.Foreground = NY930Theme.ShortRedBrush;
                }
                else
                {
                    _directionBadge.Text = NY930Localization.T("hedge.none").ToUpperInvariant();
                    _directionBadge.Foreground = NY930Theme.GoldDimBrush;
                }

                if (snap.InPosition)
                {
                    _status.Text = snap.Direction == "Long"
                        ? NY930Localization.T("status.in_long")
                        : NY930Localization.T("status.in_short");
                    _status.Foreground = snap.Direction == "Long"
                        ? NY930Theme.LongGreenBrush
                        : NY930Theme.ShortRedBrush;
                }
                else if (snap.SessionDone)
                {
                    _status.Text = NY930Localization.T("status.session_done");
                    _status.Foreground = NY930Theme.TextLowBrush;
                }
                else
                {
                    _status.Text = NY930Localization.T("status.waiting").Replace("{0}", "09:29:58");
                    _status.Foreground = NY930Theme.WarnAmberBrush;
                }

                SetProg(_progEntry,     snap.EntryFill > 0 ? snap.EntryFill.ToString("F5") : "—");
                SetProg(_progLast,      snap.LastPrice > 0 ? snap.LastPrice.ToString("F5") : "—");
                SetProg(_progSL,        snap.SlPrice  > 0 ? snap.SlPrice.ToString("F5") : "—");
                SetProg(_progTP1,       snap.P1Price  > 0 ? snap.P1Price.ToString("F5") + (snap.Partial1Done ? " ✓" : "") : "—");
                SetProg(_progTP2,       snap.P2Price  > 0 ? snap.P2Price.ToString("F5") + (snap.Partial2Done ? " ✓" : "") : "—");
                SetProg(_progTP,        snap.TpPrice  > 0 ? snap.TpPrice.ToString("F5") : "—");
                SetProg(_progPnL,       snap.UnrealizedTicks.ToString("F1") + " " + NY930Localization.T("progress.ticks"));
                SetProg(_progContracts, snap.ContractsRemaining + " / " + snap.Quantity);

                if (snap.TradeStartTime != DateTime.MinValue && snap.InPosition)
                {
                    var dur = DateTime.Now - snap.TradeStartTime;
                    SetProg(_progDuration, ((int)dur.TotalMinutes).ToString("D2") + ":" + dur.Seconds.ToString("D2"));
                }
                else SetProg(_progDuration, "—");

                if (snap.UnrealizedTicks > 0) _progPnL.Foreground = NY930Theme.LongGreenBrush;
                else if (snap.UnrealizedTicks < 0) _progPnL.Foreground = NY930Theme.ShortRedBrush;
                else _progPnL.Foreground = NY930Theme.TextHiBrush;

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

        private void UpdateButtonStates(NY930HedgeSnapshot snap)
        {
            bool canEnter = snap == null || (!snap.InPosition && !snap.SessionDone);
            bool inPos    = snap != null && snap.InPosition;

            _btnBuyNow.IsEnabled  = canEnter;
            _btnSellNow.IsEnabled = canEnter;
            _btnFlatten.IsEnabled = inPos;
            _btnBE.IsEnabled      = inPos;
            _btnPartial.IsEnabled = inPos;
            _btnCancelEntry.IsEnabled = snap != null && !inPos && !canEnter;
        }

        public void RefreshLocalization()
        {
            _btnBuyNow.Content     = NY930Localization.T("or.buy_now");
            _btnSellNow.Content    = NY930Localization.T("or.sell_now");
            _btnFlatten.Content    = NY930Localization.T("or.flatten");
            _progTitle.Text        = NY930Localization.T("progress.title");
            _resultTitle.Text      = NY930Localization.T("result.title");
        }

        public void Dispose()
        {
            NY930Bridge.HedgeChanged -= OnSnapshot;
        }
    }
}
