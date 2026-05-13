// ============================================================
//  NY930OpenRangeView — v1.3 pixel-match
//  Reference: ny930-openrange-panel.html
// ------------------------------------------------------------
//  This is the SETUP / PARAMETER view shown before the trade
//  fires. After ACTIVAR the schedule, a countdown takes over.
//  Once the strategy fires, the shell routes to NY930ProgressView.
//
//  Layout:
//    .header            NY930 logo
//    .strategy-label    OPEN RANGE
//    .account-wrap      Cuenta dropdown
//    .tabs              Horario | Manual
//    Horario panel:
//      Hora inputs HH:MM:SS AM/PM + ACTIVAR
//      (after activate: TIEMPO RESTANTE countdown + STOP)
//    Manual panel:
//      ACTIVAR ÓRDENES button
//    Divider + Configuración label
//    Sub-tabs Estándar | Avanzada
//      Estándar:
//        Contratos input
//        Buy Stop accordion (Ticks, SL, TP)
//        Sell Stop accordion (Ticks, SL, TP)
//        Single-Stop Reverse nested in the active side when solo
//      Avanzada (5 accordions):
//        1. Breakeven        (Ticks activar, Offset SL)
//        2. Trailing Stop    (Ticks activar, Escalón)
//        3. Parciales        (P1 / P2 ticks + qty)
//        4. Gap Guards       (SL Guard + TP Guard / Trailing TP)
//        5. Salida por Tiempo (Duración + Modo + Cerrar si superó TP)
//    Apply: "▶ Aplicar cambios" + "↩ Cambiar de estrategia"
// ============================================================

