// ============================================================
//  NY930Theme — gold-on-black palette + reusable WPF helpers
// ------------------------------------------------------------
//  Colours match the approved HOMEPAGE APP 2 reference:
//    - Background : near-black with subtle warm tint
//    - Accent     : warm gold (#d4af37)
//    - Long  cue  : emerald green (#22c55e)
//    - Short cue  : crimson red   (#ef4444)
//    - Neutral text: light grey on dark / muted grey for hints
// ============================================================

#region Using declarations
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public static class NY930Theme
    {
        // ── Palette ───────────────────────────────────────────
        public static readonly Color BgBase     = ColorFromHex("#0a0a0a");
        public static readonly Color BgPanel    = ColorFromHex("#13110b");
        public static readonly Color BgCard     = ColorFromHex("#1a160d");
        public static readonly Color BgInput    = ColorFromHex("#0f0d08");
        public static readonly Color BorderSoft = ColorFromHex("#2a2418");

        public static readonly Color GoldBright = ColorFromHex("#f0c14b");
        public static readonly Color Gold       = ColorFromHex("#d4af37");
        public static readonly Color GoldDim    = ColorFromHex("#8b7325");

        public static readonly Color TextHi     = ColorFromHex("#f5f1e6");
        public static readonly Color TextMid    = ColorFromHex("#bdb38e");
        public static readonly Color TextLow    = ColorFromHex("#74694a");

        public static readonly Color LongGreen  = ColorFromHex("#22c55e");
        public static readonly Color LongGreenDim = ColorFromHex("#1f7a3f");
        public static readonly Color ShortRed   = ColorFromHex("#ef4444");
        public static readonly Color ShortRedDim = ColorFromHex("#7a1f1f");
        public static readonly Color WarnAmber  = ColorFromHex("#f59e0b");

        // ── Brushes (cached singletons) ───────────────────────
        public static readonly SolidColorBrush BgBaseBrush     = Freeze(new SolidColorBrush(BgBase));
        public static readonly SolidColorBrush BgPanelBrush    = Freeze(new SolidColorBrush(BgPanel));
        public static readonly SolidColorBrush BgCardBrush     = Freeze(new SolidColorBrush(BgCard));
        public static readonly SolidColorBrush BgInputBrush    = Freeze(new SolidColorBrush(BgInput));
        public static readonly SolidColorBrush BorderBrush     = Freeze(new SolidColorBrush(BorderSoft));
        public static readonly SolidColorBrush GoldBrush       = Freeze(new SolidColorBrush(Gold));
        public static readonly SolidColorBrush GoldBrightBrush = Freeze(new SolidColorBrush(GoldBright));
        public static readonly SolidColorBrush GoldDimBrush    = Freeze(new SolidColorBrush(GoldDim));
        public static readonly SolidColorBrush TextHiBrush     = Freeze(new SolidColorBrush(TextHi));
        public static readonly SolidColorBrush TextMidBrush    = Freeze(new SolidColorBrush(TextMid));
        public static readonly SolidColorBrush TextLowBrush    = Freeze(new SolidColorBrush(TextLow));
        public static readonly SolidColorBrush LongGreenBrush  = Freeze(new SolidColorBrush(LongGreen));
        public static readonly SolidColorBrush ShortRedBrush   = Freeze(new SolidColorBrush(ShortRed));
        public static readonly SolidColorBrush WarnAmberBrush  = Freeze(new SolidColorBrush(WarnAmber));

        // ── Helpers ───────────────────────────────────────────
        public static Color ColorFromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }

        public static SolidColorBrush BrushFromHex(string hex)
        {
            return Freeze(new SolidColorBrush(ColorFromHex(hex)));
        }

        public static SolidColorBrush BrushAlpha(Color baseColor, byte alpha)
        {
            return Freeze(new SolidColorBrush(Color.FromArgb(alpha, baseColor.R, baseColor.G, baseColor.B)));
        }

        public static SolidColorBrush SolidBrush(Color c)
        {
            return Freeze(new SolidColorBrush(c));
        }

        private static SolidColorBrush Freeze(SolidColorBrush b)
        {
            if (b.CanFreeze) b.Freeze();
            return b;
        }

        // ── Component factories ───────────────────────────────

        public static TextBlock Heading(string text, double size = 22)
        {
            return new TextBlock
            {
                Text                = text,
                FontSize            = size,
                FontWeight          = FontWeights.Black,
                Foreground          = GoldBrightBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 4)
            };
        }

        public static TextBlock Subheading(string text)
        {
            return new TextBlock
            {
                Text                = text,
                FontSize            = 10,
                FontWeight          = FontWeights.SemiBold,
                Foreground          = TextLowBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 12)
            };
        }

        public static TextBlock Label(string text, double size = 11)
        {
            return new TextBlock
            {
                Text       = text,
                FontSize   = size,
                Foreground = TextMidBrush,
                Margin     = new Thickness(0, 0, 0, 2)
            };
        }

        public static Border Panel(UIElement child, Thickness? margin = null, Thickness? padding = null)
        {
            return new Border
            {
                Background      = BgPanelBrush,
                BorderBrush     = BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Padding         = padding ?? new Thickness(12),
                Margin          = margin  ?? new Thickness(0, 0, 0, 8),
                Child           = child
            };
        }

        public static Border Card(UIElement child, Brush accent = null, Thickness? margin = null)
        {
            var border = new Border
            {
                Background      = BgCardBrush,
                BorderBrush     = accent ?? GoldDimBrush,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(10),
                Padding         = new Thickness(14),
                Margin          = margin ?? new Thickness(0, 0, 0, 10),
                Child           = child
            };
            border.Effect = new DropShadowEffect
            {
                BlurRadius  = 14,
                ShadowDepth = 0,
                Color       = GoldDim,
                Opacity     = 0.18
            };
            return border;
        }

        public static Button GoldButton(string text)
        {
            var b = new Button
            {
                Content         = text,
                Foreground      = BgBaseBrush,
                Background      = GoldBrush,
                BorderBrush     = GoldBrightBrush,
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(0, 8, 0, 8),
                FontWeight      = FontWeights.Bold,
                FontSize        = 12,
                Cursor          = System.Windows.Input.Cursors.Hand
            };
            return b;
        }

        public static Button OutlineButton(string text, Brush stroke = null)
        {
            var b = new Button
            {
                Content         = text,
                Foreground      = TextHiBrush,
                Background      = BgInputBrush,
                BorderBrush     = stroke ?? GoldDimBrush,
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(0, 8, 0, 8),
                FontWeight      = FontWeights.SemiBold,
                FontSize        = 11,
                Cursor          = System.Windows.Input.Cursors.Hand
            };
            return b;
        }

        public static Button ActionButton(string text, Color tint)
        {
            var bg     = BrushAlpha(tint, 0x33);
            var border = BrushAlpha(tint, 0xaa);
            var fg     = Freeze(new SolidColorBrush(tint));
            var b = new Button
            {
                Content         = text,
                Foreground      = fg,
                Background      = bg,
                BorderBrush     = border,
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(0, 10, 0, 10),
                FontWeight      = FontWeights.Bold,
                FontSize        = 12,
                Cursor          = System.Windows.Input.Cursors.Hand
            };
            return b;
        }

        public static TextBox InputBox(double width = 70)
        {
            return new TextBox
            {
                Background      = BgInputBrush,
                Foreground      = TextHiBrush,
                BorderBrush     = BorderBrush,
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(6, 4, 6, 4),
                Width           = width,
                FontSize        = 12,
                CaretBrush      = GoldBrush
            };
        }

        public static CheckBox Toggle(string text)
        {
            return new CheckBox
            {
                Content    = text,
                Foreground = TextHiBrush,
                FontSize   = 11,
                Margin     = new Thickness(0, 4, 0, 4)
            };
        }

        public static Separator HRule()
        {
            return new Separator
            {
                Background = BorderBrush,
                Margin     = new Thickness(0, 8, 0, 8)
            };
        }

        // ════════════════════════════════════════════════════
        //  Rich components — added in v1.1 for the redesigned
        //  trade-progress, result and parameter views. The
        //  legacy helpers above are unchanged so existing
        //  views keep working until they migrate.
        // ════════════════════════════════════════════════════

        public static readonly Color BlueAccent  = ColorFromHex("#3b82f6");
        public static readonly Color CyanAccent  = ColorFromHex("#22d3ee");
        public static readonly Color SuccessBg   = ColorFromHex("#0e2418");
        public static readonly Color DangerBg    = ColorFromHex("#2a0d10");

        public static readonly SolidColorBrush BlueAccentBrush = Freeze(new SolidColorBrush(BlueAccent));
        public static readonly SolidColorBrush CyanAccentBrush = Freeze(new SolidColorBrush(CyanAccent));
        public static readonly SolidColorBrush SuccessBgBrush  = Freeze(new SolidColorBrush(SuccessBg));
        public static readonly SolidColorBrush DangerBgBrush   = Freeze(new SolidColorBrush(DangerBg));

        public enum TpState { Pending, Active, Done, Failed }

        // ── BigPnL ───────────────────────────────────────────
        // Hero PnL display, like:
        //   +$312.50          ▲ LONG
        //   +25 ticks
        public sealed class BigPnLDisplay : StackPanel
        {
            public TextBlock Currency { get; }
            public TextBlock Side     { get; }
            public TextBlock Ticks    { get; }

            public BigPnLDisplay()
            {
                Margin = new Thickness(0, 0, 0, 8);

                var top = new Grid();
                top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                Currency = new TextBlock
                {
                    Text       = "$0.00",
                    FontSize   = 30,
                    FontWeight = FontWeights.Black,
                    Foreground = CyanAccentBrush,
                    FontFamily = new FontFamily("Segoe UI"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(Currency, 0);
                top.Children.Add(Currency);

                Side = new TextBlock
                {
                    Text       = "—",
                    FontSize   = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = TextLowBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding    = new Thickness(8, 4, 8, 4)
                };
                Grid.SetColumn(Side, 1);
                top.Children.Add(Side);

                Ticks = new TextBlock
                {
                    Text       = "0 ticks",
                    FontSize   = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextMidBrush,
                    Margin     = new Thickness(0, 2, 0, 0)
                };

                Children.Add(top);
                Children.Add(Ticks);
            }

            public void Update(double currency, double ticks, string side)
            {
                bool positive = ticks >= 0;
                Color tint    = positive ? LongGreen : ShortRed;
                Currency.Foreground = positive ? CyanAccentBrush : Freeze(new SolidColorBrush(tint));

                string sign       = ticks >= 0 ? "+" : "";
                Currency.Text     = sign + currency.ToString("C");
                Ticks.Text        = sign + ticks.ToString("F1") + " ticks";
                Ticks.Foreground  = positive ? LongGreenBrush : ShortRedBrush;

                if (string.IsNullOrEmpty(side) || side == "None")
                {
                    Side.Text       = "—";
                    Side.Foreground = TextLowBrush;
                    Side.Background = Brushes.Transparent;
                }
                else
                {
                    bool isLong = side.Equals("Long", StringComparison.OrdinalIgnoreCase);
                    Color sideColor = isLong ? LongGreen : ShortRed;
                    Side.Text       = (isLong ? "▲ " : "▼ ") + side.ToUpperInvariant();
                    Side.Foreground = Freeze(new SolidColorBrush(sideColor));
                    Side.Background = BrushAlpha(sideColor, 0x33);
                }
            }
        }

        // ── Pill ─────────────────────────────────────────────
        // Small inline badge for stats like "06:44", "10 contratos"
        public static Border Pill(string text, Color tint)
        {
            return new Border
            {
                Background      = BrushAlpha(tint, 0x22),
                BorderBrush     = BrushAlpha(tint, 0x66),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(8, 3, 8, 3),
                Margin          = new Thickness(0, 0, 6, 0),
                Child = new TextBlock
                {
                    Text       = text,
                    FontSize   = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Freeze(new SolidColorBrush(tint)),
                    FontFamily = new FontFamily("Consolas")
                }
            };
        }

        // ── TpProgressCard ───────────────────────────────────
        // Card that represents one of TP1 / TP2 / TP / SL with
        // a state icon, label, distance text and optional value.
        public sealed class TpProgressCard : Border
        {
            private readonly TextBlock _icon;
            private readonly TextBlock _label;
            private readonly TextBlock _detail;
            private readonly TextBlock _value;
            private readonly bool      _isSlSide;

            public TpProgressCard(string label, bool isSlSide = false)
            {
                _isSlSide       = isSlSide;
                BorderThickness = new Thickness(1);
                CornerRadius    = new CornerRadius(6);
                Padding         = new Thickness(10, 8, 10, 8);
                Margin          = new Thickness(0, 0, 0, 4);

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                _icon = new TextBlock
                {
                    Text       = "○",
                    FontSize   = 14,
                    FontWeight = FontWeights.Bold,
                    VerticalAlignment   = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                Grid.SetColumn(_icon, 0);
                grid.Children.Add(_icon);

                var inner = new StackPanel();
                _label = new TextBlock
                {
                    Text       = label,
                    FontSize   = 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextHiBrush
                };
                _detail = new TextBlock
                {
                    Text       = "—",
                    FontSize   = 9,
                    Foreground = TextLowBrush,
                    Margin     = new Thickness(0, 1, 0, 0)
                };
                inner.Children.Add(_label);
                inner.Children.Add(_detail);
                Grid.SetColumn(inner, 1);
                grid.Children.Add(inner);

                _value = new TextBlock
                {
                    Text       = "",
                    FontSize   = 12,
                    FontWeight = FontWeights.Bold,
                    FontFamily = new FontFamily("Consolas"),
                    Foreground = TextMidBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(_value, 2);
                grid.Children.Add(_value);

                Child = grid;
                SetState(TpState.Pending, "—", "");
            }

            public void SetState(TpState state, string detail, string value)
            {
                _detail.Text = string.IsNullOrEmpty(detail) ? "—" : detail;
                _value.Text  = value ?? "";

                Color tint;
                string glyph;
                switch (state)
                {
                    case TpState.Done:
                        tint  = _isSlSide ? ShortRed   : LongGreen;
                        glyph = "✓";
                        break;
                    case TpState.Failed:
                        tint  = ShortRed;
                        glyph = "✕";
                        break;
                    case TpState.Active:
                        tint  = _isSlSide ? WarnAmber : CyanAccent;
                        glyph = "●";
                        break;
                    default:
                        tint  = TextLow;
                        glyph = "○";
                        break;
                }

                Background  = BrushAlpha(tint, 0x18);
                BorderBrush = BrushAlpha(tint, 0x55);
                _icon.Text  = glyph;
                _icon.Foreground = Freeze(new SolidColorBrush(tint));
                if (state == TpState.Done || state == TpState.Failed)
                    _value.Foreground = Freeze(new SolidColorBrush(tint));
                else
                    _value.Foreground = TextMidBrush;
            }
        }

        // ── BigActionButton ──────────────────────────────────
        // Larger, more prominent than ActionButton — used for
        // BREAKEVEN / CERRAR YA / PARTIAL CLOSE / TRAILING STOP.
        public static Button BigActionButton(string text, Color tint, bool filled = false)
        {
            var fg     = filled ? Freeze(new SolidColorBrush(BgBase))
                                : Freeze(new SolidColorBrush(tint));
            var bg     = filled ? Freeze(new SolidColorBrush(tint))
                                : BrushAlpha(tint, 0x2a);
            var border = filled ? BrushAlpha(tint, 0xff)
                                : BrushAlpha(tint, 0xaa);

            return new Button
            {
                Content         = text,
                Foreground      = fg,
                Background      = bg,
                BorderBrush     = border,
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(0, 12, 0, 12),
                FontWeight      = FontWeights.Bold,
                FontSize        = 13,
                Cursor          = System.Windows.Input.Cursors.Hand
            };
        }

        // ── ResultIcon ───────────────────────────────────────
        // Big circular check / cross used in the trade result
        // screens (Resultado positivo / Resultado negativo).
        public static Border ResultIcon(bool win, double size = 80)
        {
            Color tint = win ? LongGreen : ShortRed;

            var grid = new Grid { Width = size, Height = size };

            grid.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width  = size,
                Height = size,
                Fill   = BrushAlpha(tint, 0x33),
                Stroke = BrushAlpha(tint, 0xff),
                StrokeThickness = 3
            });

            grid.Children.Add(new TextBlock
            {
                Text       = win ? "✓" : "✕",
                FontSize   = size * 0.55,
                FontWeight = FontWeights.Black,
                Foreground = Freeze(new SolidColorBrush(tint)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            });

            return new Border
            {
                Child   = grid,
                Margin  = new Thickness(0, 4, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Center
            };
        }

        // ── FormField ────────────────────────────────────────
        // Label + input row used in the parameter editor.
        public static Grid FormField(string label, FrameworkElement input)
        {
            var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });

            var lbl = new TextBlock
            {
                Text       = label,
                FontSize   = 11,
                Foreground = TextMidBrush,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbl, 0);
            g.Children.Add(lbl);

            input.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(input, 1);
            g.Children.Add(input);
            return g;
        }

        // ── SectionHeader ────────────────────────────────────
        public static TextBlock SectionHeader(string text)
        {
            return new TextBlock
            {
                Text         = text.ToUpperInvariant(),
                FontSize     = 9,
                FontWeight   = FontWeights.Bold,
                Foreground   = GoldDimBrush,
                Margin       = new Thickness(0, 8, 0, 6)
            };
        }
    }
}
