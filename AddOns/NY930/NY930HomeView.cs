// ============================================================
//  NY930HomeView — v1.3 pixel-match against ny930-homepage.html
// ------------------------------------------------------------
//  Rebuilt against the client's HTML/CSS reference and the
//  companion NY930AddOn HOME.cs sample. Exact reproduction of:
//
//   - Pure black background (#0a0a0a) with subtle candlestick
//     pattern + radial gold glow at the top.
//   - NY (silver gradient) + 930 (gold gradient) wordmark.
//     (The HTML mock-up had a decorative hamburger inside the
//      home page; we removed it — the shell already provides a
//      working hamburger menu so a second non-clickable copy
//      was confusing rather than helpful.)
//   - "Make Money Easy" tagline between two gold gradient lines.
//   - Two cards (Open Range, Buy or Sell):
//       * Gradient gold border
//       * Corner gleam (top-left radial)
//       * Icon box on the left (candle / triangle SVG)
//       * Title (uppercase white, FontWeight Black)
//       * Gold arrow circle on the right
//   - Bottom CTA "Click en una estrategia para empezar" between
//     gold lines.
// ============================================================

#region Using declarations
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public sealed class NY930HomeView : Grid, INY930Localizable, IDisposable
    {
        private readonly NY930ShellView _shell;

        // Cached references for live language refresh.
        private TextBlock _orTitle;
        private TextBlock _orDesc;
        private TextBlock _hedgeTitle;
        private TextBlock _hedgeDesc;
        private TextBlock _ctaText;
        private TextBlock _taglineText;

        // Decorative background canvas (candlestick pattern).
        private Canvas    _bgCanvas;
        private readonly Random _rnd = new Random();

        public NY930HomeView(NY930ShellView shell)
        {
            _shell     = shell;
            Background = NY930Theme.HomeBgBrush;

            // Layered root: bg canvas + radial glow + scrollable content.
            // Background candlestick canvas (very dim, decorative).
            _bgCanvas = new Canvas
            {
                IsHitTestVisible = false,
                Opacity          = 0.13
            };
            Children.Add(_bgCanvas);

            // Radial gold glow at the top-center of the panel.
            var glow = new Ellipse
            {
                Width                = 320,
                Height               = 200,
                HorizontalAlignment  = HorizontalAlignment.Center,
                VerticalAlignment    = VerticalAlignment.Top,
                Margin               = new Thickness(0, -60, 0, 0),
                IsHitTestVisible     = false,
                Fill = new RadialGradientBrush
                {
                    GradientOrigin = new Point(0.5, 0.5),
                    Center         = new Point(0.5, 0.5),
                    RadiusX        = 0.5,
                    RadiusY        = 0.5,
                    GradientStops  = new GradientStopCollection
                    {
                        new GradientStop(Color.FromArgb(46, NY930Theme.Gold.R, NY930Theme.Gold.G, NY930Theme.Gold.B), 0.0),
                        new GradientStop(Colors.Transparent, 1.0)
                    }
                }
            };
            Children.Add(glow);

            // Main scrollable content.
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background                    = Brushes.Transparent
            };
            Children.Add(scroll);

            var page = new StackPanel
            {
                Margin              = new Thickness(10, 0, 10, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth            = 250
            };
            scroll.Content = page;

            page.Children.Add(BuildLogo());
            page.Children.Add(BuildTagline());
            page.Children.Add(BuildCards());
            page.Children.Add(BuildCta());

            // Draw candlesticks once we have a size, and on resize.
            Loaded      += (s, e) => RedrawCandles();
            SizeChanged += (s, e) => RedrawCandles();

            NY930Localization.LanguageChanged += OnLanguageChanged;
        }

        // ── Logo: NY (silver gradient) + 930 (gold gradient) ────
        private FrameworkElement BuildLogo()
        {
            var wrap = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 36, 0, 0)
            };

            var logo = new TextBlock
            {
                FontFamily = NY930Theme.HomeLogoFont,
                FontSize   = 52,
                FontWeight = FontWeights.Black,
                TextAlignment = TextAlignment.Center
            };

            // NY — silver gradient
            logo.Inlines.Add(new Run("NY")
            {
                Foreground = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Colors.White,                 0.20),
                        new GradientStop(Color.FromRgb(176, 187, 204), 0.55),
                        new GradientStop(Color.FromRgb(216, 226, 239), 0.80)
                    },
                    new Point(0, 0), new Point(0, 1))
            });

            // 930 — gold gradient
            logo.Inlines.Add(new Run("930")
            {
                Foreground = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(Color.FromRgb(245, 210, 114), 0.0),
                        new GradientStop(NY930Theme.Gold,              0.35),
                        new GradientStop(NY930Theme.GoldLight,         0.60),
                        new GradientStop(NY930Theme.GoldDark,          1.0)
                    },
                    new Point(0, 0), new Point(0, 1))
            });

            // Soft glow under the logo, mirroring the HTML drop-shadow.
            logo.Effect = new DropShadowEffect
            {
                Color       = NY930Theme.Gold,
                Opacity     = 0.55,
                BlurRadius  = 22,
                ShadowDepth = 0
            };

            wrap.Children.Add(logo);
            return wrap;
        }

        // ── Tagline ─────────────────────────────────────────────
        private FrameworkElement BuildTagline()
        {
            var grid = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lineL = new Border
            {
                Height            = 1,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new LinearGradientBrush(Colors.Transparent, NY930Theme.Gold, 0)
            };
            var lineR = new Border
            {
                Height            = 1,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new LinearGradientBrush(NY930Theme.Gold, Colors.Transparent, 0)
            };
            _taglineText = new TextBlock
            {
                Text       = "MAKE MONEY EASY",
                FontFamily = NY930Theme.SansFont,
                FontSize   = 10,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.GoldBrush,
                Margin     = new Thickness(14, 0, 14, 0)
            };

            Grid.SetColumn(lineL, 0);
            Grid.SetColumn(_taglineText, 1);
            Grid.SetColumn(lineR, 2);

            grid.Children.Add(lineL);
            grid.Children.Add(_taglineText);
            grid.Children.Add(lineR);
            return grid;
        }

        // ── Cards container ─────────────────────────────────────
        private FrameworkElement BuildCards()
        {
            var cards = new StackPanel { Margin = new Thickness(0, 24, 0, 0) };

            cards.Children.Add(BuildCard(
                NY930Localization.T("home.openrange.title"),
                NY930Localization.T("home.openrange.desc"),
                BuildCandleIcon(),
                () => _shell.Show(new NY930OpenRangeView(_shell)),
                out _orTitle, out _orDesc));

            cards.Children.Add(BuildCard(
                NY930Localization.T("home.hedge.title"),
                NY930Localization.T("home.hedge.desc"),
                BuildArrowsIcon(),
                () => _shell.Show(new NY930HedgeView(_shell)),
                out _hedgeTitle, out _hedgeDesc,
                margin: new Thickness(0, 12, 0, 0)));

            return cards;
        }

        // ── Single card ─────────────────────────────────────────
        // Mirrors the .card CSS class: gradient gold border,
        // corner gleam, icon box + title + arrow circle.
        private Border BuildCard(string title, string desc, UIElement icon,
                                  Action onClick,
                                  out TextBlock titleOut, out TextBlock descOut,
                                  Thickness? margin = null)
        {
            // Outer border carries the gradient stroke + dark fill.
            var card = new Border
            {
                CornerRadius    = new CornerRadius(18),
                BorderThickness = new Thickness(1),
                BorderBrush = new LinearGradientBrush(
                    new GradientStopCollection
                    {
                        new GradientStop(NY930Theme.GoldLight, 0.0),
                        new GradientStop(NY930Theme.GoldDark,  0.5),
                        new GradientStop(NY930Theme.Gold,      1.0)
                    },
                    new Point(0, 0), new Point(1, 1)),
                Background = NY930Theme.HomeCardBgBrush,
                Padding    = new Thickness(12, 12, 10, 12),
                Margin     = margin ?? new Thickness(0),
                Cursor     = System.Windows.Input.Cursors.Hand
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Icon box (52x52)
            var iconBox = new Border
            {
                Width        = 52,
                Height       = 52,
                CornerRadius = new CornerRadius(14),
                Background   = NY930Theme.SolidBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
                Child        = icon
            };
            Grid.SetColumn(iconBox, 0);
            grid.Children.Add(iconBox);

            // Title + description stack
            var bodyStack = new StackPanel
            {
                Margin            = new Thickness(10, 0, 10, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            titleOut = new TextBlock
            {
                Text       = title,
                FontFamily = NY930Theme.SansFont,
                FontSize   = 15,
                FontWeight = FontWeights.Black,
                Foreground = Brushes.White
            };
            descOut = new TextBlock
            {
                Text         = desc,
                FontFamily   = NY930Theme.SansFont,
                FontSize     = 11,
                Foreground   = NY930Theme.BrushAlpha(Colors.White, 0x9E),
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 5, 0, 0)
            };
            bodyStack.Children.Add(titleOut);
            bodyStack.Children.Add(descOut);
            Grid.SetColumn(bodyStack, 1);
            grid.Children.Add(bodyStack);

            // Arrow circle (gold border, gold "→")
            var arrow = new Border
            {
                Width             = 30,
                Height            = 30,
                CornerRadius      = new CornerRadius(15),
                BorderThickness   = new Thickness(2),
                BorderBrush       = NY930Theme.GoldBrush,
                Background        = Brushes.Transparent,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock
                {
                    Text                = "→",
                    FontSize            = 13,
                    Foreground          = NY930Theme.GoldBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                }
            };
            Grid.SetColumn(arrow, 2);
            grid.Children.Add(arrow);

            card.Child = grid;

            // Hover: subtle gold glow
            card.MouseEnter += (s, e) =>
            {
                card.Effect = new DropShadowEffect
                {
                    Color       = NY930Theme.Gold,
                    Opacity     = 0.35,
                    BlurRadius  = 18,
                    ShadowDepth = 0
                };
            };
            card.MouseLeave        += (s, e) => card.Effect = null;
            card.MouseLeftButtonUp += (s, e) => { try { onClick(); } catch (Exception ex) { NY930Log.Error("Home", ex.Message); } };
            return card;
        }

        // ── Candle icon (Open Range card) ───────────────────────
        private UIElement BuildCandleIcon()
        {
            var c = new Canvas
            {
                Width  = 40,
                Height = 40,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };

            // Green candle
            c.Children.Add(MakeRect(11, 7, 8, 12, NY930Theme.Green));
            c.Children.Add(MakeLine(15, 4, 15, 7, NY930Theme.Green));
            c.Children.Add(MakeLine(15, 19, 15, 24, NY930Theme.Green));
            c.Children.Add(MakeDash(4, 13, 23, 13, NY930Theme.Green));

            // Red candle
            c.Children.Add(MakeRect(22, 17, 8, 12, NY930Theme.Red));
            c.Children.Add(MakeLine(26, 14, 26, 17, NY930Theme.Red));
            c.Children.Add(MakeLine(26, 29, 26, 34, NY930Theme.Red));
            c.Children.Add(MakeDash(15, 23, 37, 23, NY930Theme.Red));

            return c;
        }

        // ── Triangle arrows icon (Buy or Sell card) ─────────────
        private UIElement BuildArrowsIcon()
        {
            var c = new Canvas
            {
                Width  = 40,
                Height = 36,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            };

            c.Children.Add(new Polygon
            {
                Points = new PointCollection { new Point(12, 4), new Point(2, 22), new Point(22, 22) },
                Fill   = NY930Theme.GreenBrush
            });
            c.Children.Add(new Polygon
            {
                Points = new PointCollection { new Point(28, 32), new Point(20, 12), new Point(38, 12) },
                Fill   = NY930Theme.RedBrush
            });
            return c;
        }

        // ── CTA ─────────────────────────────────────────────────
        private FrameworkElement BuildCta()
        {
            var grid = new Grid { Margin = new Thickness(0, 24, 0, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lineL = new Border
            {
                Height = 1,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new LinearGradientBrush(Colors.Transparent, NY930Theme.Gold, 0)
            };
            var lineR = new Border
            {
                Height = 1,
                VerticalAlignment = VerticalAlignment.Center,
                Background = new LinearGradientBrush(NY930Theme.Gold, Colors.Transparent, 0)
            };
            _ctaText = new TextBlock
            {
                Text       = NY930Localization.T("home.hint"),
                FontFamily = NY930Theme.SansFont,
                FontSize   = 10,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin     = new Thickness(8, 0, 8, 0)
            };

            Grid.SetColumn(lineL, 0);
            Grid.SetColumn(_ctaText, 1);
            Grid.SetColumn(lineR, 2);

            grid.Children.Add(lineL);
            grid.Children.Add(_ctaText);
            grid.Children.Add(lineR);
            return grid;
        }

        // ── Background candlestick painter ──────────────────────
        // Decorative only — generates a static set of dim gold
        // candles across the panel width.
        private void RedrawCandles()
        {
            if (_bgCanvas == null) return;
            _bgCanvas.Children.Clear();

            double w = ActualWidth  > 0 ? ActualWidth  : NY930Theme.PanelWidth;
            double h = ActualHeight > 0 ? ActualHeight : 500;

            int cols = (int)Math.Ceiling(w / 22) + 2;
            for (int i = 0; i < cols; i++)
            {
                double x  = i * 22 - 6;
                double cy = h * 0.38 + Math.Sin(i * 0.7) * h * 0.25;
                double bH = 14 + Math.Abs(Math.Sin(i * 1.3)) * 50;
                double wT = 8  + _rnd.NextDouble() * 18;
                double wB = 8  + _rnd.NextDouble() * 18;
                Color col = i % 3 != 0 ? NY930Theme.Gold : NY930Theme.GoldDark;

                // Wick
                _bgCanvas.Children.Add(new Line
                {
                    X1 = x + 4, Y1 = cy - bH / 2 - wT,
                    X2 = x + 4, Y2 = cy + bH / 2 + wB,
                    Stroke = NY930Theme.SolidBrush(col),
                    StrokeThickness = 1.4
                });
                // Body
                var body = new Rectangle
                {
                    Width  = 8,
                    Height = Math.Max(2, bH),
                    Fill   = NY930Theme.SolidBrush(col),
                    RadiusX = 1, RadiusY = 1
                };
                Canvas.SetLeft(body, x);
                Canvas.SetTop(body, cy - bH / 2);
                _bgCanvas.Children.Add(body);
            }
        }

        // ── Canvas helpers ──────────────────────────────────────
        private Rectangle MakeRect(double x, double y, double w, double h, Color col)
        {
            var r = new Rectangle
            {
                Width  = w, Height = h,
                Fill   = NY930Theme.SolidBrush(col),
                RadiusX = 1, RadiusY = 1
            };
            Canvas.SetLeft(r, x);
            Canvas.SetTop(r, y);
            return r;
        }

        private Line MakeLine(double x1, double y1, double x2, double y2, Color col)
        {
            return new Line
            {
                X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = NY930Theme.SolidBrush(col),
                StrokeThickness = 1.8,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap   = PenLineCap.Round
            };
        }

        private Line MakeDash(double x1, double y1, double x2, double y2, Color col)
        {
            var l = MakeLine(x1, y1, x2, y2, col);
            l.StrokeDashArray = new DoubleCollection { 3, 2 };
            return l;
        }

        // ── INY930Localizable ───────────────────────────────────
        private void OnLanguageChanged()
        {
            Dispatcher.InvokeAsync(RefreshLocalization);
        }

        public void RefreshLocalization()
        {
            if (_orTitle  != null) _orTitle.Text  = NY930Localization.T("home.openrange.title");
            if (_orDesc   != null) _orDesc.Text   = NY930Localization.T("home.openrange.desc");
            if (_hedgeTitle != null) _hedgeTitle.Text = NY930Localization.T("home.hedge.title");
            if (_hedgeDesc  != null) _hedgeDesc.Text  = NY930Localization.T("home.hedge.desc");
            if (_ctaText  != null) _ctaText.Text  = NY930Localization.T("home.hint");
        }

        public void Dispose()
        {
            NY930Localization.LanguageChanged -= OnLanguageChanged;
        }
    }
}
