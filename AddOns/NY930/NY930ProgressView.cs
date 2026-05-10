// ============================================================
//  NY930ProgressView — v1.3
//  Reference: ny930-progreso_positivo.html / _negativo.html
// ------------------------------------------------------------
//  Shown while a position is open. Layout:
//
//    .header          NY930 logo + side badge (▲ LONG / ▼ SHORT)
//    Account bar      "Live · Principal" right-aligned mono
//    .price-row       instrument + current price
//    .pnl-box         label, ticks, big PnL value, entry + ctos
//    .stats-row       Realizado + Duración (2 cols)
//    .prog-wrap       horizontal SL ← center → TP progress bar
//    Divider
//    .gestion-row     PARTIAL CLOSE | %selector | TRAILING STOP
//    Acciones row     BREAKEVEN + CERRAR (gold + red)
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
    public sealed class NY930ProgressView : Grid, INY930Localizable, IDisposable
    {
        private readonly NY930ShellView _shell;
        private readonly bool _isOpenRange;

        // Header
        private NY930Theme.SideBadge _sideBadge;

        // Price row
        private TextBlock _instrumentTb, _currentPriceTb;

        // PnL box
        private Border    _pnlBox;
        private TextBlock _pnlValueTb, _pnlTicksTb, _pnlEntryTb, _pnlContractsTb;

        // Stats
        private TextBlock _realTb, _durTb;

        // Progress bar
        private Border _progGreen, _progRed;
        private TextBlock _progSlValTb, _progTpValTb;

        // Buttons (cached for state)
        private Button _btnPartial, _btnTrailing, _btnBreakeven, _btnCerrar;

        // Percentage selector for partial close
        private NY930Theme.PartialPercentSelector _pctSelector;
        private TextBlock _pctText;

        // Ticker for live duration update
        private System.Windows.Threading.DispatcherTimer _ticker;

        public NY930ProgressView(NY930ShellView shell, bool isOpenRange)
        {
            _shell = shell;
            _isOpenRange = isOpenRange;
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
            stack.Children.Add(BuildAccountBar());
            stack.Children.Add(BuildPriceRow());
            stack.Children.Add(BuildPnlBox());
            stack.Children.Add(BuildStatsRow());
            stack.Children.Add(BuildProgressBar());
            stack.Children.Add(NY930Theme.Divider());
            stack.Children.Add(NY930Theme.SectionLabel("Gestión de posición"));
            stack.Children.Add(BuildGestionRow());
            stack.Children.Add(NY930Theme.SectionLabel("Acciones"));
            stack.Children.Add(BuildActionsRow());
            stack.Children.Add(new Border { Height = 12 });

            // Bridge wiring
            if (_isOpenRange) NY930Bridge.OpenRangeChanged += OnOR;
            else              NY930Bridge.HedgeChanged     += OnHedge;

            // Initial render from cached snapshot
            if (_isOpenRange)
            {
                var s = NY930Bridge.GetOpenRange();
                if (s != null) RenderOR(s);
            }
            else
            {
                var s = NY930Bridge.GetHedge();
                if (s != null) RenderHedge(s);
            }

            _ticker = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _ticker.Tick += (s, e) => RefreshDuration();
            _ticker.Start();
        }

        // ── Header ─────────────────────────────────────────────
        private FrameworkElement BuildHeader()
        {
            var h = new NY930Theme.PanelHeader();
            _sideBadge = new NY930Theme.SideBadge();
            h.Right.Children.Add(_sideBadge);
            return h;
        }

        // ── Account bar ────────────────────────────────────────
        private FrameworkElement BuildAccountBar()
        {
            return new Border
            {
                BorderBrush     = NY930Theme.BorderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding         = new Thickness(10, 3, 10, 3),
                Child = new TextBlock
                {
                    Text       = "Live · Principal",
                    FontFamily = NY930Theme.MonoFont,
                    FontSize   = 8,
                    FontWeight = FontWeights.Bold,
                    Foreground = NY930Theme.Text3Brush,
                    HorizontalAlignment = HorizontalAlignment.Right
                }
            };
        }

        // ── Price row ──────────────────────────────────────────
        private FrameworkElement BuildPriceRow()
        {
            var grid = new Grid { Margin = new Thickness(10, 6, 10, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _instrumentTb = new TextBlock
            {
                Text       = "—",
                FontFamily = NY930Theme.MonoFont,
                FontSize   = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = NY930Theme.Text3Brush
            };
            Grid.SetColumn(_instrumentTb, 0);
            grid.Children.Add(_instrumentTb);

            _currentPriceTb = new TextBlock
            {
                Text       = "—",
                FontFamily = NY930Theme.MonoFont,
                FontSize   = 15,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.TextBrush
            };
            Grid.SetColumn(_currentPriceTb, 1);
            grid.Children.Add(_currentPriceTb);
            return grid;
        }

        // ── PnL box ────────────────────────────────────────────
        private FrameworkElement BuildPnlBox()
        {
            _pnlBox = new Border
            {
                Margin          = new Thickness(9, 6, 9, 0),
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(5),
                Padding         = new Thickness(10, 8, 10, 8)
            };

            var stack = new StackPanel();

            // Top: label + ticks
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.Children.Add(WithCol(new TextBlock
            {
                Text = "P&L NO REALIZADO",
                FontSize = 8, FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.Text3Brush
            }, 0));
            _pnlTicksTb = new TextBlock
            {
                Text = "▲ +0 ticks",
                FontFamily = NY930Theme.MonoFont,
                FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.GreenBrush
            };
            top.Children.Add(WithCol(_pnlTicksTb, 1));
            stack.Children.Add(top);

            // Big value
            _pnlValueTb = new TextBlock
            {
                Text       = "+$0.00",
                FontFamily = NY930Theme.MonoFont,
                FontSize   = 30,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.GreenBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 3, 0, 0)
            };
            stack.Children.Add(_pnlValueTb);

            // Bottom: entry + contracts
            var bot = new Grid { Margin = new Thickness(0, 5, 0, 0) };
            bot.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _pnlEntryTb = new TextBlock
            {
                Text = "Entrada: —",
                FontFamily = NY930Theme.MonoFont, FontSize = 8,
                Foreground = NY930Theme.Text3Brush
            };
            bot.Children.Add(WithCol(_pnlEntryTb, 0));
            _pnlContractsTb = new TextBlock
            {
                Text = "0 ctos",
                FontFamily = NY930Theme.MonoFont, FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.Text2Brush
            };
            bot.Children.Add(WithCol(_pnlContractsTb, 1));
            stack.Children.Add(bot);

            _pnlBox.Child = stack;
            ApplyPnlStyle(true); // default win style for empty state
            return _pnlBox;
        }

        private void ApplyPnlStyle(bool win)
        {
            Color tint = win ? NY930Theme.Green : NY930Theme.Red;
            _pnlBox.Background  = NY930Theme.SolidBrush(win
                ? Color.FromArgb(0x2E, 22, 101, 52)
                : Color.FromArgb(0x2E, 153, 27, 27));
            _pnlBox.BorderBrush = NY930Theme.BrushAlpha(tint, 0x33);
            _pnlValueTb.Foreground = NY930Theme.SolidBrush(tint);
            _pnlTicksTb.Foreground = NY930Theme.BrushAlpha(tint, 0xCC);
        }

        // ── Stats row ──────────────────────────────────────────
        private FrameworkElement BuildStatsRow()
        {
            var grid = new Grid { Margin = new Thickness(9, 5, 9, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            grid.Children.Add(WithCol(BuildStatBox("REALIZADO", "+$0", out _realTb, NY930Theme.GreenBrush, false), 0));
            grid.Children.Add(WithCol(BuildStatBox("DURACIÓN", "00:00", out _durTb, NY930Theme.TextBrush, true),  1));
            return grid;
        }

        private Border BuildStatBox(string lbl, string defVal, out TextBlock valOut, Brush valBrush, bool rightSide)
        {
            var s = new StackPanel();
            s.Children.Add(new TextBlock
            {
                Text = lbl, FontSize = 8, FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.Text3Brush,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            valOut = new TextBlock
            {
                Text = defVal, FontFamily = NY930Theme.MonoFont,
                FontSize = 13, FontWeight = FontWeights.Bold,
                Foreground = valBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            };
            s.Children.Add(valOut);

            return new Border
            {
                Background      = NY930Theme.Bg3Brush,
                BorderBrush     = NY930Theme.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(4),
                Padding         = new Thickness(7, 5, 7, 5),
                Margin          = rightSide ? new Thickness(2, 0, 0, 0) : new Thickness(0, 0, 2, 0),
                Child           = s
            };
        }

        // ── Progress bar (SL ← center → TP) ────────────────────
        private FrameworkElement BuildProgressBar()
        {
            var wrap = new StackPanel { Margin = new Thickness(9, 7, 9, 0) };

            // Header row: SL — Progreso — TP
            var head = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.ColumnDefinitions.Add(new ColumnDefinition());
            head.Children.Add(WithCol(new TextBlock
            {
                Text = "SL", FontSize = 8, FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.BrushAlpha(NY930Theme.Red, 0x99),
                HorizontalAlignment = HorizontalAlignment.Left
            }, 0));
            head.Children.Add(WithCol(new TextBlock
            {
                Text = "Progreso", FontSize = 8, FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.Text3Brush,
                HorizontalAlignment = HorizontalAlignment.Center
            }, 1));
            head.Children.Add(WithCol(new TextBlock
            {
                Text = "TP", FontSize = 8, FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.BrushAlpha(NY930Theme.Green, 0x99),
                HorizontalAlignment = HorizontalAlignment.Right
            }, 2));
            wrap.Children.Add(head);

            // Track with center marker, two halves
            var track = new Border
            {
                Height          = 20,
                Background      = NY930Theme.Bg3Brush,
                BorderBrush     = NY930Theme.Border2Brush,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                ClipToBounds    = true
            };
            var canvas = new Grid();
            // Red bar: anchored to right of left-half
            _progRed = new Border
            {
                Background = new LinearGradientBrush(
                    NY930Theme.BrushAlpha(NY930Theme.Red, 0xCC).Color,
                    NY930Theme.BrushAlpha(NY930Theme.Red, 0x4C).Color, 180),
                Width      = 0,
                HorizontalAlignment = HorizontalAlignment.Center,
                CornerRadius = new CornerRadius(6, 0, 0, 6)
            };
            // Green bar: anchored to left of right-half
            _progGreen = new Border
            {
                Background = new LinearGradientBrush(
                    NY930Theme.BrushAlpha(NY930Theme.Green, 0xCC).Color,
                    NY930Theme.BrushAlpha(NY930Theme.Green, 0x4C).Color, 0),
                Width      = 0,
                HorizontalAlignment = HorizontalAlignment.Center,
                CornerRadius = new CornerRadius(0, 6, 6, 0)
            };
            // Use a 2-col grid with both halves
            var halves = new Grid();
            halves.ColumnDefinitions.Add(new ColumnDefinition());
            halves.ColumnDefinitions.Add(new ColumnDefinition());
            var leftHalf = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            var rightHalf = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            _progRed.HorizontalAlignment   = HorizontalAlignment.Right;
            _progGreen.HorizontalAlignment = HorizontalAlignment.Left;
            leftHalf.Children.Add(_progRed);
            rightHalf.Children.Add(_progGreen);
            Grid.SetColumn(leftHalf, 0);
            Grid.SetColumn(rightHalf, 1);
            halves.Children.Add(leftHalf);
            halves.Children.Add(rightHalf);
            canvas.Children.Add(halves);

            // Center marker
            var marker = new Rectangle
            {
                Width  = 1.5,
                Fill   = NY930Theme.BrushAlpha(Colors.White, 0x33),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            canvas.Children.Add(marker);
            track.Child = canvas;
            wrap.Children.Add(track);

            // Values row
            var vals = new Grid { Margin = new Thickness(0, 3, 0, 0) };
            vals.ColumnDefinitions.Add(new ColumnDefinition());
            vals.ColumnDefinitions.Add(new ColumnDefinition());
            _progSlValTb = new TextBlock
            {
                Text = "−0 tks", FontFamily = NY930Theme.MonoFont, FontSize = 8,
                Foreground = NY930Theme.BrushAlpha(NY930Theme.Red, 0x99),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _progTpValTb = new TextBlock
            {
                Text = "+0 tks", FontFamily = NY930Theme.MonoFont, FontSize = 8,
                Foreground = NY930Theme.BrushAlpha(NY930Theme.Green, 0x99),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            vals.Children.Add(WithCol(_progSlValTb, 0));
            vals.Children.Add(WithCol(_progTpValTb, 1));
            wrap.Children.Add(vals);
            return wrap;
        }

        // ── Gestión row (PARTIAL CLOSE | % | TRAILING STOP) ───
        private FrameworkElement BuildGestionRow()
        {
            var grid = new Grid { Margin = new Thickness(9, 5, 9, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            _btnPartial = MakeGestBtn("PARTIAL\nCLOSE", NY930Theme.BlueDeep);
            _btnPartial.Margin = new Thickness(0, 0, 4, 0);
            _btnPartial.Click += (s, e) => SendPartial();
            Grid.SetColumn(_btnPartial, 0);
            grid.Children.Add(_btnPartial);

            _pctSelector = new NY930Theme.PartialPercentSelector
            {
                Margin = new Thickness(0, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_pctSelector, 1);
            grid.Children.Add(_pctSelector);

            _btnTrailing = MakeGestBtn("TRAILING\nSTOP", NY930Theme.Green);
            _btnTrailing.Click += (s, e) =>
            {
                if (_isOpenRange)
                    NY930Bridge.RequestOpenRangeAction(new NY930Action { Type = NY930ActionType.OpenRangeTrailingTrigger });
                else
                    NY930Bridge.RequestHedgeAction(new NY930Action { Type = NY930ActionType.HedgeTrailingTrigger });
            };
            Grid.SetColumn(_btnTrailing, 2);
            grid.Children.Add(_btnTrailing);
            return grid;
        }

        private Button MakeGestBtn(string text, Color tint)
        {
            return new Button
            {
                Content = text,
                Background = NY930Theme.BrushAlpha(tint, 0x1F),
                BorderBrush = NY930Theme.BrushAlpha(tint, 0x59),
                BorderThickness = new Thickness(1),
                Foreground = NY930Theme.BrushAlpha(tint, 0xCC),
                Padding = new Thickness(4, 8, 4, 8),
                FontFamily = NY930Theme.SansFont,
                FontSize = 9, FontWeight = FontWeights.Black,
                Cursor = System.Windows.Input.Cursors.Hand
            };
        }

        private void SendPartial()
        {
            int pct = _pctSelector.Percent;
            if (_isOpenRange)
            {
                var s = NY930Bridge.GetOpenRange();
                int total = s != null ? Math.Max(1, s.ContractsRemaining) : 1;
                int qty = Math.Max(1, Math.Min(total, (int)Math.Round(total * pct / 100.0)));
                NY930Bridge.RequestOpenRangeAction(new NY930Action
                { Type = NY930ActionType.OpenRangePartialClose, IntArg = qty });
            }
            else
            {
                var s = NY930Bridge.GetHedge();
                int total = s != null ? Math.Max(1, s.ContractsRemaining) : 1;
                int qty = Math.Max(1, Math.Min(total, (int)Math.Round(total * pct / 100.0)));
                NY930Bridge.RequestHedgeAction(new NY930Action
                { Type = NY930ActionType.HedgePartialClose, IntArg = qty });
            }
        }

        // ── Acciones row (BREAKEVEN + CERRAR) ─────────────────
        private FrameworkElement BuildActionsRow()
        {
            var grid = new Grid { Margin = new Thickness(9, 5, 9, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            _btnBreakeven = NY930Theme.ApplyButton("⇌ BREAKEVEN");
            _btnBreakeven.Margin = new Thickness(0, 0, 2, 0);
            _btnBreakeven.Click += (s, e) =>
            {
                if (_isOpenRange)
                    NY930Bridge.RequestOpenRangeAction(new NY930Action { Type = NY930ActionType.OpenRangeBreakeven });
                else
                    NY930Bridge.RequestHedgeAction(new NY930Action { Type = NY930ActionType.HedgeBreakeven });
            };
            Grid.SetColumn(_btnBreakeven, 0);
            grid.Children.Add(_btnBreakeven);

            _btnCerrar = new Button
            {
                Content = "■ CERRAR",
                Background = Brushes.Transparent,
                Foreground = NY930Theme.RedBrush,
                BorderBrush = NY930Theme.BrushAlpha(NY930Theme.Red, 0x80),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(0, 7, 0, 7),
                FontFamily = NY930Theme.MonoFont,
                FontSize = 9, FontWeight = FontWeights.Bold,
                Margin = new Thickness(2, 0, 0, 0),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _btnCerrar.Click += (s, e) =>
            {
                if (_isOpenRange)
                    NY930Bridge.RequestOpenRangeAction(new NY930Action { Type = NY930ActionType.OpenRangeFlatten });
                else
                    NY930Bridge.RequestHedgeAction(new NY930Action { Type = NY930ActionType.HedgeFlatten });
            };
            Grid.SetColumn(_btnCerrar, 1);
            grid.Children.Add(_btnCerrar);
            return grid;
        }

        // ── Bridge events ─────────────────────────────────────
        private void OnOR(NY930OpenRangeSnapshot s)    => Dispatcher.InvokeAsync(() => RenderOR(s));
        private void OnHedge(NY930HedgeSnapshot s)     => Dispatcher.InvokeAsync(() => RenderHedge(s));

        // ── Render Open Range ─────────────────────────────────
        private DateTime _tradeStart;
        private double   _entryPrice;
        private void RenderOR(NY930OpenRangeSnapshot s)
        {
            if (s == null) return;
            _instrumentTb.Text = s.Instrument ?? "—";
            _currentPriceTb.Text = s.LastPrice > 0 ? s.LastPrice.ToString("N2", CultureInfo.CurrentCulture) : "—";

            string side = s.InLong ? "Long" : (s.InShort ? "Short" : "None");
            _sideBadge.SetSide(side);

            _tradeStart = s.TradeStartTime;
            _entryPrice = s.EntryFill;

            bool win = s.UnrealizedTicks >= 0;
            ApplyPnlStyle(win);
            _pnlValueTb.Text = (win ? "+$" : "−$") + Math.Abs(s.UnrealizedCurrency).ToString("N2", CultureInfo.CurrentCulture);
            _pnlTicksTb.Text = (win ? "▲ +" : "▼ −") + Math.Abs(s.UnrealizedTicks).ToString("F0") + " ticks";
            _pnlEntryTb.Text = "Entrada: " + (s.EntryFill > 0 ? s.EntryFill.ToString("N2", CultureInfo.CurrentCulture) : "—");
            _pnlContractsTb.Text = s.ContractsRemaining + " ctos";

            // Progress: distance from entry to TP/SL.
            if (s.EntryFill > 0 && s.TickSize > 0)
            {
                double slDist = s.InLong ? (s.SlPrice - s.EntryFill) / s.TickSize : (s.EntryFill - s.SlPrice) / s.TickSize;
                double tpDist = s.InLong ? (s.TpPrice - s.EntryFill) / s.TickSize : (s.EntryFill - s.TpPrice) / s.TickSize;
                UpdateProgress(s.UnrealizedTicks, slDist, tpDist);
            }

            // Auto-route to result
            if (s.LastResult != null && !s.InLong && !s.InShort && s.SessionDone
                && _shell.CurrentViewIs<NY930ProgressView>())
                _shell.Show(new NY930ResultView(_shell, s.LastResult));

            RefreshDuration();
        }

        // ── Render Hedge ──────────────────────────────────────
        private void RenderHedge(NY930HedgeSnapshot s)
        {
            if (s == null) return;
            _instrumentTb.Text = s.Instrument ?? "—";
            _currentPriceTb.Text = s.LastPrice > 0 ? s.LastPrice.ToString("N2", CultureInfo.CurrentCulture) : "—";
            _sideBadge.SetSide(s.Direction ?? "None");

            _tradeStart = s.TradeStartTime;
            _entryPrice = s.EntryFill;

            bool win = s.UnrealizedTicks >= 0;
            ApplyPnlStyle(win);
            _pnlValueTb.Text = (win ? "+$" : "−$") + Math.Abs(s.UnrealizedCurrency).ToString("N2", CultureInfo.CurrentCulture);
            _pnlTicksTb.Text = (win ? "▲ +" : "▼ −") + Math.Abs(s.UnrealizedTicks).ToString("F0") + " ticks";
            _pnlEntryTb.Text = "Entrada: " + (s.EntryFill > 0 ? s.EntryFill.ToString("N2", CultureInfo.CurrentCulture) : "—");
            _pnlContractsTb.Text = s.ContractsRemaining + " ctos";

            if (s.EntryFill > 0 && s.TickSize > 0)
            {
                bool isLong = string.Equals(s.Direction, "Long", StringComparison.OrdinalIgnoreCase);
                double slDist = isLong ? (s.SlPrice - s.EntryFill) / s.TickSize : (s.EntryFill - s.SlPrice) / s.TickSize;
                double tpDist = isLong ? (s.TpPrice - s.EntryFill) / s.TickSize : (s.EntryFill - s.TpPrice) / s.TickSize;
                UpdateProgress(s.UnrealizedTicks, slDist, tpDist);
            }

            if (s.LastResult != null && !s.InPosition && s.SessionDone
                && _shell.CurrentViewIs<NY930ProgressView>())
                _shell.Show(new NY930ResultView(_shell, s.LastResult));

            RefreshDuration();
        }

        // ── Progress bar update (mirrors progreso JS) ─────────
        private void UpdateProgress(double curTicks, double slTicks, double tpTicks)
        {
            double maxRange = Math.Max(Math.Abs(slTicks), Math.Abs(tpTicks));
            if (maxRange <= 0) maxRange = 1;
            double halfWidth = (ActualWidth > 0 ? ActualWidth - 18 : NY930Theme.PanelWidth - 18) / 2.0;
            double fillRatio = Math.Min(1.0, Math.Abs(curTicks) / maxRange);
            double fillPx = halfWidth * fillRatio;

            if (curTicks >= 0)
            {
                _progGreen.Width = fillPx;
                _progRed.Width   = 0;
            }
            else
            {
                _progRed.Width   = fillPx;
                _progGreen.Width = 0;
            }

            _progTpValTb.Text = (curTicks >= 0 ? "+" : "−") + Math.Abs((int)Math.Round(curTicks)) + " tks";
            _progSlValTb.Text = "−" + Math.Abs((int)Math.Round(slTicks)) + " tks";
        }

        // ── Live duration tick ────────────────────────────────
        private void RefreshDuration()
        {
            if (_durTb == null) return;
            if (_tradeStart == default(DateTime))
            {
                _durTb.Text = "00:00";
                return;
            }
            var d = DateTime.Now - _tradeStart;
            if (d.TotalSeconds < 0) d = TimeSpan.Zero;
            _durTb.Text = ((int)d.TotalMinutes).ToString("D2") + ":" + d.Seconds.ToString("D2");
        }

        // ── Helpers ───────────────────────────────────────────
        private static T WithCol<T>(T element, int col) where T : UIElement
        {
            Grid.SetColumn(element, col);
            return element;
        }

        public void RefreshLocalization() { /* labels are Spanish */ }

        public void Dispose()
        {
            if (_isOpenRange) NY930Bridge.OpenRangeChanged -= OnOR;
            else              NY930Bridge.HedgeChanged     -= OnHedge;
            if (_ticker != null) { _ticker.Stop(); _ticker = null; }
        }
    }
}
