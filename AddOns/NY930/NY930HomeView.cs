// ============================================================
//  NY930HomeView — landing page (HOMEPAGE APP 2 layout)
// ------------------------------------------------------------
//  - Big NY930 wordmark + tagline
//  - Two cards: OPEN RANGE and HEDGE
//  - Status footer indicating whether each strategy is attached
//    to a chart (driven by NY930Bridge attachment counts).
// ============================================================

#region Using declarations
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public sealed class NY930HomeView : Grid, INY930Localizable, IDisposable
    {
        private readonly NY930ShellView _shell;
        private readonly TextBlock _hint;
        private readonly TextBlock _orStatus;
        private readonly TextBlock _hedgeStatus;
        private readonly Border _orCard;
        private readonly Border _hedgeCard;
        private readonly TextBlock _orTitle;
        private readonly TextBlock _orDesc;
        private readonly TextBlock _hedgeTitle;
        private readonly TextBlock _hedgeDesc;

        public NY930HomeView(NY930ShellView shell)
        {
            _shell = shell;
            Background = NY930Theme.BgBaseBrush;

            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility   = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Children.Add(scroll);

            var root = new StackPanel { Margin = new Thickness(20, 24, 20, 20) };
            scroll.Content = root;

            // Hero
            var hero = new TextBlock
            {
                Text     = "NY930",
                FontSize = 56,
                FontWeight = FontWeights.Black,
                Foreground = NY930Theme.GoldBrightBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin   = new Thickness(0, 6, 0, 4)
            };
            root.Children.Add(hero);

            var tagline = new TextBlock
            {
                Text     = NY930ShellView.SpaceLetters(NY930Localization.T("brand.tagline")),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.GoldBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin   = new Thickness(0, 0, 0, 24)
            };
            root.Children.Add(tagline);

            // ── OPEN RANGE card ───────────────────────────────
            _orTitle = new TextBlock
            {
                Text       = NY930Localization.T("home.openrange.title"),
                FontSize   = 18,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.GoldBrightBrush
            };
            _orDesc = new TextBlock
            {
                Text       = NY930Localization.T("home.openrange.desc"),
                FontSize   = 11,
                Foreground = NY930Theme.TextMidBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin     = new Thickness(0, 4, 0, 0)
            };
            var orStack = new StackPanel();
            orStack.Children.Add(BuildIcon("▲▼", NY930Theme.LongGreen, NY930Theme.ShortRed));
            orStack.Children.Add(_orTitle);
            orStack.Children.Add(_orDesc);

            _orCard = NY930Theme.Card(orStack, NY930Theme.GoldDimBrush, new Thickness(0, 0, 0, 14));
            _orCard.Cursor = System.Windows.Input.Cursors.Hand;
            _orCard.MouseLeftButtonUp += (s, e) => _shell.Show(new NY930OpenRangeView(_shell));
            root.Children.Add(_orCard);

            // ── HEDGE card ────────────────────────────────────
            _hedgeTitle = new TextBlock
            {
                Text       = NY930Localization.T("home.hedge.title"),
                FontSize   = 18,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.GoldBrightBrush
            };
            _hedgeDesc = new TextBlock
            {
                Text       = NY930Localization.T("home.hedge.desc"),
                FontSize   = 11,
                Foreground = NY930Theme.TextMidBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin     = new Thickness(0, 4, 0, 0)
            };
            var hedgeStack = new StackPanel();
            hedgeStack.Children.Add(BuildIcon("◆", NY930Theme.GoldBright, NY930Theme.GoldDim));
            hedgeStack.Children.Add(_hedgeTitle);
            hedgeStack.Children.Add(_hedgeDesc);

            _hedgeCard = NY930Theme.Card(hedgeStack, NY930Theme.GoldDimBrush, new Thickness(0, 0, 0, 14));
            _hedgeCard.Cursor = System.Windows.Input.Cursors.Hand;
            _hedgeCard.MouseLeftButtonUp += (s, e) => _shell.Show(new NY930HedgeView(_shell));
            root.Children.Add(_hedgeCard);

            // Hint
            _hint = new TextBlock
            {
                Text       = NY930Localization.T("home.hint"),
                FontSize   = 10,
                Foreground = NY930Theme.TextLowBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin     = new Thickness(0, 18, 0, 0)
            };
            root.Children.Add(_hint);

            // Attachment status
            var statusBox = new StackPanel { Margin = new Thickness(0, 14, 0, 0) };
            _orStatus = new TextBlock
            {
                FontSize = 9, Foreground = NY930Theme.TextLowBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            _hedgeStatus = new TextBlock
            {
                FontSize = 9, Foreground = NY930Theme.TextLowBrush,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            statusBox.Children.Add(_orStatus);
            statusBox.Children.Add(_hedgeStatus);
            root.Children.Add(statusBox);

            NY930Bridge.AttachmentChanged += OnAttachmentChanged;
            UpdateAttachmentStatus();
        }

        private static FrameworkElement BuildIcon(string glyph, Color top, Color bottom)
        {
            var grid = new Grid { HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 8) };
            var t = new TextBlock
            {
                Text     = glyph,
                FontSize = 22,
                FontWeight = FontWeights.Black,
                Foreground = new LinearGradientBrush(top, bottom, 90)
            };
            grid.Children.Add(t);
            return grid;
        }

        private void OnAttachmentChanged() => Dispatcher.InvokeAsync(UpdateAttachmentStatus);

        private void UpdateAttachmentStatus()
        {
            string ok = NY930Localization.T("common.yes");
            string no = NY930Localization.T("common.no");
            string none = NY930Localization.T("status.no_strategy");

            bool orAtt    = NY930Bridge.OpenRangeAttached;
            bool hedgeAtt = NY930Bridge.HedgeAttached;

            _orStatus.Text    = "Open Range: " + (orAtt    ? ok : no);
            _hedgeStatus.Text = "Hedge: "      + (hedgeAtt ? ok : no);

            _orStatus.Foreground    = orAtt    ? NY930Theme.LongGreenBrush : NY930Theme.TextLowBrush;
            _hedgeStatus.Foreground = hedgeAtt ? NY930Theme.LongGreenBrush : NY930Theme.TextLowBrush;

            if (!orAtt && !hedgeAtt)
            {
                _hint.Text = none;
                _hint.Foreground = NY930Theme.WarnAmberBrush;
            }
            else
            {
                _hint.Text = NY930Localization.T("home.hint");
                _hint.Foreground = NY930Theme.TextLowBrush;
            }
        }

        public void RefreshLocalization()
        {
            _orTitle.Text     = NY930Localization.T("home.openrange.title");
            _orDesc.Text      = NY930Localization.T("home.openrange.desc");
            _hedgeTitle.Text  = NY930Localization.T("home.hedge.title");
            _hedgeDesc.Text   = NY930Localization.T("home.hedge.desc");
            UpdateAttachmentStatus();
        }

        public void Dispose()
        {
            NY930Bridge.AttachmentChanged -= OnAttachmentChanged;
        }
    }
}
