// ============================================================
//  NY930Localization — bilingual EN/ES with live switching
// ------------------------------------------------------------
//  - Two flat dictionaries.
//  - NY930Localization.T("key") returns the localised string for
//    the active language (falls back to the key itself).
//  - LanguageChanged event lets every visible TextBlock refresh
//    in place when the user toggles language from Settings.
// ============================================================

#region Using declarations
using System;
using System.Collections.Generic;
#endregion

namespace NinjaTrader.NinjaScript.AddOns.NY930
{
    public enum NY930Language { English, Spanish }

    public static class NY930Localization
    {
        public static event Action LanguageChanged;

        private static NY930Language _current = NY930Language.English;

        public static NY930Language Current
        {
            get { return _current; }
            set
            {
                if (_current == value) return;
                _current = value;
                try { LanguageChanged?.Invoke(); } catch { }
            }
        }

        public static string T(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;
            var dict = _current == NY930Language.Spanish ? _es : _en;
            string val;
            return dict.TryGetValue(key, out val) ? val : key;
        }

        // ─────────────────────────────────────────────────────
        //  Strings
        //  Keys are intentionally human-readable so unrecognised
        //  keys still render acceptably as fallback.
        // ─────────────────────────────────────────────────────

        private static readonly Dictionary<string, string> _en = new Dictionary<string, string>
        {
            // Brand
            { "brand.tagline",            "MAKE MONEY EASY" },
            { "brand.subtitle",           "NY930 — 9:30 NY Open trading platform" },

            // Navigation
            { "nav.home",                 "Home" },
            { "nav.openrange",            "Open Range" },
            { "nav.hedge",                "Hedge" },
            { "nav.settings",             "Settings" },
            { "nav.about",                "About" },
            { "nav.back",                 "Back" },
            { "nav.close",                "Close" },

            // Home
            { "home.openrange.title",     "OPEN RANGE" },
            { "home.openrange.desc",      "Buy Stop + Sell Stop placed automatically at the open." },
            { "home.hedge.title",         "HEDGE" },
            { "home.hedge.desc",          "Direct market entry with full position management." },
            { "home.hint",                "Click a strategy to start." },

            // Status
            { "status.waiting",           "Waiting for {0}" },
            { "status.active",            "Orders working" },
            { "status.in_long",           "In Long position" },
            { "status.in_short",          "In Short position" },
            { "status.cancelled",         "Orders cancelled" },
            { "status.session_done",      "Session done" },
            { "status.no_strategy",       "No strategy attached to a chart" },

            // Open Range card
            { "or.buystop",               "BUY STOP" },
            { "or.sellstop",              "SELL STOP" },
            { "or.spread",                "Spread" },
            { "or.move.title",            "MOVE BOTH" },
            { "or.move.sub",              "Keeps the distance" },
            { "or.spread.title",          "SPREAD" },
            { "or.spread.sub",            "Keeps the midpoint" },
            { "or.cancel",                "CANCEL ORDERS" },
            { "or.buy_now",               "BUY NOW" },
            { "or.sell_now",              "SELL NOW" },
            { "or.flatten",               "CLOSE POSITION" },

            // Hedge fields
            { "hedge.time",               "Entry time" },
            { "hedge.qty",                "Contracts" },
            { "hedge.direction",          "Direction" },
            { "hedge.long",               "Long" },
            { "hedge.short",              "Short" },
            { "hedge.none",               "(none)" },
            { "hedge.sl",                 "Stop Loss (ticks)" },
            { "hedge.tp",                 "Take Profit (ticks)" },
            { "hedge.be",                 "Breakeven" },
            { "hedge.be.trigger",         "BE trigger (ticks)" },
            { "hedge.be.offset",          "BE offset (ticks)" },
            { "hedge.trail",              "Trailing Stop" },
            { "hedge.trail.trigger",      "Trail trigger (ticks)" },
            { "hedge.trail.step",         "Trail step (ticks)" },
            { "hedge.trailtp",            "Trailing TP" },
            { "hedge.trailtp.dist",       "Distance from extreme (ticks)" },
            { "hedge.trailtp.timeout",    "Timeout if no fill (s)" },
            { "hedge.partials",           "Partials" },
            { "hedge.partials.p1t",       "P1 ticks" },
            { "hedge.partials.p1c",       "P1 contracts" },
            { "hedge.partials.p2t",       "P2 ticks (0 = off)" },
            { "hedge.partials.p2c",       "P2 contracts (0 = off)" },
            { "hedge.timeexit",           "Time Exit" },
            { "hedge.timeexit.dur",       "Min duration (s)" },
            { "hedge.timeexit.mode",      "Exit mode" },
            { "hedge.timeexit.beyondtp",  "Close if price beyond TP" },

            // Gap guard / single-stop reverse
            { "guard.tp",                 "TP Gap Guard" },
            { "guard.sl",                 "SL Gap Guard" },
            { "guard.ticks",              "Trigger ticks" },
            { "guard.seconds",            "Trigger seconds" },
            { "guard.singlestop",         "Single-stop reverse cancel" },
            { "guard.singlestop.ticks",   "Reverse ticks (0 = use stop offset)" },

            // Progress
            { "progress.title",           "Trade progress" },
            { "progress.tp1",             "TP1" },
            { "progress.tp2",             "TP2" },
            { "progress.tp3",             "TP" },
            { "progress.sl",              "SL" },
            { "progress.pnl",             "PnL" },
            { "progress.duration",        "Duration" },
            { "progress.contracts",       "Contracts" },
            { "progress.entry",           "Entry" },
            { "progress.last",            "Last" },
            { "progress.ticks",           "ticks" },

            // Result
            { "result.title",             "Trade result" },
            { "result.profit",            "Profit" },
            { "result.loss",              "Loss" },
            { "result.entry",             "Entry" },
            { "result.exit",              "Exit" },
            { "result.tp_hits",           "TP hits" },
            { "result.reason",            "Reason" },

            // Settings
            { "settings.language",        "Language" },
            { "settings.lang.en",         "English" },
            { "settings.lang.es",         "Spanish" },
            { "settings.about.title",     "About NY930" },
            { "settings.about.body",      "NY930 unifies Open Range and Hedge under a single, themed control plane for the 9:30 NY open. Phase 1." },

            // Common
            { "common.enabled",           "Enabled" },
            { "common.disabled",          "Disabled" },
            { "common.yes",               "Yes" },
            { "common.no",                "No" },
            { "common.ticks",             "ticks" },
        };

