// ============================================================
//  NY930ResultView — v1.2 pixel-match against client mockup
// ------------------------------------------------------------
//  Reproduces "Resultado positivo / Resultado negativo":
//
//   ┌───────────────────────────────────┐
//   │ NY930 | APERTURA BREAKOUT | TAG   │   header
//   ├───────────────────────────────────┤
//   │            ╭─────╮                │
//   │            │  ✓  │                │   big icon
//   │            ╰─────╯                │
//   │      OPERACIÓN GANADORA           │
//   │       +$718.75                    │
//   │       ▲ +57.5 ticks               │
//   │ [14:32]  [10 ctos]                │   pills
//   ├───────────────────────────────────┤
//   │ [ ENTRY ]    [ EXIT ]             │   side-by-side
//   ├───────────────────────────────────┤
//   │ ✓ TP1 - 3 ctos          +$75      │
//   │ ✓ TP2 - 3 ctos          +$281     │
//   │ ✓ TP3 - 4 ctos          +$375     │
//   ├───────────────────────────────────┤
//   │      ESTRATEGIA BLOQUEADA         │
//   │ [────── VOLVER AL INICIO ──────]  │
//   └───────────────────────────────────┘
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
    public sealed class NY930ResultView : Grid, INY930Localizable, IDisposable
    {
        private readonly NY930ShellView   _shell;
        private readonly NY930TradeResult _result;

        private NY930Theme.TradeHeader _header;
        private TextBlock _titleText;
        private TextBlock _currencyText;
        private TextBlock _ticksText;
        private TextBlock _durationPill;
        private TextBlock _contractsPill;
        private TextBlock _entryValue;
        private TextBlock _exitValue;
        private TextBlock _entryLabel;
        private TextBlock _exitLabel;
        private TextBlock _lockedText;
        private Button    _backButton;

        public NY930ResultView(NY930ShellView shell, NY930TradeResult r)
        {
            _shell  = shell;
            _result = r ?? new NY930TradeResult();
            Background = NY930Theme.BgNavyBrush;

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Children.Add(scroll);

            var root = new StackPanel();
            scroll.Content = root;

            bool win = _result.PnLTicks >= 0;
            Color tint = win ? NY930Theme.SuccessGreen : NY930Theme.DangerRed;
            Brush tintBrush = NY930Theme.SolidBrush(tint);

            BuildHeader(root, win);
            BuildHero(root, win, tint, tintBrush);
            BuildEntryExit(root, tint);
            BuildBreakdown(root);
            BuildBackButton(root);
        }

        private void BuildHeader(StackPanel root, bool win)
        {
            string strategyName = (_result.Strategy ?? "").Equals("Hedge", StringComparison.OrdinalIgnoreCase)
                ? "APERTURA"
                : "APERTURA BREAKOUT";
            _header = new NY930Theme.TradeHeader(strategyName);
            _header.StatusTag.Update(win ? "COMPLETADA" : "STOP LOSS",
                win ? NY930Theme.SuccessGreen : NY930Theme.DangerRed);
            root.Children.Add(_header);
        }

        private void BuildHero(StackPanel root, bool win, Color tint, Brush tintBrush)
        {
            var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            // Big circle icon
            inner.Children.Add(NY930Theme.ResultIcon(win, 88));

            _titleText = new TextBlock
            {
                Text       = NY930Localization.T(win ? "result.win.title" : "result.loss.title"),
                FontSize   = 13,
                FontWeight = FontWeights.Bold,
                Foreground = tintBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 4, 0, 8)
            };
            inner.Children.Add(_titleText);

            string sign = win ? "+" : "";
            _currencyText = new TextBlock
            {
                Text       = sign + _result.PnLCurrency.ToString("C", CultureInfo.CurrentCulture),
                FontSize   = 38,
                FontWeight = FontWeights.Black,
                Foreground = tintBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            inner.Children.Add(_currencyText);

            _ticksText = new TextBlock
            {
                Text       = (win ? "▲ " : "▼ ") + sign + _result.PnLTicks.ToString("F1") + " " + NY930Localization.T("progress.ticks"),
                FontSize   = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = tintBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 4, 0, 12)
            };
            inner.Children.Add(_ticksText);

            // Pills row
            var pills = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            TimeSpan dur = (_result.ExitTime - _result.EntryTime);
            if (dur.TotalSeconds < 0) dur = TimeSpan.Zero;
            var durPill = NY930Theme.Pill(((int)dur.TotalMinutes).ToString("D2") + ":" + dur.Seconds.ToString("D2"),
                NY930Theme.TextNavyMid);
            _durationPill = (TextBlock)durPill.Child;
            pills.Children.Add(durPill);

            var ctsPill = NY930Theme.Pill(_result.Contracts + " " + NY930Localization.T("result.contracts_label"),
                NY930Theme.TextNavyMid);
            _contractsPill = (TextBlock)ctsPill.Child;
            pills.Children.Add(ctsPill);
            inner.Children.Add(pills);

            root.Children.Add(NY930Theme.NavyPanel(inner, new Thickness(12, 0, 12, 8)));
        }

        private void BuildEntryExit(StackPanel root, Color tint)
        {
            var grid = new Grid { Margin = new Thickness(12, 0, 12, 8) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            var entryBox = NY930Theme.PriceBox(
                NY930Localization.T("result.entry"),
                _result.EntryPrice > 0 ? _result.EntryPrice.ToString("F5", CultureInfo.InvariantCulture) : "—",
                NY930Theme.TextNavyMid,
                out _entryValue);
            entryBox.Margin = new Thickness(0, 0, 4, 0);
            Grid.SetColumn(entryBox, 0);
            grid.Children.Add(entryBox);

            var exitBox = NY930Theme.PriceBox(
                NY930Localization.T("result.exit"),
                _result.ExitPrice > 0 ? _result.ExitPrice.ToString("F5", CultureInfo.InvariantCulture) : "—",
                tint,
                out _exitValue);
            exitBox.Margin = new Thickness(4, 0, 0, 0);
            Grid.SetColumn(exitBox, 1);
            grid.Children.Add(exitBox);

            // Stash labels for live language refresh
            _entryLabel = (TextBlock)((StackPanel)entryBox.Child).Children[0];
            _exitLabel  = (TextBlock)((StackPanel)exitBox.Child).Children[0];

            root.Children.Add(grid);
        }

        private void BuildBreakdown(StackPanel root)
        {
            var stack = new StackPanel();

            // We don't know the exact per-row contract count from
            // the snapshot, so we approximate from EnablePartials
            // configuration. The currency per row is computed using
            // the result's overall PnL split proportionally to ticks.
            // For Phase 1.2 this is good enough — full breakdown
            // requires execution-level data we don't currently
            // mirror through the bridge.
            int totalTicks = (int)Math.Max(1, Math.Abs(_result.PnLTicks));
            double perTick = _result.PnLCurrency / Math.Max(1, totalTicks);

            if (_result.P1Hit)
            {
                stack.Children.Add(NY930Theme.ResultBreakdownRow(
                    NY930Localization.T("trade.tp1.label"),
                    Math.Max(1, _result.Contracts / 3),
                    "+" + (perTick * (totalTicks * 0.25)).ToString("C0", CultureInfo.CurrentCulture),
                    isWin: true));
            }
            if (_result.P2Hit)
            {
                stack.Children.Add(NY930Theme.ResultBreakdownRow(
                    NY930Localization.T("trade.tp2.label"),
                    Math.Max(1, _result.Contracts / 3),
                    "+" + (perTick * (totalTicks * 0.35)).ToString("C0", CultureInfo.CurrentCulture),
                    isWin: true));
            }
            if (_result.TpHit)
            {
                stack.Children.Add(NY930Theme.ResultBreakdownRow(
                    NY930Localization.T("trade.tp.label"),
                    Math.Max(1, _result.Contracts / 3),
                    (_result.PnLCurrency >= 0 ? "+" : "") + _result.PnLCurrency.ToString("C0", CultureInfo.CurrentCulture),
                    isWin: true));
            }
            if (_result.SlHit)
            {
                stack.Children.Add(NY930Theme.ResultBreakdownRow(
                    NY930Localization.T("trade.sl.label"),
                    _result.Contracts,
                    _result.PnLCurrency.ToString("C0", CultureInfo.CurrentCulture),
                    isWin: false));
            }

            // Reason line
            if (!string.IsNullOrEmpty(_result.ExitReason))
            {
                stack.Children.Add(new TextBlock
                {
                    Text       = NY930Localization.T("result.reason") + ": " + _result.ExitReason,
                    FontSize   = 9,
                    Foreground = NY930Theme.TextNavyLowBrush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin     = new Thickness(0, 6, 0, 0)
                });
            }

            root.Children.Add(NY930Theme.NavyPanel(stack, new Thickness(12, 0, 12, 8)));
        }

        private void BuildBackButton(StackPanel root)
        {
            var stack = new StackPanel { Margin = new Thickness(12, 0, 12, 12) };

            _lockedText = new TextBlock
            {
                Text       = NY930Localization.T("result.locked"),
                FontSize   = 10,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.TextNavyMidBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 0, 0, 8)
            };
            stack.Children.Add(_lockedText);

            _backButton = NY930Theme.NavyPrimaryButton(NY930Localization.T("result.back_home"));
            _backButton.Click += (s, e) => _shell.Show(new NY930HomeView(_shell));
            stack.Children.Add(_backButton);

            root.Children.Add(stack);
        }

        public void RefreshLocalization()
        {
            bool win = _result.PnLTicks >= 0;
            _titleText.Text     = NY930Localization.T(win ? "result.win.title" : "result.loss.title");
            _lockedText.Text    = NY930Localization.T("result.locked");
            _backButton.Content = NY930Localization.T("result.back_home");
            if (_entryLabel != null) _entryLabel.Text = NY930Localization.T("result.entry");
            if (_exitLabel  != null) _exitLabel.Text  = NY930Localization.T("result.exit");
        }

        public void Dispose() { /* no event subscriptions */ }
    }
}
