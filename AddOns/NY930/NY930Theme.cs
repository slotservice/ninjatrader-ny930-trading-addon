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
    }
}
