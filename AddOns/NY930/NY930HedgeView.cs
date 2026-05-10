// ============================================================
//  NY930HedgeView — v1.3 pixel-match
//  Reference: ny930-buy_or_sell-panel.html
// ------------------------------------------------------------
//  Same shape as the Open Range setup view, plus:
//    - BUY / or / SELL row at top to pick the side.
//    - Three top tabs instead of two: Horario | Precio | Manual.
//      * Precio tab is new (price-triggered entry).
//    - Estándar config has just SL/TP/Contratos (no Long/Short
//      accordions).
//    - No Single-Stop Reverse.
//    - Same Avanzada accordions.
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
    public sealed class NY930HedgeView : Grid, INY930Localizable, IDisposable
    {
        private readonly NY930ShellView _shell;

        // Header status dot/label
        private System.Windows.Shapes.Ellipse _statusDot;
        private TextBlock _statusLabelTb;

        // BUY/SELL selector
        private Button _btnBuy, _btnSell;
        private string _selectedSide = "none"; // "buy" | "sell" | "none"

        // Tabs
        private NY930Theme.TabButton _tabHorario, _tabPrecio, _tabManual;
        private StackPanel _panelHorario, _panelPrecio, _panelManual;

        // Sub-tabs
        private NY930Theme.TabButton _stabEst, _stabAvz;
        private StackPanel _panelEst, _panelAvz;

        // Schedule
        private TextBox _hh, _hm, _hs;
        private ComboBox _ampm;
        private Button   _btnActivarH;
        private StackPanel _scheduleForm, _scheduleCountdown;
        private TextBlock _countdownTb;
        private Button   _btnStopH;

        // Precio
        private TextBox _priceInput;
        private Button  _btnActivarP;
        private StackPanel _precioForm, _precioActive;
        private TextBlock _precioActiveTb;
        private Button   _btnStopP;

        // Manual
        private Button _btnManualActivate;

        // Estándar
        private TextBox _cfgQty, _cfgSl, _cfgTp;

        // Avanzada (same set as Open Range)
        private NY930Theme.Accordion _accBe, _accTs, _accPar, _accGg, _accSt;
        private TextBox _beTrigger, _beOffset;
        private TextBox _tsTrigger, _tsStep;
        private TextBox _p1Ticks, _p1Qty, _p2Ticks, _p2Qty;
        private TextBox _slGgTicks, _slGgSecs;
        private NY930Theme.TabButton _ggTpBtn, _ggTrBtn;
        private StackPanel _ggTpFields, _ggTrFields;
        private TextBox _tpGgTicks, _tpGgSecs;
        private TextBox _trDist, _trTimeout;
        private TextBox _stDuration;
        private ComboBox _stMode;
        private NY930Theme.ToggleSwitch _stBeyondTpTog;

        // Apply / change
        private Button _btnApply, _btnChangeStrat;

        // Countdown
        private System.Windows.Threading.DispatcherTimer _ticker;
        private DateTime _scheduledEntry;
        private bool     _scheduleArmed;

        public NY930HedgeView(NY930ShellView shell)
        {
            _shell = shell;
            Background = NY930Theme.BgBrush;

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent
            };
            Children.Add(scroll);

            var root = new StackPanel { MaxWidth = NY930Theme.PanelWidth };
            scroll.Content = root;

            root.Children.Add(BuildHeader());
            root.Children.Add(BuildBuySellRow());
            root.Children.Add(BuildAccountRow());
            root.Children.Add(BuildTabs());
            root.Children.Add(BuildTabPanels());
            root.Children.Add(NY930Theme.Divider());
            root.Children.Add(new TextBlock
            {
                Text = "CONFIGURACIÓN", FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.Text3Brush, Margin = new Thickness(9, 6, 9, 0)
            });
            root.Children.Add(BuildSubTabs());
            root.Children.Add(BuildSubPanels());
            root.Children.Add(BuildApplyArea());

            // Bridge
            NY930Bridge.HedgeChanged += OnSnapshot;
            var current = NY930Bridge.GetHedge();
            if (current != null) ApplySnapshotToInputs(current);

            _ticker = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _ticker.Tick += (s, e) => RefreshCountdown();
            _ticker.Start();
        }

        // ── Header with status dot/label ───────────────────────
        private FrameworkElement BuildHeader()
        {
            var h = new NY930Theme.PanelHeader();
            var rightStack = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            _statusDot = new System.Windows.Shapes.Ellipse
            {
                Width = 6, Height = 6,
                Fill = NY930Theme.Text3Brush,
                Margin = new Thickness(0, 0, 6, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            _statusLabelTb = new TextBlock
            {
                Text = "—",
                FontFamily = NY930Theme.MonoFont,
                FontSize = 10, FontWeight = FontWeights.SemiBold,
                Foreground = NY930Theme.Text2Brush,
                VerticalAlignment = VerticalAlignment.Center
            };
            rightStack.Children.Add(_statusDot);
            rightStack.Children.Add(_statusLabelTb);
            h.Right.Children.Add(rightStack);
            return h;
        }

        // ── BUY / or / SELL row ────────────────────────────────
        private FrameworkElement BuildBuySellRow()
        {
            var grid = new Grid { Margin = new Thickness(9, 9, 9, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            _btnBuy  = MakeBsBtn("BUY",  true);
            _btnSell = MakeBsBtn("SELL", false);
            _btnBuy.Click  += (s, e) => SelectSide("buy");
            _btnSell.Click += (s, e) => SelectSide("sell");

            var sep = new Border
            {
                Background      = NY930Theme.Bg3Brush,
                BorderBrush     = NY930Theme.Border2Brush,
                BorderThickness = new Thickness(0, 1, 0, 1),
                Child = new TextBlock
                {
                    Text       = "or",
                    FontFamily = NY930Theme.SansFont,
                    FontSize   = 9,
                    FontStyle  = FontStyles.Italic,
                    Foreground = NY930Theme.Text3Brush,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                }
            };

            Grid.SetColumn(_btnBuy, 0);
            Grid.SetColumn(sep, 1);
            Grid.SetColumn(_btnSell, 2);
            grid.Children.Add(_btnBuy);
            grid.Children.Add(sep);
            grid.Children.Add(_btnSell);
            return grid;
        }

        private Button MakeBsBtn(string text, bool leftRound)
        {
            return new Button
            {
                Content = text,
                Background = NY930Theme.Bg3Brush,
                BorderBrush = NY930Theme.Border2Brush,
                BorderThickness = new Thickness(1),
                Foreground = NY930Theme.Text2Brush,
                FontFamily = NY930Theme.MonoFont,
                FontSize = 9, FontWeight = FontWeights.Bold,
                Padding = new Thickness(0, 7, 0, 7),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
        }

        private void SelectSide(string side)
        {
            _selectedSide = side;
            ApplySideStyle();
        }

        private void ApplySideStyle()
        {
            if (_selectedSide == "buy")
            {
                _btnBuy.Background  = NY930Theme.BrushAlpha(NY930Theme.Green, 0x1F);
                _btnBuy.BorderBrush = NY930Theme.GreenBrush;
                _btnBuy.Foreground  = NY930Theme.GreenBrush;
                _btnSell.Background  = NY930Theme.Bg3Brush;
                _btnSell.BorderBrush = NY930Theme.Border2Brush;
                _btnSell.Foreground  = NY930Theme.Text2Brush;
                _statusDot.Fill = NY930Theme.GreenBrush;
                _statusLabelTb.Text = "BUY";
                _statusLabelTb.Foreground = NY930Theme.GreenBrush;
            }
            else if (_selectedSide == "sell")
            {
                _btnSell.Background  = NY930Theme.BrushAlpha(NY930Theme.Red, 0x1F);
                _btnSell.BorderBrush = NY930Theme.RedBrush;
                _btnSell.Foreground  = NY930Theme.RedBrush;
                _btnBuy.Background  = NY930Theme.Bg3Brush;
                _btnBuy.BorderBrush = NY930Theme.Border2Brush;
                _btnBuy.Foreground  = NY930Theme.Text2Brush;
                _statusDot.Fill = NY930Theme.RedBrush;
                _statusLabelTb.Text = "SELL";
                _statusLabelTb.Foreground = NY930Theme.RedBrush;
            }
            else
            {
                _btnBuy.Background  = NY930Theme.Bg3Brush;
                _btnBuy.BorderBrush = NY930Theme.Border2Brush;
                _btnBuy.Foreground  = NY930Theme.Text2Brush;
                _btnSell.Background  = NY930Theme.Bg3Brush;
                _btnSell.BorderBrush = NY930Theme.Border2Brush;
                _btnSell.Foreground  = NY930Theme.Text2Brush;
                _statusDot.Fill = NY930Theme.Text3Brush;
                _statusLabelTb.Text = "—";
                _statusLabelTb.Foreground = NY930Theme.Text2Brush;
            }
        }

        // ── Cuenta dropdown row ────────────────────────────────
        private FrameworkElement BuildAccountRow()
        {
            var grid = new Grid { Margin = new Thickness(9, 6, 9, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock
            {
                Text = "CUENTA", FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.Text3Brush, VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(lbl, 0);

            var combo = NY930Theme.FSelect(150);
            combo.Margin = new Thickness(8, 0, 0, 0);
            combo.Items.Add("Simulada");
            combo.Items.Add("Live · Principal");
            combo.Items.Add("Live · Secundaria");
            combo.Items.Add("Paper Trading");
            combo.SelectedIndex = 0;
            Grid.SetColumn(combo, 1);

            grid.Children.Add(lbl);
            grid.Children.Add(combo);
            return grid;
        }

        // ── 3-tab row: Horario | Precio | Manual ──────────────
        private FrameworkElement BuildTabs()
        {
            var border = new Border
            {
                Margin = new Thickness(9, 9, 9, 0),
                BorderBrush = NY930Theme.Border2Brush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4)
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition());

            _tabHorario = new NY930Theme.TabButton("Horario", true);
            _tabPrecio  = new NY930Theme.TabButton("Precio",  false);
            _tabManual  = new NY930Theme.TabButton("Manual",  false);
            _tabHorario.Click += (s, e) => SetActiveTab(0);
            _tabPrecio.Click  += (s, e) => SetActiveTab(1);
            _tabManual.Click  += (s, e) => SetActiveTab(2);

            Grid.SetColumn(_tabHorario, 0);
            Grid.SetColumn(_tabPrecio,  1);
            Grid.SetColumn(_tabManual,  2);
            grid.Children.Add(_tabHorario);
            grid.Children.Add(_tabPrecio);
            grid.Children.Add(_tabManual);
            border.Child = grid;
            return border;
        }

        private void SetActiveTab(int idx)
        {
            _tabHorario.SetActive(idx == 0);
            _tabPrecio.SetActive(idx == 1);
            _tabManual.SetActive(idx == 2);
            _panelHorario.Visibility = idx == 0 ? Visibility.Visible : Visibility.Collapsed;
            _panelPrecio.Visibility  = idx == 1 ? Visibility.Visible : Visibility.Collapsed;
            _panelManual.Visibility  = idx == 2 ? Visibility.Visible : Visibility.Collapsed;
        }

        // ── Tab panels ─────────────────────────────────────────
        private FrameworkElement BuildTabPanels()
        {
            var wrap = new StackPanel { Margin = new Thickness(9, 9, 9, 0) };

            // Horario
            _panelHorario = new StackPanel();
            _scheduleForm      = BuildScheduleForm();
            _scheduleCountdown = BuildScheduleCountdown();
            _scheduleCountdown.Visibility = Visibility.Collapsed;
            _panelHorario.Children.Add(_scheduleForm);
            _panelHorario.Children.Add(_scheduleCountdown);
            wrap.Children.Add(_panelHorario);

            // Precio
            _panelPrecio = new StackPanel { Visibility = Visibility.Collapsed };
            _precioForm   = BuildPrecioForm();
            _precioActive = BuildPrecioActive();
            _precioActive.Visibility = Visibility.Collapsed;
            _panelPrecio.Children.Add(_precioForm);
            _panelPrecio.Children.Add(_precioActive);
            wrap.Children.Add(_panelPrecio);

            // Manual
            _panelManual = new StackPanel { Visibility = Visibility.Collapsed };
            _btnManualActivate = NY930Theme.ActionButton("COMPRAR AHORA");
            _btnManualActivate.HorizontalAlignment = HorizontalAlignment.Stretch;
            _btnManualActivate.Padding = new Thickness(0, 6, 0, 6);
            _btnManualActivate.Click += (s, e) => DoManualEntry();
            _panelManual.Children.Add(_btnManualActivate);
            wrap.Children.Add(_panelManual);

            return wrap;
        }

        private StackPanel BuildScheduleForm()
        {
            var stack = new StackPanel();
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lbl = NY930Theme.FieldLabel("Hora");
            Grid.SetColumn(lbl, 0); row.Children.Add(lbl);

            var hourGroup = new StackPanel { Orientation = Orientation.Horizontal,
                                              HorizontalAlignment = HorizontalAlignment.Center,
                                              VerticalAlignment   = VerticalAlignment.Center };
            _hh = NY930Theme.FInput("9",  27);
            _hm = NY930Theme.FInput("29", 27);
            _hs = NY930Theme.FInput("58", 27);
            _ampm = NY930Theme.FSelect(40);
            _ampm.Items.Add("AM");
            _ampm.Items.Add("PM");
            _ampm.SelectedIndex = 0;
            hourGroup.Children.Add(_hh);
            hourGroup.Children.Add(MakeColon());
            hourGroup.Children.Add(_hm);
            hourGroup.Children.Add(MakeColon());
            hourGroup.Children.Add(_hs);
            hourGroup.Children.Add(_ampm);
            Grid.SetColumn(hourGroup, 1); row.Children.Add(hourGroup);

            _btnActivarH = NY930Theme.ActionButton("ACTIVAR");
            _btnActivarH.Click += (s, e) => ActivarHorario();
            Grid.SetColumn(_btnActivarH, 2); row.Children.Add(_btnActivarH);

            stack.Children.Add(row);
            return stack;
        }

        private StackPanel BuildScheduleCountdown()
        {
            var stack = new StackPanel();
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            inner.Children.Add(new TextBlock
            {
                Text = "TIEMPO RESTANTE", FontSize = 9,
                Foreground = NY930Theme.Text2Brush,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            _countdownTb = new TextBlock
            {
                Text = "00:00:00", FontFamily = NY930Theme.MonoFont,
                FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.GoldLightBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            inner.Children.Add(_countdownTb);
            Grid.SetColumn(inner, 0); row.Children.Add(inner);

            _btnStopH = NY930Theme.ActionButton("STOP", danger: true);
            _btnStopH.Click += (s, e) => DesactivarHorario();
            Grid.SetColumn(_btnStopH, 1); row.Children.Add(_btnStopH);

            stack.Children.Add(row);
            return stack;
        }

        private StackPanel BuildPrecioForm()
        {
            var stack = new StackPanel();
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lbl = NY930Theme.FieldLabel("Precio");
            Grid.SetColumn(lbl, 0); row.Children.Add(lbl);

            _priceInput = NY930Theme.FInput("28000.00", 80);
            _priceInput.HorizontalAlignment = HorizontalAlignment.Center;
            Grid.SetColumn(_priceInput, 1); row.Children.Add(_priceInput);

            _btnActivarP = NY930Theme.ActionButton("ACTIVAR");
            _btnActivarP.Click += (s, e) => ActivarPrecio();
            Grid.SetColumn(_btnActivarP, 2); row.Children.Add(_btnActivarP);

            stack.Children.Add(row);
            return stack;
        }

        private StackPanel BuildPrecioActive()
        {
            var stack = new StackPanel();
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var lbl = NY930Theme.FieldLabel("Precio");
            Grid.SetColumn(lbl, 0); row.Children.Add(lbl);

            _precioActiveTb = new TextBlock
            {
                Text = "—", FontFamily = NY930Theme.MonoFont,
                FontSize = 14, FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.GoldLightBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_precioActiveTb, 1); row.Children.Add(_precioActiveTb);

            _btnStopP = NY930Theme.ActionButton("STOP", danger: true);
            _btnStopP.Click += (s, e) => DesactivarPrecio();
            Grid.SetColumn(_btnStopP, 2); row.Children.Add(_btnStopP);

            stack.Children.Add(row);
            return stack;
        }

        private TextBlock MakeColon()
        {
            return new TextBlock
            {
                Text = ":", FontFamily = NY930Theme.MonoFont, FontSize = 12,
                FontWeight = FontWeights.Bold, Foreground = NY930Theme.Text3Brush,
                Margin = new Thickness(2, 0, 2, 0), VerticalAlignment = VerticalAlignment.Center
            };
        }

        // ── Sub-tabs Estándar | Avanzada ───────────────────────
        private FrameworkElement BuildSubTabs()
        {
            var border = new Border
            {
                BorderBrush = NY930Theme.Border2Brush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin = new Thickness(9, 4, 9, 0)
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal };
            _stabEst = new NY930Theme.TabButton("Estándar", true)  { Padding = new Thickness(12, 5, 12, 5) };
            _stabAvz = new NY930Theme.TabButton("Avanzada", false) { Padding = new Thickness(12, 5, 12, 5) };
            _stabEst.Click += (s, e) => { _stabEst.SetActive(true); _stabAvz.SetActive(false); _panelEst.Visibility = Visibility.Visible; _panelAvz.Visibility = Visibility.Collapsed; };
            _stabAvz.Click += (s, e) => { _stabEst.SetActive(false); _stabAvz.SetActive(true); _panelEst.Visibility = Visibility.Collapsed; _panelAvz.Visibility = Visibility.Visible; };
            stack.Children.Add(_stabEst);
            stack.Children.Add(_stabAvz);
            border.Child = stack;
            return border;
        }

        private FrameworkElement BuildSubPanels()
        {
            var wrap = new StackPanel { Margin = new Thickness(9, 9, 9, 0) };
            _panelEst = BuildEstandar();
            _panelAvz = BuildAvanzada();
            _panelAvz.Visibility = Visibility.Collapsed;
            wrap.Children.Add(_panelEst);
            wrap.Children.Add(_panelAvz);
            return wrap;
        }

        private StackPanel BuildEstandar()
        {
            var s = new StackPanel();
            _cfgQty = NY930Theme.FInput("15");
            _cfgSl  = NY930Theme.FInput("90");
            _cfgTp  = NY930Theme.FInput("61");
            s.Children.Add(NY930Theme.FieldRow("Contratos", _cfgQty));
            s.Children.Add(NY930Theme.FieldRow("Stop Loss (ticks)", _cfgSl));
            s.Children.Add(NY930Theme.FieldRow("Take Profit (ticks)", _cfgTp));
            return s;
        }

        private StackPanel BuildAvanzada()
        {
            var s = new StackPanel();

            _accBe = new NY930Theme.Accordion("1. Breakeven", false, withTopBorder: false);
            _beTrigger = NY930Theme.FInput("30");
            _beOffset  = NY930Theme.FInput("5");
            _accBe.Body.Children.Add(BuildTwoFieldRow("Ticks activar", _beTrigger, "Offset SL", _beOffset));
            s.Children.Add(_accBe);

            _accTs = new NY930Theme.Accordion("2. Trailing Stop", false);
            _tsTrigger = NY930Theme.FInput("35");
            _tsStep    = NY930Theme.FInput("5");
            _accTs.Body.Children.Add(BuildTwoFieldRow("Ticks activar", _tsTrigger, "Escalón", _tsStep));
            s.Children.Add(_accTs);

            _accPar = new NY930Theme.Accordion("3. Parciales", false);
            _p1Ticks = NY930Theme.FInput("30");
            _p1Qty   = NY930Theme.FInput("5");
            _p2Ticks = NY930Theme.FInput("50");
            _p2Qty   = NY930Theme.FInput("5");
            _accPar.Body.Children.Add(NY930Theme.AccordionSubLabel("P1"));
            _accPar.Body.Children.Add(BuildTwoFieldRow("Ticks", _p1Ticks, "Contratos", _p1Qty));
            _accPar.Body.Children.Add(NY930Theme.AccordionSubLabel("P2"));
            _accPar.Body.Children.Add(BuildTwoFieldRow("Ticks", _p2Ticks, "Contratos", _p2Qty));
            s.Children.Add(_accPar);

            _accGg = new NY930Theme.Accordion("4. Gap Guards", true);
            _slGgTicks = NY930Theme.FInput("5");
            _slGgSecs  = NY930Theme.FInput("1");
            _accGg.Body.Children.Add(NY930Theme.AccordionSubLabel("4.1 SL Guard"));
            _accGg.Body.Children.Add(BuildTwoFieldRow("Ticks", _slGgTicks, "Segundos", _slGgSecs));

            var modeHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 4) };
            modeHead.Children.Add(new TextBlock
            {
                Text = "4.2", FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.GoldBrush, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            _ggTpBtn = new NY930Theme.TabButton("TP Guard", true)   { Padding = new Thickness(9, 2, 9, 2), Margin = new Thickness(0, 0, 4, 0) };
            _ggTrBtn = new NY930Theme.TabButton("Trailing TP", false) { Padding = new Thickness(9, 2, 9, 2) };
            _ggTpBtn.Click += (s2, e2) => { _ggTpBtn.SetActive(true); _ggTrBtn.SetActive(false); _ggTpFields.Visibility = Visibility.Visible; _ggTrFields.Visibility = Visibility.Collapsed; };
            _ggTrBtn.Click += (s2, e2) => { _ggTpBtn.SetActive(false); _ggTrBtn.SetActive(true); _ggTpFields.Visibility = Visibility.Collapsed; _ggTrFields.Visibility = Visibility.Visible; };
            modeHead.Children.Add(_ggTpBtn);
            modeHead.Children.Add(_ggTrBtn);
            _accGg.Body.Children.Add(modeHead);

            _ggTpFields = new StackPanel();
            _tpGgTicks = NY930Theme.FInput("5");
            _tpGgSecs  = NY930Theme.FInput("1");
            _ggTpFields.Children.Add(BuildTwoFieldRow("Ticks", _tpGgTicks, "Segundos", _tpGgSecs));
            _accGg.Body.Children.Add(_ggTpFields);

            _ggTrFields = new StackPanel { Visibility = Visibility.Collapsed };
            _trDist    = NY930Theme.FInput("10");
            _trTimeout = NY930Theme.FInput("2");
            _ggTrFields.Children.Add(BuildTwoFieldRow("Distancia", _trDist, "Timeout(s)", _trTimeout));
            _accGg.Body.Children.Add(_ggTrFields);
            s.Children.Add(_accGg);

            _accSt = new NY930Theme.Accordion("5. Salida por Tiempo", false);
            _stDuration = NY930Theme.FInput("10");
            _stMode = NY930Theme.FSelect(95);
            _stMode.Items.Add("Siempre");
            _stMode.Items.Add("Si +");
            _stMode.Items.Add("Poner TP");
            _stMode.SelectedIndex = 2;
            _accSt.Body.Children.Add(BuildTwoFieldRow("Duración (seg)", _stDuration, "Modo", _stMode));
            var beyondRow = new Grid { Margin = new Thickness(0, 2, 0, 0) };
            beyondRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            beyondRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            _stBeyondTpTog = new NY930Theme.ToggleSwitch(true);
            Grid.SetColumn(_stBeyondTpTog, 0); beyondRow.Children.Add(_stBeyondTpTog);
            var beyondLbl = new TextBlock
            {
                Text = "Cerrar si superó TP", FontSize = 10,
                Foreground = NY930Theme.Text2Brush, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(beyondLbl, 1); beyondRow.Children.Add(beyondLbl);
            _accSt.Body.Children.Add(beyondRow);
            s.Children.Add(_accSt);
            return s;
        }

        private FrameworkElement BuildTwoFieldRow(string lbl1, FrameworkElement input1, string lbl2, FrameworkElement input2)
        {
            var s = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            s.Children.Add(new TextBlock { Text = lbl1, FontSize = 10, Foreground = NY930Theme.Text2Brush,
                                            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            input1.Margin = new Thickness(0, 0, 12, 0);
            s.Children.Add(input1);
            s.Children.Add(new TextBlock { Text = lbl2, FontSize = 10, Foreground = NY930Theme.Text2Brush,
                                            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) });
            s.Children.Add(input2);
            return s;
        }

        // ── Apply / Cambiar ───────────────────────────────────
        private FrameworkElement BuildApplyArea()
        {
            var stack = new StackPanel { Margin = new Thickness(9, 10, 9, 14) };
            _btnApply = NY930Theme.ApplyButton("▶ Aplicar cambios");
            _btnApply.Click += (s, e) => { ApplyParametersToStrategy(); FlashApply(); };
            _btnChangeStrat = NY930Theme.ApplyButton("↩ Cambiar de estrategia");
            _btnChangeStrat.Margin = new Thickness(0, 5, 0, 0);
            _btnChangeStrat.Click += (s, e) => _shell.Show(new NY930HomeView(_shell));
            stack.Children.Add(_btnApply);
            stack.Children.Add(_btnChangeStrat);
            return stack;
        }

        private void FlashApply()
        {
            var orig = _btnApply.Content;
            _btnApply.Content = "✓ Aplicado";
            _btnApply.Background = NY930Theme.GoldBrush;
            _btnApply.Foreground = Brushes.Black;
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            t.Tick += (s, e) =>
            {
                _btnApply.Content = orig;
                _btnApply.Background = Brushes.Transparent;
                _btnApply.Foreground = NY930Theme.GoldBrush;
                t.Stop();
            };
            t.Start();
        }

        // ── Schedule activate/deactivate ──────────────────────
        private void ActivarHorario()
        {
            ApplyParametersToStrategy();

            int hh = ParseInt(_hh.Text) ?? 9;
            int mm = ParseInt(_hm.Text) ?? 29;
            int ss = ParseInt(_hs.Text) ?? 58;
            string ap = _ampm.SelectedItem != null ? _ampm.SelectedItem.ToString() : "AM";
            int hour24 = hh % 12;
            if (string.Equals(ap, "PM", StringComparison.OrdinalIgnoreCase)) hour24 += 12;

            var now = DateTime.Now;
            var t = new DateTime(now.Year, now.Month, now.Day, hour24, mm, ss);
            if (t <= now) t = t.AddDays(1);
            _scheduledEntry = t;
            _scheduleArmed = true;

            _scheduleForm.Visibility      = Visibility.Collapsed;
            _scheduleCountdown.Visibility = Visibility.Visible;
            RefreshCountdown();
        }

        private void DesactivarHorario()
        {
            _scheduleArmed = false;
            _scheduleForm.Visibility = Visibility.Visible;
            _scheduleCountdown.Visibility = Visibility.Collapsed;
            _countdownTb.Text = "00:00:00";
            _countdownTb.Foreground = NY930Theme.GoldLightBrush;
        }

        private void RefreshCountdown()
        {
            if (!_scheduleArmed || _countdownTb == null) return;
            DateTime now = DateTime.Now;
            if (now >= _scheduledEntry)
            {
                _countdownTb.Text = "00:00:00";
                _countdownTb.Foreground = NY930Theme.GoldLightBrush;
                return;
            }
            var rem = _scheduledEntry - now;
            _countdownTb.Text = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)rem.TotalHours, rem.Minutes, rem.Seconds);
            _countdownTb.Foreground = rem.TotalSeconds <= 60
                ? NY930Theme.RedBrush : NY930Theme.GoldLightBrush;
        }

        // ── Precio activate / deactivate ──────────────────────
        private void ActivarPrecio()
        {
            ApplyParametersToStrategy();
            double price;
            if (!double.TryParse(_priceInput.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out price)
                || price <= 0)
            {
                FlashError(_priceInput);
                return;
            }

            _precioActiveTb.Text = price.ToString("N2", CultureInfo.CurrentCulture);
            _precioForm.Visibility   = Visibility.Collapsed;
            _precioActive.Visibility = Visibility.Visible;

            // Tell the strategy to start price-trigger monitoring.
            // (Wired in Step 9 — strategy handles this action.)
            NY930Bridge.RequestHedgeAction(new NY930Action
            {
                Type   = NY930ActionType.HedgeApplyParameters,
                Parameters = new NY930Parameters
                {
                    // EntryPrice is handled by Step 9 (new field).
                    // For now we send an immediate Buy/Sell only when
                    // the strategy implements price-trigger mode.
                }
            });
        }

        private void DesactivarPrecio()
        {
            _precioActive.Visibility = Visibility.Collapsed;
            _precioForm.Visibility   = Visibility.Visible;
        }

        private void FlashError(Control c)
        {
            var orig = c.BorderBrush;
            c.BorderBrush = NY930Theme.RedBrush;
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            t.Tick += (s, e) => { c.BorderBrush = orig; t.Stop(); };
            t.Start();
        }

        // ── Manual entry ──────────────────────────────────────
        private void DoManualEntry()
        {
            ApplyParametersToStrategy();
            if (_selectedSide == "buy")
                NY930Bridge.RequestHedgeAction(new NY930Action { Type = NY930ActionType.HedgeBuyNow });
            else if (_selectedSide == "sell")
                NY930Bridge.RequestHedgeAction(new NY930Action { Type = NY930ActionType.HedgeSellNow });
            else
                FlashError(_btnBuy);
        }

        // ── Apply parameters to strategy ──────────────────────
        private void ApplyParametersToStrategy()
        {
            int hh = ParseInt(_hh.Text) ?? 9;
            int mm = ParseInt(_hm.Text) ?? 29;
            int ss = ParseInt(_hs.Text) ?? 58;
            string ap = _ampm.SelectedItem != null ? _ampm.SelectedItem.ToString() : "AM";
            int hour24 = hh % 12;
            if (string.Equals(ap, "PM", StringComparison.OrdinalIgnoreCase)) hour24 += 12;

            string direction = _selectedSide == "buy"  ? "Long"
                              : _selectedSide == "sell" ? "Short" : null;

            var p = new NY930Parameters
            {
                EntryHour   = hour24,
                EntryMinute = mm,
                EntrySecond = ss,
                Quantity    = ParseInt(_cfgQty.Text),
                StopLossTicks   = ParseInt(_cfgSl.Text),
                TakeProfitTicks = ParseInt(_cfgTp.Text),
                Direction   = direction,

                EnableBreakeven  = _accBe.IsOn,
                EnableTrailing   = _accTs.IsOn,
                EnablePartials   = _accPar.IsOn,
                EnableTimeExit   = _accSt.IsOn,
                EnableTpGapGuard = _accGg.IsOn,
                EnableSlGapGuard = _accGg.IsOn,
                TpGapGuardTicks  = ParseInt(_tpGgTicks.Text),
                SlGapGuardTicks  = ParseInt(_slGgTicks.Text)
            };

            NY930Bridge.RequestHedgeAction(new NY930Action
            {
                Type       = NY930ActionType.HedgeApplyParameters,
                Parameters = p
            });
        }

        // ── Snapshot binding ──────────────────────────────────
        private void OnSnapshot(NY930HedgeSnapshot s) => Dispatcher.InvokeAsync(() => ApplySnapshotToInputs(s));

        private void ApplySnapshotToInputs(NY930HedgeSnapshot s)
        {
            if (s == null) return;
            if (s.EntryTime != default(DateTime) && _hh != null && string.IsNullOrEmpty(_hh.Text))
            {
                int h12 = s.EntryTime.Hour % 12;
                if (h12 == 0) h12 = 12;
                _hh.Text = h12.ToString();
                _hm.Text = s.EntryTime.Minute.ToString();
                _hs.Text = s.EntryTime.Second.ToString();
                _ampm.SelectedIndex = s.EntryTime.Hour >= 12 ? 1 : 0;
            }
            if (s.Quantity > 0 && string.IsNullOrEmpty(_cfgQty.Text))
                _cfgQty.Text = s.Quantity.ToString();
            if (s.StopLossTicks > 0 && string.IsNullOrEmpty(_cfgSl.Text))
                _cfgSl.Text = s.StopLossTicks.ToString();
            if (s.TakeProfitTicks > 0 && string.IsNullOrEmpty(_cfgTp.Text))
                _cfgTp.Text = s.TakeProfitTicks.ToString();

            if (s.Direction == "Long" && _selectedSide != "buy")  SelectSide("buy");
            if (s.Direction == "Short" && _selectedSide != "sell") SelectSide("sell");

            // Auto-route to in-trade view on fill.
            if (s.InPosition && _shell.CurrentViewIs<NY930HedgeView>())
                _shell.Show(new NY930ProgressView(_shell, isOpenRange: false));
            if (s.LastResult != null && !s.InPosition && s.SessionDone
                && _shell.CurrentViewIs<NY930HedgeView>())
                _shell.Show(new NY930ResultView(_shell, s.LastResult));
        }

        private static int? ParseInt(string text)
        {
            int v;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                return v;
            return null;
        }

        public void RefreshLocalization() { /* labels are Spanish */ }

        public void Dispose()
        {
            NY930Bridge.HedgeChanged -= OnSnapshot;
            if (_ticker != null) { _ticker.Stop(); _ticker = null; }
        }
    }
}
