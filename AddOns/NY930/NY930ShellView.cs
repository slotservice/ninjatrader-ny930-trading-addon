// ============================================================
//  NY930ShellView — top-level container hosted by the panel
// ------------------------------------------------------------
//  Responsibilities:
//    - Renders the persistent header (NY930 wordmark + hamburger
//      menu with Home / Open Range / Hedge / Control / Progress /
//      Result / Settings / About).
//    - Hosts a single child view at a time.
//    - Re-renders all visible strings when language changes.
//    - Locks back-navigation while a trade is active (per video 5
//      "once stop orders are placed, no going back unless cancel
//      or close"). Back button still works in setup views.
// ============================================================

#region Using declarations
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public sealed class NY930ShellView : Grid, IDisposable
    {
        private readonly Border _header;
        private readonly TextBlock _brand;
        private readonly TextBlock _tagline;
        private readonly StackPanel _brandStack;
        private readonly Button _backBtn;
        private readonly ToggleButton _menuBtn;
        private readonly Popup _menuPopup;
        private readonly ContentControl _host;

        private FrameworkElement _currentView;
        private bool _isTradeActive;

        public NY930ShellView()
        {
            Background = NY930Theme.BgBaseBrush;

            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // ── Header ───────────────────────────────────────────
            _header = new Border
            {
                Background      = NY930Theme.BgPanelBrush,
                BorderBrush     = NY930Theme.BorderBrush,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding         = new Thickness(14, 12, 10, 12)
            };

            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _backBtn = new Button
            {
                Content    = "‹",
                Foreground = NY930Theme.GoldBrush,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                FontSize   = 22,
                FontWeight = FontWeights.Bold,
                Width      = 28,
                Height     = 28,
                Padding    = new Thickness(0),
                Visibility = Visibility.Collapsed,
                Cursor     = System.Windows.Input.Cursors.Hand,
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            _backBtn.Click += OnBackClicked;
            Grid.SetColumn(_backBtn, 0);

            _brandStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            _brand = new TextBlock
            {
                Text     = "NY930",
                FontSize = 18,
                FontWeight = FontWeights.Black,
                Foreground = NY930Theme.GoldBrightBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _tagline = new TextBlock
            {
                Text     = SpaceLetters(NY930Localization.T("brand.tagline")),
                FontSize = 8,
                FontWeight = FontWeights.SemiBold,
                Foreground = NY930Theme.GoldDimBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 1, 0, 0)
            };
            _brandStack.Children.Add(_brand);
            _brandStack.Children.Add(_tagline);
            Grid.SetColumn(_brandStack, 1);

            _menuBtn = new ToggleButton
            {
                Content    = "☰",
                Foreground = NY930Theme.GoldBrush,
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                FontSize   = 18,
                Width      = 32,
                Height     = 28,
                Padding    = new Thickness(0),
                Cursor     = System.Windows.Input.Cursors.Hand,
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(_menuBtn, 2);

            headerGrid.Children.Add(_backBtn);
            headerGrid.Children.Add(_brandStack);
            headerGrid.Children.Add(_menuBtn);
            _header.Child = headerGrid;
            Grid.SetRow(_header, 0);
            Children.Add(_header);

            // ── Hamburger popup ─────────────────────────────────
            _menuPopup = new Popup
            {
                PlacementTarget = _menuBtn,
                Placement       = PlacementMode.Bottom,
                StaysOpen       = false,
                AllowsTransparency = true
            };
            _menuPopup.Closed += (s, e) => _menuBtn.IsChecked = false;
            _menuBtn.Checked   += (s, e) => { BuildMenuPopup(); _menuPopup.IsOpen = true; };
            _menuBtn.Unchecked += (s, e) => _menuPopup.IsOpen = false;

            // ── Host area ────────────────────────────────────────
            _host = new ContentControl();
            Grid.SetRow(_host, 1);
            Children.Add(_host);

            // Initial view
            Show(new NY930HomeView(this));

            NY930Localization.LanguageChanged += OnLanguageChanged;

            // Subscribe to live trade-state changes so we can lock
            // navigation while orders are working / position is open.
            NY930Bridge.OpenRangeChanged += OnOpenRangeSnapshot;
            NY930Bridge.HedgeChanged     += OnHedgeSnapshot;
            // Apply current state on construction (in case a trade
            // was already active when the panel was opened).
            ReevaluateTradeActive();
        }

        public void Show(FrameworkElement view)
        {
            if (_currentView is IDisposable d) d.Dispose();
            _currentView = view;
            _host.Content = view;

            UpdateChromeForView();
        }

        // Chrome rules per view:
        //   - Home: brand HIDDEN (home view has its own big NY930
        //     logo — two logos was the v1.5 client-feedback issue),
        //     back hidden. Header is just the hamburger.
        //   - Setup views (OpenRange/Hedge): brand hidden (body has
        //     its own PanelHeader with NY930), back visible
        //     (disabled if a trade is active).
        //   - Trade-state views (Control / Progress / Result): brand
        //     hidden, back HIDDEN per the client's "No arrow" image.
        //     User leaves these views via in-view actions or the
        //     hamburger menu.
        //   - Other (Settings): brand hidden, back visible.
        private void UpdateChromeForView()
        {
            bool isHome       = _currentView is NY930HomeView;
            bool isTradeState = _currentView is NY930OpenRangeControlView
                             || _currentView is NY930ProgressView
                             || _currentView is NY930ResultView;

            // Shell brand is always hidden in v1.5 — every view owns
            // its own NY930 branding internally.
            _brandStack.Visibility = Visibility.Collapsed;

            if (isHome || isTradeState)
            {
                _backBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                _backBtn.Visibility = Visibility.Visible;
                _backBtn.IsEnabled  = !_isTradeActive;
                _backBtn.Opacity    = _backBtn.IsEnabled ? 1.0 : 0.35;
                _backBtn.ToolTip    = _backBtn.IsEnabled ? null : NY930Localization.T("nav.locked");
            }
        }

        private void OnBackClicked(object sender, RoutedEventArgs e)
        {
            if (_isTradeActive)
            {
                NY930Log.Info("Shell", "Back navigation refused — trade active.");
                return;
            }
            Show(new NY930HomeView(this));
        }

        public bool CurrentViewIs<T>() where T : FrameworkElement
        {
            return _currentView is T;
        }

        public FrameworkElement CurrentView { get { return _currentView; } }

        // ── Trade-active gate ───────────────────────────────────
        private void OnOpenRangeSnapshot(NY930OpenRangeSnapshot s)
        {
            Dispatcher.InvokeAsync(ReevaluateTradeActive);
        }

        private void OnHedgeSnapshot(NY930HedgeSnapshot s)
        {
            Dispatcher.InvokeAsync(ReevaluateTradeActive);
        }

        // A trade is "active" when at least one of:
        //   - Open Range has a working stop order
        //   - Open Range is in a long/short position
        //   - Hedge is in a position
        // Snapshots are null until the first publish; treat that as
        // not active.
        private void ReevaluateTradeActive()
        {
            var or = NY930Bridge.GetOpenRange();
            var hg = NY930Bridge.GetHedge();

            bool orActive = or != null && (or.LongEntryWorking || or.ShortEntryWorking || or.InLong || or.InShort);
            bool hgActive = hg != null && hg.InPosition;
            bool active   = orActive || hgActive;

            if (active == _isTradeActive) return;
            _isTradeActive = active;
            UpdateChromeForView();
        }

        // ── Hamburger popup contents ────────────────────────────
        private void BuildMenuPopup()
        {
            var border = new Border
            {
                Background      = NY930Theme.BgPanelBrush,
                BorderBrush     = NY930Theme.BorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius    = new CornerRadius(6),
                Padding         = new Thickness(4)
            };

            var stack = new StackPanel { Width = 200 };

            var or = NY930Bridge.GetOpenRange();
            var hg = NY930Bridge.GetHedge();

            bool hasOrControl  = or != null && (or.LongEntryWorking || or.ShortEntryWorking) && !(or.InLong || or.InShort);
            bool hasOrProgress = or != null && (or.InLong || or.InShort);
            bool hasHgProgress = hg != null && hg.InPosition;
            bool hasResult     = (or != null && or.LastResult != null) || (hg != null && hg.LastResult != null);

            // Refusing nav while trade is active applies to anything
            // that would abandon trade state (Home / Open Range setup
            // / Hedge setup). Control / Progress / Result are always
            // safe to open because they're trade-state views.
            stack.Children.Add(MenuRow(NY930Localization.T("nav.home"),
                () => SafeNavigate(() => new NY930HomeView(this), refuseWhileActive: true)));
            stack.Children.Add(MenuRow(NY930Localization.T("nav.openrange"),
                () => SafeNavigate(() => new NY930OpenRangeView(this), refuseWhileActive: true)));
            stack.Children.Add(MenuRow(NY930Localization.T("nav.hedge"),
                () => SafeNavigate(() => new NY930HedgeView(this), refuseWhileActive: true)));
            stack.Children.Add(NY930Theme.HRule());

            // Trade-state views — enabled only when relevant data exists.
            stack.Children.Add(MenuRow(NY930Localization.T("nav.control"),
                () => SafeNavigate(() => new NY930OpenRangeControlView(this), refuseWhileActive: false),
                enabled: hasOrControl));
            stack.Children.Add(MenuRow(NY930Localization.T("nav.progress"),
                () => SafeNavigate(() => new NY930ProgressView(this, isOpenRange: hasOrProgress), refuseWhileActive: false),
                enabled: hasOrProgress || hasHgProgress));
            stack.Children.Add(MenuRow(NY930Localization.T("nav.result"),
                () => SafeNavigate(() => BuildLastResultView(), refuseWhileActive: false),
                enabled: hasResult));

            stack.Children.Add(NY930Theme.HRule());
            stack.Children.Add(MenuRow(NY930Localization.T("nav.settings"),
                () => SafeNavigate(() => new NY930SettingsView(this), refuseWhileActive: false)));

            border.Child = stack;
            _menuPopup.Child = border;
        }

        private FrameworkElement BuildLastResultView()
        {
            var or = NY930Bridge.GetOpenRange();
            var hg = NY930Bridge.GetHedge();
            // Prefer the most recently produced result (compare timestamps via ExitTime).
            NY930TradeResult result = null;
            if (or != null && or.LastResult != null) result = or.LastResult;
            if (hg != null && hg.LastResult != null
                && (result == null || hg.LastResult.ExitTime > result.ExitTime))
                result = hg.LastResult;
            return new NY930ResultView(this, result);
        }

        private void SafeNavigate(Func<FrameworkElement> factory, bool refuseWhileActive)
        {
            _menuPopup.IsOpen = false;
            if (refuseWhileActive && _isTradeActive)
            {
                NY930Log.Info("Shell", "Menu navigation refused — trade active.");
                return;
            }
            try { Show(factory()); }
            catch (Exception ex) { NY930Log.Error("Shell", "Navigation failed: " + ex.Message); }
        }

        private Button MenuRow(string text, Action onClick, bool enabled = true)
        {
            var b = new Button
            {
                Content     = text,
                Foreground  = enabled ? NY930Theme.TextHiBrush : NY930Theme.TextDimBrush,
                Background  = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Padding     = new Thickness(10, 6, 10, 6),
                FontSize    = 12,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Cursor      = enabled ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow,
                IsEnabled   = enabled,
                Opacity     = enabled ? 1.0 : 0.45
            };
            b.Click += (s, e) => onClick();
            return b;
        }

        private void OnLanguageChanged()
        {
            Dispatcher.InvokeAsync(() =>
            {
                _tagline.Text = SpaceLetters(NY930Localization.T("brand.tagline"));
                BuildMenuPopup();
                if (_currentView is INY930Localizable loc) loc.RefreshLocalization();
                UpdateChromeForView();
            });
        }

        internal static string SpaceLetters(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder(s.Length * 2);
            for (int i = 0; i < s.Length; i++)
            {
                sb.Append(s[i]);
                if (i < s.Length - 1 && s[i] != ' ') sb.Append(' ');
            }
            return sb.ToString();
        }

        public void Dispose()
        {
            NY930Localization.LanguageChanged -= OnLanguageChanged;
            NY930Bridge.OpenRangeChanged      -= OnOpenRangeSnapshot;
            NY930Bridge.HedgeChanged          -= OnHedgeSnapshot;
            if (_currentView is IDisposable d) d.Dispose();
        }
    }

    public interface INY930Localizable
    {
        void RefreshLocalization();
    }
}
