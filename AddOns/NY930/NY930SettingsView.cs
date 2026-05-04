// ============================================================
//  NY930SettingsView — Settings (language + About)
// ============================================================

#region Using declarations
using System;
using System.Windows;
using System.Windows.Controls;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public sealed class NY930SettingsView : Grid, INY930Localizable, IDisposable
    {
        private readonly NY930ShellView _shell;
        private readonly RadioButton _rbEn;
        private readonly RadioButton _rbEs;
        private readonly TextBlock _languageLabel;
        private readonly TextBlock _aboutTitle;
        private readonly TextBlock _aboutBody;

        public NY930SettingsView(NY930ShellView shell)
        {
            _shell = shell;
            Background = NY930Theme.BgBaseBrush;

            var root = new StackPanel { Margin = new Thickness(20) };
            Children.Add(root);

            // Language
            _languageLabel = new TextBlock
            {
                Text     = NY930Localization.T("settings.language"),
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.GoldBrightBrush,
                Margin   = new Thickness(0, 0, 0, 10)
            };
            root.Children.Add(_languageLabel);

            _rbEn = new RadioButton
            {
                GroupName  = "lang",
                Content    = NY930Localization.T("settings.lang.en"),
                Foreground = NY930Theme.TextHiBrush,
                IsChecked  = NY930Settings.GetLanguage() == NY930Language.English,
                Margin     = new Thickness(0, 0, 0, 6)
            };
            _rbEs = new RadioButton
            {
                GroupName  = "lang",
                Content    = NY930Localization.T("settings.lang.es"),
                Foreground = NY930Theme.TextHiBrush,
                IsChecked  = NY930Settings.GetLanguage() == NY930Language.Spanish,
                Margin     = new Thickness(0, 0, 0, 12)
            };
            _rbEn.Checked += (s, e) => NY930Settings.SetLanguage(NY930Language.English);
            _rbEs.Checked += (s, e) => NY930Settings.SetLanguage(NY930Language.Spanish);
            root.Children.Add(_rbEn);
            root.Children.Add(_rbEs);

            // About
            var aboutStack = new StackPanel();
            _aboutTitle = new TextBlock
            {
                Text       = NY930Localization.T("settings.about.title"),
                FontSize   = 13,
                FontWeight = FontWeights.Bold,
                Foreground = NY930Theme.GoldBrightBrush,
                Margin     = new Thickness(0, 0, 0, 6)
            };
            _aboutBody = new TextBlock
            {
                Text         = NY930Localization.T("settings.about.body"),
                FontSize     = 11,
                Foreground   = NY930Theme.TextMidBrush,
                TextWrapping = TextWrapping.Wrap
            };
            aboutStack.Children.Add(_aboutTitle);
            aboutStack.Children.Add(_aboutBody);
            root.Children.Add(NY930Theme.Panel(aboutStack, new Thickness(0, 14, 0, 0)));
        }

        public void RefreshLocalization()
        {
            _languageLabel.Text = NY930Localization.T("settings.language");
            _rbEn.Content       = NY930Localization.T("settings.lang.en");
            _rbEs.Content       = NY930Localization.T("settings.lang.es");
            _aboutTitle.Text    = NY930Localization.T("settings.about.title");
            _aboutBody.Text     = NY930Localization.T("settings.about.body");
        }

        public void Dispose() { }
    }
}
