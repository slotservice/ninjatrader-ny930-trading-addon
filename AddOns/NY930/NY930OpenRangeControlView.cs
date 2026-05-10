// ============================================================
//  NY930OpenRangeControlView — v1.3
//  Reference: ny930-openrange-control.html
// ------------------------------------------------------------
//  Floating control panel for the working Buy Stop / Sell Stop
//  orders. Shown after the strategy fires and before any side
//  fills. Contains:
//
//    .header               NY930 logo + "OPEN RANGE / CONTROL"
//    .strategy-label       "OPEN RANGE CONTROL"
//    .price-box            ▲ BUY STOP + price
//                          midpoint • spread
//                          ▼ SELL STOP + price
//    .section-lbl          "Control de órdenes"
//    .cards-row            MOVE BOTH (blue) + SPREAD (green)
//                          each: ▲ / num / TICKS / ▼ / chips
//    .cancel-btn           ✕ Cancel Orders
// ============================================================

#region Using declarations
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public sealed class NY930OpenRangeControlView : Grid, INY930Localizable, IDisposable
    {
        private readonly NY930ShellView _shell;

        // Live read-out
        private TextBlock _buyPrice, _sellPrice, _midText, _spreadText;

        // Move / Spread step state
        private TextBlock _moveNum,   _spreadNum;
        private int       _moveStep   = 5;
        private int       _spreadStep = 10;

        public NY930OpenRangeControlView(NY930ShellView shell)
        {
            _shell     = shell;
            Background = NY930Theme.BgBrush;

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent
            };
            Children.Add(scroll);

            var stack = new StackPanel { MaxWidth = NY930Theme.PanelWidth };
            scroll.Content = stack;

            stack.Children.Add(BuildHeader());
            stack.Children.Add(NY930Theme.StrategyLabel("OPEN RANGE CONTROL"));
            stack.Children.Add(BuildPriceBox());
            stack.Children.Add(NY930Theme.Divider());
            stack.Children.Add(NY930Theme.SectionLabel("Control de órdenes"));
            stack.Children.Add(BuildCardsRow());
            stack.Children.Add(BuildCancel());

            NY930Bridge.OpenRangeChanged += OnSnapshot;
            var current = NY930Bridge.GetOpenRange();
            if (current != null) Render(current);
        }

        // ── Header (NY930 + "OPEN RANGE / CONTROL") ────────────
        private FrameworkElement BuildHeader()
        {
            var h = new NY930Theme.PanelHeader();
            var rightText = new TextBlock
            {
                FontFamily = NY930Theme.MonoFont,
                FontSize   = 8,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.Text3Brush,
                LineHeight = 11,
                TextAlignment = TextAlignment.Right
            };
            rightText.Inlines.Add(new Run("OPEN RANGE"));
            rightText.Inlines.Add(new LineBreak());
            rightText.Inlines.Add(new Run("CONTROL"));
            h.Right.Children.Add(rightText);
            return h;
        }

        // ── Price box ──────────────────────────────────────────
        private FrameworkElement BuildPriceBox()
        {
            var box = new Border
            {
                Margin          = new Thickness(9, 7, 9, 0),
                Background      = NY930Theme.Bg2Brush,
                BorderBrush     = NY930Theme.Border2Brush,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(5),
                Padding         = new Thickness(9, 7, 9, 7)
            };

            var stack = new StackPanel();

            // BUY STOP row
            stack.Children.Add(BuildPriceRow(true, out _buyPrice));

            // Midpoint / Spread divider row
            var midRow = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            midRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            midRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var midText = new TextBlock
            {
                FontFamily = NY930Theme.MonoFont,
                FontSize   = 9,
                Foreground = NY930Theme.Text3Brush
            };
            midText.Inlines.Add(new Run("Midpoint: ") { Foreground = NY930Theme.Text3Brush });
            _midText = new TextBlock
            {
                Text = "—",
                FontFamily = NY930Theme.MonoFont,
                FontSize = 9, FontWeight = FontWeights.SemiBold,
                Foreground = NY930Theme.Text2Brush
            };
            // Embed the dynamic value as an inline UIContainer.
            midText.Inlines.Add(new InlineUIContainer(_midText));
            Grid.SetColumn(midText, 0);
            midRow.Children.Add(midText);

            var spreadHolder = new TextBlock
            {
                FontFamily = NY930Theme.MonoFont,
                FontSize   = 9,
                Foreground = NY930Theme.Text3Brush,
                TextAlignment = TextAlignment.Right
            };
            spreadHolder.Inlines.Add(new Run("Spread: ") { Foreground = NY930Theme.Text3Brush });
            _spreadText = new TextBlock
            {
                Text = "—",
                FontFamily = NY930Theme.MonoFont,
                FontSize = 9, FontWeight = FontWeights.SemiBold,
                Foreground = NY930Theme.Text2Brush
            };
            spreadHolder.Inlines.Add(new InlineUIContainer(_spreadText));
            Grid.SetColumn(spreadHolder, 1);
            midRow.Children.Add(spreadHolder);

            // Top + bottom borders for the mid row to match CSS
            var midBordered = new Border
            {
                BorderBrush     = NY930Theme.BorderBrush,
                BorderThickness = new Thickness(0, 1, 0, 1),
                Padding         = new Thickness(0, 3, 0, 3),
                Child           = midRow
            };
            stack.Children.Add(midBordered);

            // SELL STOP row
            stack.Children.Add(BuildPriceRow(false, out _sellPrice));

            box.Child = stack;
            return box;
        }

        private FrameworkElement BuildPriceRow(bool isBuy, out TextBlock priceTb)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Color tint = isBuy ? NY930Theme.Green : NY930Theme.Red;
            string label = isBuy ? "▲ BUY STOP" : "▼ SELL STOP";

            var badge = new Border
            {
                Background      = NY930Theme.BrushAlpha(tint, 0x1F),
                BorderBrush     = NY930Theme.BrushAlpha(tint, 0x33),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(10),
                Padding         = new Thickness(7, 2, 7, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = new TextBlock
                {
                    Text       = label,
                    FontFamily = NY930Theme.MonoFont,
                    FontSize   = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = NY930Theme.BrushAlpha(tint, 0xD8)
                }
            };
            Grid.SetColumn(badge, 0);
            grid.Children.Add(badge);

            priceTb = new TextBlock
            {
                Text       = "—",
                FontFamily = NY930Theme.MonoFont,
                FontSize   = 13,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.BrushAlpha(tint, 0xB3),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(priceTb, 1);
            grid.Children.Add(priceTb);
            return grid;
        }

        // ── Move / Spread cards ────────────────────────────────
        private FrameworkElement BuildCardsRow()
        {
            var grid = new Grid { Margin = new Thickness(9, 5, 9, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            var moveCard   = BuildCtrlCard("MOVE BOTH", "Keeps the distance",
                NY930Theme.Blue, "▲", "▼",
                () => Send(NY930ActionType.OpenRangeMoveBoth, +_moveStep),
                () => Send(NY930ActionType.OpenRangeMoveBoth, -_moveStep),
                v => _moveStep = v, _moveStep, out _moveNum);
            moveCard.Margin = new Thickness(0, 0, 2, 0);
            Grid.SetColumn(moveCard, 0);

            var spreadCard = BuildCtrlCard("SPREAD", "Keeps the midpoint",
                NY930Theme.Green, "+", "−",
                () => Send(NY930ActionType.OpenRangeAdjustSpread, +_spreadStep),
                () => Send(NY930ActionType.OpenRangeAdjustSpread, -_spreadStep),
                v => _spreadStep = v, _spreadStep, out _spreadNum);
            spreadCard.Margin = new Thickness(2, 0, 0, 0);
            Grid.SetColumn(spreadCard, 1);

            grid.Children.Add(moveCard);
            grid.Children.Add(spreadCard);
            return grid;
        }

        private Border BuildCtrlCard(string title, string sub, Color tint,
                                     string upGlyph, string downGlyph,
                                     Action onUp, Action onDown,
                                     Action<int> onStep, int defStep,
                                     out TextBlock numTb)
        {
            var card = new Border
            {
                Background      = NY930Theme.Bg3Brush,
                BorderBrush     = NY930Theme.Border2Brush,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(5),
                Padding         = new Thickness(5, 7, 5, 7)
            };

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

            stack.Children.Add(new TextBlock
            {
                Text       = title,
                FontFamily = NY930Theme.SansFont,
                FontSize   = 9, FontWeight = FontWeights.Black,
                Foreground = NY930Theme.BrushAlpha(tint, 0xC0),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center
            });
            stack.Children.Add(new TextBlock
            {
                Text       = sub,
                FontSize   = 8,
                Foreground = NY930Theme.Text3Brush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 0, 0, 4)
            });

            var btnUp = MakeCtrlBtn(upGlyph, tint);
            btnUp.Click += (s, e) => onUp();
            stack.Children.Add(btnUp);

            numTb = new TextBlock
            {
                Text       = defStep.ToString(),
                FontFamily = NY930Theme.MonoFont,
                FontSize   = 22, FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.TextBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 4, 0, 0)
            };
            stack.Children.Add(numTb);
            stack.Children.Add(new TextBlock
            {
                Text       = "TICKS",
                FontSize   = 8, FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.Text3Brush,
                HorizontalAlignment = HorizontalAlignment.Center
            });

            var btnDown = MakeCtrlBtn(downGlyph, tint);
            btnDown.Margin = new Thickness(0, 4, 0, 0);
            btnDown.Click += (s, e) => onDown();
            stack.Children.Add(btnDown);

            // Chips
            int[] steps = { 1, 5, 10, 25 };
            var chips = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
            Button[] chipBtns = new Button[steps.Length];
            TextBlock numLocal = numTb;
            for (int i = 0; i < steps.Length; i++)
            {
                int sv = steps[i]; int idx = i;
                var chip = MakeChip(sv, sv == defStep, tint);
                chipBtns[i] = chip;
                int captured = sv;
                chip.Click += (s, e) =>
                {
                    onStep(captured);
                    numLocal.Text = captured.ToString();
                    for (int j = 0; j < chipBtns.Length; j++)
                        StyleChip(chipBtns[j], j == idx, tint);
                };
                chips.Children.Add(chip);
            }
            stack.Children.Add(chips);

            card.Child = stack;
            return card;
        }

        private Button MakeCtrlBtn(string glyph, Color tint)
        {
            return new Button
            {
                Content    = glyph,
                Background = NY930Theme.Bg2Brush,
                Foreground = NY930Theme.Text2Brush,
                BorderBrush = NY930Theme.Border2Brush,
                BorderThickness = new Thickness(1),
                Padding    = new Thickness(0, 5, 0, 5),
                FontSize   = 15, FontWeight = FontWeights.Bold,
                Cursor     = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        private Button MakeChip(int value, bool active, Color tint)
        {
            var b = new Button
            {
                Content    = value.ToString(),
                FontFamily = NY930Theme.MonoFont,
                FontSize   = 8, FontWeight = FontWeights.Bold,
                Padding    = new Thickness(4, 2, 4, 2),
                Margin     = new Thickness(1),
                BorderThickness = new Thickness(1),
                Cursor     = System.Windows.Input.Cursors.Hand
            };
            StyleChip(b, active, tint);
            return b;
        }

        private void StyleChip(Button b, bool active, Color tint)
        {
            if (active)
            {
                b.Background = NY930Theme.BrushAlpha(tint, 0x38);
                b.BorderBrush = NY930Theme.BrushAlpha(tint, 0x66);
                b.Foreground = NY930Theme.SolidBrush(tint);
            }
            else
            {
                b.Background = Brushes.Transparent;
                b.BorderBrush = NY930Theme.BorderBrush;
                b.Foreground = NY930Theme.Text3Brush;
            }
        }

        // ── Cancel button ─────────────────────────────────────
        private FrameworkElement BuildCancel()
        {
            var btn = NY930Theme.CancelButton("✕ Cancel Orders");
            btn.Margin = new Thickness(9, 7, 9, 12);
            btn.Click += (s, e) => Send(NY930ActionType.OpenRangeCancelAll, 0);
            return btn;
        }

        // ── Bridge plumbing ───────────────────────────────────
        private void Send(NY930ActionType type, int arg = 0)
        {
            NY930Bridge.RequestOpenRangeAction(new NY930Action { Type = type, IntArg = arg });
        }

        private void OnSnapshot(NY930OpenRangeSnapshot s) => Dispatcher.InvokeAsync(() => Render(s));

        private void Render(NY930OpenRangeSnapshot s)
        {
            if (s == null) return;

            Func<double, string> fmt = v => v > 0 ? v.ToString("F2", CultureInfo.InvariantCulture) : "—";
            _buyPrice.Text  = fmt(s.LongEntryStopPrice);
            _sellPrice.Text = fmt(s.ShortEntryStopPrice);

            if (s.LongEntryStopPrice > 0 && s.ShortEntryStopPrice > 0 && s.TickSize > 0)
            {
                int sp = (int)Math.Round((s.LongEntryStopPrice - s.ShortEntryStopPrice) / s.TickSize);
                double mid = (s.LongEntryStopPrice + s.ShortEntryStopPrice) / 2.0;
                _midText.Text    = mid.ToString("F2", CultureInfo.InvariantCulture);
                _spreadText.Text = sp + " tks";
            }
            else
            {
                _midText.Text    = "—";
                _spreadText.Text = "—";
            }

            // Auto-route to in-trade view on fill.
            if ((s.InLong || s.InShort) && _shell.CurrentViewIs<NY930OpenRangeControlView>())
                _shell.Show(new NY930ProgressView(_shell, isOpenRange: true));
        }

        public void RefreshLocalization() { /* labels are Spanish per mockup */ }

        public void Dispose()
        {
            NY930Bridge.OpenRangeChanged -= OnSnapshot;
        }
    }
}
