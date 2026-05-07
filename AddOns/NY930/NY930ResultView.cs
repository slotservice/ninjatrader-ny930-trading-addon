// ============================================================
//  NY930ResultView — trade result screen
// ------------------------------------------------------------
//  Shown by the shell when a trade closes. Matches the
//  "Resultado positivo / Resultado negativo" mockup screens
//  from the client's reference flow image:
//
//    ┌────────────────────────────────┐
//    │            ╭─────╮             │
//    │            │  ✓  │             │   ← big icon
//    │            ╰─────╯             │
//    │       WINNING TRADE            │
//    │       +$718.75                 │
//    │       ▲ +57.5 ticks            │
//    │   [14:32]   [10 contracts]     │
//    ├────────────────────────────────┤
//    │ Entry  21,349.50               │
//    │ Exit   21,407.00               │
//    ├────────────────────────────────┤
//    │ ✓ TP1   3 contracts   +$75    │
//    │ ✓ TP2   3 contracts   +$281   │
//    │ ✓ TP3   4 contracts   +$375   │
//    ├────────────────────────────────┤
//    │      STRATEGY LOCKED           │
//    │   [BACK TO HOME]               │
//    └────────────────────────────────┘
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
        private readonly NY930ShellView _shell;
        private readonly NY930TradeResult _result;

        private TextBlock _titleText;
        private TextBlock _currencyText;
        private TextBlock _ticksText;
        private TextBlock _durationText;
        private TextBlock _contractsText;
        private TextBlock _entryRow;
        private TextBlock _exitRow;
        private TextBlock _lockedText;
        private Button    _backButton;
        private TextBlock _entryLabel;
        private TextBlock _exitLabel;

        public NY930ResultView(NY930ShellView shell, NY930TradeResult r)
        {
            _shell  = shell;
            _result = r ?? new NY930TradeResult();
            Background = NY930Theme.BgBaseBrush;

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Children.Add(scroll);

            var root = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            scroll.Content = root;

            bool win   = _result.PnLTicks >= 0;
            Color tint = win ? NY930Theme.LongGreen : NY930Theme.ShortRed;
            Brush tintBrush = NY930Theme.SolidBrush(tint);

            // ── Hero ─────────────────────────────────────────────
            var heroBox = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            heroBox.Children.Add(NY930Theme.ResultIcon(win, 88));

            _titleText = new TextBlock
            {
                Text       = NY930Localization.T(win ? "result.win.title" : "result.loss.title"),
                FontSize   = 13,
                FontWeight = FontWeights.Bold,
                Foreground = tintBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 0, 0, 6)
            };
            heroBox.Children.Add(_titleText);

            string sign = win ? "+" : "";
            _currencyText = new TextBlock
            {
                Text       = sign + _result.PnLCurrency.ToString("C", CultureInfo.CurrentCulture),
                FontSize   = 38,
                FontWeight = FontWeights.Black,
                Foreground = tintBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            heroBox.Children.Add(_currencyText);

            _ticksText = new TextBlock
            {
                Text       = (win ? "▲ " : "▼ ") + sign + _result.PnLTicks.ToString("F1") + " " + NY930Localization.T("progress.ticks"),
                FontSize   = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = tintBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 4, 0, 12)
            };
            heroBox.Children.Add(_ticksText);

            // Stat pills row
            var pills = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4)
            };
            TimeSpan dur = (_result.ExitTime - _result.EntryTime);
            if (dur.TotalSeconds < 0) dur = TimeSpan.Zero;
            var durPill = NY930Theme.Pill(((int)dur.TotalMinutes).ToString("D2") + ":" + dur.Seconds.ToString("D2"), NY930Theme.GoldDim);
            _durationText = (TextBlock)durPill.Child;
            pills.Children.Add(durPill);

            var ctsPill = NY930Theme.Pill(_result.Contracts + " " + NY930Localization.T("result.contracts_label"), NY930Theme.GoldDim);
            _contractsText = (TextBlock)ctsPill.Child;
            pills.Children.Add(ctsPill);
            heroBox.Children.Add(pills);

            root.Children.Add(NY930Theme.Panel(heroBox, new Thickness(0, 0, 0, 12)));

            // ── Entry / Exit ─────────────────────────────────────
            var trades = new StackPanel();
            _entryLabel = MakeLabelRow(trades, NY930Localization.T("result.entry"),
                _result.EntryPrice > 0 ? _result.EntryPrice.ToString("F5", CultureInfo.InvariantCulture) : "—",
                out _entryRow);
            _exitLabel = MakeLabelRow(trades, NY930Localization.T("result.exit"),
                _result.ExitPrice  > 0 ? _result.ExitPrice.ToString("F5",  CultureInfo.InvariantCulture) : "—",
                out _exitRow);
            root.Children.Add(NY930Theme.Panel(trades, new Thickness(0, 0, 0, 12)));

            // ── TP/SL summary ────────────────────────────────────
            var summaryBox = new StackPanel();
            summaryBox.Children.Add(NY930Theme.SectionHeader(NY930Localization.T("result.tp_hits")));

            if (_result.P1Hit)
            {
                var c = new NY930Theme.TpProgressCard(NY930Localization.T("trade.tp1.label"));
                c.SetState(NY930Theme.TpState.Done, NY930Localization.T("trade.tp1.label"), "✓");
                summaryBox.Children.Add(c);
            }
            if (_result.P2Hit)
            {
                var c = new NY930Theme.TpProgressCard(NY930Localization.T("trade.tp2.label"));
                c.SetState(NY930Theme.TpState.Done, NY930Localization.T("trade.tp2.label"), "✓");
                summaryBox.Children.Add(c);
            }
            if (_result.TpHit)
            {
                var c = new NY930Theme.TpProgressCard(NY930Localization.T("trade.tp.label"));
                c.SetState(NY930Theme.TpState.Done, NY930Localization.T("trade.tp.label"), "✓");
                summaryBox.Children.Add(c);
            }
            if (_result.SlHit)
            {
                var c = new NY930Theme.TpProgressCard(NY930Localization.T("trade.sl.label"), isSlSide: true);
                c.SetState(NY930Theme.TpState.Failed, NY930Localization.T("trade.sl.hit"), "✕");
                summaryBox.Children.Add(c);
            }

            // Reason
            if (!string.IsNullOrEmpty(_result.ExitReason))
            {
                summaryBox.Children.Add(new TextBlock
                {
                    Text       = NY930Localization.T("result.reason") + ": " + _result.ExitReason,
                    FontSize   = 10,
                    Foreground = NY930Theme.TextLowBrush,
                    Margin     = new Thickness(0, 6, 0, 0)
                });
            }
            root.Children.Add(NY930Theme.Panel(summaryBox, new Thickness(0, 0, 0, 12)));

            // ── Locked notice + back button ──────────────────────
            _lockedText = new TextBlock
            {
                Text       = NY930Localization.T("result.locked"),
                FontSize   = 10,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.TextLowBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 0, 0, 8)
            };
            root.Children.Add(_lockedText);

            _backButton = NY930Theme.BigActionButton(NY930Localization.T("result.back_home"),
                NY930Theme.BlueAccent, true);
            _backButton.Click += (s, e) => _shell.Show(new NY930HomeView(_shell));
            root.Children.Add(_backButton);
        }

        private static TextBlock MakeLabelRow(StackPanel parent, string label, string value, out TextBlock valueOut)
        {
            var g = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 11,
                Foreground = NY930Theme.TextMidBrush
            };
            Grid.SetColumn(lbl, 0);

            valueOut = new TextBlock
            {
                Text = value,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Consolas"),
                Foreground = NY930Theme.TextHiBrush
            };
            Grid.SetColumn(valueOut, 1);

            g.Children.Add(lbl);
            g.Children.Add(valueOut);
            parent.Children.Add(g);
            return lbl;
        }

        public void RefreshLocalization()
        {
            bool win = _result.PnLTicks >= 0;
            _titleText.Text     = NY930Localization.T(win ? "result.win.title" : "result.loss.title");
            _lockedText.Text    = NY930Localization.T("result.locked");
            _backButton.Content = NY930Localization.T("result.back_home");
            _entryLabel.Text    = NY930Localization.T("result.entry");
            _exitLabel.Text     = NY930Localization.T("result.exit");
        }

        public void Dispose() { /* no event subscriptions */ }
    }
}