        private static readonly Dictionary<string, string> _es = new Dictionary<string, string>
        {
            { "brand.tagline",            "MAKE MONEY EASY" },
            { "brand.subtitle",           "NY930 — Plataforma para la apertura 9:30 NY" },

            { "nav.home",                 "Inicio" },
            { "nav.openrange",            "Open Range" },
            { "nav.hedge",                "Hedge" },
            { "nav.settings",             "Ajustes" },
            { "nav.about",                "Acerca de" },
            { "nav.back",                 "Volver" },
            { "nav.close",                "Cerrar" },

            { "home.openrange.title",     "OPEN RANGE" },
            { "home.openrange.desc",      "Buy Stop + Sell Stop colocados automáticamente en la apertura." },
            { "home.hedge.title",         "HEDGE" },
            { "home.hedge.desc",          "Entrada directa a mercado con gestión completa de la posición." },
            { "home.hint",                "Toca una estrategia para empezar." },

            { "status.waiting",           "Esperando {0}" },
            { "status.active",            "Órdenes activas" },
            { "status.in_long",           "Posición Long" },
            { "status.in_short",          "Posición Short" },
            { "status.cancelled",         "Órdenes canceladas" },
            { "status.session_done",      "Sesión terminada" },
            { "status.no_strategy",       "Ninguna estrategia adjunta a un gráfico" },

            { "or.buystop",               "BUY STOP" },
            { "or.sellstop",              "SELL STOP" },
            { "or.spread",                "Spread" },
            { "or.move.title",            "MOVER AMBAS" },
            { "or.move.sub",              "Mantiene la distancia" },
            { "or.spread.title",          "SPREAD" },
            { "or.spread.sub",            "Mantiene el punto medio" },
            { "or.cancel",                "CANCELAR ÓRDENES" },
            { "or.buy_now",               "COMPRAR YA" },
            { "or.sell_now",              "VENDER YA" },
            { "or.flatten",               "CERRAR POSICIÓN" },

            { "hedge.time",               "Hora de entrada" },
            { "hedge.qty",                "Contratos" },
            { "hedge.direction",          "Dirección" },
            { "hedge.long",               "Long" },
            { "hedge.short",              "Short" },
            { "hedge.none",               "(ninguno)" },
            { "hedge.sl",                 "Stop Loss (ticks)" },
            { "hedge.tp",                 "Take Profit (ticks)" },
            { "hedge.be",                 "Breakeven" },
            { "hedge.be.trigger",         "Trigger BE (ticks)" },
            { "hedge.be.offset",          "Offset BE (ticks)" },
            { "hedge.trail",              "Trailing Stop" },
            { "hedge.trail.trigger",      "Trigger trail (ticks)" },
            { "hedge.trail.step",         "Paso del trail (ticks)" },
            { "hedge.trailtp",            "Trailing TP" },
            { "hedge.trailtp.dist",       "Distancia al extremo (ticks)" },
            { "hedge.trailtp.timeout",    "Timeout sin fill (s)" },
            { "hedge.partials",           "Parciales" },
            { "hedge.partials.p1t",       "Ticks P1" },
            { "hedge.partials.p1c",       "Contratos P1" },
            { "hedge.partials.p2t",       "Ticks P2 (0 = off)" },
            { "hedge.partials.p2c",       "Contratos P2 (0 = off)" },
            { "hedge.timeexit",           "Salida por Tiempo" },
            { "hedge.timeexit.dur",       "Duración mínima (s)" },
            { "hedge.timeexit.mode",      "Modo de salida" },
            { "hedge.timeexit.beyondtp",  "Cerrar si el precio supera el TP" },

            { "guard.tp",                 "TP Gap Guard" },
            { "guard.sl",                 "SL Gap Guard" },
            { "guard.ticks",              "Ticks de disparo" },
            { "guard.seconds",            "Segundos de disparo" },
            { "guard.singlestop",         "Cancelar Stop único en reversa" },
            { "guard.singlestop.ticks",   "Ticks en contra (0 = usar offset)" },

            { "progress.title",           "Progreso de la operación" },
            { "progress.tp1",             "TP1" },
            { "progress.tp2",             "TP2" },
            { "progress.tp3",             "TP" },
            { "progress.sl",              "SL" },
            { "progress.pnl",             "PnL" },
            { "progress.duration",        "Duración" },
            { "progress.contracts",       "Contratos" },
            { "progress.entry",           "Entrada" },
            { "progress.last",            "Último" },
            { "progress.ticks",           "ticks" },

            { "result.title",             "Resultado de la operación" },
            { "result.profit",            "Ganancia" },
            { "result.loss",              "Pérdida" },
            { "result.entry",             "Entrada" },
            { "result.exit",              "Salida" },
            { "result.tp_hits",           "TPs alcanzados" },
            { "result.reason",            "Motivo" },

            { "settings.language",        "Idioma" },
            { "settings.lang.en",         "Inglés" },
            { "settings.lang.es",         "Español" },
            { "settings.about.title",     "Acerca de NY930" },
            { "settings.about.body",      "NY930 unifica Open Range y Hedge bajo un único panel temático para la apertura 9:30 NY. Fase 1." },

            { "common.enabled",           "Activado" },
            { "common.disabled",          "Desactivado" },
            { "common.yes",               "Sí" },
            { "common.no",                "No" },
            { "common.ticks",             "ticks" },
        };
    }
}
