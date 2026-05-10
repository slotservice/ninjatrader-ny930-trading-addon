// ============================================================
//  NY930Theme — v1.3 pixel-match against client HTML reference
// ------------------------------------------------------------
//  Palette and helpers translated 1:1 from the client's HTML/CSS
//  files (ny930-homepage.html, ny930-openrange-panel.html,
//  ny930-openrange-control.html, ny930-buy_or_sell-panel.html,
//  ny930-progreso_positivo/negativo.html,
//  ny930-resultado_positivo/negativo.html).
//
//  Two distinct palettes:
//    HOME  -> --bg #0a0a0a + gold gradient + candle background.
//    PANEL -> --bg #141414, --bg2 #1c1c1c, --bg3 #222222 (used
//             by every view except Home).
//
//  Fonts: 'JetBrains Mono' (numbers / labels) + 'Barlow' (text).
//  WPF doesn't ship these, so we resolve the family list with
//  fallbacks (Consolas / Segoe UI). The visual feel matches even
//  when the fonts aren't installed.
// ============================================================

#region Using declarations
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public static class NY930Theme
    {
        // ── HOME palette (homepage only) ────────────────────
        public static readonly Color HomeBg     = ColorFromHex("#0a0a0a");
        public static readonly Color HomeCardBg = ColorFromHex("#111111");

        public static readonly SolidColorBrush HomeBgBrush     = Freeze(new SolidColorBrush(HomeBg));
        public static readonly SolidColorBrush HomeCardBgBrush = Freeze(new SolidColorBrush(HomeCardBg));

        // ── PANEL palette (every view except home) ──────────
        public static readonly Color Bg       = ColorFromHex("#141414");
        public static readonly Color Bg2      = ColorFromHex("#1c1c1c");
        public static readonly Color Bg3      = ColorFromHex("#222222");

        public static readonly SolidColorBrush BgBrush  = Freeze(new SolidColorBrush(Bg));
        public static readonly SolidColorBrush Bg2Brush = Freeze(new SolidColorBrush(Bg2));
        public static readonly SolidColorBrush Bg3Brush = Freeze(new SolidColorBrush(Bg3));

        // Borders (rgba based on white)
        public static readonly SolidColorBrush BorderBrush  = Freeze(new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)));   // 0.08
        public static readonly SolidColorBrush Border2Brush = Freeze(new SolidColorBrush(Color.FromArgb(0x24, 0xFF, 0xFF, 0xFF)));   // 0.14
        public static readonly SolidColorBrush DividerBrush = Freeze(new SolidColorBrush(Color.FromArgb(0x14, 0xFF, 0xFF, 0xFF)));

        // ── Text tiers ──────────────────────────────────────
        public static readonly Color Text      = ColorFromHex("#e8e8e8");
        public static readonly Color TextMuted = ColorFromHex("#d8e2ef");

        public static readonly SolidColorBrush TextBrush      = Freeze(new SolidColorBrush(Text));
        public static readonly SolidColorBrush Text2Brush     = Freeze(new SolidColorBrush(Color.FromArgb(0x73, 0xFF, 0xFF, 0xFF))); // 0.45
        public static readonly SolidColorBrush Text3Brush     = Freeze(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF))); // 0.25
        public static readonly SolidColorBrush TextMutedBrush = Freeze(new SolidColorBrush(TextMuted));

        // ── Accent colors ───────────────────────────────────
        public static readonly Color Gold       = ColorFromHex("#c9a227");
        public static readonly Color GoldLight  = ColorFromHex("#f0c84a");
        public static readonly Color GoldDark   = ColorFromHex("#8a6a0a");
        public static readonly Color Green      = ColorFromHex("#22c55e");
        public static readonly Color GreenDark  = ColorFromHex("#166534");
        public static readonly Color Red        = ColorFromHex("#ef4444");
        public static readonly Color RedDark    = ColorFromHex("#991b1b");
        public static readonly Color Blue       = ColorFromHex("#60a5fa"); // long badge
        public static readonly Color BlueDeep   = ColorFromHex("#2563eb"); // active blue button
        public static readonly Color RedSoft    = ColorFromHex("#f87171"); // short badge

        public static readonly SolidColorBrush GoldBrush      = Freeze(new SolidColorBrush(Gold));
        public static readonly SolidColorBrush GoldLightBrush = Freeze(new SolidColorBrush(GoldLight));
        public static readonly SolidColorBrush GoldDarkBrush  = Freeze(new SolidColorBrush(GoldDark));
        public static readonly SolidColorBrush GreenBrush     = Freeze(new SolidColorBrush(Green));
        public static readonly SolidColorBrush RedBrush       = Freeze(new SolidColorBrush(Red));
        public static readonly SolidColorBrush BlueBrush      = Freeze(new SolidColorBrush(Blue));
        public static readonly SolidColorBrush BlueDeepBrush  = Freeze(new SolidColorBrush(BlueDeep));
        public static readonly SolidColorBrush RedSoftBrush   = Freeze(new SolidColorBrush(RedSoft));

        // ── Fonts ───────────────────────────────────────────
        // WPF can take a comma-separated family list. If JetBrains
        // Mono is installed we use it; otherwise Consolas; otherwise
        // any monospace. Same idea for Barlow → Segoe UI fallback.
        public static readonly FontFamily MonoFont = new FontFamily("JetBrains Mono, Consolas, Courier New, monospace");
        public static readonly FontFamily SansFont = new FontFamily("Barlow, Segoe UI, Arial, sans-serif");
        public static readonly FontFamily HomeLogoFont = new FontFamily("Bebas Neue, Impact, Arial Black, Arial");

        // ── Standard panel width (matches HTML width:250px) ──
        public const double PanelWidth = 250;

        // ════════════════════════════════════════════════════
        //  BACKWARD-COMPAT ALIASES
        //
        //  v1.2 views still reference the old palette names
        //  (BgBaseBrush, GoldBrightBrush, NavyTpRow, BigPnLDisplay,
        //  etc). The aliases below keep them compiling until the
        //  per-step v1.3 rewrite lands. They map to the closest
        //  v1.3 equivalent. Once every view is migrated, this
        //  whole block is deleted.
        // ════════════════════════════════════════════════════

        // Color names from v1.0 / v1.1 (gold / navy variants)
        public static readonly Color BgBase     = Bg;
        public static readonly Color BgPanel    = Bg2;
        public static readonly Color BgCard     = Bg3;
        public static readonly Color BgInput    = Bg3;
        public static readonly Color BorderSoft = ColorFromHex("#2a2418");

        public static readonly Color GoldBright = GoldLight;
        public static readonly Color GoldDim    = GoldDark;

        public static readonly Color TextHi  = Text;
        public static readonly Color TextMid = ColorFromHex("#bdb38e");
        public static readonly Color TextLow = ColorFromHex("#74694a");

        public static readonly Color LongGreen    = Green;
        public static readonly Color LongGreenDim = GreenDark;
        public static readonly Color ShortRed     = Red;
        public static readonly Color ShortRedDim  = RedDark;
        public static readonly Color WarnAmber    = ColorFromHex("#f59e0b");

        public static readonly SolidColorBrush BgBaseBrush     = BgBrush;
        public static readonly SolidColorBrush BgPanelBrush    = Bg2Brush;
        public static readonly SolidColorBrush BgCardBrush     = Bg3Brush;
        public static readonly SolidColorBrush BgInputBrush    = Bg3Brush;
        public static readonly SolidColorBrush GoldBrightBrush = GoldLightBrush;
        public static readonly SolidColorBrush GoldDimBrush    = GoldDarkBrush;
        public static readonly SolidColorBrush TextHiBrush     = TextBrush;
        public static readonly SolidColorBrush TextMidBrush    = Freeze(new SolidColorBrush(TextMid));
        public static readonly SolidColorBrush TextLowBrush    = Freeze(new SolidColorBrush(TextLow));
        public static readonly SolidColorBrush LongGreenBrush  = GreenBrush;
        public static readonly SolidColorBrush ShortRedBrush   = RedBrush;
        public static readonly SolidColorBrush WarnAmberBrush  = Freeze(new SolidColorBrush(WarnAmber));

        // Navy palette aliases (v1.2)
        public static readonly Color BgNavy       = Bg;
        public static readonly Color BgNavyDeep   = HomeBg;
        public static readonly Color BgNavyCard   = Bg3;
        public static readonly Color BgNavyInput  = Bg3;
        public static readonly Color BorderNavy   = ColorFromHex("#1f3554");

        public static readonly Color BlueAccent   = BlueDeep;
        public static readonly Color BlueAccentHi = Blue;
        public static readonly Color CyanAccent   = ColorFromHex("#22d3ee");
        public static readonly Color CyanAccentHi = ColorFromHex("#67e8f9");
        public static readonly Color SuccessGreen = Green;
        public static readonly Color SuccessGreenHi = ColorFromHex("#34d399");
        public static readonly Color DangerRed    = Red;
        public static readonly Color DangerRedHi  = RedSoft;
        public static readonly Color WarnAmberHi  = ColorFromHex("#fbbf24");

        public static readonly Color TextNavyHi  = Text;
        public static readonly Color TextNavyMid = ColorFromHex("#94a3b8");
        public static readonly Color TextNavyLow = ColorFromHex("#475569");

        public static readonly SolidColorBrush BgNavyBrush       = BgBrush;
        public static readonly SolidColorBrush BgNavyDeepBrush   = HomeBgBrush;
        public static readonly SolidColorBrush BgNavyCardBrush   = Bg3Brush;
        public static readonly SolidColorBrush BgNavyInputBrush  = Bg3Brush;
        public static readonly SolidColorBrush BorderNavyBrush   = Border2Brush;

        public static readonly SolidColorBrush BlueAccentBrush   = BlueDeepBrush;
        public static readonly SolidColorBrush BlueAccentHiBrush = BlueBrush;
        public static readonly SolidColorBrush CyanAccentBrush   = Freeze(new SolidColorBrush(CyanAccent));
        public static readonly SolidColorBrush CyanAccentHiBrush = Freeze(new SolidColorBrush(CyanAccentHi));
        public static readonly SolidColorBrush SuccessGreenBrush = GreenBrush;
        public static readonly SolidColorBrush DangerRedBrush    = RedBrush;
        public static readonly SolidColorBrush WarnAmberHiBrush  = Freeze(new SolidColorBrush(WarnAmberHi));

        public static readonly SolidColorBrush TextNavyHiBrush   = TextBrush;
        public static readonly SolidColorBrush TextNavyMidBrush  = Freeze(new SolidColorBrush(TextNavyMid));
        public static readonly SolidColorBrush TextNavyLowBrush  = Freeze(new SolidColorBrush(TextNavyLow));

        // ── Legacy helpers used by v1.2 views ────────────────
        public static SolidColorBrush BrushFromHex(string hex)
            => Freeze(new SolidColorBrush(ColorFromHex(hex)));

        public static TextBlock Heading(string text, double size = 22)
        {
            return new TextBlock
            {
                Text = text, FontSize = size,
                FontWeight = FontWeights.Black,
                Foreground = GoldLightBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
        }

        public static TextBlock Subheading(string text)
        {
            return new TextBlock
            {
                Text = text, FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = TextLowBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };
        }

        public static TextBlock Label(string text, double size = 11)
        {
            return new TextBlock
            {
                Text = text, FontSize = size,
                Foreground = TextMidBrush,
                Margin = new Thickness(0, 0, 0, 2)
            };
        }

        public static Border Panel(UIElement child, Thickness? margin = null, Thickness? padding = null)
        {
            return new Border
            {
                Background = Bg2Brush,
                BorderBrush = Border2Brush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = padding ?? new Thickness(12),
                Margin = margin ?? new Thickness(0, 0, 0, 8),
                Child = child
            };
        }

        public static Border Card(UIElement child, Brush accent = null, Thickness? margin = null)
        {
            return new Border
            {
                Background = Bg3Brush,
                BorderBrush = accent ?? GoldDarkBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14),
                Margin = margin ?? new Thickness(0, 0, 0, 10),
                Child = child
            };
        }

        public static Button GoldButton(string text)
        {
            return new Button
            {
                Content = text, Foreground = BgBrush,
                Background = GoldBrush, BorderBrush = GoldLightBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 8, 0, 8),
                FontWeight = FontWeights.Bold, FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand
            };
        }

        public static Button OutlineButton(string text, Brush stroke = null)
        {
            return new Button
            {
                Content = text, Foreground = TextBrush,
                Background = Bg3Brush, BorderBrush = stroke ?? GoldDarkBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 8, 0, 8),
                FontWeight = FontWeights.SemiBold, FontSize = 11,
                Cursor = System.Windows.Input.Cursors.Hand
            };
        }

        public static Button ActionButton(string text, Color tint)
        {
            return new Button
            {
                Content = text,
                Foreground = SolidBrush(tint),
                Background = BrushAlpha(tint, 0x33),
                BorderBrush = BrushAlpha(tint, 0xaa),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 10, 0, 10),
                FontWeight = FontWeights.Bold, FontSize = 12,
                Cursor = System.Windows.Input.Cursors.Hand
            };
        }

        public static Button BigActionButton(string text, Color tint, bool filled = false)
        {
            return new Button
            {
                Content = text,
                Foreground = filled ? SolidBrush(Color.FromRgb(0, 0, 0)) : SolidBrush(tint),
                Background = filled ? SolidBrush(tint) : BrushAlpha(tint, 0x2a),
                BorderBrush = filled ? BrushAlpha(tint, 0xff) : BrushAlpha(tint, 0xaa),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 12, 0, 12),
                FontWeight = FontWeights.Bold, FontSize = 13,
                Cursor = System.Windows.Input.Cursors.Hand
            };
        }

        public static Button NavyButton(string text, Color tint, bool primary = false)
            => BigActionButton(text, tint, primary);

        public static Button NavyPrimaryButton(string text)
            => new Button
            {
                Content = text, Foreground = Brushes.White,
                Background = BlueDeepBrush, BorderBrush = BlueBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 14, 0, 14),
                FontWeight = FontWeights.Bold, FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor = System.Windows.Input.Cursors.Hand
            };

        public static TextBox InputBox(double width = 70)
            => FInput(string.Empty, width);

        public static TextBox NavyInputBox(double width = 70)
            => FInput(string.Empty, width);

        public static CheckBox Toggle(string text)
            => new CheckBox
            {
                Content = text, Foreground = TextBrush,
                FontSize = 11, Margin = new Thickness(0, 4, 0, 4)
            };

        public static CheckBox NavyToggle(string text) => Toggle(text);

        public static Separator HRule()
            => new Separator { Background = BorderBrush, Margin = new Thickness(0, 8, 0, 8) };

        public static TextBlock SectionHeader(string text)
            => SectionLabel(text);

        public static TextBlock NavySectionHeader(string text)
            => SectionLabel(text);

        public static Border NavyPanel(UIElement child, Thickness? margin = null, Thickness? padding = null)
            => Panel(child, margin, padding);

        public static Border Pill(string text, Color tint)
            => new Border
            {
                Background = BrushAlpha(tint, 0x22),
                BorderBrush = BrushAlpha(tint, 0x66),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(8, 3, 8, 3),
                Margin = new Thickness(0, 0, 6, 0),
                Child = new TextBlock
                {
                    Text = text, FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = SolidBrush(tint),
                    FontFamily = MonoFont
                }
            };

        public enum TpState { Pending, Active, Done, Failed }

        // BigPnLDisplay (v1.2) — kept as an alias for backwards
        // compat. The new HTML-based pnl box is built inline in
        // each view instead of through this class.
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
                    Text = "$0.00", FontSize = 30,
                    FontWeight = FontWeights.Black,
                    Foreground = CyanAccentBrush,
                    FontFamily = MonoFont,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(Currency, 0);
                top.Children.Add(Currency);

                Side = new TextBlock
                {
                    Text = "—", FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = TextLowBrush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(8, 4, 8, 4)
                };
                Grid.SetColumn(Side, 1);
                top.Children.Add(Side);

                Ticks = new TextBlock
                {
                    Text = "0 ticks", FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = TextMidBrush,
                    Margin = new Thickness(0, 2, 0, 0)
                };

                Children.Add(top);
                Children.Add(Ticks);
            }

            public void Update(double currency, double ticks, string side)
            {
                bool positive = ticks >= 0;
                Currency.Foreground = positive ? CyanAccentBrush : RedBrush;
                string sign = ticks >= 0 ? "+" : "";
                Currency.Text = sign + currency.ToString("C");
                Ticks.Text = sign + ticks.ToString("F1") + " ticks";
                Ticks.Foreground = positive ? GreenBrush : RedBrush;

                if (string.IsNullOrEmpty(side) || side == "None")
                {
                    Side.Text = "—"; Side.Foreground = TextLowBrush;
                    Side.Background = Brushes.Transparent;
                }
                else
                {
                    bool isLong = side.Equals("Long", StringComparison.OrdinalIgnoreCase);
                    Color sideColor = isLong ? Green : Red;
                    Side.Text = (isLong ? "▲ " : "▼ ") + side.ToUpperInvariant();
                    Side.Foreground = SolidBrush(sideColor);
                    Side.Background = BrushAlpha(sideColor, 0x33);
                }
            }
        }

        // Legacy v1.1 progress card — replaced by NavyTpRow then
        // by the new HTML row layout. Kept as a no-op shim so old
        // code still compiles.
        public sealed class TpProgressCard : Border
        {
            private readonly TextBlock _label, _detail, _value, _icon;
            public TpProgressCard(string label, bool isSlSide = false)
            {
                BorderThickness = new Thickness(1);
                CornerRadius = new CornerRadius(6);
                Padding = new Thickness(10, 8, 10, 8);
                Margin = new Thickness(0, 0, 0, 4);

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                _icon = new TextBlock { Text = "○", FontSize = 14, FontWeight = FontWeights.Bold,
                                        VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
                Grid.SetColumn(_icon, 0); grid.Children.Add(_icon);

                var inner = new StackPanel();
                _label = new TextBlock { Text = label, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = TextBrush };
                _detail = new TextBlock { Text = "", FontSize = 9, Foreground = TextLowBrush, Margin = new Thickness(0, 1, 0, 0) };
                inner.Children.Add(_label); inner.Children.Add(_detail);
                Grid.SetColumn(inner, 1); grid.Children.Add(inner);

                _value = new TextBlock { Text = "", FontSize = 12, FontWeight = FontWeights.Bold,
                                        FontFamily = MonoFont, Foreground = TextMidBrush, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(_value, 2); grid.Children.Add(_value);

                Child = grid;
                SetState(TpState.Pending, "—", "");
            }

            public void SetState(TpState state, string detail, string value)
            {
                _detail.Text = string.IsNullOrEmpty(detail) ? "—" : detail;
                _value.Text = value ?? "";
                Color tint; string g;
                switch (state)
                {
                    case TpState.Done:    tint = Green; g = "✓"; break;
                    case TpState.Failed:  tint = Red;   g = "✕"; break;
                    case TpState.Active:  tint = Blue;  g = "●"; break;
                    default:              tint = TextLow; g = "○"; break;
                }
                Background  = BrushAlpha(tint, 0x18);
                BorderBrush = BrushAlpha(tint, 0x55);
                _icon.Text = g;
                _icon.Foreground = SolidBrush(tint);
                if (state == TpState.Done || state == TpState.Failed) _value.Foreground = SolidBrush(tint);
                else _value.Foreground = TextMidBrush;
            }
        }

        // Legacy v1.2 progress row (replaced by HTML-spec rows).
        public sealed class NavyTpRow : Border
        {
            private readonly TextBlock _icon, _title, _detail, _value;
            public NavyTpRow(bool isStop = false)
            {
                CornerRadius = new CornerRadius(6);
                Padding = new Thickness(10, 8, 10, 8);
                Margin = new Thickness(0, 0, 0, 5);
                BorderThickness = new Thickness(1);
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                _icon  = new TextBlock { Text = "○", FontSize = 14 };
                _title = new TextBlock { Text = "—", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = TextBrush };
                _detail= new TextBlock { Text = "", FontSize = 9, Foreground = TextLowBrush };
                _value = new TextBlock { Text = "", FontSize = 12, FontWeight = FontWeights.Bold, FontFamily = MonoFont };
                Grid.SetColumn(_icon, 0);
                var inner = new StackPanel(); inner.Children.Add(_title); inner.Children.Add(_detail);
                Grid.SetColumn(inner, 1);
                Grid.SetColumn(_value, 2);
                g.Children.Add(_icon); g.Children.Add(inner); g.Children.Add(_value);
                Child = g;
                SetIdle();
            }
            public void SetIdle(string title = "—") { _title.Text = title; _detail.Text = ""; _value.Text = ""; Background = BrushAlpha(TextLow, 0x18); BorderBrush = BrushAlpha(TextLow, 0x33); _icon.Text = "○"; _icon.Foreground = TextLowBrush; }
            public void SetDone(string title, string detail, string value) { _title.Text = title; _detail.Text = detail; _value.Text = value; Background = BrushAlpha(Green, 0x22); BorderBrush = BrushAlpha(Green, 0x66); _icon.Text = "✓"; _icon.Foreground = GreenBrush; }
            public void SetActive(string title, string detail, string value, double progress) { _title.Text = title; _detail.Text = detail; _value.Text = value; Background = BrushAlpha(Blue, 0x22); BorderBrush = BrushAlpha(Blue, 0x66); _icon.Text = "●"; _icon.Foreground = BlueBrush; }
            public void SetPending(string title, string detail, string value) { _title.Text = title; _detail.Text = detail; _value.Text = value; Background = BrushAlpha(TextLow, 0x10); BorderBrush = BrushAlpha(TextLow, 0x33); _icon.Text = "○"; _icon.Foreground = Text2Brush; }
            public void SetDanger(string title, string warning) { _title.Text = title; _detail.Text = warning; _value.Text = ""; Background = BrushAlpha(Red, 0x33); BorderBrush = BrushAlpha(Red, 0xaa); _icon.Text = "!"; _icon.Foreground = RedBrush; }
        }

        // Legacy v1.2 selectors / shapes
        public sealed class PartialPercentSelector : Border
        {
            private static readonly int[] _values = { 25, 50, 75, 100 };
            private int _index = 1;
            private readonly TextBlock _t;
            public int Percent { get { return _values[_index]; } }
            public PartialPercentSelector()
            {
                Background = Bg3Brush; BorderBrush = Border2Brush;
                BorderThickness = new Thickness(1); CornerRadius = new CornerRadius(6);
                Padding = new Thickness(10, 6, 10, 6);
                Cursor = System.Windows.Input.Cursors.Hand;
                _t = new TextBlock { Text = _values[_index] + "%", FontSize = 11,
                                     FontWeight = FontWeights.Bold, Foreground = TextBrush,
                                     HorizontalAlignment = HorizontalAlignment.Center };
                Child = _t;
                MouseLeftButtonUp += (s, e) => { _index = (_index + 1) % _values.Length; _t.Text = _values[_index] + "%"; };
            }
        }

        public sealed class TradeHeader : Border
        {
            public SideBadge StatusTagSide;
            private readonly StatusTagShim _shim;
            public StatusTagShim StatusTag { get { return _shim; } }
            public TradeHeader(string strategyTitle)
            {
                Background = Bg2Brush;
                BorderBrush = Border2Brush;
                BorderThickness = new Thickness(0, 0, 0, 1);
                Padding = new Thickness(12, 10, 12, 10);
                _shim = new StatusTagShim();
                Child = new TextBlock { Text = strategyTitle, Foreground = TextBrush };
            }
        }

        public sealed class StatusTagShim
        {
            public void Update(string text, Color tint) { /* no-op shim */ }
        }

        public static Border ResultIcon(bool win, double size = 80)
        {
            Color tint = win ? Green : Red;
            var grid = new Grid { Width = size, Height = size };
            grid.Children.Add(new Ellipse { Width = size, Height = size, Fill = SolidBrush(tint) });
            grid.Children.Add(new TextBlock { Text = win ? "✓" : "✕", FontSize = size * 0.55,
                FontWeight = FontWeights.Black, Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
            return new Border { Child = grid, HorizontalAlignment = HorizontalAlignment.Center };
        }

        public static Border PriceBox(string label, string value, Color tint, out TextBlock valueOut)
        {
            var s = new StackPanel();
            s.Children.Add(new TextBlock { Text = label, FontSize = 9,
                FontWeight = FontWeights.Bold, Foreground = SolidBrush(tint),
                HorizontalAlignment = HorizontalAlignment.Center });
            valueOut = new TextBlock { Text = value, FontSize = 16,
                FontWeight = FontWeights.Bold, FontFamily = MonoFont,
                Foreground = TextBrush, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 4, 0, 0) };
            s.Children.Add(valueOut);
            return new Border { Background = Bg3Brush, BorderBrush = BrushAlpha(tint, 0x55),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 10, 12, 10), Child = s };
        }

        public static Border ResultBreakdownRow(string label, int contracts, string currency, bool isWin)
        {
            Color tint = isWin ? Green : Red;
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var icon = new TextBlock { Text = isWin ? "✓" : "✕", FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = SolidBrush(tint), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var lbl = new TextBlock { Text = label + " - " + contracts + " ctos", FontSize = 11,
                FontWeight = FontWeights.SemiBold, Foreground = TextBrush, VerticalAlignment = VerticalAlignment.Center };
            var ccy = new TextBlock { Text = currency ?? "", FontSize = 12, FontWeight = FontWeights.Bold,
                FontFamily = MonoFont, Foreground = SolidBrush(tint), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(icon, 0); Grid.SetColumn(lbl, 1); Grid.SetColumn(ccy, 2);
            g.Children.Add(icon); g.Children.Add(lbl); g.Children.Add(ccy);
            return new Border { Background = BrushAlpha(tint, 0x14), BorderBrush = BrushAlpha(tint, 0x44),
                BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 8, 10, 8), Margin = new Thickness(0, 0, 0, 5), Child = g };
        }

        public static Grid FormField(string label, FrameworkElement input)
        {
            var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            var lbl = new TextBlock { Text = label, FontSize = 11, Foreground = TextMidBrush, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(lbl, 0); g.Children.Add(lbl);
            input.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetColumn(input, 1); g.Children.Add(input);
            return g;
        }
        // ════════════════════════════════════════════════════
        //  END BACKWARD-COMPAT ALIASES
        // ════════════════════════════════════════════════════

        // ────────────────────────────────────────────────────
        //  Helpers
        // ────────────────────────────────────────────────────
        public static Color ColorFromHex(string hex)
        {
            return (Color)ColorConverter.ConvertFromString(hex);
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

        // ────────────────────────────────────────────────────
        //  Component factories — translate the most-used CSS
        //  classes from the HTML files into reusable WPF.
        // ────────────────────────────────────────────────────

        // ── HEADER ── matches `.header` from openrange-panel.html
        // NY logo + 930 logo (mono) on the left, status-area on
        // right. The logo gets silver and gold colored runs to
        // mirror the HTML colors exactly.
        public sealed class PanelHeader : Border
        {
            public TextBlock Logo  { get; private set; }
            public Grid      Right { get; private set; }

            public PanelHeader()
            {
                Background      = Bg2Brush;
                BorderBrush     = Border2Brush;
                BorderThickness = new Thickness(0, 0, 0, 1);
                Padding         = new Thickness(10, 9, 10, 7);

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                Logo = new TextBlock
                {
                    FontFamily = MonoFont,
                    FontSize   = 22,
                    FontWeight = FontWeights.Bold,
                    LineHeight = 22,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Logo.Inlines.Add(new Run("NY")  { Foreground = TextMutedBrush });
                Logo.Inlines.Add(new Run("930") { Foreground = GoldBrush });
                Grid.SetColumn(Logo, 0);
                grid.Children.Add(Logo);

                Right = new Grid { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(Right, 1);
                grid.Children.Add(Right);

                Child = grid;
            }
        }

        // ── STRATEGY LABEL ── matches `.strategy-label` (centered
        // gold mono label under the header).
        public static TextBlock StrategyLabel(string text)
        {
            return new TextBlock
            {
                Text       = text.ToUpperInvariant(),
                FontFamily = MonoFont,
                FontSize   = 12,
                FontWeight = FontWeights.Bold,
                Foreground = GoldBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 7, 0, 0)
            };
        }

        // ── SIDE BADGE ── matches `.side-badge.long/.short` —
        // small rounded pill in the top-right of trade views.
        public sealed class SideBadge : Border
        {
            private readonly TextBlock _t;
            public SideBadge()
            {
                CornerRadius    = new CornerRadius(10);
                Padding         = new Thickness(9, 3, 9, 3);
                BorderThickness = new Thickness(1);
                _t = new TextBlock
                {
                    FontFamily = MonoFont,
                    FontSize   = 9,
                    FontWeight = FontWeights.Bold
                };
                Child = _t;
                SetSide("none");
            }

            public void SetSide(string side)
            {
                if (string.Equals(side, "Long", StringComparison.OrdinalIgnoreCase))
                {
                    _t.Text       = "▲ LONG";
                    _t.Foreground = BlueBrush;
                    Background    = BrushAlpha(BlueDeep, 0x33);
                    BorderBrush   = BrushAlpha(BlueDeep, 0x59);
                }
                else if (string.Equals(side, "Short", StringComparison.OrdinalIgnoreCase))
                {
                    _t.Text       = "▼ SHORT";
                    _t.Foreground = RedSoftBrush;
                    Background    = BrushAlpha(Red, 0x33);
                    BorderBrush   = BrushAlpha(Red, 0x59);
                }
                else
                {
                    _t.Text       = "—";
                    _t.Foreground = Text3Brush;
                    Background    = BrushAlpha(Color.FromRgb(255,255,255), 0x10);
                    BorderBrush   = Border2Brush;
                }
            }
        }

        // ── RESULT BADGE ── matches `.result-badge.win/.loss`
        public sealed class ResultBadge : Border
        {
            private readonly TextBlock _t;
            public ResultBadge()
            {
                CornerRadius    = new CornerRadius(10);
                Padding         = new Thickness(9, 3, 9, 3);
                BorderThickness = new Thickness(1);
                _t = new TextBlock
                {
                    FontFamily = MonoFont,
                    FontSize   = 9,
                    FontWeight = FontWeights.Bold
                };
                Child = _t;
                SetWin();
            }

            public void SetWin()
            {
                _t.Text       = "COMPLETADA";
                _t.Foreground = GreenBrush;
                Background    = BrushAlpha(Green, 0x26);
                BorderBrush   = BrushAlpha(Green, 0x4D);
            }

            public void SetLoss()
            {
                _t.Text       = "STOP LOSS";
                _t.Foreground = RedBrush;
                Background    = BrushAlpha(Red, 0x26);
                BorderBrush   = BrushAlpha(Red, 0x4D);
            }

            public void SetCustom(string text, Color tint)
            {
                _t.Text       = text;
                _t.Foreground = SolidBrush(tint);
                Background    = BrushAlpha(tint, 0x26);
                BorderBrush   = BrushAlpha(tint, 0x4D);
            }
        }

        // ── DIVIDER ── matches `.divider`
        public static Border Divider(double topMargin = 9, double sideMargin = 9)
        {
            return new Border
            {
                Height = 1,
                Background = DividerBrush,
                Margin = new Thickness(sideMargin, topMargin, sideMargin, 0)
            };
        }

        // ── SECTION LABEL ── matches `.section-lbl` / `.cfg-label`
        public static TextBlock SectionLabel(string text)
        {
            return new TextBlock
            {
                Text       = text.ToUpperInvariant(),
                FontSize   = 9,
                FontWeight = FontWeights.Bold,
                Foreground = Text3Brush,
                Margin     = new Thickness(9, 6, 9, 4)
            };
        }

        // ── FIELD LABEL ── matches `.flbl`
        public static TextBlock FieldLabel(string text)
        {
            return new TextBlock
            {
                Text       = text,
                FontSize   = 10,
                FontWeight = FontWeights.SemiBold,
                Foreground = Text2Brush,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        // ── INPUT BOX ── matches `.finput`
        public static TextBox FInput(string text = "", double width = 52)
        {
            return new TextBox
            {
                Text            = text,
                Background      = Bg3Brush,
                BorderBrush     = Border2Brush,
                BorderThickness = new Thickness(1),
                Foreground      = TextBrush,
                FontFamily      = MonoFont,
                FontSize        = 10,
                FontWeight      = FontWeights.SemiBold,
                Padding         = new Thickness(4, 2, 4, 2),
                Width           = width,
                TextAlignment   = TextAlignment.Center,
                CaretBrush      = GoldBrush
            };
        }

        // ── F-SELECT (combo box) ── matches `.fselect`
        public static ComboBox FSelect(double width = 60)
        {
            return new ComboBox
            {
                Background      = Bg3Brush,
                BorderBrush     = Border2Brush,
                BorderThickness = new Thickness(1),
                Foreground      = TextBrush,
                FontFamily      = MonoFont,
                FontSize        = 10,
                FontWeight      = FontWeights.SemiBold,
                Padding         = new Thickness(4, 2, 4, 2),
                Width           = width
            };
        }

        // ── ACTION BUTTON ── matches `.act-btn`
        public static Button ActionButton(string text, bool danger = false)
        {
            Color stroke = danger ? Red : Color.FromRgb(255,255,255);
            byte  alpha  = danger ? (byte)0xFF : (byte)0x99;
            return new Button
            {
                Content    = text,
                Background = Brushes.Transparent,
                Foreground = danger ? RedBrush : TextBrush,
                BorderBrush = BrushAlpha(stroke, alpha),
                BorderThickness = new Thickness(1),
                Padding    = new Thickness(7, 2, 7, 2),
                FontFamily = MonoFont,
                FontSize   = 10,
                FontWeight = FontWeights.Bold,
                Cursor     = System.Windows.Input.Cursors.Hand
            };
        }

        // ── APPLY BUTTON ── matches `.apply-btn`
        public static Button ApplyButton(string text)
        {
            return new Button
            {
                Content    = text,
                Background = Brushes.Transparent,
                Foreground = GoldBrush,
                BorderBrush = GoldBrush,
                BorderThickness = new Thickness(1),
                Padding    = new Thickness(0, 7, 0, 7),
                FontFamily = MonoFont,
                FontSize   = 10,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor     = System.Windows.Input.Cursors.Hand
            };
        }

        // ── CANCEL BUTTON ── matches `.cancel-btn` (red outline)
        public static Button CancelButton(string text)
        {
            return new Button
            {
                Content    = text,
                Background = BrushAlpha(Red, 0x14),
                Foreground = BrushAlpha(Red, 0xCC),
                BorderBrush = BrushAlpha(Red, 0x59),
                BorderThickness = new Thickness(1),
                Padding    = new Thickness(0, 7, 0, 7),
                FontFamily = MonoFont,
                FontSize   = 10,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Cursor     = System.Windows.Input.Cursors.Hand
            };
        }

        // ── TAB BUTTON ── matches `.tab` (bottom-bordered tab)
        public sealed class TabButton : Button
        {
            private readonly bool _withinTabs; // controls border placement
            public TabButton(string text, bool active)
            {
                Content     = text;
                Background  = active ? Bg2Brush : Bg3Brush;
                Foreground  = active ? TextBrush : Text2Brush;
                BorderBrush = active ? GoldBrush : Brushes.Transparent;
                BorderThickness = active ? new Thickness(0, 0, 0, 2) : new Thickness(0);
                Padding     = new Thickness(0, 5, 0, 5);
                FontFamily  = SansFont;
                FontSize    = 9;
                FontWeight  = FontWeights.Bold;
                Cursor      = System.Windows.Input.Cursors.Hand;
                HorizontalContentAlignment = HorizontalAlignment.Center;
            }

            public void SetActive(bool active)
            {
                Background  = active ? Bg2Brush : Bg3Brush;
                Foreground  = active ? TextBrush : Text2Brush;
                BorderBrush = active ? GoldBrush : Brushes.Transparent;
                BorderThickness = active ? new Thickness(0, 0, 0, 2) : new Thickness(0);
            }
        }

        // ── TOGGLE SWITCH ── matches `.tog`
        public sealed class ToggleSwitch : Border
        {
            private readonly Ellipse _knob;
            private bool _on;
            public event Action<bool> Toggled;

            public ToggleSwitch(bool initial = false)
            {
                Width  = 28;
                Height = 15;
                CornerRadius    = new CornerRadius(9);
                BorderThickness = new Thickness(1);
                Cursor = System.Windows.Input.Cursors.Hand;

                var canvas = new Canvas { Width = 26, Height = 13 };
                _knob = new Ellipse
                {
                    Width  = 9,
                    Height = 9
                };
                Canvas.SetTop(_knob, 1);
                canvas.Children.Add(_knob);
                Child = canvas;

                MouseLeftButtonUp += (s, e) =>
                {
                    Set(!_on);
                    Toggled?.Invoke(_on);
                };

                Set(initial);
            }

            public bool IsOn { get { return _on; } }

            public void Set(bool on)
            {
                _on = on;
                if (on)
                {
                    Background  = GoldBrush;
                    BorderBrush = GoldDarkBrush;
                    Canvas.SetLeft(_knob, 14);
                    _knob.Fill = SolidBrush(Color.FromRgb(0, 0, 0));
                }
                else
                {
                    Background  = Bg3Brush;
                    BorderBrush = Border2Brush;
                    Canvas.SetLeft(_knob, 1);
                    _knob.Fill = Text3Brush;
                }
            }
        }

        // ── ACCORDION SECTION ── matches `.acc` (header click
        // toggles the body open/closed).
        public sealed class Accordion : StackPanel
        {
            public ToggleSwitch Toggle { get; private set; }
            public StackPanel   Body   { get; private set; }
            private readonly Border _hdr;
            private readonly TextBlock _name;
            private readonly bool _withTopBorder;

            public Accordion(string name, bool initialOn = false, Brush nameColor = null, bool withTopBorder = true)
            {
                _withTopBorder = withTopBorder;

                _hdr = new Border
                {
                    BorderBrush = BorderBrush,
                    BorderThickness = withTopBorder ? new Thickness(0, 1, 0, 0) : new Thickness(0),
                    Padding     = new Thickness(0, 6, 0, 6),
                    Cursor      = System.Windows.Input.Cursors.Hand
                };

                var hdrGrid = new Grid();
                hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                hdrGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                _name = new TextBlock
                {
                    Text       = name,
                    FontSize   = 9,
                    FontWeight = FontWeights.Bold,
                    Foreground = nameColor ?? Text2Brush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(_name, 0);
                hdrGrid.Children.Add(_name);

                Toggle = new ToggleSwitch(initialOn) { VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(Toggle, 1);
                hdrGrid.Children.Add(Toggle);

                _hdr.Child = hdrGrid;
                _hdr.MouseLeftButtonUp += (s, e) =>
                {
                    Toggle.Set(!Toggle.IsOn);
                    Apply();
                };
                Toggle.Toggled += _ => Apply();

                Body = new StackPanel
                {
                    Margin = new Thickness(0, 0, 0, 6)
                };

                Children.Add(_hdr);
                Children.Add(Body);
                Apply();
            }

            public bool IsOn { get { return Toggle.IsOn; } }
            public void Set(bool on) { Toggle.Set(on); Apply(); }

            private void Apply()
            {
                Body.Visibility = Toggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
                _name.Foreground = Toggle.IsOn ? TextBrush : Text2Brush;
            }
        }

        // ── ACCORDION SUB-LABEL ── matches `.acc-sublbl`
        public static TextBlock AccordionSubLabel(string text)
        {
            return new TextBlock
            {
                Text       = text.ToUpperInvariant(),
                FontSize   = 9,
                FontWeight = FontWeights.Bold,
                Foreground = GoldBrush,
                Margin     = new Thickness(0, 6, 0, 4)
            };
        }

        // ── FIELD ROW (label + input) ── matches `.frow`
        public static Grid FieldRow(string label, FrameworkElement input)
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lbl = FieldLabel(label);
            Grid.SetColumn(lbl, 0);
            g.Children.Add(lbl);

            Grid.SetColumn(input, 1);
            g.Children.Add(input);
            return g;
        }

        // Multi-input field row (label + N inputs side by side)
        public static StackPanel FieldRowMulti(params UIElement[] children)
        {
            var s = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 6)
            };
            foreach (var c in children)
            {
                if (c is FrameworkElement fe) fe.Margin = new Thickness(0, 0, 6, 0);
                s.Children.Add(c);
            }
            return s;
        }
    }
}
