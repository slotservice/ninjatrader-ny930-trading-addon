// ============================================================
//  NY930ResultView — v1.3 pixel-match
//  Reference: ny930-resultado_positivo.html / _negativo.html
// ------------------------------------------------------------
//  Layout:
//    .header             NY930 logo + COMPLETADA/STOP LOSS pill
//    .strategy-label     "OPEN RANGE" (gold mono)
//    Account bar         "Live · Principal" right-aligned mono
//    .result-box         tinted bg + icon circle + big PnL + ticks
//                         + side pill (▲ LONG / ▼ SHORT)
//    .stats-grid         2×2: Duración / Contratos / Entrada / Salida
//    .locked-label       "Estrategia bloqueada"
//    .btns-wrap          PROGRAMAR ENTRADA + CAMBIAR DE ESTRATEGIA
// ============================================================

#region Using declarations
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public sealed class NY930ResultView : Grid, INY930Localizable, IDisposable
    {
        private readonly NY930ShellView   _shell;
        private readonly NY930TradeResult _result;
        private readonly bool _isOpenRange;

        // Cached for live language refresh.
        private TextBlock _strategyLabelTb;
        private TextBlock _lockedLabelTb;
        private TextBlock _btnProgramTb;
        private TextBlock _btnSwitchTb;
        private TextBlock _accountTb;

        public NY930ResultView(NY930ShellView shell, NY930TradeResult r)
        {
            _shell  = shell;
            _result = r ?? new NY930TradeResult();
            _isOpenRange = _result.Strategy != null
                && _result.Strategy.Equals("OpenRange", StringComparison.OrdinalIgnoreCase);
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
            stack.Children.Add(BuildStrategyLabel());
            stack.Children.Add(BuildAccountBar());
            stack.Children.Add(BuildResultBox());
            stack.Children.Add(BuildStatsGrid());
            stack.Children.Add(BuildLockedLabel());
            stack.Children.Add(BuildButtons());

            NY930Localization.LanguageChanged += OnLanguageChanged;
        }

        // ── Header ─────────────────────────────────────────────
        private FrameworkElement BuildHeader()
        {
            var hdr = new NY930Theme.PanelHeader();
            var badge = new NY930Theme.ResultBadge();
            if (_result.PnLTicks >= 0) badge.SetWin();
            else                        badge.SetLoss();
            hdr.Right.Children.Add(badge);
            return hdr;
        }

        private FrameworkElement BuildStrategyLabel()
        {
            string strategyName = _isOpenRange ? "OPEN RANGE" : "BUY OR SELL";
            _strategyLabelTb = NY930Theme.StrategyLabel(strategyName);
            return _strategyLabelTb;
        }

        // Account bar matching the .account-name micro-row above
        // the result box ("Live · Principal" right-aligned).
        private FrameworkElement BuildAccountBar()
        {
            _accountTb = new TextBlock
            {
                Text       = "Live · Principal",
                FontFamily = NY930Theme.MonoFont,
                FontSize   = 8,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.Text3Brush,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin     = new Thickness(10, 2, 10, 0)
            };
            return _accountTb;
        }

        // ── Result box (.result-box.win / .loss) ───────────────
        private FrameworkElement BuildResultBox()
        {
            bool win = _result.PnLTicks >= 0;
            Color tint = win ? NY930Theme.Green : NY930Theme.Red;
            // Tinted background per CSS (rgba(22,101,52,.2) / rgba(153,27,27,.2))
            Color tintBg = win
                ? Color.FromArgb(0x33, 22, 101, 52)
                : Color.FromArgb(0x33, 153, 27, 27);

            var box = new Border
            {
                Background      = NY930Theme.SolidBrush(tintBg),
                BorderBrush     = NY930Theme.BrushAlpha(tint, 0x40),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(8),
                Padding         = new Thickness(12, 16, 12, 14),
                Margin          = new Thickness(9, 7, 9, 0)
            };

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Stretch };

            // Icon circle (52x52)
            var iconGrid = new Grid
            {
                Width               = 52,
                Height              = 52,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin              = new Thickness(0, 0, 0, 10)
            };
            iconGrid.Children.Add(new Ellipse
            {
                Width = 52, Height = 52,
                Fill  = NY930Theme.SolidBrush(tint)
            });
            iconGrid.Children.Add(new TextBlock
            {
                Text       = win ? "✓" : "✕",
                FontSize   = 26,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center
            });
            stack.Children.Add(iconGrid);

            // Big PnL value
            string sign = _result.PnLCurrency >= 0 ? "+" : "−";
            stack.Children.Add(new TextBlock
            {
                Text       = sign + "$" + Math.Abs(_result.PnLCurrency).ToString("N2", CultureInfo.CurrentCulture),
                FontFamily = NY930Theme.MonoFont,
                FontSize   = 32,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.SolidBrush(tint),
                HorizontalAlignment = HorizontalAlignment.Center
            });

            // Ticks line
            stack.Children.Add(new TextBlock
            {
                Text       = (_result.PnLTicks >= 0 ? "▲ +" : "▼ −")
                              + Math.Abs(_result.PnLTicks).ToString("F1") + " ticks totales",
                FontFamily = NY930Theme.MonoFont,
                FontSize   = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = NY930Theme.BrushAlpha(tint, 0xA6),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 4, 0, 0)
            });

            // Side badge (▲ LONG / ▼ SHORT)
            var sideBadge = new NY930Theme.SideBadge { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
            sideBadge.SetSide(_result.Side ?? "None");
            stack.Children.Add(sideBadge);

            box.Child = stack;
            return box;
        }

        // ── Stats grid (.stats-grid: 2×2) ─────────────────────
        private FrameworkElement BuildStatsGrid()
        {
            var grid = new Grid { Margin = new Thickness(9, 6, 9, 0) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            TimeSpan dur = _result.ExitTime - _result.EntryTime;
            if (dur.TotalSeconds < 0) dur = TimeSpan.Zero;

            string entry = _result.EntryPrice > 0
                ? _result.EntryPrice.ToString("N2", CultureInfo.CurrentCulture) : "—";
            string exit = _result.ExitPrice > 0
                ? _result.ExitPrice.ToString("N2", CultureInfo.CurrentCulture) : "—";

            grid.Children.Add(StatBox("Duración",
                ((int)dur.TotalMinutes).ToString("D2") + ":" + dur.Seconds.ToString("D2"),
                0, 0));
            grid.Children.Add(StatBox("Contratos",
                _result.Contracts.ToString(),
                0, 1));
            grid.Children.Add(StatBox("Entrada", entry, 1, 0));
            grid.Children.Add(StatBox("Salida",  exit,  1, 1));

            return grid;
        }

        private Border StatBox(string label, string value, int row, int col)
        {
            var s = new StackPanel();
            s.Children.Add(new TextBlock
            {
                Text       = label.ToUpperInvariant(),
                FontSize   = 7,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.Text3Brush,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            s.Children.Add(new TextBlock
            {
                Text       = value,
                FontFamily = NY930Theme.MonoFont,
                FontSize   = 12,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.TextBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 1, 0, 0)
            });

            var b = new Border
            {
                Background      = NY930Theme.Bg3Brush,
                BorderBrush     = NY930Theme.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(4),
                Padding         = new Thickness(7, 5, 7, 5),
                Margin          = col == 0 ? new Thickness(0, row == 0 ? 0 : 4, 2, 0)
                                            : new Thickness(2, row == 0 ? 0 : 4, 0, 0),
                Child           = s
            };
            Grid.SetRow(b, row);
            Grid.SetColumn(b, col);
            return b;
        }

        // ── "Estrategia bloqueada" line ───────────────────────
        private FrameworkElement BuildLockedLabel()
        {
            _lockedLabelTb = new TextBlock
            {
                Text       = "Estrategia bloqueada",
                FontFamily = NY930Theme.SansFont,
                FontSize   = 8,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.Text3Brush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(9, 8, 9, 0)
            };
            return _lockedLabelTb;
        }

        // ── Two buttons (apply-btn style) ─────────────────────
        private FrameworkElement BuildButtons()
        {
            var stack = new StackPanel { Margin = new Thickness(9, 8, 9, 14) };

            var btnProgram = NY930Theme.ApplyButton("⏱ PROGRAMAR ENTRADA");
            _btnProgramTb = (TextBlock)null; // applied as TextBlock content later if needed
            btnProgram.Click += (s, e) =>
            {
                if (_isOpenRange) _shell.Show(new NY930OpenRangeView(_shell));
                else              _shell.Show(new NY930HedgeView(_shell));
            };

            var btnSwitch = NY930Theme.ApplyButton("⇄ CAMBIAR DE ESTRATEGIA");
            btnSwitch.Margin = new Thickness(0, 5, 0, 0);
            btnSwitch.Click += (s, e) => _shell.Show(new NY930HomeView(_shell));

            stack.Children.Add(btnProgram);
            stack.Children.Add(btnSwitch);
            return stack;
        }

        // ── INY930Localizable ─────────────────────────────────
        private void OnLanguageChanged() => Dispatcher.InvokeAsync(RefreshLocalization);

        public void RefreshLocalization()
        {
            // Strategy label and locked text are Spanish per the
            // mockup, no i18n table for them. Keep as-is for now.
        }

        public void Dispose()
        {
            NY930Localization.LanguageChanged -= OnLanguageChanged;
        }
    }
}
