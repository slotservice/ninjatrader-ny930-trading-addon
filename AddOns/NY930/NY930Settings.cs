// ============================================================
//  NY930Settings — persisted user preferences
// ------------------------------------------------------------
//  Stored as a small INI-style file in the NinjaTrader 8 user
//  data folder (next to workspaces). Keeps language preference
//  and any per-user UI defaults across NT restarts.
// ============================================================

#region Using declarations
using System;
using System.Collections.Generic;
using System.IO;
using NinjaTrader.Core;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public static class NY930Settings
    {
        private static readonly object _sync = new object();
        private static readonly Dictionary<string, string> _store
            = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static string _filePath;
        private static bool   _loaded;

        public static void EnsureLoaded()
        {
            lock (_sync)
            {
                if (_loaded) return;
                _loaded = true;

                try
                {
                    string root = Globals.UserDataDir; // typically: My Documents\NinjaTrader 8\
                    string dir  = Path.Combine(root, "NY930");
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    _filePath = Path.Combine(dir, "ny930.settings");

                    if (!File.Exists(_filePath)) return;

                    foreach (var raw in File.ReadAllLines(_filePath))
                    {
                        string line = raw == null ? string.Empty : raw.Trim();
                        if (line.Length == 0 || line.StartsWith("#")) continue;
                        int eq = line.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = line.Substring(0, eq).Trim();
                        string v = line.Substring(eq + 1).Trim();
                        _store[k] = v;
                    }

                    NY930Log.Info("Settings", "Loaded " + _store.Count
                        + " preference(s) from " + _filePath);

                    string lang;
                    if (_store.TryGetValue("language", out lang))
                    {
                        if (string.Equals(lang, "es", StringComparison.OrdinalIgnoreCase))
                            NY930Localization.Current = NY930Language.Spanish;
                        else
                            NY930Localization.Current = NY930Language.English;
                    }
                }
                catch (Exception ex)
                {
                    NY930Log.Warn("Settings", "Could not load preferences: " + ex.Message);
                }
            }
        }

        public static string Get(string key, string fallback = null)
        {
            EnsureLoaded();
            lock (_sync)
            {
                string v;
                return _store.TryGetValue(key, out v) ? v : fallback;
            }
        }

        public static void Set(string key, string value)
        {
            EnsureLoaded();
            lock (_sync)
            {
                _store[key] = value ?? string.Empty;
                Save();
            }
        }

        public static void SetLanguage(NY930Language lang)
        {
            Set("language", lang == NY930Language.Spanish ? "es" : "en");
            NY930Localization.Current = lang;
        }

        public static NY930Language GetLanguage()
        {
            return string.Equals(Get("language", "en"), "es",
                       StringComparison.OrdinalIgnoreCase)
                ? NY930Language.Spanish
                : NY930Language.English;
        }

        private static void Save()
        {
            if (string.IsNullOrEmpty(_filePath)) return;
            try
            {
                using (var w = new StreamWriter(_filePath, false))
                {
                    w.WriteLine("# NY930 user preferences — managed file");
                    w.WriteLine("# Generated " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    foreach (var kv in _store)
                        w.WriteLine(kv.Key + "=" + kv.Value);
                }
            }
            catch (Exception ex)
            {
                NY930Log.Warn("Settings", "Could not save preferences: " + ex.Message);
            }
        }
    }
}