#region Using declarations
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public sealed class NY930OpenRangeView : Grid, INY930Localizable, IDisposable
    {
        private readonly NY930ShellView _shell;

        // Tab state
        private NY930Theme.TabButton _tabHorario, _tabManual;
        private StackPanel           _panelHorario, _panelManual;

        // Sub-tab state
        private NY930Theme.TabButton _stabEst, _stabAvz;
        private StackPanel           _panelEst,  _panelAvz;

        // Schedule fields
        private TextBox _hh, _hm, _hs;
        private ComboBox _ampm;
        private Button   _btnActivar;
        private StackPanel _scheduleForm, _scheduleCountdown;
        private TextBlock _countdownTb;
        private Button   _btnStop;

        // Manual button
        private Button _btnManualActivate;

        // Estándar fields
        private TextBox _cfgQty;
        private NY930Theme.Accordion _accLong, _accShort;
        private TextBox _longTicks, _longSl, _longTp;
        private TextBox _shortTicks, _shortSl, _shortTp;

        // Single-Stop Reverse (nested) — created on the fly inside
        // whichever side is solo.
        private NY930Theme.ToggleSwitch _ssrToggle;
        private TextBox  _ssrTicks;
        private StackPanel _ssrLongSlot, _ssrShortSlot;

        // Avanzada fields
        private NY930Theme.Accordion _accBe, _accTs, _accPar, _accGg, _accSt;
        private TextBox _beTrigger, _beOffset;
        private TextBox _tsTrigger, _tsStep;
        private TextBox _p1Ticks, _p1Qty, _p2Ticks, _p2Qty;
        // Gap Guards
        private TextBox _slGgTicks, _slGgSecs;
        private NY930Theme.TabButton _ggTpBtn, _ggTrBtn;
        private StackPanel _ggTpFields, _ggTrFields;
        private TextBox _tpGgTicks, _tpGgSecs;
        private TextBox _trDist, _trTimeout;
        // Tracks 4.2 mode explicitly so we don't have to inspect
        // the TabButton's Background brush to know which sub-mode
        // is active. true = TP Guard, false = Trailing TP.
        private bool _ggTpMode = true;
        // Salida por Tiempo
        private TextBox _stDuration;
        private ComboBox _stMode;
        private NY930Theme.ToggleSwitch _stBeyondTpTog;

        // Apply / change strategy
        private Button _btnApply, _btnChangeStrat;

        // Countdown ticker
        private System.Windows.Threading.DispatcherTimer _ticker;
        private DateTime _scheduledEntry;
        private bool _scheduleArmed;

        // Tracks whether we've done the one-shot restoration of
        // accordion / toggle / mode-pill state from the strategy
        // snapshot. After the first snapshot we DON'T overwrite
        // these controls anymore — otherwise the high-frequency
        // snapshot stream would snap accordions closed every
        // time the user expands one before clicking "Aplicar".
        private bool _snapshotInitialized;

        public NY930OpenRangeView(NY930ShellView shell)
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

            var root = new StackPanel { MaxWidth = NY930Theme.PanelWidth };
            scroll.Content = root;

            root.Children.Add(BuildHeader());
            root.Children.Add(NY930Theme.StrategyLabel("OPEN RANGE"));
            root.Children.Add(BuildAccountRow());
            root.Children.Add(BuildTabs());
            root.Children.Add(BuildTabPanels());
            root.Children.Add(NY930Theme.Divider());
            root.Children.Add(BuildConfigHeader());
            root.Children.Add(BuildSubTabs());
            root.Children.Add(BuildSubPanels());
            root.Children.Add(BuildApplyArea());

            // Bridge
            NY930Bridge.OpenRangeChanged += OnSnapshot;
            var current = NY930Bridge.GetOpenRange();
            if (current != null) ApplySnapshotToInputs(current);

            // Ticker for countdown UI
            _ticker = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _ticker.Tick += (s, e) => RefreshCountdown();
            _ticker.Start();

            UpdateSsrSlots();
        }

        // ── Header ─────────────────────────────────────────────
        private FrameworkElement BuildHeader()
        {
            var h = new NY930Theme.PanelHeader();
            return h;
        }

        // ── Cuenta dropdown row ────────────────────────────────
        private FrameworkElement BuildAccountRow()
        {
            var grid = new Grid { Margin = new Thickness(9, 6, 9, 0) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var lbl = new TextBlock
            {
                Text       = "CUENTA",
                FontSize   = 9,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.Text3Brush,
                VerticalAlignment = VerticalAlignment.Center
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

        // ── Top tabs: Horario | Manual ─────────────────────────
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

            _tabHorario = new NY930Theme.TabButton("Horario", true);
            _tabManual  = new NY930Theme.TabButton("Manual",  false);
            _tabHorario.Click += (s, e) => SetActiveTab(true);
            _tabManual.Click  += (s, e) => SetActiveTab(false);

            Grid.SetColumn(_tabHorario, 0);
            Grid.SetColumn(_tabManual,  1);
            grid.Children.Add(_tabHorario);
            grid.Children.Add(_tabManual);
            border.Child = grid;
            return border;
        }

        private void SetActiveTab(bool horario)
        {
            _tabHorario.SetActive(horario);
            _tabManual.SetActive(!horario);
            _panelHorario.Visibility = horario ? Visibility.Visible : Visibility.Collapsed;
            _panelManual.Visibility  = horario ? Visibility.Collapsed : Visibility.Visible;
        }

        // ── Tab panels ─────────────────────────────────────────
        private FrameworkElement BuildTabPanels()
        {
            var wrap = new StackPanel { Margin = new Thickness(9, 9, 9, 0) };

            // Horario panel (form OR countdown)
            _panelHorario = new StackPanel();
            _scheduleForm = BuildScheduleForm();
            _scheduleCountdown = BuildCountdown();
            _scheduleCountdown.Visibility = Visibility.Collapsed;
            _panelHorario.Children.Add(_scheduleForm);
            _panelHorario.Children.Add(_scheduleCountdown);
            wrap.Children.Add(_panelHorario);

            // Manual panel
            _panelManual = new StackPanel { Visibility = Visibility.Collapsed };
            _btnManualActivate = NY930Theme.ActionButton("ACTIVAR ÓRDENES");
            _btnManualActivate.Padding = new Thickness(0, 6, 0, 6);
            _btnManualActivate.HorizontalAlignment = HorizontalAlignment.Stretch;
            _btnManualActivate.Click += (s, e) =>
            {
                if (!ValidateInputs()) return;
                if (!RequireOpenRangeAttached()) return;
                ApplyParametersToStrategy();
                NY930Bridge.RequestOpenRangeAction(new NY930Action { Type = NY930ActionType.OpenRangeBuyNow });
            };
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
            Grid.SetColumn(lbl, 0);
            row.Children.Add(lbl);

            var hourGroup = new StackPanel { Orientation = Orientation.Horizontal,
                                              HorizontalAlignment = HorizontalAlignment.Center,
                                              VerticalAlignment   = VerticalAlignment.Center };
            // Inputs sized so the whole row (label + hh:mm:ss + AM/PM
            // + ACTIVAR) fits inside the 250px panel without clipping.
            // Earlier 36px values pushed the row past the right edge,
            // hiding the hour digit.
            _hh   = NY930Theme.FInput("9",  30);
            _hm   = NY930Theme.FInput("29", 30);
            _hs   = NY930Theme.FInput("58", 30);
            _ampm = NY930Theme.FSelect(40);
            _ampm.Margin = new Thickness(4, 0, 0, 0);
            _ampm.Items.Add("AM");
            _ampm.Items.Add("PM");
            _ampm.SelectedIndex = 0;
            hourGroup.Children.Add(_hh);
            hourGroup.Children.Add(MakeColon());
            hourGroup.Children.Add(_hm);
            hourGroup.Children.Add(MakeColon());
            hourGroup.Children.Add(_hs);
            hourGroup.Children.Add(_ampm);
            Grid.SetColumn(hourGroup, 1);
            row.Children.Add(hourGroup);

            _btnActivar = NY930Theme.ActionButton("ACTIVAR");
            _btnActivar.Padding = new Thickness(6, 3, 6, 3); // compact
            _btnActivar.Click += (s, e) => ActivarHorario();
            Grid.SetColumn(_btnActivar, 2);
            row.Children.Add(_btnActivar);

            stack.Children.Add(row);
            return stack;
        }

        private StackPanel BuildCountdown()
        {
            var stack = new StackPanel();
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var inner = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            inner.Children.Add(new TextBlock
            {
                Text       = "TIEMPO RESTANTE",
                FontSize   = 9,
                Foreground = NY930Theme.Text2Brush,
                HorizontalAlignment = HorizontalAlignment.Center
            });
            _countdownTb = new TextBlock
            {
                Text       = "00:00:00",
                FontFamily = NY930Theme.MonoFont,
                FontSize   = 14,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.GoldLightBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            inner.Children.Add(_countdownTb);
            Grid.SetColumn(inner, 0);
            row.Children.Add(inner);

            _btnStop = NY930Theme.ActionButton("STOP", danger: true);
            _btnStop.Click += (s, e) => DesactivarHorario();
            Grid.SetColumn(_btnStop, 1);
            row.Children.Add(_btnStop);

            stack.Children.Add(row);
            return stack;
        }

        private TextBlock MakeColon()
        {
            return new TextBlock
            {
                Text       = ":",
                FontFamily = NY930Theme.MonoFont,
                FontSize   = 12,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.Text3Brush,
                Margin     = new Thickness(2, 0, 2, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        // ── Configuración header / sub-tabs ────────────────────
        private FrameworkElement BuildConfigHeader()
        {
            var lbl = new TextBlock
            {
                Text       = "CONFIGURACIÓN",
                FontSize   = 9,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.Text3Brush,
                Margin     = new Thickness(9, 6, 9, 0)
            };
            return lbl;
        }

        private FrameworkElement BuildSubTabs()
        {
            var border = new Border
            {
                BorderBrush     = NY930Theme.Border2Brush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Margin          = new Thickness(9, 4, 9, 0)
            };
            var stack = new StackPanel { Orientation = Orientation.Horizontal };

            _stabEst = new NY930Theme.TabButton("Estándar", true);
            _stabEst.Padding = new Thickness(12, 5, 12, 5);
            _stabEst.HorizontalContentAlignment = HorizontalAlignment.Left;
            _stabEst.Click += (s, e) => SetActiveSubTab(true);

            _stabAvz = new NY930Theme.TabButton("Avanzada", false);
            _stabAvz.Padding = new Thickness(12, 5, 12, 5);
            _stabAvz.HorizontalContentAlignment = HorizontalAlignment.Left;
            _stabAvz.Click += (s, e) => SetActiveSubTab(false);

            stack.Children.Add(_stabEst);
            stack.Children.Add(_stabAvz);
            border.Child = stack;
            return border;
        }

        private void SetActiveSubTab(bool est)
        {
            _stabEst.SetActive(est);
            _stabAvz.SetActive(!est);
            _panelEst.Visibility = est ? Visibility.Visible : Visibility.Collapsed;
            _panelAvz.Visibility = est ? Visibility.Collapsed : Visibility.Visible;
        }

        // ── Sub-panels: Estándar + Avanzada ────────────────────
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
            s.Children.Add(NY930Theme.FieldRow("Contratos", _cfgQty));

            // Buy Stop accordion (default ON, green name)
            _accLong = new NY930Theme.Accordion("Buy Stop", initialOn: true,
                nameColor: NY930Theme.GreenBrush, withTopBorder: false);
            _longTicks = NY930Theme.FInput("40");
            _longSl    = NY930Theme.FInput("90");
            _longTp    = NY930Theme.FInput("61");
            _accLong.Body.Children.Add(NY930Theme.FieldRow("Ticks sobre precio", _longTicks));
            _accLong.Body.Children.Add(NY930Theme.FieldRow("Stop Loss",          _longSl));
            _accLong.Body.Children.Add(NY930Theme.FieldRow("Take Profit",        _longTp));
            _ssrLongSlot = new StackPanel();
            _accLong.Body.Children.Add(_ssrLongSlot);
            _accLong.Toggle.Toggled += _ => UpdateSsrSlots();
            s.Children.Add(_accLong);

            // Sell Stop accordion (default ON, red name)
            _accShort = new NY930Theme.Accordion("Sell Stop", initialOn: true,
                nameColor: NY930Theme.RedBrush);
            _shortTicks = NY930Theme.FInput("40");
            _shortSl    = NY930Theme.FInput("90");
            _shortTp    = NY930Theme.FInput("61");
            _accShort.Body.Children.Add(NY930Theme.FieldRow("Ticks bajo precio", _shortTicks));
            _accShort.Body.Children.Add(NY930Theme.FieldRow("Stop Loss",         _shortSl));
            _accShort.Body.Children.Add(NY930Theme.FieldRow("Take Profit",       _shortTp));
            _ssrShortSlot = new StackPanel();
            _accShort.Body.Children.Add(_ssrShortSlot);
            _accShort.Toggle.Toggled += _ => UpdateSsrSlots();
            s.Children.Add(_accShort);

            // Single-Stop Reverse — created lazily, mounted into
            // whichever side is solo.
            _ssrToggle = new NY930Theme.ToggleSwitch();
            // Default to 0 = "auto" (use the selected stop ticks). The
            // strategy interprets 0 as auto-fallback to TicksLong /
            // TicksShort per video 7. User can override with any
            // positive value.
            _ssrTicks  = NY930Theme.FInput("0");
            _ssrTicks.ToolTip = "0 = mismo número de ticks que la orden Stop seleccionada";

            return s;
        }

        // SSR appears only when exactly one side (Long XOR Short)
        // is enabled. Mounts into that side's accordion body.
        //
        // _ssrToggle / _ssrTicks are class-level fields so the user's
        // configured state survives re-parenting. Before re-adding
        // them we explicitly detach from any previous parent to avoid
        // WPF's "Specified element is already the logical child of
        // another element" exception (would crash the panel when the
        // user toggled the active side back and forth).
        private void UpdateSsrSlots()
        {
            if (_ssrLongSlot == null || _ssrShortSlot == null) return;

            bool longOn  = _accLong.IsOn;
            bool shortOn = _accShort.IsOn;

            DetachFromParent(_ssrToggle);
            DetachFromParent(_ssrTicks);
            _ssrLongSlot.Children.Clear();
            _ssrShortSlot.Children.Clear();

            if (longOn ^ shortOn) // exactly one
            {
                var slot = longOn ? _ssrLongSlot : _ssrShortSlot;

                var sep = new Border
                {
                    BorderBrush     = NY930Theme.BorderBrush,
                    BorderThickness = new Thickness(0, 1, 0, 0),
                    Margin          = new Thickness(0, 4, 0, 4)
                };
                slot.Children.Add(sep);

                var head = new Grid();
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var lbl = new TextBlock
                {
                    Text       = "Single-Stop Reverse",
                    FontSize   = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = NY930Theme.GoldBrush,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(lbl, 0);
                head.Children.Add(lbl);
                Grid.SetColumn(_ssrToggle, 1);
                head.Children.Add(_ssrToggle);
                slot.Children.Add(head);

                var ticksRow = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Margin      = new Thickness(0, 4, 0, 0)
                };
                ticksRow.Children.Add(new TextBlock
                {
                    Text       = "Ticks en contra",
                    FontSize   = 10,
                    Foreground = NY930Theme.Text2Brush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin     = new Thickness(0, 0, 6, 0)
                });
                ticksRow.Children.Add(_ssrTicks);
                ticksRow.Children.Add(new TextBlock
                {
                    Text       = " (0 = auto)",
                    FontSize   = 9,
                    Foreground = NY930Theme.Text3Brush,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin     = new Thickness(4, 0, 0, 0)
                });
                slot.Children.Add(ticksRow);
            }
        }

        // Pull a UIElement out of its current Panel/Decorator parent
        // so it can be re-added elsewhere without WPF complaining.
        private static void DetachFromParent(UIElement el)
        {
            if (el == null) return;
            var parent = System.Windows.Media.VisualTreeHelper.GetParent(el)
                       ?? System.Windows.LogicalTreeHelper.GetParent(el);
            var panel = parent as Panel;
            if (panel != null) { panel.Children.Remove(el); return; }
            var decorator = parent as Decorator;
            if (decorator != null) { decorator.Child = null; return; }
            var border = parent as Border;
            if (border != null) { border.Child = null; }
        }

        private StackPanel BuildAvanzada()
        {
            var s = new StackPanel();

            // 1. Breakeven
            _accBe = new NY930Theme.Accordion("1. Breakeven", false, withTopBorder: false);
            _beTrigger = NY930Theme.FInput("30");
            _beOffset  = NY930Theme.FInput("5");
            _accBe.Body.Children.Add(BuildTwoFieldRow("Ticks activar", _beTrigger, "Offset SL", _beOffset));
            s.Children.Add(_accBe);

            // 2. Trailing Stop
            _accTs = new NY930Theme.Accordion("2. Trailing Stop", false);
            _tsTrigger = NY930Theme.FInput("35");
            _tsStep    = NY930Theme.FInput("5");
            _accTs.Body.Children.Add(BuildTwoFieldRow("Ticks activar", _tsTrigger, "Escalón", _tsStep));
            s.Children.Add(_accTs);

            // 3. Parciales
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

            // 4. Gap Guards (default ON)
            _accGg = new NY930Theme.Accordion("4. Gap Guards", true);
            _slGgTicks = NY930Theme.FInput("5");
            _slGgSecs  = NY930Theme.FInput("1");
            _accGg.Body.Children.Add(NY930Theme.AccordionSubLabel("4.1 SL Guard"));
            _accGg.Body.Children.Add(BuildTwoFieldRow("Ticks", _slGgTicks, "Segundos", _slGgSecs));

            // 4.2 mode pills
            var modeHead = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 4) };
            modeHead.Children.Add(new TextBlock
            {
                Text = "4.2",
                FontSize = 9, FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.GoldBrush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            _ggTpBtn = new NY930Theme.TabButton("TP Guard", true)   { Padding = new Thickness(9, 2, 9, 2), Margin = new Thickness(0, 0, 4, 0) };
            _ggTrBtn = new NY930Theme.TabButton("Trailing TP", false) { Padding = new Thickness(9, 2, 9, 2) };
            _ggTpBtn.Click += (s2, e2) => SetGgMode(true);
            _ggTrBtn.Click += (s2, e2) => SetGgMode(false);
            modeHead.Children.Add(_ggTpBtn);
            modeHead.Children.Add(_ggTrBtn);
            _accGg.Body.Children.Add(modeHead);

            _ggTpFields = new StackPanel();
            _tpGgTicks  = NY930Theme.FInput("5");
            _tpGgSecs   = NY930Theme.FInput("1");
            _ggTpFields.Children.Add(BuildTwoFieldRow("Ticks", _tpGgTicks, "Segundos", _tpGgSecs));
            _accGg.Body.Children.Add(_ggTpFields);

            _ggTrFields = new StackPanel { Visibility = Visibility.Collapsed };
            _trDist     = NY930Theme.FInput("10");
            _trTimeout  = NY930Theme.FInput("2");
            _ggTrFields.Children.Add(BuildTwoFieldRow("Distancia", _trDist, "Timeout(s)", _trTimeout));
            _accGg.Body.Children.Add(_ggTrFields);

            s.Children.Add(_accGg);

            // 5. Salida por Tiempo
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
            Grid.SetColumn(_stBeyondTpTog, 0);
            beyondRow.Children.Add(_stBeyondTpTog);
            var beyondLbl = new TextBlock
            {
                Text = "Cerrar si superó TP",
                FontSize = 10, Foreground = NY930Theme.Text2Brush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0)
            };
            Grid.SetColumn(beyondLbl, 1);
            beyondRow.Children.Add(beyondLbl);
            _accSt.Body.Children.Add(beyondRow);

            s.Children.Add(_accSt);
            return s;
        }

        private void SetGgMode(bool tpMode)
        {
            _ggTpMode = tpMode;
            _ggTpBtn.SetActive(tpMode);
            _ggTrBtn.SetActive(!tpMode);
            _ggTpFields.Visibility = tpMode ? Visibility.Visible : Visibility.Collapsed;
            _ggTrFields.Visibility = tpMode ? Visibility.Collapsed : Visibility.Visible;
        }

        private FrameworkElement BuildTwoFieldRow(string lbl1, FrameworkElement input1,
                                                   string lbl2, FrameworkElement input2)
        {
            var s = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 6)
            };
            s.Children.Add(new TextBlock
            {
                Text = lbl1, FontSize = 10,
                Foreground = NY930Theme.Text2Brush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            input1.Margin = new Thickness(0, 0, 12, 0);
            s.Children.Add(input1);
            s.Children.Add(new TextBlock
            {
                Text = lbl2, FontSize = 10,
                Foreground = NY930Theme.Text2Brush,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });
            s.Children.Add(input2);
            return s;
        }

        // ── Apply / Cambiar ────────────────────────────────────
        private FrameworkElement BuildApplyArea()
        {
            var stack = new StackPanel { Margin = new Thickness(9, 10, 9, 14) };
            _btnApply = NY930Theme.ApplyButton("▶ Aplicar cambios");
            _btnApply.Click += (s, e) =>
            {
                if (!ValidateInputs()) return;
                ApplyParametersToStrategy();
                FlashApply();
            };
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
            _btnApply.Content    = "✓ Aplicado";
            _btnApply.Background = NY930Theme.GoldBrush;
            _btnApply.Foreground = Brushes.Black;
            var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(900) };
            t.Tick += (s, e) =>
            {
                _btnApply.Content    = orig;
                _btnApply.Background = Brushes.Transparent;
                _btnApply.Foreground = NY930Theme.GoldBrush;
                t.Stop();
            };
            t.Start();
        }

        // ── Activar / Desactivar Horario ───────────────────────
        private void ActivarHorario()
        {
            if (!ValidateInputs()) return;
            if (!RequireOpenRangeAttached()) return;
            ApplyParametersToStrategy();

            int hh = ParseInt(_hh.Text) ?? 9;
            int mm = ParseInt(_hm.Text) ?? 29;
            int ss = ParseInt(_hs.Text) ?? 58;
            string ap = _ampm.SelectedItem != null ? _ampm.SelectedItem.ToString() : "AM";

            int hour24 = hh % 12;
            if (string.Equals(ap, "PM", StringComparison.OrdinalIgnoreCase)) hour24 += 12;

            var now = DateTime.Now;
            var t   = new DateTime(now.Year, now.Month, now.Day, hour24, mm, ss);
            if (t <= now) t = t.AddDays(1);
            _scheduledEntry = t;
            _scheduleArmed  = true;

            _scheduleForm.Visibility      = Visibility.Collapsed;
            _scheduleCountdown.Visibility = Visibility.Visible;
            // Per client video 8: once Horario is armed, the Manual
            // tab is no longer a valid entry path — disable it until
            // the user presses STOP or the strategy resets.
            SetManualTabEnabled(false);
            RefreshCountdown();
        }

        private void DesactivarHorario()
        {
            _scheduleArmed = false;
            _scheduleForm.Visibility      = Visibility.Visible;
            _scheduleCountdown.Visibility = Visibility.Collapsed;
            _countdownTb.Text = "00:00:00";
            _countdownTb.Foreground = NY930Theme.GoldLightBrush;
            SetManualTabEnabled(true);
        }

        private void SetManualTabEnabled(bool enabled)
        {
            if (_tabManual == null) return;
            _tabManual.IsEnabled = enabled;
            _tabManual.Opacity   = enabled ? 1.0 : 0.4;
            _tabManual.Cursor    = enabled
                ? System.Windows.Input.Cursors.Hand
                : System.Windows.Input.Cursors.Arrow;
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
            TimeSpan rem = _scheduledEntry - now;
            _countdownTb.Text = string.Format("{0:D2}:{1:D2}:{2:D2}",
                (int)rem.TotalHours, rem.Minutes, rem.Seconds);
            _countdownTb.Foreground = rem.TotalSeconds <= 60
                ? NY930Theme.RedBrush : NY930Theme.GoldLightBrush;
        }

        // ── Input validation (v1.5) ────────────────────────────
        // Returns true if all fields pass their min/max + cross rules.
        // Bad fields get a red border flash. Caller refuses to Apply
        // / Activar when this returns false.
        private bool ValidateInputs()
        {
            int v;
            bool ok = true;
            // Time: HH 1-12, MM 0-59, SS 0-59
            if (!NY930Theme.ValidateIntRange(_hh, 1, 12, out v)) ok = false;
            if (!NY930Theme.ValidateIntRange(_hm, 0, 59, out v)) ok = false;
            if (!NY930Theme.ValidateIntRange(_hs, 0, 59, out v)) ok = false;
            // Contratos >= 1
            int totalQty = 0;
            if (!NY930Theme.ValidateIntRange(_cfgQty, 1, int.MaxValue, out totalQty)) ok = false;

            // Per-side fields only when that side is enabled.
            if (_accLong != null && _accLong.IsOn)
            {
                if (!NY930Theme.ValidateIntRange(_longTicks, 1, int.MaxValue, out v)) ok = false;
                if (!NY930Theme.ValidateIntRange(_longSl,    1, int.MaxValue, out v)) ok = false;
                if (!NY930Theme.ValidateIntRange(_longTp,    1, int.MaxValue, out v)) ok = false;
            }
            if (_accShort != null && _accShort.IsOn)
            {
                if (!NY930Theme.ValidateIntRange(_shortTicks, 1, int.MaxValue, out v)) ok = false;
                if (!NY930Theme.ValidateIntRange(_shortSl,    1, int.MaxValue, out v)) ok = false;
                if (!NY930Theme.ValidateIntRange(_shortTp,    1, int.MaxValue, out v)) ok = false;
            }
            // SSR ticks: >= 0 (0 = auto)
            if (_ssrTicks != null && _ssrToggle != null && _ssrToggle.IsOn)
            {
                if (!NY930Theme.ValidateIntRange(_ssrTicks, 0, int.MaxValue, out v)) ok = false;
            }

            // Partials cross-rule: P1+P2 contracts <= total, P2 ticks > P1 ticks.
            if (_accPar != null && _accPar.IsOn)
            {
                int p1t, p1q, p2t, p2q;
                bool p1tOk = NY930Theme.ValidateIntRange(_p1Ticks, 0, int.MaxValue, out p1t);
                bool p1qOk = NY930Theme.ValidateIntRange(_p1Qty,   0, int.MaxValue, out p1q);
                bool p2tOk = NY930Theme.ValidateIntRange(_p2Ticks, 0, int.MaxValue, out p2t);
                bool p2qOk = NY930Theme.ValidateIntRange(_p2Qty,   0, int.MaxValue, out p2q);
                if (!p1tOk || !p1qOk || !p2tOk || !p2qOk) ok = false;
                if (p1qOk && p2qOk && totalQty > 0 && (p1q + p2q) > totalQty)
                {
                    NY930Theme.FlashInvalid(_p1Qty);
                    NY930Theme.FlashInvalid(_p2Qty);
                    ok = false;
                }
                if (p1tOk && p2tOk && p2t > 0 && p2t <= p1t)
                {
                    NY930Theme.FlashInvalid(_p2Ticks);
                    ok = false;
                }
            }
            return ok;
        }

        // Returns false (and shows a clear dialog) if the AperturaBreakout
        // strategy is not currently attached + enabled on any chart.
        // Used as a guard before any action that would queue an order:
        // ACTIVAR (Horario), ACTIVAR ÓRDENES (Manual).
        private static bool RequireOpenRangeAttached()
        {
            if (NY930Bridge.OpenRangeAttached) return true;
            MessageBox.Show(
                "La estrategia 'AperturaBreakout' no está activa en ningún gráfico.\n\n" +
                "Para que NY930 coloque las órdenes Buy Stop / Sell Stop, primero adjunte la estrategia:\n\n" +
                "1. Clic derecho en el gráfico → Strategies\n" +
                "2. Seleccione 'AperturaBreakout' en la lista\n" +
                "3. Configure parámetros y haga clic en Enable",
                "NY930 — Estrategia no adjunta",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        // ── Apply parameters to strategy ───────────────────────
        private void ApplyParametersToStrategy()
        {
            int hh = ParseInt(_hh.Text) ?? 9;
            int mm = ParseInt(_hm.Text) ?? 29;
            int ss = ParseInt(_hs.Text) ?? 58;
            string ap = _ampm.SelectedItem != null ? _ampm.SelectedItem.ToString() : "AM";
            int hour24 = hh % 12;
            if (string.Equals(ap, "PM", StringComparison.OrdinalIgnoreCase)) hour24 += 12;

            var p = new NY930Parameters
            {
                EntryHour   = hour24,
                EntryMinute = mm,
                EntrySecond = ss,

                Quantity            = ParseInt(_cfgQty.Text),
                EnableLong          = _accLong.IsOn,
                EnableShort         = _accShort.IsOn,
                TicksLong           = ParseInt(_longTicks.Text),
                StopLossLongTicks   = ParseInt(_longSl.Text),
                TakeProfitLongTicks = ParseInt(_longTp.Text),
                TicksShort          = ParseInt(_shortTicks.Text),
                StopLossShortTicks  = ParseInt(_shortSl.Text),
                TakeProfitShortTicks= ParseInt(_shortTp.Text),

                EnableBreakeven  = _accBe.IsOn,
                EnableTrailing   = _accTs.IsOn,
                EnablePartials   = _accPar.IsOn,
                EnableTimeExit   = _accSt.IsOn,
                EnableTpGapGuard = _accGg.IsOn &&  _ggTpMode,
                EnableSlGapGuard = _accGg.IsOn,
                TpGapGuardTicks  = ParseInt(_tpGgTicks.Text),
                SlGapGuardTicks  = ParseInt(_slGgTicks.Text),
                EnableTrailingTP = _accGg.IsOn && !_ggTpMode,
                TimeExitDurationSeconds = ParseInt(_stDuration.Text),
                TimeExitMode            = StModeKey(),
                CloseIfBeyondTP         = _stBeyondTpTog != null && _stBeyondTpTog.IsOn,

                EnableSingleStopReverseProtection = _ssrToggle != null && _ssrToggle.IsOn,
                SingleStopReverseTicks = _ssrTicks != null ? ParseInt(_ssrTicks.Text) : null
            };

            NY930Bridge.RequestOpenRangeAction(new NY930Action
            {
                Type       = NY930ActionType.OpenRangeApplyParameters,
                Parameters = p
            });
        }

        // ── Snapshot → input fields ────────────────────────────
        private void OnSnapshot(NY930OpenRangeSnapshot s) => Dispatcher.InvokeAsync(() => ApplySnapshotToInputs(s));

        private void ApplySnapshotToInputs(NY930OpenRangeSnapshot s)
        {
            if (s == null) return;
            // Only populate empty fields once at startup.
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

            // TextBox empty-fills run on every snapshot (their
            // string.IsNullOrEmpty guard prevents clobbering user
            // typing). Accordion / toggle / mode-pill restoration
            // is gated to ONCE — see _snapshotInitialized comment.
            if (s.TpGapGuardTicks > 0 && _tpGgTicks != null && string.IsNullOrEmpty(_tpGgTicks.Text))
                _tpGgTicks.Text = s.TpGapGuardTicks.ToString();
            if (s.SlGapGuardTicks > 0 && _slGgTicks != null && string.IsNullOrEmpty(_slGgTicks.Text))
                _slGgTicks.Text = s.SlGapGuardTicks.ToString();
            if (_stDuration != null && s.TimeExitDurationSeconds > 0
                && string.IsNullOrEmpty(_stDuration.Text))
                _stDuration.Text = s.TimeExitDurationSeconds.ToString();
            if (_ssrTicks != null && s.SingleStopReverseTicks > 0
                && string.IsNullOrEmpty(_ssrTicks.Text))
                _ssrTicks.Text = s.SingleStopReverseTicks.ToString();

            if (!_snapshotInitialized)
            {
                // First snapshot: align UI to strategy's actual state.
                if (_accLong  != null) _accLong.Set(s.EnableLong);
                if (_accShort != null) _accShort.Set(s.EnableShort);
                if (_ssrToggle != null) _ssrToggle.Set(s.EnableSingleStopReverseProtection);
                UpdateSsrSlots();

                if (_accGg != null) _accGg.Set(s.EnableTpGapGuard || s.EnableSlGapGuard || s.EnableTrailingTP);
                if (_ggTpBtn != null && _ggTrBtn != null)
                {
                    if      (s.EnableTrailingTP) SetGgMode(false);
                    else if (s.EnableTpGapGuard) SetGgMode(true);
                }

                if (_accSt != null) _accSt.Set(s.EnableTimeExit);
                RestoreStModeFromKey(s.TimeExitMode);
                if (_stBeyondTpTog != null) _stBeyondTpTog.Set(s.CloseIfBeyondTP);

                _snapshotInitialized = true;
            }

            // Auto-route to control view once stops are placed
            // and still working (no fill yet).
            if (!s.InLong && !s.InShort
                && (s.LongEntryWorking || s.ShortEntryWorking)
                && _shell.CurrentViewIs<NY930OpenRangeView>())
                _shell.Show(new NY930OpenRangeControlView(_shell));

            // Auto-route to in-trade view on fill.
            if ((s.InLong || s.InShort) && _shell.CurrentViewIs<NY930OpenRangeView>())
                _shell.Show(new NY930ProgressView(_shell, isOpenRange: true));
            if (s.LastResult != null && !s.InLong && !s.InShort && s.SessionDone
                && _shell.CurrentViewIs<NY930OpenRangeView>())
                _shell.Show(new NY930ResultView(_shell, s.LastResult));
        }

        // ── Helpers ────────────────────────────────────────────
        private static int? ParseInt(string text)
        {
            int v;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out v))
                return v;
            return null;
        }

        // The dropdown labels are user-friendly Spanish, but the
        // strategy enum values are English. Map index → enum name
        // so the strategy reads what the user picked.
        private string StModeKey()
        {
            if (_stMode == null) return null;
            switch (_stMode.SelectedIndex)
            {
                case 0:  return "CloseAlways";
                case 1:  return "CloseIfPositive";
                case 2:  return "PlaceTPAfterTime";
                default: return null;
            }
        }

        private void RestoreStModeFromKey(string key)
        {
            if (_stMode == null || string.IsNullOrEmpty(key)) return;
            if      (key.Equals("CloseAlways",      StringComparison.OrdinalIgnoreCase)) _stMode.SelectedIndex = 0;
            else if (key.Equals("CloseIfPositive",  StringComparison.OrdinalIgnoreCase)) _stMode.SelectedIndex = 1;
            else if (key.Equals("PlaceTPAfterTime", StringComparison.OrdinalIgnoreCase)) _stMode.SelectedIndex = 2;
        }

        public void RefreshLocalization() { /* schedule labels are Spanish per mockup */ }

        public void Dispose()
        {
            NY930Bridge.OpenRangeChanged -= OnSnapshot;
            if (_ticker != null) { _ticker.Stop(); _ticker = null; }
        }
    }
}
