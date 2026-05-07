// ============================================================
//  NY930ShellView — top-level container hosted by the NTWindow
// ------------------------------------------------------------
//  Responsibilities:
//    - Renders the persistent header (NY930 wordmark + hamburger
//      menu with Settings / About / Back).
//    - Hosts a single child view at a time (Home, Open Range,
//      Hedge, Settings, Result).
//    - Re-renders all visible strings when language changes.
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
        private readonly Button _backBtn;
        private readonly ToggleButton _menuBtn;
        private readonly Popup _menuPopup;
        private readonly ContentControl _host;

        private FrameworkElement _currentView;

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
            _backBtn.Click += (s, e) => Show(new NY930HomeView(this));
            Grid.SetColumn(_backBtn, 0);

            var brandStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
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
            brandStack.Children.Add(_brand);
            brandStack.Children.Add(_tagline);
            Grid.SetColumn(brandStack, 1);

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
            headerGrid.Children.Add(brandStack);
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
        }

        public void Show(FrameworkElement view)
        {
            if (_currentView is IDisposable d) d.Dispose();
            _currentView = view;
            _host.Content = view;

            // Show "back" arrow on every page except Home.
            _backBtn.Visibility = (view is NY930HomeView) ? Visibility.Collapsed : Visibility.Visible;
        }

        // Used by views that need to perform a one-time auto-navigation
        // (e.g. trade-just-closed → result screen) and want to make sure
        // they're still the active view before pushing the next one.
        public bool CurrentViewIs<T>() where T : FrameworkElement
        {
            return _currentView is T;
        }

        public FrameworkElement CurrentView { get { return _currentView; } }

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

            var stack = new StackPanel { Width = 180 };

            stack.Children.Add(MenuRow(NY930Localization.T("nav.home"), () =>
            {
                _menuPopup.IsOpen = false;
                Show(new NY930HomeView(this));
            }));
            stack.Children.Add(MenuRow(NY930Localization.T("nav.openrange"), () =>
            {
                _menuPopup.IsOpen = false;
                Show(new NY930OpenRangeView(this));
            }));
            stack.Children.Add(MenuRow(NY930Localization.T("nav.hedge"), () =>
            {
                _menuPopup.IsOpen = false;
                Show(new NY930HedgeView(this));
            }));
            stack.Children.Add(NY930Theme.HRule());
            stack.Children.Add(MenuRow(NY930Localization.T("nav.settings"), () =>
            {
                _menuPopup.IsOpen = false;
                Show(new NY930SettingsView(this));
            }));

            border.Child = stack;
            _menuPopup.Child = border;
        }

        private Button MenuRow(string text, Action onClick)
        {
            var b = new Button
            {
                Content     = text,
                Foreground  = NY930Theme.TextHiBrush,
                Background  = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                Padding     = new Thickness(10, 6, 10, 6),
                FontSize    = 12,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Cursor      = System.Windows.Input.Cursors.Hand
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
                // Re-build the current view so all its strings refresh.
                if (_currentView is INY930Localizable loc) loc.RefreshLocalization();
            });
        }

        // Cheap visual letter-spacing without relying on WPF Typography.
        // Inserts a thin space between each letter — works on any font.
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
            if (_currentView is IDisposable d) d.Dispose();
        }
    }

    // Marker interface — views that want to react to live language
    // changes implement this. The shell calls it on the active view
    // when LanguageChanged fires.
    public interface INY930Localizable
    {
        void RefreshLocalization();
    }
}
